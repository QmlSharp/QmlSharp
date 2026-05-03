#include "qmlsharp/qmlsharp_abi.h"

#include <qqml.h>

#include <QCoreApplication>
#include <QEventLoop>
#include <QMetaObject>
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
#include <set>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

#include "RegistrationCounterViewModel.h"
#include "RegistrationStatusViewModel.h"
#include "qmlsharp_instances.h"

namespace {
struct CallbackProbe {
    bool called = false;
    bool ran_on_main_thread = false;
};

struct InstanceEvent {
    std::string instance_id;
    std::string class_name;
    std::string compiler_slot_key;
};

struct CommandEvent {
    std::string instance_id;
    std::string command_name;
    std::string args_json;
};

struct InstanceCommandProbe {
    std::vector<InstanceEvent> created;
    std::vector<std::string> destroyed;
    std::vector<CommandEvent> commands;
    bool throw_on_command = false;
};

InstanceCommandProbe* current_instance_probe = nullptr;

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

void instance_created_callback(const char* instance_id, const char* class_name, const char* compiler_slot_key) {
    if (current_instance_probe == nullptr) {
        return;
    }

    current_instance_probe->created.push_back(InstanceEvent{
        instance_id == nullptr ? std::string() : std::string(instance_id),
        class_name == nullptr ? std::string() : std::string(class_name),
        compiler_slot_key == nullptr ? std::string() : std::string(compiler_slot_key),
    });
}

void instance_destroyed_callback(const char* instance_id) {
    if (current_instance_probe == nullptr) {
        return;
    }

    current_instance_probe->destroyed.push_back(instance_id == nullptr ? std::string() : std::string(instance_id));
}

void command_callback(const char* instance_id, const char* command_name, const char* args_json) {
    if (current_instance_probe == nullptr) {
        return;
    }

    if (current_instance_probe->throw_on_command) {
        throw std::runtime_error("simulated managed command failure");
    }

    current_instance_probe->commands.push_back(CommandEvent{
        instance_id == nullptr ? std::string() : std::string(instance_id),
        command_name == nullptr ? std::string() : std::string(command_name),
        args_json == nullptr ? std::string() : std::string(args_json),
    });
}

void clear_instance_command_callbacks() {
    qmlsharp_set_command_callback(nullptr);
    qmlsharp_set_instance_callbacks(nullptr, nullptr);
    current_instance_probe = nullptr;
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

int32_t register_status_type_after_one_failure(const char* module_uri, int32_t version_major, int32_t version_minor,
                                               const char* type_name) {
    static int attempts = 0;
    ++attempts;
    if (attempts == 1) {
        return -1;
    }

    return register_status_type(module_uri, version_major, version_minor, type_name);
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

int register_instance_counter_type(void* engine, const char* module_uri, const char* type_name) {
    return qmlsharp_register_type(engine, module_uri, 1, 0, type_name, "schema-instance-counter",
                                  "RegistrationView::__qmlsharp_vm0", register_counter_type);
}

std::unique_ptr<QObject> create_counter_instance(QQmlEngine& qml_engine, const char* module_uri, const char* type_name,
                                                 std::string& error) {
    const std::string qml_source = std::string("import ") + module_uri + " 1.0\n" + type_name + " {}\n";
    return create_qml_object(qml_engine, qml_source.c_str(), error);
}

bool invoke_no_arg_command(QObject* object, const char* command_name) {
    return QMetaObject::invokeMethod(object, command_name, Qt::DirectConnection);
}

bool invoke_int_command(QObject* object, const char* command_name, int value) {
    return QMetaObject::invokeMethod(object, command_name, Qt::DirectConnection, Q_ARG(int, value));
}

bool invoke_string_command(QObject* object, const char* command_name, const QString& value) {
    return QMetaObject::invokeMethod(object, command_name, Qt::DirectConnection, Q_ARG(QString, value));
}

bool invoke_mixed_command(QObject* object, int number, const QString& text, bool enabled) {
    return QMetaObject::invokeMethod(object, "commandMixed", Qt::DirectConnection, Q_ARG(int, number),
                                     Q_ARG(QString, text), Q_ARG(bool, enabled));
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
    const int null_type_result =
        qmlsharp_register_type(nullptr, "QmlSharp.NativeRegistration.NullEngine", 1, 0, "RegistrationNullEngine",
                               "schema-null-engine", "RegistrationView::__qmlsharp_vm0", register_counter_type);
    const std::string null_type_error = read_last_error();
    const qmlsharp_type_registration_entry null_engine_entries[] = {
        {"RegistrationNullModule", "schema-null-module", "RegistrationView::__qmlsharp_vm0", register_counter_type},
    };
    const int null_module_result =
        qmlsharp_register_module(nullptr, "QmlSharp.NativeRegistration.NullEngineModule", 1, 0, null_engine_entries, 1);
    const std::string null_module_error = read_last_error();

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

    if (null_type_result != -2 || null_type_error.find("non-null") == std::string::npos) {
        return fail(test_name, "register_type null engine should fail as invalid argument.");
    }

    if (null_module_result != -2 || null_module_error.find("non-null") == std::string::npos) {
        return fail(test_name, "register_module null engine should fail as invalid argument.");
    }

    if (invalid_version != -2 || version_error.find("module version") == std::string::npos) {
        return fail(test_name, "negative module version should fail as invalid argument.");
    }

    if (invalid_type_name != -2 || type_error.find("uppercase") == std::string::npos) {
        return fail(test_name, "lowercase QML type name should fail as invalid argument.");
    }

    return EXIT_SUCCESS;
}

int test_register_module_callback_failure_is_retry_safe() {
    constexpr const char* test_name = "REG-04B register_module callback failure retry";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    const qmlsharp_type_registration_entry entries[] = {
        {"RegistrationRetryCounter", "schema-retry-counter", "RegistrationRetryView::__qmlsharp_vm0",
         register_counter_type},
        {"RegistrationRetryStatus", "schema-retry-status", "RegistrationRetryStatusView::__qmlsharp_vm0",
         register_status_type_after_one_failure},
    };

    const int first = qmlsharp_register_module(engine, "QmlSharp.NativeRegistration.Retry", 1, 3, entries, 2);
    const std::string first_error = read_last_error();
    const int second = qmlsharp_register_module(engine, "QmlSharp.NativeRegistration.Retry", 1, 3, entries, 2);
    if (first != -6 || first_error.find("callback failed") == std::string::npos) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, "first registration should surface the generated callback failure.");
    }

    if (second != 0) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string counter_error;
    std::unique_ptr<QObject> counter = create_qml_object(qml_engine,
                                                         "import QmlSharp.NativeRegistration.Retry 1.3\n"
                                                         "RegistrationRetryCounter { count: 11 }\n",
                                                         counter_error);
    std::string status_error;
    std::unique_ptr<QObject> status = create_qml_object(qml_engine,
                                                        "import QmlSharp.NativeRegistration.Retry 1.3\n"
                                                        "RegistrationRetryStatus { status: \"retried\" }\n",
                                                        status_error);
    if (counter == nullptr) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, counter_error);
    }

    if (status == nullptr) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, status_error);
    }

    qmlsharp_engine_shutdown(engine);
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
    const int conflicting_schema =
        qmlsharp_register_type(engine, "QmlSharp.NativeRegistration.Metadata", 3, 4, "RegistrationMetadataCounter",
                               "schema-metadata-other", "MetadataView::__qmlsharp_vm0", register_counter_type);
    const std::string schema_error = read_last_error();
    const int conflicting_slot =
        qmlsharp_register_type(engine, "QmlSharp.NativeRegistration.Metadata", 3, 4, "RegistrationMetadataCounter",
                               "schema-metadata-counter", "OtherView::__qmlsharp_vm0", register_counter_type);
    const std::string slot_error = read_last_error();
    qmlsharp_engine_shutdown(engine);
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    if (conflicting_schema != -6 || schema_error.find("schema metadata") == std::string::npos) {
        return fail(test_name, "conflicting schema ID should be detected from retained metadata.");
    }

    if (conflicting_slot != -6 || slot_error.find("schema metadata") == std::string::npos) {
        return fail(test_name, "conflicting compilerSlotKey should be detected from retained metadata.");
    }

    return EXIT_SUCCESS;
}

