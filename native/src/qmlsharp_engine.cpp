#include "qmlsharp_engine.h"

#include <QCoreApplication>
#include <QGuiApplication>
#include <QMetaObject>
#include <QQmlEngine>
#include <QThread>
#include <exception>
#include <memory>
#include <mutex>
#include <string>
#include <utility>
#include <vector>

#include "qmlsharp_errors.h"

namespace qmlsharp {
namespace {
std::mutex engine_mutex;
std::unique_ptr<QGuiApplication> application;
std::unique_ptr<QmlSharpEngine> current_engine;
std::vector<std::string> argument_storage;
std::vector<char*> argument_pointers;
QThread* qt_main_thread = nullptr;
bool event_loop_running = false;
bool shutdown_requested = false;

bool is_qt_main_thread() noexcept {
    return qt_main_thread != nullptr && QThread::currentThread() == qt_main_thread;
}

bool validate_engine_handle(void* engine, const char* operation) noexcept {
    if (engine == nullptr) {
        set_last_error(std::string(operation) + " requires a non-null engine handle.");
        return false;
    }

    if (current_engine.get() == nullptr || engine != current_engine.get()) {
        set_last_error(std::string(operation) + " requires an initialized QmlSharp engine.");
        return false;
    }

    return true;
}

bool validate_qt_main_thread(const char* operation) noexcept {
    if (!is_qt_main_thread()) {
        set_last_error(std::string(operation) + " must be called on the Qt main thread.");
        return false;
    }

    return true;
}

void reset_argument_storage() {
    argument_pointers.clear();
    argument_storage.clear();
}

void reset_engine_state() {
    current_engine.reset();
    application.reset();
    qt_main_thread = nullptr;
    event_loop_running = false;
    shutdown_requested = false;
    reset_argument_storage();
}
}  // namespace

bool validate_engine_call(void* engine, const char* operation) noexcept {
    std::lock_guard<std::mutex> lock(engine_mutex);
    return validate_engine_handle(engine, operation) && validate_qt_main_thread(operation);
}

QmlSharpEngine::QmlSharpEngine(std::unique_ptr<QQmlEngine> engine, QObject* parent)
    : QObject(parent), engine_(std::move(engine)) {}

QQmlEngine* QmlSharpEngine::qml_engine() const noexcept {
    return engine_.get();
}

QObject* QmlSharpEngine::primary_root_object() const noexcept {
    for (const QPointer<QObject>& object : root_objects_) {
        if (!object.isNull()) {
            return object.data();
        }
    }

    return nullptr;
}

QObject* QmlSharpEngine::error_overlay() const noexcept {
    return error_overlay_.data();
}

void QmlSharpEngine::replace_root_object(QObject* object) {
    clear_root_objects();
    if (object == nullptr) {
        return;
    }

    QQmlEngine::setObjectOwnership(object, QQmlEngine::CppOwnership);
    object->setParent(this);
    root_objects_.push_back(object);
}

void QmlSharpEngine::clear_root_objects() noexcept {
    for (const QPointer<QObject>& object : root_objects_) {
        if (!object.isNull()) {
            delete object.data();
        }
    }

    root_objects_.clear();
}

void QmlSharpEngine::set_error_overlay(QObject* object) {
    clear_error_overlay();
    if (object == nullptr) {
        return;
    }

    QQmlEngine::setObjectOwnership(object, QQmlEngine::CppOwnership);
    object->setParent(this);
    error_overlay_ = object;
}

void QmlSharpEngine::clear_error_overlay() noexcept {
    if (!error_overlay_.isNull()) {
        delete error_overlay_.data();
        error_overlay_.clear();
    }
}

void* engine_init(int argc, const char** argv) noexcept {
    try {
        std::lock_guard<std::mutex> lock(engine_mutex);

        if (argc < 0) {
            set_last_error("qmlsharp_engine_init requires argc to be zero or greater.");
            return nullptr;
        }

        if (argc > 0 && argv == nullptr) {
            set_last_error("qmlsharp_engine_init requires argv when argc is greater than zero.");
            return nullptr;
        }

        if (current_engine != nullptr) {
            clear_last_error();
            return current_engine.get();
        }

        if (QCoreApplication::instance() != nullptr && application == nullptr) {
            set_last_error("A Qt application already exists outside QmlSharp ownership.");
            return nullptr;
        }

        reset_argument_storage();
        argument_storage.reserve(static_cast<std::size_t>(argc == 0 ? 1 : argc));

        if (argc == 0) {
            argument_storage.emplace_back("qmlsharp");
        } else {
            for (int index = 0; index < argc; ++index) {
                if (argv[index] == nullptr) {
                    reset_argument_storage();
                    set_last_error("qmlsharp_engine_init received a null argv entry.");
                    return nullptr;
                }

                argument_storage.emplace_back(argv[index]);
            }
        }

        argument_pointers.reserve(argument_storage.size());
        for (std::string& argument : argument_storage) {
            argument_pointers.push_back(argument.data());
        }

        int qt_argc = static_cast<int>(argument_pointers.size());
        application = std::make_unique<QGuiApplication>(qt_argc, argument_pointers.data());
        qt_main_thread = QThread::currentThread();
        shutdown_requested = false;

        auto qml_engine = std::make_unique<QQmlEngine>();
        current_engine = std::make_unique<QmlSharpEngine>(std::move(qml_engine));

        clear_last_error();
        return current_engine.get();
    } catch (const std::exception& error) {
        reset_engine_state();
        set_last_error(std::string("Failed to initialize QmlSharp engine: ") + error.what());
        return nullptr;
    } catch (...) {
        reset_engine_state();
        set_last_error("Failed to initialize QmlSharp engine due to an unknown native exception.");
        return nullptr;
    }
}

void engine_shutdown(void* engine) noexcept {
    try {
        std::lock_guard<std::mutex> lock(engine_mutex);

        if (engine == nullptr) {
            clear_last_error();
            return;
        }

        if (current_engine.get() == nullptr || engine != current_engine.get()) {
            set_last_error("qmlsharp_engine_shutdown received an engine handle that is not active.");
            return;
        }

        if (!validate_qt_main_thread("qmlsharp_engine_shutdown")) {
            return;
        }

        if (event_loop_running) {
            shutdown_requested = true;
            application->quit();
            clear_last_error();
            return;
        }

        reset_engine_state();
        clear_last_error();
    } catch (const std::exception& error) {
        set_last_error(std::string("Failed to shut down QmlSharp engine: ") + error.what());
    } catch (...) {
        set_last_error("Failed to shut down QmlSharp engine due to an unknown native exception.");
    }
}

int engine_exec(void* engine) noexcept {
    try {
        std::unique_lock<std::mutex> lock(engine_mutex);

        if (engine == nullptr) {
            set_last_error("qmlsharp_engine_exec requires a non-null engine handle.");
            return QmlSharpInvalidArgument;
        }

        if (!validate_engine_handle(engine, "qmlsharp_engine_exec")) {
            return QmlSharpEngineNotInitialized;
        }

        if (!validate_qt_main_thread("qmlsharp_engine_exec")) {
            return QmlSharpGeneralFailure;
        }

        QGuiApplication* app = application.get();
        clear_last_error();
        event_loop_running = true;
        lock.unlock();
        const int exit_code = app->exec();
        lock.lock();
        event_loop_running = false;
        if (shutdown_requested) {
            reset_engine_state();
        }

        return exit_code;
    } catch (const std::exception& error) {
        std::lock_guard<std::mutex> lock(engine_mutex);
        event_loop_running = false;
        shutdown_requested = false;
        set_last_error(std::string("QmlSharp engine event loop failed: ") + error.what());
        return QmlSharpGeneralFailure;
    } catch (...) {
        std::lock_guard<std::mutex> lock(engine_mutex);
        event_loop_running = false;
        shutdown_requested = false;
        set_last_error("QmlSharp engine event loop failed due to an unknown native exception.");
        return QmlSharpGeneralFailure;
    }
}

void post_to_main_thread(qmlsharp_main_thread_callback callback, void* user_data) noexcept {
    try {
        std::lock_guard<std::mutex> lock(engine_mutex);

        if (callback == nullptr) {
            set_last_error("qmlsharp_post_to_main_thread requires a non-null callback.");
            return;
        }

        QCoreApplication* app = QCoreApplication::instance();
        if (app == nullptr || qt_main_thread == nullptr) {
            set_last_error("qmlsharp_post_to_main_thread requires an initialized Qt application.");
            return;
        }

        const bool posted =
            QMetaObject::invokeMethod(app, [callback, user_data]() { callback(user_data); }, Qt::QueuedConnection);

        if (!posted) {
            set_last_error("Failed to post callback to the Qt main thread.");
            return;
        }

        clear_last_error();
    } catch (const std::exception& error) {
        set_last_error(std::string("Failed to post callback to the Qt main thread: ") + error.what());
    } catch (...) {
        set_last_error("Failed to post callback to the Qt main thread due to an unknown native exception.");
    }
}
}  // namespace qmlsharp
