#include "qmlsharp_error_overlay.h"

#include <QGuiApplication>
#include <QQmlComponent>
#include <QString>
#include <QUrl>
#include <exception>
#include <string>

#include "qmlsharp_engine.h"
#include "qmlsharp_errors.h"
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

constexpr const char* overlay_qml = R"qml(
import QtQuick
import QtQuick.Window

Window {
    id: root
    objectName: "__qmlsharp_error_overlay"
    width: 720
    height: 320
    visible: true
    color: "#2a0808"
    title: "QmlSharp Error"

    property string overlayTitle: ""
    property string overlayMessage: ""
    property string overlayFile: ""
    property int overlayLine: 0
    property int overlayColumn: 0

    Rectangle {
        anchors.fill: parent
        color: "#2a0808"
        border.color: "#ff6b6b"
        border.width: 2

        Text {
            anchors.fill: parent
            anchors.margins: 24
            color: "white"
            wrapMode: Text.Wrap
            font.pixelSize: 16
            text: root.overlayTitle + "\n\n" + root.overlayMessage + "\n\n" +
                root.overlayFile + ":" + root.overlayLine + ":" + root.overlayColumn
        }
    }
}
)qml";

int validate_payload(const char* title, const char* message, int32_t line, int32_t column) noexcept {
    if (is_blank(title)) {
        set_last_error("qmlsharp_show_error requires a non-empty title.");
        return QmlSharpInvalidArgument;
    }

    if (is_blank(message)) {
        set_last_error("qmlsharp_show_error requires a non-empty message.");
        return QmlSharpInvalidArgument;
    }

    if (line < 0 || column < 0) {
        set_last_error("qmlsharp_show_error requires non-negative line and column values.");
        return QmlSharpInvalidArgument;
    }

    return QmlSharpSuccess;
}
}  // namespace

void show_error(void* engine, const char* title, const char* message, const char* file_path, int32_t line,
                int32_t column) noexcept {
    try {
        if (!validate_engine_call(engine, "qmlsharp_show_error")) {
            return;
        }

        if (validate_payload(title, message, line, column) != QmlSharpSuccess) {
            return;
        }

        auto* native_engine = static_cast<QmlSharpEngine*>(engine);
        const QString platform_name = QGuiApplication::platformName();
        if (platform_name == QStringLiteral("offscreen") || platform_name == QStringLiteral("minimal")) {
            set_error_overlay_visible(true);
            clear_last_error();
            return;
        }

        QQmlComponent component(native_engine->qml_engine());
        component.setData(overlay_qml, QUrl(QStringLiteral("qrc:/qmlsharp/error-overlay.qml")));
        QObject* overlay = component.create();
        if (overlay == nullptr) {
            set_last_error(std::string("qmlsharp_show_error failed to create overlay: ") +
                           component.errorString().toStdString());
            set_error_overlay_visible(false);
            return;
        }

        overlay->setProperty("overlayTitle", QString::fromUtf8(title));
        overlay->setProperty("overlayMessage", QString::fromUtf8(message));
        overlay->setProperty("overlayFile", QString::fromUtf8(file_path == nullptr ? "" : file_path));
        overlay->setProperty("overlayLine", line);
        overlay->setProperty("overlayColumn", column);
        native_engine->set_error_overlay(overlay);
        set_error_overlay_visible(true);
        clear_last_error();
    } catch (const std::exception& error) {
        set_error_overlay_visible(false);
        set_last_error(std::string("qmlsharp_show_error failed: ") + error.what());
    } catch (...) {
        set_error_overlay_visible(false);
        set_last_error("qmlsharp_show_error failed due to an unknown native exception.");
    }
}

void hide_error(void* engine) noexcept {
    try {
        if (!validate_engine_call(engine, "qmlsharp_hide_error")) {
            return;
        }

        auto* native_engine = static_cast<QmlSharpEngine*>(engine);
        native_engine->clear_error_overlay();
        set_error_overlay_visible(false);
        clear_last_error();
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_hide_error failed: ") + error.what());
    } catch (...) {
        set_last_error("qmlsharp_hide_error failed due to an unknown native exception.");
    }
}
}  // namespace qmlsharp
