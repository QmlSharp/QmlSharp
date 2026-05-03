#include "qmlsharp/qmlsharp_abi.h"

#include <QCoreApplication>
#include <QEventLoop>
#include <QThread>
#include <QtGlobal>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <string>
#include <thread>

namespace {
struct CallbackProbe {
    bool called = false;
    bool ran_on_main_thread = false;
};

int fail(const char* test_name, const std::string& message) {
    std::cerr << test_name << ": " << message << '\n';
    return EXIT_FAILURE;
}

std::string read_last_error() {
    const char* message = qmlsharp_get_last_error();
    if (message == nullptr) {
        return {};
    }

    std::string result(message);
    qmlsharp_free_string(message);
    return result;
}

void pump_events_until(const CallbackProbe& probe) {
    for (int attempt = 0; attempt < 100 && !probe.called; ++attempt) {
        QCoreApplication::processEvents(QEventLoop::AllEvents, 10);
        QThread::msleep(1);
    }
}

void main_thread_probe_callback(void* user_data) {
    auto* probe = static_cast<CallbackProbe*>(user_data);
    probe->called = true;
    probe->ran_on_main_thread =
        QCoreApplication::instance() != nullptr && QThread::currentThread() == QCoreApplication::instance()->thread();
}

void quit_callback(void* user_data) {
    Q_UNUSED(user_data);
    QCoreApplication::quit();
}

void configure_headless_qt() {
    if (qEnvironmentVariableIsEmpty("QT_QPA_PLATFORM")) {
        qputenv("QT_QPA_PLATFORM", "offscreen");
    }
}

int test_abi_version_and_initial_error() {
    constexpr const char* test_name = "ABI version and initial error";

    if (qmlsharp_get_abi_version() != QMLSHARP_ABI_VERSION) {
        return fail(test_name, "qmlsharp_get_abi_version returned an unexpected ABI version.");
    }

    qmlsharp_free_string(nullptr);

    const char* last_error = qmlsharp_get_last_error();
    if (last_error != nullptr) {
        qmlsharp_free_string(last_error);
        return fail(test_name, "qmlsharp_get_last_error should return null before any native error.");
    }

    return EXIT_SUCCESS;
}

int test_engine_init_valid_args_returns_non_null() {
    constexpr const char* test_name = "ENG-01 engine_init with valid args";
    const char* argv[] = {"qmlsharp_native_tests", "--smoke"};
    void* engine = qmlsharp_engine_init(2, argv);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    qmlsharp_engine_shutdown(engine);
    return EXIT_SUCCESS;
}

int test_engine_init_empty_args_returns_non_null() {
    constexpr const char* test_name = "ENG-02 engine_init with empty args";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    qmlsharp_engine_shutdown(engine);
    return EXIT_SUCCESS;
}

int test_engine_shutdown_invalidates_handle() {
    constexpr const char* test_name = "ENG-03 engine_shutdown on valid engine";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    qmlsharp_engine_shutdown(engine);
    if (qmlsharp_engine_exec(engine) != -4) {
        return fail(test_name, "engine handle should be invalid after shutdown.");
    }

    return EXIT_SUCCESS;
}

int test_engine_shutdown_null_is_no_op() {
    constexpr const char* test_name = "ENG-04 engine_shutdown with null engine";
    qmlsharp_engine_shutdown(nullptr);
    if (!read_last_error().empty()) {
        return fail(test_name, "null shutdown should be a graceful no-op.");
    }

    return EXIT_SUCCESS;
}

int test_post_to_main_thread_executes_callback() {
    constexpr const char* test_name = "ENG-05 post_to_main_thread executes callback";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    CallbackProbe probe;
    qmlsharp_post_to_main_thread(main_thread_probe_callback, &probe);
    pump_events_until(probe);
    qmlsharp_engine_shutdown(engine);

    if (!probe.called) {
        return fail(test_name, "callback did not execute within one event pump window.");
    }

    if (!probe.ran_on_main_thread) {
        return fail(test_name, "callback did not run on the Qt main thread.");
    }

    return EXIT_SUCCESS;
}

int test_post_to_main_thread_from_background_thread() {
    constexpr const char* test_name = "ENG-06 post_to_main_thread from background thread";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    CallbackProbe probe;
    std::thread worker([&probe]() { qmlsharp_post_to_main_thread(main_thread_probe_callback, &probe); });
    worker.join();

    pump_events_until(probe);
    qmlsharp_engine_shutdown(engine);

    if (!probe.called) {
        return fail(test_name, "background-thread callback did not execute.");
    }

    if (!probe.ran_on_main_thread) {
        return fail(test_name, "background-thread callback did not run on the Qt main thread.");
    }

    return EXIT_SUCCESS;
}

int test_double_init_returns_existing_handle() {
    constexpr const char* test_name = "double init";
    void* first = qmlsharp_engine_init(0, nullptr);
    if (first == nullptr) {
        return fail(test_name, read_last_error());
    }

    void* second = qmlsharp_engine_init(0, nullptr);
    if (second != first) {
        qmlsharp_engine_shutdown(first);
        return fail(test_name, "second init should return the active process-global engine handle.");
    }

    qmlsharp_engine_shutdown(first);
    return EXIT_SUCCESS;
}

int test_exec_policy_quits_cleanly() {
    constexpr const char* test_name = "engine exec policy";
    if (qmlsharp_engine_exec(nullptr) != -2) {
        return fail(test_name, "exec with null engine should return invalid argument.");
    }

    if (read_last_error().find("non-null") == std::string::npos) {
        return fail(test_name, "exec with null engine should set a stable last-error message.");
    }

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    qmlsharp_post_to_main_thread(quit_callback, nullptr);
    const int exit_code = qmlsharp_engine_exec(engine);
    qmlsharp_engine_shutdown(engine);

    if (exit_code != 0) {
        return fail(test_name, "event loop should return the Qt success exit code.");
    }

    return EXIT_SUCCESS;
}

int test_null_argument_validation() {
    constexpr const char* test_name = "null argument validation";
    void* engine = qmlsharp_engine_init(1, nullptr);
    if (engine != nullptr) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, "engine_init should reject argc > 0 with null argv.");
    }

    if (read_last_error().find("argv") == std::string::npos) {
        return fail(test_name, "engine_init null argv failure should describe argv.");
    }

    qmlsharp_post_to_main_thread(nullptr, nullptr);
    if (read_last_error().find("callback") == std::string::npos) {
        return fail(test_name, "post_to_main_thread null callback failure should describe callback.");
    }

    return EXIT_SUCCESS;
}