int test_instance_created_callback_receives_identity() {
    constexpr const char* test_name = "INS-01 instance creation callback";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeInstance.Created";
    const char* type_name = "InstanceCreatedCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    const std::string object_id = object->property("instanceId").toString().toStdString();
    const bool found_native_object = qmlsharp::find_instance_object(object_id.c_str()) == object.get();
    object.reset();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (probe.created.size() != 1U) {
        return fail(test_name, "creation callback should fire exactly once.");
    }

    if (probe.created[0].instance_id.empty() || probe.created[0].instance_id != object_id) {
        return fail(test_name, "creation callback did not receive the generated instanceId.");
    }

    if (probe.created[0].class_name != "RegistrationCounterViewModel") {
        return fail(test_name, "creation callback did not receive the fixture className.");
    }

    if (probe.created[0].compiler_slot_key != "RegistrationView::__qmlsharp_vm0") {
        return fail(test_name, "creation callback did not receive the compilerSlotKey.");
    }

    if (!found_native_object) {
        return fail(test_name, "native instance lookup did not return the generated QObject handle.");
    }

    return EXIT_SUCCESS;
}

int test_instance_destroyed_callback_receives_same_instance_id() {
    constexpr const char* test_name = "INS-02 instance destruction callback";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeInstance.Destroyed";
    const char* type_name = "InstanceDestroyedCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    const std::string instance_id = probe.created[0].instance_id;
    object.reset();
    const bool lookup_cleared = qmlsharp::find_instance_object(instance_id.c_str()) == nullptr;
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (probe.destroyed.size() != 1U || probe.destroyed[0] != instance_id) {
        return fail(test_name, "destruction callback should receive the same instanceId from creation.");
    }

    if (!lookup_cleared) {
        return fail(test_name, "destroyed instances should be removed from native lookup.");
    }

    return EXIT_SUCCESS;
}

