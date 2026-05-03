#include "qmlsharp_effects.h"

#include <QByteArray>
#include <QJsonDocument>
#include <QJsonParseError>
#include <QMetaObject>
#include <QObject>
#include <QPointer>
#include <QString>
#include <QThread>
#include <exception>
#include <string>
#include <utility>
#include <vector>

#include "qmlsharp_errors.h"
#include "qmlsharp_instances.h"

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

int normalize_payload(const char* payload_json, std::string& payload) {
    if (is_blank(payload_json)) {
        payload = "{}";
        return QmlSharpSuccess;
    }

    QJsonParseError parse_error;
    const QJsonDocument document = QJsonDocument::fromJson(QByteArray(payload_json), &parse_error);
    if (parse_error.error != QJsonParseError::NoError || document.isNull()) {
        set_last_error(std::string("Effect dispatch received invalid JSON payload: ") +
                       parse_error.errorString().toStdString());
        return QmlSharpJsonParseFailure;
    }

    payload = payload_json;
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
        set_last_error("Effect dispatch failed to marshal to the QObject thread.");
        return QmlSharpGeneralFailure;
    }

    if (captured_error.empty()) {
        clear_last_error();
    } else {
        set_last_error(captured_error);
    }

    return result;
}

int emit_effect_on_object(QObject* object, const std::string& effect_name, const std::string& payload_json) {
    const QString effect_name_qt = QString::fromUtf8(effect_name.c_str(), static_cast<qsizetype>(effect_name.size()));
    const QString payload_json_qt =
        QString::fromUtf8(payload_json.c_str(), static_cast<qsizetype>(payload_json.size()));
    bool emitted = QMetaObject::invokeMethod(object, "emitEffectDispatched", Qt::DirectConnection,
                                             Q_ARG(QString, effect_name_qt), Q_ARG(QString, payload_json_qt));
    if (!emitted) {
        emitted = QMetaObject::invokeMethod(object, "effectDispatched", Qt::DirectConnection,
                                            Q_ARG(QString, effect_name_qt), Q_ARG(QString, payload_json_qt));
    }

    if (!emitted) {
        set_last_error("Effect dispatch target does not expose an effect hook.");
        return QmlSharpGeneralFailure;
    }

    clear_last_error();
    return QmlSharpSuccess;
}

int emit_effect(QObject* object, const std::string& effect_name, const std::string& payload_json) noexcept {
    return run_on_object_thread(object, [object, effect_name, payload_json]() {
        return emit_effect_on_object(object, effect_name, payload_json);
    });
}
}  // namespace

int dispatch_effect(const char* instance_id, const char* effect_name, const char* payload_json) noexcept {
    try {
        if (is_blank(instance_id)) {
            set_last_error("qmlsharp_dispatch_effect requires a non-empty instanceId.");
            return QmlSharpInvalidArgument;
        }

        if (is_blank(effect_name)) {
            set_last_error("qmlsharp_dispatch_effect requires a non-empty effect name.");
            return QmlSharpInvalidArgument;
        }

        std::string payload;
        const int payload_result = normalize_payload(payload_json, payload);
        if (payload_result != QmlSharpSuccess) {
            return payload_result;
        }

        QObject* object = find_instance_object(instance_id);
        if (object == nullptr) {
            set_last_error(std::string("qmlsharp_dispatch_effect target instance '") + instance_id +
                           "' was not found.");
            return QmlSharpInstanceNotFound;
        }

        return emit_effect(object, effect_name, payload);
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_dispatch_effect failed: ") + error.what());
        return QmlSharpGeneralFailure;
    } catch (...) {
        set_last_error("qmlsharp_dispatch_effect failed due to an unknown native exception.");
        return QmlSharpGeneralFailure;
    }
}

int broadcast_effect(const char* class_name, const char* effect_name, const char* payload_json) noexcept {
    try {
        if (is_blank(class_name)) {
            set_last_error("qmlsharp_broadcast_effect requires a non-empty class name.");
            return QmlSharpInvalidArgument;
        }

        if (is_blank(effect_name)) {
            set_last_error("qmlsharp_broadcast_effect requires a non-empty effect name.");
            return QmlSharpInvalidArgument;
        }

        std::string payload;
        const int payload_result = normalize_payload(payload_json, payload);
        if (payload_result != QmlSharpSuccess) {
            return payload_result;
        }

        std::vector<QPointer<QObject>> objects = find_instance_objects_by_class(class_name);
        if (objects.empty()) {
            clear_last_error();
            return QmlSharpSuccess;
        }

        int first_failure = QmlSharpSuccess;
        std::string first_error;
        for (const QPointer<QObject>& object : objects) {
            if (object.isNull()) {
                continue;
            }

            const int result = emit_effect(object.data(), effect_name, payload);
            if (result != QmlSharpSuccess && first_failure == QmlSharpSuccess) {
                first_failure = result;
                first_error = last_error();
            }
        }

        if (first_failure == QmlSharpSuccess) {
            clear_last_error();
            return QmlSharpSuccess;
        }

        if (!first_error.empty()) {
            set_last_error(first_error);
        }

        return first_failure;
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_broadcast_effect failed: ") + error.what());
        return QmlSharpGeneralFailure;
    } catch (...) {
        set_last_error("qmlsharp_broadcast_effect failed due to an unknown native exception.");
        return QmlSharpGeneralFailure;
    }
}
}  // namespace qmlsharp