int test_last_error_and_string_lifetime() {
    constexpr const char* test_name = "last error and string lifetime";
    qmlsharp_post_to_main_thread(nullptr, nullptr);

    const char* first = qmlsharp_get_last_error();
    const char* second = qmlsharp_get_last_error();
    if (first == nullptr || second == nullptr) {
        qmlsharp_free_string(first);
        qmlsharp_free_string(second);
        return fail(test_name, "last error should allocate a string for each read.");
    }

    const bool content_matches = std::strcmp(first, second) == 0;
    qmlsharp_free_string(first);
    qmlsharp_free_string(second);
    qmlsharp_free_string(nullptr);

    if (!content_matches) {
        return fail(test_name, "last error reads should return stable content.");
    }

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    qmlsharp_engine_shutdown(engine);
    if (!read_last_error().empty()) {
        return fail(test_name, "successful shutdown should clear the last native error.");
    }

    return EXIT_SUCCESS;
}

int run_test(int (*test)()) {
    const int result = test();
    if (result != EXIT_SUCCESS) {
        return result;
    }

    return EXIT_SUCCESS;
}
}  // namespace

int main() {
    configure_headless_qt();

    int result = EXIT_SUCCESS;
    result |= run_test(test_abi_version_and_initial_error);
    result |= run_test(test_engine_init_valid_args_returns_non_null);
    result |= run_test(test_engine_init_empty_args_returns_non_null);
    result |= run_test(test_engine_shutdown_invalidates_handle);
    result |= run_test(test_engine_shutdown_null_is_no_op);
    result |= run_test(test_post_to_main_thread_executes_callback);
    result |= run_test(test_post_to_main_thread_from_background_thread);
    result |= run_test(test_double_init_returns_existing_handle);
    result |= run_test(test_exec_policy_quits_cleanly);
    result |= run_test(test_null_argument_validation);
    result |= run_test(test_last_error_and_string_lifetime);

    return result;
}
