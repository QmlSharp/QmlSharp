#include "qmlsharp_hot_reload.h"

#include <QByteArray>
#include <QElapsedTimer>
#include <QFileInfo>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonParseError>
#include <QJsonValue>
#include <QMetaObject>
#include <QMetaProperty>
#include <QObject>
#include <QQmlComponent>
#include <QStringList>
#include <QUrl>
#include <QVariant>
#include <cstdlib>
#include <cstring>
#include <deque>
#include <exception>
#include <map>
#include <string>
#include <vector>

#include "qmlsharp_engine.h"
#include "qmlsharp_errors.h"
#include "qmlsharp_instances.h"
#include "qmlsharp_metrics.h"
#include "qmlsharp_state.h"

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
        set_last_error("Failed to allocate native snapshot string.");
        return nullptr;
    }

    std::memcpy(buffer, value.constData(), static_cast<std::size_t>(value.size()));
    buffer[value.size()] = '\0';
    return buffer;
}

std::string to_utf8_string(const QString& value) {
    const QByteArray utf8 = value.toUtf8();
    return std::string(utf8.constData(), static_cast<std::size_t>(utf8.size()));
}

void write_property_if_valid(QJsonObject& target, QObject* object, const char* property_name) {
    const QVariant value = object->property(property_name);
    if (value.isValid()) {
        target.insert(QString::fromLatin1(property_name), QJsonValue::fromVariant(value));
    }
}

QJsonObject capture_root_window(QObject* root) {
    QJsonObject window;
    if (root == nullptr) {
        return window;
    }

    write_property_if_valid(window, root, "x");
    write_property_if_valid(window, root, "y");
    write_property_if_valid(window, root, "width");
    write_property_if_valid(window, root, "height");
    write_property_if_valid(window, root, "visible");
    return window;
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

QJsonObject capture_instance(const NativeInstanceRecordSnapshot& instance) {
    QJsonObject snapshot;
    snapshot.insert(QStringLiteral("instanceId"), QString::fromStdString(instance.instance_id));
    snapshot.insert(QStringLiteral("className"), QString::fromStdString(instance.class_name));
    snapshot.insert(QStringLiteral("compilerSlotKey"), QString::fromStdString(instance.compiler_slot_key));
    snapshot.insert(QStringLiteral("ready"), instance.ready);
    snapshot.insert(QStringLiteral("properties"), capture_instance_properties(instance.object.data()));
    return snapshot;
}

QJsonDocument make_snapshot(QmlSharpEngine* native_engine) {
    QJsonArray instances;
    const std::vector<NativeInstanceRecordSnapshot> records = snapshot_instances();
    for (const NativeInstanceRecordSnapshot& instance : records) {
        if (!instance.object.isNull()) {
            instances.append(capture_instance(instance));
        }
    }

    QJsonObject root;
    root.insert(QStringLiteral("window"), capture_root_window(native_engine->primary_root_object()));
    root.insert(QStringLiteral("focusId"), QString());
    root.insert(QStringLiteral("scrollPositions"), QJsonObject());
    root.insert(QStringLiteral("instances"), instances);
    return QJsonDocument(root);
}

void restore_root_window(QObject* root, const QJsonObject& window) {
    if (root == nullptr) {
        return;
    }

    const QStringList property_names{
        QStringLiteral("x"),      QStringLiteral("y"),       QStringLiteral("width"),
        QStringLiteral("height"), QStringLiteral("visible"),
    };

    for (const QString& property_name : property_names) {
        const QJsonValue value = window.value(property_name);
        if (!value.isUndefined()) {
            const QByteArray property_name_utf8 = property_name.toUtf8();
            root->setProperty(property_name_utf8.constData(), value.toVariant());
        }
    }
}

using InstanceRestoreKey = std::pair<std::string, std::string>;
using InstanceRestoreTargets = std::map<InstanceRestoreKey, std::deque<std::string>>;

InstanceRestoreTargets make_restore_targets() {
    InstanceRestoreTargets targets;
    const std::vector<NativeInstanceRecordSnapshot> records = snapshot_instances();
    for (const NativeInstanceRecordSnapshot& record : records) {
        if (record.object.isNull()) {
            continue;
        }

        targets[{record.class_name, record.compiler_slot_key}].push_back(record.instance_id);
    }

    return targets;
}

int restore_instance_state(const QJsonObject& instance, InstanceRestoreTargets& targets) {
    const QString class_name = instance.value(QStringLiteral("className")).toString();
    const QString compiler_slot_key = instance.value(QStringLiteral("compilerSlotKey")).toString();
    const QJsonValue properties_value = instance.value(QStringLiteral("properties"));
    if (class_name.isEmpty() || compiler_slot_key.isEmpty() || !properties_value.isObject()) {
        return QmlSharpSuccess;
    }

    const std::string class_name_utf8 = to_utf8_string(class_name);
    const std::string compiler_slot_key_utf8 = to_utf8_string(compiler_slot_key);
    const auto target = targets.find({class_name_utf8, compiler_slot_key_utf8});
    if (target == targets.end() || target->second.empty()) {
        return QmlSharpSuccess;
    }

    const std::string instance_id = target->second.front();
    target->second.pop_front();

    const QByteArray properties_json = QJsonDocument(properties_value.toObject()).toJson(QJsonDocument::Compact);
    return sync_state_batch(instance_id.c_str(), properties_json.constData());
}
}  // namespace

