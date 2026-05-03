#pragma once

#include "qmlsharp/qmlsharp_abi.h"

#include <QObject>
#include <QPointer>
#include <QQmlEngine>
#include <memory>
#include <vector>

namespace qmlsharp {
// Thread affinity: created on the Qt main thread and used only from that thread.
class QmlSharpEngine final : public QObject {
    Q_OBJECT

public:
    explicit QmlSharpEngine(std::unique_ptr<QQmlEngine> engine, QObject* parent = nullptr);

    QQmlEngine* qml_engine() const noexcept;
    QObject* primary_root_object() const noexcept;
    QObject* error_overlay() const noexcept;
    void replace_root_object(QObject* object);
    void clear_root_objects() noexcept;
    void set_error_overlay(QObject* object);
    void clear_error_overlay() noexcept;

private:
    std::unique_ptr<QQmlEngine> engine_;
    std::vector<QPointer<QObject>> root_objects_;
    QPointer<QObject> error_overlay_;
};

void* engine_init(int argc, const char** argv) noexcept;
void engine_shutdown(void* engine) noexcept;
int engine_exec(void* engine) noexcept;
void post_to_main_thread(qmlsharp_main_thread_callback callback, void* user_data) noexcept;
bool validate_engine_call(void* engine, const char* operation) noexcept;
}  // namespace qmlsharp