int test_instance_ready_opens_gate_for_immediate_commands() {
    constexpr const char* test_name = "INS-03 instance_ready opens ready gate";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);
    qmlsharp_set_command_callback(command_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeInstance.Ready";
    const char* type_name = "InstanceReadyCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    qmlsharp_instance_ready(probe.created[0].instance_id.c_str());
    const bool invoked = invoke_no_arg_command(object.get(), "commandNoArgs");
    object.reset();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (!invoked) {
        return fail(test_name, "QMetaObject could not invoke the generated-test command.");
    }

    if (probe.commands.size() != 1U || probe.commands[0].command_name != "commandNoArgs") {
        return fail(test_name, "command callback should fire immediately after the ready gate opens.");
    }

    return EXIT_SUCCESS;
}

int test_commands_before_ready_are_queued() {
    constexpr const char* test_name = "INS-04 commands before ready are queued";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);
    qmlsharp_set_command_callback(command_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeInstance.Queue";
    const char* type_name = "InstanceQueueCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    const bool invoked = invoke_no_arg_command(object.get(), "commandNoArgs");
    object.reset();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (!invoked) {
        return fail(test_name, "QMetaObject could not invoke the queued command.");
    }

    if (!probe.commands.empty()) {
        return fail(test_name, "pre-ready command should not dispatch before instance_ready.");
    }

    return EXIT_SUCCESS;
}

int test_queued_commands_flush_in_order_on_ready() {
    constexpr const char* test_name = "INS-05 queued commands flush in order";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);
    qmlsharp_set_command_callback(command_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeInstance.Flush";
    const char* type_name = "InstanceFlushCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    const bool first = invoke_int_command(object.get(), "commandInt", 1);
    const bool second = invoke_string_command(object.get(), "commandString", QStringLiteral("two"));
    const bool third = invoke_mixed_command(object.get(), 3, QStringLiteral("four"), true);
    if (!first || !second || !third) {
        object.reset();
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, "QMetaObject could not invoke queued commands.");
    }

    if (!probe.commands.empty()) {
        object.reset();
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, "commands should remain queued until instance_ready.");
    }

    qmlsharp_instance_ready(probe.created[0].instance_id.c_str());
    qmlsharp_instance_ready(probe.created[0].instance_id.c_str());
    object.reset();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (probe.commands.size() != 3U) {
        return fail(test_name, "ready should flush each queued command exactly once.");
    }

    if (probe.commands[0].command_name != "commandInt" || probe.commands[0].args_json != "[1]") {
        return fail(test_name, "first queued command did not preserve name and payload.");
    }

    if (probe.commands[1].command_name != "commandString" || probe.commands[1].args_json != "[\"two\"]") {
        return fail(test_name, "second queued command did not preserve name and payload.");
    }

    if (probe.commands[2].command_name != "commandMixed" || probe.commands[2].args_json != "[3,\"four\",true]") {
        return fail(test_name, "third queued command did not preserve name, payload, and order.");
    }

    return EXIT_SUCCESS;
}

int test_multiple_instances_get_unique_ids() {
    constexpr const char* test_name = "INS-06 multiple instances get unique instanceIds";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeInstance.Unique";
    const char* type_name = "InstanceUniqueCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string first_error;
    std::unique_ptr<QObject> first = create_counter_instance(qml_engine, module_uri, type_name, first_error);
    std::string second_error;
    std::unique_ptr<QObject> second = create_counter_instance(qml_engine, module_uri, type_name, second_error);
    std::string third_error;
    std::unique_ptr<QObject> third = create_counter_instance(qml_engine, module_uri, type_name, third_error);
    if (first == nullptr || second == nullptr || third == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, first_error + second_error + third_error);
    }

    const int active_count = qmlsharp::active_instance_count();
    first.reset();
    second.reset();
    third.reset();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    std::set<std::string> instance_ids;
    for (const InstanceEvent& event : probe.created) {
        instance_ids.insert(event.instance_id);
    }

    if (probe.created.size() != 3U || instance_ids.size() != 3U) {
        return fail(test_name, "three active instances should produce three distinct instanceIds.");
    }

    if (active_count < 3) {
        return fail(test_name, "native registry did not retain all active instances before destruction.");
    }

    return EXIT_SUCCESS;
}