const char* capture_snapshot(void* engine) noexcept {
    try {
        if (!validate_engine_call(engine, "qmlsharp_capture_snapshot")) {
            return nullptr;
        }

        auto* native_engine = static_cast<QmlSharpEngine*>(engine);
        const QByteArray snapshot = make_snapshot(native_engine).toJson(QJsonDocument::Compact);
        const char* result = allocate_string(snapshot);
        if (result != nullptr) {
            clear_last_error();
        }

        return result;
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_capture_snapshot failed: ") + error.what());
        return nullptr;
    } catch (...) {
        set_last_error("qmlsharp_capture_snapshot failed due to an unknown native exception.");
        return nullptr;
    }
}

int32_t reload_qml(void* engine, const char* qml_source_path) noexcept {
    QElapsedTimer timer;
    timer.start();
    try {
        if (!validate_engine_call(engine, "qmlsharp_reload_qml")) {
            return engine == nullptr ? QmlSharpInvalidArgument : QmlSharpEngineNotInitialized;
        }

        if (is_blank(qml_source_path)) {
            set_last_error("qmlsharp_reload_qml requires a non-empty QML source path.");
            return QmlSharpInvalidArgument;
        }

        const QFileInfo source_file(QString::fromUtf8(qml_source_path));
        if (!source_file.exists() || !source_file.isFile()) {
            set_last_error(std::string("qmlsharp_reload_qml QML source file was not found: ") + qml_source_path);
            record_hot_reload(timer.elapsed());
            return QmlSharpQmlLoadFailure;
        }

        auto* native_engine = static_cast<QmlSharpEngine*>(engine);
        QQmlComponent component(native_engine->qml_engine(), QUrl::fromLocalFile(source_file.absoluteFilePath()));
        QObject* root = component.create();
        if (root == nullptr) {
            set_last_error(std::string("qmlsharp_reload_qml failed to create QML root: ") +
                           component.errorString().toStdString());
            record_hot_reload(timer.elapsed());
            return QmlSharpQmlLoadFailure;
        }

        native_engine->replace_root_object(root);
        record_hot_reload(timer.elapsed());
        clear_last_error();
        return QmlSharpSuccess;
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_reload_qml failed: ") + error.what());
        record_hot_reload(timer.elapsed());
        return QmlSharpQmlLoadFailure;
    } catch (...) {
        set_last_error("qmlsharp_reload_qml failed due to an unknown native exception.");
        record_hot_reload(timer.elapsed());
        return QmlSharpQmlLoadFailure;
    }
}

void restore_snapshot(void* engine, const char* snapshot_json) noexcept {
    try {
        if (!validate_engine_call(engine, "qmlsharp_restore_snapshot")) {
            return;
        }

        if (is_blank(snapshot_json)) {
            set_last_error("qmlsharp_restore_snapshot requires a non-empty snapshot JSON payload.");
            return;
        }

        QJsonParseError parse_error;
        const QJsonDocument document = QJsonDocument::fromJson(QByteArray(snapshot_json), &parse_error);
        if (parse_error.error != QJsonParseError::NoError || document.isNull() || !document.isObject()) {
            set_last_error(std::string("qmlsharp_restore_snapshot received invalid JSON object: ") +
                           parse_error.errorString().toStdString());
            return;
        }

        auto* native_engine = static_cast<QmlSharpEngine*>(engine);
        const QJsonObject snapshot = document.object();
        const QJsonValue window = snapshot.value(QStringLiteral("window"));
        if (window.isObject()) {
            restore_root_window(native_engine->primary_root_object(), window.toObject());
        }

        const QJsonValue instances = snapshot.value(QStringLiteral("instances"));
        if (instances.isArray()) {
            InstanceRestoreTargets targets = make_restore_targets();
            for (const QJsonValue& instance : instances.toArray()) {
                if (!instance.isObject()) {
                    continue;
                }

                const int result = restore_instance_state(instance.toObject(), targets);
                if (result != QmlSharpSuccess) {
                    return;
                }
            }
        }

        clear_last_error();
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_restore_snapshot failed: ") + error.what());
    } catch (...) {
        set_last_error("qmlsharp_restore_snapshot failed due to an unknown native exception.");
    }
}
}  // namespace qmlsharp
