#pragma once

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
void post_to_main_thread(void (*callback)(void* user_data), void* user_data) noexcept;
}  // namespace qmlsharp
