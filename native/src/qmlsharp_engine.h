#pragma once

#include "qmlsharp/qmlsharp_abi.h"

#include <QObject>
#include <QQmlEngine>
#include <memory>

namespace qmlsharp {
// Thread affinity: created on the Qt main thread and used only from that thread.
class QmlSharpEngine final : public QObject {
    Q_OBJECT

public:
    explicit QmlSharpEngine(std::unique_ptr<QQmlEngine> engine, QObject* parent = nullptr);

    QQmlEngine* qml_engine() const noexcept;

private:
    std::unique_ptr<QQmlEngine> engine_;
};

void* engine_init(int argc, const char** argv) noexcept;
void engine_shutdown(void* engine) noexcept;
int engine_exec(void* engine) noexcept;
void post_to_main_thread(qmlsharp_main_thread_callback callback, void* user_data) noexcept;
bool validate_engine_call(void* engine, const char* operation) noexcept;
}  // namespace qmlsharp