int test_instance_ready_unknown_instance_is_safe() {
    constexpr const char* test_name = "INS-07 instance_ready with unknown instanceId";
    qmlsharp_instance_ready("unknown-instance-id");
    const std::string error = read_last_error();
    clear_instance_command_callbacks();

    if (!error.empty()) {
        return fail(test_name, "unknown instance_ready should be ignored without a stable error.");
    }

    return EXIT_SUCCESS;
}

int test_command_dispatch_invocable_fires_callback() {
    constexpr const char* test_name = "CMD-01 QML invocable command callback";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);
    qmlsharp_set_command_callback(command_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeCommand.Callback";
    const char* type_name = "CommandCallbackCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    qmlsharp_instance_ready(probe.created[0].instance_id.c_str());
    const bool invoked = invoke_no_arg_command(object.get(), "increment");
    object.reset();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (!invoked || probe.commands.size() != 1U) {
        return fail(test_name, "increment should invoke the command callback once.");
    }

    if (probe.commands[0].instance_id != probe.created[0].instance_id ||
        probe.commands[0].command_name != "increment" || probe.commands[0].args_json != "[]") {
        return fail(test_name, "command callback did not receive the expected instanceId, commandName, and argsJson.");
    }

    return EXIT_SUCCESS;
}

int test_command_dispatch_no_arguments_uses_empty_json_array() {
    constexpr const char* test_name = "CMD-02 command with no arguments";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);
    qmlsharp_set_command_callback(command_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeCommand.NoArgs";
    const char* type_name = "CommandNoArgsCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    qmlsharp_instance_ready(probe.created[0].instance_id.c_str());
    const bool invoked = invoke_no_arg_command(object.get(), "commandNoArgs");
    object.reset();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (!invoked || probe.commands.empty() || probe.commands[0].args_json != "[]") {
        return fail(test_name, "no-argument command should dispatch an empty JSON array.");
    }

    return EXIT_SUCCESS;
}

int test_command_dispatch_int_argument_serializes_payload() {
    constexpr const char* test_name = "CMD-03 command with int argument";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);
    qmlsharp_set_command_callback(command_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeCommand.Int";
    const char* type_name = "CommandIntCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    qmlsharp_instance_ready(probe.created[0].instance_id.c_str());
    const bool invoked = invoke_int_command(object.get(), "commandInt", 42);
    object.reset();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (!invoked || probe.commands.empty() || probe.commands[0].args_json != "[42]") {
        return fail(test_name, "int command should dispatch a numeric JSON array payload.");
    }

    return EXIT_SUCCESS;
}

int test_command_dispatch_string_argument_serializes_payload() {
    constexpr const char* test_name = "CMD-04 command with string argument";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);
    qmlsharp_set_command_callback(command_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeCommand.String";
    const char* type_name = "CommandStringCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    qmlsharp_instance_ready(probe.created[0].instance_id.c_str());
    const bool invoked = invoke_string_command(object.get(), "commandString", QStringLiteral("hello"));
    object.reset();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (!invoked || probe.commands.empty() || probe.commands[0].args_json != "[\"hello\"]") {
        return fail(test_name, "string command should dispatch a quoted JSON array payload.");
    }

    return EXIT_SUCCESS;
}

int test_command_dispatch_multiple_arguments_preserves_order() {
    constexpr const char* test_name = "CMD-05 command with multiple arguments";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);
    qmlsharp_set_command_callback(command_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeCommand.Mixed";
    const char* type_name = "CommandMixedCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    qmlsharp_instance_ready(probe.created[0].instance_id.c_str());
    const bool invoked = invoke_mixed_command(object.get(), 1, QStringLiteral("two"), true);
    object.reset();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (!invoked || probe.commands.empty() || probe.commands[0].args_json != "[1,\"two\",true]") {
        return fail(test_name, "multi-argument command should preserve argument order in argsJson.");
    }

    return EXIT_SUCCESS;
}

