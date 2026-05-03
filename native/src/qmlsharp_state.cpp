#include "qmlsharp_state.h"

#include <QByteArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonParseError>
#include <QMetaObject>
#include <QMetaProperty>
#include <QObject>
#include <QString>
#include <QStringList>
#include <QThread>
#include <QVariant>
#include <algorithm>
#include <exception>
#include <string>
#include <utility>
#include <vector>

#include "qmlsharp_errors.h"
#include "qmlsharp_instances.h"

namespace qmlsharp {
namespace {
struct StateUpdate {
    std::string property_name;
    QVariant value;
};

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

QByteArray property_name_bytes(const std::string& property_name) {
    return QByteArray(property_name.data(), static_cast<qsizetype>(property_name.size()));
}

int validate_common_arguments(const char* instance_id, const char* property_name, const char* operation) noexcept {
    if (is_blank(instance_id)) {
        set_last_error(std::string(operation) + " requires a non-empty instanceId.");
        return QmlSharpInvalidArgument;
    }

    if (is_blank(property_name)) {
        set_last_error(std::string(operation) + " requires a non-empty property name.");
        return QmlSharpInvalidArgument;
    }

    return QmlSharpSuccess;
}

int validate_writable_property(QObject* object, const std::string& property_name) {
    const QByteArray property_name_utf8 = property_name_bytes(property_name);
    const int property_index = object->metaObject()->indexOfProperty(property_name_utf8.constData());
    if (property_index < 0) {
        set_last_error("State sync target property '" + property_name + "' was not found.");
        return QmlSharpPropertyNotFound;
    }

    const QMetaProperty property = object->metaObject()->property(property_index);
    if (!property.isWritable()) {
        set_last_error("State sync target property '" + property_name + "' is not writable.");
        return QmlSharpPropertyNotFound;
    }

    return QmlSharpSuccess;
}

int set_property(QObject* object, const std::string& property_name, const QVariant& value) {
    const int validation = validate_writable_property(object, property_name);
    if (validation != QmlSharpSuccess) {
        return validation;
    }

    const QByteArray property_name_utf8 = property_name_bytes(property_name);
    if (!object->setProperty(property_name_utf8.constData(), value)) {
        set_last_error("State sync failed to set property '" + property_name + "'.");
        return QmlSharpGeneralFailure;
    }

    clear_last_error();
    return QmlSharpSuccess;
}

template <typename Operation>
int run_on_object_thread(QObject* object, Operation operation) noexcept {
    if (object->thread() == QThread::currentThread()) {
        return operation();
    }

    int result = QmlSharpGeneralFailure;
    std::string captured_error;
    const bool invoked = QMetaObject::invokeMethod(
        object,
        [&result, &captured_error, operation = std::move(operation)]() mutable {
            result = operation();
            captured_error = last_error();
        },
        Qt::BlockingQueuedConnection);

    if (!invoked) {
        set_last_error("State sync failed to marshal to the QObject thread.");
        return QmlSharpGeneralFailure;
    }

    if (captured_error.empty()) {
        clear_last_error();
    } else {
        set_last_error(captured_error);
    }

    return result;
}

int parse_json_value(const char* json_value, QVariant& value, const char* operation) {
    if (is_blank(json_value)) {
        set_last_error(std::string(operation) + " requires a non-empty JSON payload.");
        return QmlSharpInvalidArgument;
    }

    QJsonParseError parse_error;
    const QJsonDocument document = QJsonDocument::fromJson(QByteArray(json_value), &parse_error);
    if (parse_error.error != QJsonParseError::NoError || document.isNull()) {
        set_last_error(std::string(operation) + " received invalid JSON: " + parse_error.errorString().toStdString());
        return QmlSharpJsonParseFailure;
    }

    value = document.toVariant();
    if (!value.isValid()) {
        set_last_error(std::string(operation) + " requires a JSON object or array payload.");
        return QmlSharpJsonParseFailure;
    }

    return QmlSharpSuccess;
}

int sync_single_state_value(const char* instance_id, const char* property_name, QVariant value,
                            const char* operation) noexcept {
    try {
        const int validation = validate_common_arguments(instance_id, property_name, operation);
        if (validation != QmlSharpSuccess) {
            return validation;
        }

        QObject* object = find_instance_object(instance_id);
        if (object == nullptr) {
            set_last_error(std::string(operation) + " target instance '" + instance_id + "' was not found.");
            return QmlSharpInstanceNotFound;
        }

        const std::string property_name_utf8(property_name);
        return run_on_object_thread(object, [object, property_name_utf8, value = std::move(value)]() {
            return set_property(object, property_name_utf8, value);
        });
    } catch (const std::exception& error) {
        set_last_error(std::string(operation) + " failed: " + error.what());
        return QmlSharpGeneralFailure;
    } catch (...) {
        set_last_error(std::string(operation) + " failed due to an unknown native exception.");
        return QmlSharpGeneralFailure;
    }
}
}  // namespace

int sync_state_string(const char* instance_id, const char* property_name, const char* value) noexcept {
    const char* safe_value = value == nullptr ? "" : value;
    return sync_single_state_value(instance_id, property_name, QString::fromUtf8(safe_value),
                                   "qmlsharp_sync_state_string");
}

int sync_state_int(const char* instance_id, const char* property_name, int32_t value) noexcept {
    return sync_single_state_value(instance_id, property_name, value, "qmlsharp_sync_state_int");
}

int sync_state_double(const char* instance_id, const char* property_name, double value) noexcept {
    return sync_single_state_value(instance_id, property_name, value, "qmlsharp_sync_state_double");
}

int sync_state_bool(const char* instance_id, const char* property_name, int32_t value) noexcept {
    return sync_single_state_value(instance_id, property_name, value != 0, "qmlsharp_sync_state_bool");
}

int sync_state_json(const char* instance_id, const char* property_name, const char* json_value) noexcept {
    QVariant value;
    const int parse_result = parse_json_value(json_value, value, "qmlsharp_sync_state_json");
    if (parse_result != QmlSharpSuccess) {
        return parse_result;
    }

    return sync_single_state_value(instance_id, property_name, std::move(value), "qmlsharp_sync_state_json");
}

int sync_state_batch(const char* instance_id, const char* properties_json) noexcept {
    try {
        if (is_blank(instance_id)) {
            set_last_error("qmlsharp_sync_state_batch requires a non-empty instanceId.");
            return QmlSharpInvalidArgument;
        }

        if (is_blank(properties_json)) {
            set_last_error("qmlsharp_sync_state_batch requires a non-empty JSON payload.");
            return QmlSharpInvalidArgument;
        }

        QJsonParseError parse_error;
        const QJsonDocument document = QJsonDocument::fromJson(QByteArray(properties_json), &parse_error);
        if (parse_error.error != QJsonParseError::NoError || document.isNull() || !document.isObject()) {
            set_last_error(std::string("qmlsharp_sync_state_batch received invalid JSON object: ") +
                           parse_error.errorString().toStdString());
            return QmlSharpJsonParseFailure;
        }

        const QVariantMap properties = document.object().toVariantMap();
        if (properties.isEmpty()) {
            clear_last_error();
            return QmlSharpSuccess;
        }

        QObject* object = find_instance_object(instance_id);
        if (object == nullptr) {
            set_last_error(std::string("qmlsharp_sync_state_batch target instance '") + instance_id +
                           "' was not found.");
            return QmlSharpInstanceNotFound;
        }

        std::vector<StateUpdate> updates;
        updates.reserve(static_cast<std::size_t>(properties.size()));
        QStringList keys = properties.keys();
        std::sort(keys.begin(), keys.end());
        for (const QString& key : keys) {
            const QByteArray key_utf8 = key.toUtf8();
            updates.push_back(StateUpdate{
                std::string(key_utf8.constData(), static_cast<std::size_t>(key_utf8.size())),
                properties.value(key),
            });
        }

        return run_on_object_thread(object, [object, updates = std::move(updates)]() {
            for (const StateUpdate& update : updates) {
                const int validation = validate_writable_property(object, update.property_name);
                if (validation != QmlSharpSuccess) {
                    return validation;
                }
            }

            for (const StateUpdate& update : updates) {
                const QByteArray property_name_utf8 = property_name_bytes(update.property_name);
                if (!object->setProperty(property_name_utf8.constData(), update.value)) {
                    set_last_error("Batch state sync failed to set property '" + update.property_name + "'.");
                    return QmlSharpGeneralFailure;
                }
            }

            clear_last_error();
            return QmlSharpSuccess;
        });
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_sync_state_batch failed: ") + error.what());
        return QmlSharpGeneralFailure;
    } catch (...) {
        set_last_error("qmlsharp_sync_state_batch failed due to an unknown native exception.");
        return QmlSharpGeneralFailure;
    }
}
}  // namespace qmlsharp
