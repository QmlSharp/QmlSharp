#include "qmlsharp/qmlsharp_abi.h"

#include <qqml.h>

#include <QCoreApplication>
#include <QEventLoop>
#include <QQmlComponent>
#include <QQmlEngine>
#include <QThread>
#include <QUrl>
#include <QVariant>
#include <QtGlobal>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <memory>
#include <string>
#include <thread>

#include "RegistrationCounterViewModel.h"
#include "RegistrationStatusViewModel.h"
#include "qmlsharp_type_registry.h"

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

void shutdown_callback(void* user_data) {
    qmlsharp_engine_shutdown(user_data);
}

void configure_headless_qt() {
    if (qEnvironmentVariableIsEmpty("QT_QPA_PLATFORM")) {
        qputenv("QT_QPA_PLATFORM", "offscreen");
    }
}

int32_t register_counter_type(const char* module_uri, int32_t version_major, int32_t version_minor,
                              const char* type_name) {
    return qmlRegisterType<RegistrationCounterViewModel>(module_uri, version_major, version_minor, type_name);
}

int32_t register_status_type(const char* module_uri, int32_t version_major, int32_t version_minor,
                             const char* type_name) {
    return qmlRegisterType<RegistrationStatusViewModel>(module_uri, version_major, version_minor, type_name);
}

std::unique_ptr<QObject> create_qml_object(QQmlEngine& qml_engine, const char* qml_source, std::string& error) {
    QQmlComponent component(&qml_engine);
    component.setData(qml_source, QUrl());
    std::unique_ptr<QObject> object(component.create());
    if (object == nullptr) {
        error = component.errorString().toStdString();
    }

    return object;
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

int test_shutdown_during_exec_defers_teardown_until_loop_returns() {
    constexpr const char* test_name = "shutdown during exec defers teardown";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    qmlsharp_post_to_main_thread(shutdown_callback, engine);
    const int exit_code = qmlsharp_engine_exec(engine);
    if (exit_code != 0) {
        return fail(test_name, "event loop should return success after shutdown callback quits it.");
    }

    if (qmlsharp_engine_exec(engine) != -4) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, "engine handle should be invalid after deferred shutdown completes.");
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

int test_register_type_valid_schema_makes_qml_type_available() {
    constexpr const char* test_name = "REG-01 register_type with valid schema metadata";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    const int result =
        qmlsharp_register_type(engine, "QmlSharp.NativeRegistration.Valid", 1, 0, "RegistrationCounter",
                               "schema-counter-valid", "RegistrationView::__qmlsharp_vm0", register_counter_type);
    if (result != 0) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object =
        create_qml_object(qml_engine,
                          "import QtQml\n"
                          "import QmlSharp.NativeRegistration.Valid 1.0\n"
                          "RegistrationCounter { count: 41; Component.onCompleted: increment() }\n",
                          error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, error);
    }

    if (object->property("count").toInt() != 42) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, "registered QObject fixture did not expose Q_PROPERTY/Q_INVOKABLE behavior.");
    }

    qmlsharp_engine_shutdown(engine);
    return EXIT_SUCCESS;
}

int test_register_type_duplicate_registration_fails() {
    constexpr const char* test_name = "REG-02 duplicate type registration";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    const int first =
        qmlsharp_register_type(engine, "QmlSharp.NativeRegistration.Duplicate", 1, 0, "RegistrationCounterDuplicate",
                               "schema-counter-duplicate", "RegistrationView::__qmlsharp_vm0", register_counter_type);
    const int second =
        qmlsharp_register_type(engine, "QmlSharp.NativeRegistration.Duplicate", 1, 0, "RegistrationCounterDuplicate",
                               "schema-counter-duplicate", "RegistrationView::__qmlsharp_vm0", register_counter_type);
    const std::string error = read_last_error();
    qmlsharp_engine_shutdown(engine);

    if (first != 0) {
        return fail(test_name, "first registration should succeed.");
    }

    if (second != -6 || error.find("already exists") == std::string::npos) {
        return fail(test_name, "duplicate registration should fail with a stable registration error.");
    }

    return EXIT_SUCCESS;
}

