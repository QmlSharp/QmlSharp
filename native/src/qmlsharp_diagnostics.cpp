#include "qmlsharp_diagnostics.h"

#include <QByteArray>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonValue>
#include <QMetaObject>
#include <QMetaProperty>
#include <QObject>
#include <QString>
#include <QThread>
#include <QVariant>
#include <cstdlib>
#include <cstring>
#include <exception>
#include <string>
#include <utility>
#include <vector>

#include "qmlsharp_errors.h"
#include "qmlsharp_instances.h"
#include "qmlsharp_metrics.h"

namespace qmlsharp {
namespace {
bool is_blank(const char* value) noexcept {
    if (value == nullptr) {
        return true;
    }

    while (*value != '\0') {
        if (*value != ' ' && *value != '\t' && *value != '\r' && *value != '\n') {
            return false;
        }

        ++value;
    }

    return true;
}

const char* allocate_string(const QByteArray& value) noexcept {
    char* buffer = static_cast<char*>(std::malloc(static_cast<std::size_t>(value.size()) + 1U));
    if (buffer == nullptr) {
        set_last_error("Failed to allocate native diagnostics string.");
        return nullptr;
    }

    std::memcpy(buffer, value.constData(), static_cast<std::size_t>(value.size()));
    buffer[value.size()] = '\0';
    return buffer;
}

bool should_skip_instance_property(const char* property_name) noexcept {
    return std::strcmp(property_name, "objectName") == 0 || std::strcmp(property_name, "instanceId") == 0 ||
           std::strcmp(property_name, "compilerSlotKey") == 0;
}

QJsonObject capture_instance_properties(QObject* object) {
    QJsonObject properties;
    if (object == nullptr) {
        return properties;
    }

    const QMetaObject* meta_object = object->metaObject();
    for (int index = meta_object->propertyOffset(); index < meta_object->propertyCount(); ++index) {
        const QMetaProperty property = meta_object->property(index);
        if (!property.isReadable() || should_skip_instance_property(property.name())) {
            continue;
        }

        properties.insert(QString::fromLatin1(property.name()), QJsonValue::fromVariant(property.read(object)));
    }

    return properties;
}

template <typename Operation>
bool run_on_object_thread(QObject* object, QJsonObject& result, Operation operation) {
    if (object == nullptr) {
        result = {};
        return true;
    }

    if (object->thread() == QThread::currentThread()) {
        result = operation();
        return true;
    }

    bool completed = false;
    const bool invoked = QMetaObject::invokeMethod(
        object,
        [&result, &completed, operation = std::move(operation)]() mutable {
            result = operation();
            completed = true;
        },
        Qt::BlockingQueuedConnection);

    if (!invoked || !completed) {
        set_last_error("Diagnostics failed to marshal instance property reads to the QObject thread.");
        return false;
    }

    return true;
}

bool capture_instance(const NativeInstanceRecordSnapshot& instance, QJsonObject& snapshot) {
    snapshot.insert(QStringLiteral("instanceId"), QString::fromStdString(instance.instance_id));
    snapshot.insert(QStringLiteral("className"), QString::fromStdString(instance.class_name));
    snapshot.insert(QStringLiteral("compilerSlotKey"), QString::fromStdString(instance.compiler_slot_key));
    snapshot.insert(QStringLiteral("ready"), instance.ready);
    QPointer<QObject> object = instance.object;
    QJsonObject properties;
    if (!run_on_object_thread(object.data(), properties, [object]() {
            return object.isNull() ? QJsonObject() : capture_instance_properties(object.data());
        })) {
        return false;
    }

    snapshot.insert(QStringLiteral("properties"), properties);
    return true;
}

const char* allocate_document(const QJsonDocument& document) noexcept {
    return allocate_string(document.toJson(QJsonDocument::Compact));
}
}  // namespace

const char* get_instance_info(const char* instance_id) noexcept {
    try {
        if (is_blank(instance_id)) {
            set_last_error("qmlsharp_get_instance_info requires a non-empty instanceId.");
            return nullptr;
        }

        const std::vector<NativeInstanceRecordSnapshot> records = snapshot_instances();
        for (const NativeInstanceRecordSnapshot& instance : records) {
            if (instance.instance_id == instance_id && !instance.object.isNull()) {
                QJsonObject instance_info;
                if (!capture_instance(instance, instance_info)) {
                    return nullptr;
                }

                const char* result = allocate_document(QJsonDocument(instance_info));
                if (result != nullptr) {
                    clear_last_error();
                }

                return result;
            }
        }

        set_last_error(std::string("qmlsharp_get_instance_info instance '") + instance_id + "' was not found.");
        return nullptr;
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_get_instance_info failed: ") + error.what());
        return nullptr;
    } catch (...) {
        set_last_error("qmlsharp_get_instance_info failed due to an unknown native exception.");
        return nullptr;
    }
}

const char* get_all_instances() noexcept {
    try {
        QJsonArray instances;
        const std::vector<NativeInstanceRecordSnapshot> records = snapshot_instances();
        for (const NativeInstanceRecordSnapshot& instance : records) {
            if (!instance.object.isNull()) {
                QJsonObject instance_info;
                if (!capture_instance(instance, instance_info)) {
                    return nullptr;
                }

                instances.append(instance_info);
            }
        }

        const char* result = allocate_document(QJsonDocument(instances));
        if (result != nullptr) {
            clear_last_error();
        }

        return result;
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_get_all_instances failed: ") + error.what());
        return nullptr;
    } catch (...) {
        set_last_error("qmlsharp_get_all_instances failed due to an unknown native exception.");
        return nullptr;
    }
}

const char* get_metrics() noexcept {
    try {
        const RuntimeMetricsSnapshot snapshot = metrics_snapshot();
        QJsonObject metrics;
        metrics.insert(QStringLiteral("instanceCount"), snapshot.active_instance_count);
        metrics.insert(QStringLiteral("activeInstanceCount"), snapshot.active_instance_count);
        metrics.insert(QStringLiteral("typeRegistrationCount"), static_cast<double>(snapshot.type_registration_count));
        metrics.insert(QStringLiteral("stateSyncCount"), static_cast<double>(snapshot.state_sync_count));
        metrics.insert(QStringLiteral("commandDispatchCount"), static_cast<double>(snapshot.command_dispatch_count));
        metrics.insert(QStringLiteral("effectDispatchCount"), static_cast<double>(snapshot.effect_dispatch_count));
        metrics.insert(QStringLiteral("hotReloadCount"), static_cast<double>(snapshot.hot_reload_count));
        metrics.insert(QStringLiteral("lastHotReloadDurationMs"),
                       static_cast<double>(snapshot.last_hot_reload_duration_ms));
        metrics.insert(QStringLiteral("errorCount"), static_cast<double>(snapshot.error_count));
        metrics.insert(QStringLiteral("errorOverlayVisible"), snapshot.error_overlay_visible);

        const char* result = allocate_document(QJsonDocument(metrics));
        if (result != nullptr) {
            clear_last_error();
        }

        return result;
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_get_metrics failed: ") + error.what());
        return nullptr;
    } catch (...) {
        set_last_error("qmlsharp_get_metrics failed due to an unknown native exception.");
        return nullptr;
    }
}
}  // namespace qmlsharp