int test_command_dispatch_without_callback_does_not_crash() {
    constexpr const char* test_name = "CMD-06 command callback not set";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);
    qmlsharp_set_command_callback(nullptr);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeCommand.NoCallback";
    const char* type_name = "CommandNoCallbackCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    qmlsharp_instance_ready(probe.created[0].instance_id.c_str());
    const bool invoked = invoke_no_arg_command(object.get(), "commandNoArgs");
    const std::string last_error = read_last_error();
    object.reset();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (!invoked) {
        return fail(test_name, "QMetaObject could not invoke command without callback.");
    }

    if (!last_error.empty()) {
        return fail(test_name, "missing command callback should not produce a native error.");
    }

    return EXIT_SUCCESS;
}

int test_command_callback_failure_is_reported_without_crash() {
    constexpr const char* test_name = "command callback failure is surfaced";
    InstanceCommandProbe probe;
    probe.throw_on_command = true;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);
    qmlsharp_set_command_callback(command_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeCommand.Failure";
    const char* type_name = "CommandFailureCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    qmlsharp_instance_ready(probe.created[0].instance_id.c_str());
    const bool invoked = invoke_no_arg_command(object.get(), "commandNoArgs");
    const std::string callback_error = read_last_error();
    object.reset();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (!invoked) {
        return fail(test_name, "QMetaObject command invocation should complete even when callback fails.");
    }

    if (callback_error.find("Command callback failed") == std::string::npos) {
        return fail(test_name, "callback failure should be surfaced through qmlsharp_get_last_error.");
    }

    return EXIT_SUCCESS;
}

int test_destroyed_instance_teardown_is_idempotent_and_drops_commands() {
    constexpr const char* test_name = "after-dispose command behavior";
    InstanceCommandProbe probe;
    current_instance_probe = &probe;
    qmlsharp_set_instance_callbacks(instance_created_callback, instance_destroyed_callback);
    qmlsharp_set_command_callback(command_callback);

    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    const char* module_uri = "QmlSharp.NativeInstance.Dispose";
    const char* type_name = "InstanceDisposeCounter";
    if (register_instance_counter_type(engine, module_uri, type_name) != 0) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, read_last_error());
    }

    QQmlEngine qml_engine;
    std::string error;
    std::unique_ptr<QObject> object = create_counter_instance(qml_engine, module_uri, type_name, error);
    if (object == nullptr) {
        qmlsharp_engine_shutdown(engine);
        clear_instance_command_callbacks();
        return fail(test_name, error);
    }

    const std::string instance_id = probe.created[0].instance_id;
    qmlsharp_instance_ready(instance_id.c_str());
    object.reset();
    qmlsharp::notify_instance_destroyed(QString::fromStdString(instance_id));
    qmlsharp::dispatch_command(QString::fromStdString(instance_id), QStringLiteral("commandNoArgs"),
                               QStringLiteral("[]"));
    qmlsharp_instance_ready(instance_id.c_str());
    const std::string last_error = read_last_error();
    qmlsharp_engine_shutdown(engine);
    clear_instance_command_callbacks();

    if (probe.destroyed.size() != 1U) {
        return fail(test_name, "teardown should notify destruction exactly once.");
    }

    if (!probe.commands.empty()) {
        return fail(test_name, "commands for destroyed instances should be dropped.");
    }

    if (!last_error.empty()) {
        return fail(test_name, "idempotent teardown and after-dispose calls should not leave a native error.");
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
    result |= run_test(test_register_module_callback_failure_is_retry_safe);
    result |= run_test(test_register_module_valid_entries_registers_all_types);
    result |= run_test(test_registration_preserves_schema_metadata);
    result |= run_test(test_instance_created_callback_receives_identity);
    result |= run_test(test_instance_destroyed_callback_receives_same_instance_id);
    result |= run_test(test_instance_ready_opens_gate_for_immediate_commands);
    result |= run_test(test_commands_before_ready_are_queued);
    result |= run_test(test_queued_commands_flush_in_order_on_ready);
    result |= run_test(test_multiple_instances_get_unique_ids);
    result |= run_test(test_instance_ready_unknown_instance_is_safe);
    result |= run_test(test_command_dispatch_invocable_fires_callback);
    result |= run_test(test_command_dispatch_no_arguments_uses_empty_json_array);
    result |= run_test(test_command_dispatch_int_argument_serializes_payload);
    result |= run_test(test_command_dispatch_string_argument_serializes_payload);
    result |= run_test(test_command_dispatch_multiple_arguments_preserves_order);
    result |= run_test(test_command_dispatch_without_callback_does_not_crash);
    result |= run_test(test_command_callback_failure_is_reported_without_crash);
    result |= run_test(test_destroyed_instance_teardown_is_idempotent_and_drops_commands);

    return result;
}