int test_register_type_invalid_version_and_type_name_fail() {
    constexpr const char* test_name = "REG-03 invalid module version and type name";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    const int invalid_version = qmlsharp_register_type(engine, "QmlSharp.NativeRegistration.InvalidVersion", -1, 0,
                                                       "RegistrationCounterInvalidVersion", "schema-invalid-version",
                                                       "RegistrationView::__qmlsharp_vm0", register_counter_type);
    const std::string version_error = read_last_error();
    const int invalid_type_name =
        qmlsharp_register_type(engine, "QmlSharp.NativeRegistration.InvalidTypeName", 1, 0, "registrationCounter",
                               "schema-invalid-type-name", "RegistrationView::__qmlsharp_vm0", register_counter_type);
    const std::string type_error = read_last_error();
    qmlsharp_engine_shutdown(engine);

    if (invalid_version != -2 || version_error.find("module version") == std::string::npos) {
        return fail(test_name, "negative module version should fail as invalid argument.");
    }

    if (invalid_type_name != -2 || type_error.find("uppercase") == std::string::npos) {
        return fail(test_name, "lowercase QML type name should fail as invalid argument.");
    }

    return EXIT_SUCCESS;
}

int test_register_module_valid_entries_registers_all_types() {
    constexpr const char* test_name = "REG-04 register_module with valid entries";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    const qmlsharp_type_registration_entry entries[] = {
        {"RegistrationModuleCounter", "schema-module-counter", "RegistrationModuleView::__qmlsharp_vm0",
         register_counter_type},
        {"RegistrationModuleStatus", "schema-module-status", "RegistrationStatusView::__qmlsharp_vm0",
         register_status_type},
    };

    const int result = qmlsharp_register_module(engine, "QmlSharp.NativeRegistration.Module", 1, 2, entries, 2);
    if (result != 0) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string counter_error;
    std::unique_ptr<QObject> counter = create_qml_object(qml_engine,
                                                         "import QmlSharp.NativeRegistration.Module 1.2\n"
                                                         "RegistrationModuleCounter { count: 7 }\n",
                                                         counter_error);
    std::string status_error;
    std::unique_ptr<QObject> status = create_qml_object(qml_engine,
                                                        "import QmlSharp.NativeRegistration.Module 1.2\n"
                                                        "RegistrationModuleStatus { status: \"loaded\" }\n",
                                                        status_error);
    if (counter == nullptr) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, counter_error);
    }

    if (status == nullptr) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, status_error);
    }

    if (counter->property("count").toInt() != 7 || status->property("status").toString() != QStringLiteral("loaded")) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, "registered module types did not expose expected fixture properties.");
    }

    qmlsharp_engine_shutdown(engine);
    return EXIT_SUCCESS;
}

int test_registration_preserves_schema_metadata() {
    constexpr const char* test_name = "REG-05 schema metadata retention";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    const int result =
        qmlsharp_register_type(engine, "QmlSharp.NativeRegistration.Metadata", 3, 4, "RegistrationMetadataCounter",
                               "schema-metadata-counter", "MetadataView::__qmlsharp_vm0", register_counter_type);
    qmlsharp_engine_shutdown(engine);
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    const qmlsharp::RegisteredTypeMetadata* metadata =
        qmlsharp::find_registered_type("QmlSharp.NativeRegistration.Metadata", 3, 4, "RegistrationMetadataCounter");
    if (metadata == nullptr) {
        return fail(test_name, "registered type metadata was not retained.");
    }

    if (metadata->schema_id != "schema-metadata-counter" ||
        metadata->compiler_slot_key != "MetadataView::__qmlsharp_vm0" || metadata->qt_type_id < 0) {
        return fail(test_name, "schema ID, compilerSlotKey, or Qt type ID metadata did not round-trip.");
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
    result |= run_test(test_shutdown_during_exec_defers_teardown_until_loop_returns);
    result |= run_test(test_null_argument_validation);
    result |= run_test(test_last_error_and_string_lifetime);
    result |= run_test(test_register_type_valid_schema_makes_qml_type_available);
    result |= run_test(test_register_type_duplicate_registration_fails);
    result |= run_test(test_register_type_invalid_version_and_type_name_fail);
    result |= run_test(test_register_module_valid_entries_registers_all_types);
    result |= run_test(test_registration_preserves_schema_metadata);

    return result;
}
