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
#include <atomic>
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

struct EffectEvent {
    std::string effect_name;
    std::string payload_json;
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

void QMLSHARP_CALL main_thread_probe_callback(void* user_data) {
    auto* probe = static_cast<CallbackProbe*>(user_data);
    probe->called = true;
    probe->ran_on_main_thread =
        QCoreApplication::instance() != nullptr && QThread::currentThread() == QCoreApplication::instance()->thread();
}

void QMLSHARP_CALL quit_callback(void* user_data) {
    Q_UNUSED(user_data);
    QCoreApplication::quit();
}

void QMLSHARP_CALL shutdown_callback(void* user_data) {
    qmlsharp_engine_shutdown(user_data);
}

void QMLSHARP_CALL instance_created_callback(const char* instance_id, const char* class_name,
                                             const char* compiler_slot_key) {
    if (current_instance_probe == nullptr) {
        return;
    }

    current_instance_probe->created.push_back(InstanceEvent{
        instance_id == nullptr ? std::string() : std::string(instance_id),
        class_name == nullptr ? std::string() : std::string(class_name),
        compiler_slot_key == nullptr ? std::string() : std::string(compiler_slot_key),
    });
}

void QMLSHARP_CALL instance_destroyed_callback(const char* instance_id) {
    if (current_instance_probe == nullptr) {
        return;
    }

    current_instance_probe->destroyed.push_back(instance_id == nullptr ? std::string() : std::string(instance_id));
}

void QMLSHARP_CALL command_callback(const char* instance_id, const char* command_name, const char* args_json) {
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

class CounterFixture final {
public:
    CounterFixture() {
        engine_ = qmlsharp_engine_init(0, nullptr);
        if (engine_ == nullptr) {
            return;
        }

        counter_ = std::make_unique<RegistrationCounterViewModel>();
        instance_id_ = counter_->instanceId().toStdString();
    }

    ~CounterFixture() {
        counter_.reset();
        if (engine_ != nullptr) {
            qmlsharp_engine_shutdown(engine_);
        }
    }

    CounterFixture(const CounterFixture&) = delete;
    CounterFixture& operator=(const CounterFixture&) = delete;

    bool valid() const noexcept { return engine_ != nullptr && counter_ != nullptr; }

    RegistrationCounterViewModel* counter() const noexcept { return counter_.get(); }

    const std::string& instance_id() const noexcept { return instance_id_; }

private:
    void* engine_ = nullptr;
    std::unique_ptr<RegistrationCounterViewModel> counter_;
    std::string instance_id_;
};

void configure_headless_qt() {
    if (qEnvironmentVariableIsEmpty("QT_QPA_PLATFORM")) {
        qputenv("QT_QPA_PLATFORM", "offscreen");
    }
}

int32_t QMLSHARP_CALL register_counter_type(const char* module_uri, int32_t version_major, int32_t version_minor,
                                            const char* type_name) {
    return qmlRegisterType<RegistrationCounterViewModel>(module_uri, version_major, version_minor, type_name);
}

int32_t QMLSHARP_CALL register_status_type(const char* module_uri, int32_t version_major, int32_t version_minor,
                                           const char* type_name) {
    return qmlRegisterType<RegistrationStatusViewModel>(module_uri, version_major, version_minor, type_name);
}

int32_t QMLSHARP_CALL register_status_type_after_one_failure(const char* module_uri, int32_t version_major,
                                                             int32_t version_minor, const char* type_name) {
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

int test_state_sync_int_updates_property_and_notify() {
    constexpr const char* test_name = "SSY-01 int state sync updates property";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    int notify_count = 0;
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::countChanged,
                     [&notify_count]() { ++notify_count; });

    const int result = qmlsharp_sync_state_int(fixture.instance_id().c_str(), "count", 41);
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    if (fixture.counter()->count() != 41 || notify_count != 1) {
        return fail(test_name, "count state sync should update the property and emit one NOTIFY signal.");
    }

    return EXIT_SUCCESS;
}

int test_state_sync_double_updates_property_and_notify() {
    constexpr const char* test_name = "SSY-02 double state sync updates property";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    int notify_count = 0;
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::ratioChanged,
                     [&notify_count]() { ++notify_count; });

    const int result = qmlsharp_sync_state_double(fixture.instance_id().c_str(), "ratio", 3.25);
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    if (!qFuzzyCompare(fixture.counter()->ratio() + 1.0, 4.25) || notify_count != 1) {
        return fail(test_name, "double state sync should update ratio and emit one NOTIFY signal.");
    }

    return EXIT_SUCCESS;
}

int test_state_sync_bool_true_updates_property_and_notify() {
    constexpr const char* test_name = "SSY-03 bool true state sync updates property";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    int notify_count = 0;
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::enabledChanged,
                     [&notify_count]() { ++notify_count; });

    const int result = qmlsharp_sync_state_bool(fixture.instance_id().c_str(), "enabled", 1);
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    if (!fixture.counter()->enabled() || notify_count != 1) {
        return fail(test_name, "bool true state sync should update enabled and emit one NOTIFY signal.");
    }

    return EXIT_SUCCESS;
}

int test_state_sync_bool_false_updates_property_and_notify() {
    constexpr const char* test_name = "SSY-04 bool false state sync updates property";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    fixture.counter()->setEnabled(true);
    int notify_count = 0;
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::enabledChanged,
                     [&notify_count]() { ++notify_count; });

    const int result = qmlsharp_sync_state_bool(fixture.instance_id().c_str(), "enabled", 0);
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    if (fixture.counter()->enabled() || notify_count != 1) {
        return fail(test_name, "bool false state sync should update enabled and emit one NOTIFY signal.");
    }

    return EXIT_SUCCESS;
}

int test_state_sync_string_updates_property_and_handles_null_as_empty() {
    constexpr const char* test_name = "SSY-05 string state sync updates property";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    int notify_count = 0;
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::titleChanged,
                     [&notify_count]() { ++notify_count; });

    int result = qmlsharp_sync_state_string(fixture.instance_id().c_str(), "title", "ready");
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    result = qmlsharp_sync_state_string(fixture.instance_id().c_str(), "title", nullptr);
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    if (!fixture.counter()->title().isEmpty() || notify_count != 2) {
        return fail(test_name, "string state sync should set UTF-8 strings and map null to an empty QString.");
    }

    return EXIT_SUCCESS;
}

int test_state_sync_string_preserves_utf8() {
    constexpr const char* test_name = "SSY-06 string state sync preserves UTF-8";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    const char* unicode_text =
        "state "
        "\xE6\xBC\xA2\xE5\xAD\x97"
        " "
        "\xF0\x9F\x9A\x80";
    const int result = qmlsharp_sync_state_string(fixture.instance_id().c_str(), "title", unicode_text);
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    if (fixture.counter()->title() != QString::fromUtf8(unicode_text)) {
        return fail(test_name, "UTF-8 state sync should preserve non-ASCII text.");
    }

    return EXIT_SUCCESS;
}

int test_state_sync_json_updates_complex_property() {
    constexpr const char* test_name = "SSY-07 JSON fallback state sync updates complex property";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    int notify_count = 0;
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::metadataChanged,
                     [&notify_count]() { ++notify_count; });

    const int result =
        qmlsharp_sync_state_json(fixture.instance_id().c_str(), "metadata", "{\"answer\":42,\"nested\":{\"ok\":true}}");
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    const QVariantMap metadata = fixture.counter()->metadata().toMap();
    const QVariantMap nested = metadata.value(QStringLiteral("nested")).toMap();
    if (metadata.value(QStringLiteral("answer")).toInt() != 42 || !nested.value(QStringLiteral("ok")).toBool() ||
        notify_count != 1) {
        return fail(test_name, "JSON state sync should store the complex payload and emit one NOTIFY signal.");
    }

    return EXIT_SUCCESS;
}

int test_state_sync_batch_updates_properties_deterministically() {
    constexpr const char* test_name = "SSY-08 batch state sync updates properties";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    int count_notifications = 0;
    int enabled_notifications = 0;
    int metadata_notifications = 0;
    int ratio_notifications = 0;
    int title_notifications = 0;
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::countChanged,
                     [&count_notifications]() { ++count_notifications; });
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::enabledChanged,
                     [&enabled_notifications]() { ++enabled_notifications; });
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::metadataChanged,
                     [&metadata_notifications]() { ++metadata_notifications; });
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::ratioChanged,
                     [&ratio_notifications]() { ++ratio_notifications; });
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::titleChanged,
                     [&title_notifications]() { ++title_notifications; });

    const char* updates =
        "{\"title\":\"batch\",\"ratio\":2.5,\"metadata\":{\"key\":\"value\"},\"enabled\":true,"
        "\"count\":7}";
    const int result = qmlsharp_sync_state_batch(fixture.instance_id().c_str(), updates);
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    const QVariantMap metadata = fixture.counter()->metadata().toMap();
    if (fixture.counter()->count() != 7 || !fixture.counter()->enabled() ||
        !qFuzzyCompare(fixture.counter()->ratio() + 1.0, 3.5) ||
        fixture.counter()->title() != QStringLiteral("batch") ||
        metadata.value(QStringLiteral("key")).toString() != QStringLiteral("value")) {
        return fail(test_name, "batch state sync did not apply all expected property values.");
    }

    if (count_notifications != 1 || enabled_notifications != 1 || metadata_notifications != 1 ||
        ratio_notifications != 1 || title_notifications != 1) {
        return fail(test_name, "batch state sync should emit one NOTIFY signal for each changed property.");
    }

    return EXIT_SUCCESS;
}

int test_state_sync_batch_partial_failure_is_all_or_nothing() {
    constexpr const char* test_name = "SSY-08B batch state sync partial failure";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    fixture.counter()->setCount(3);
    fixture.counter()->setTitle(QStringLiteral("stable"));
    int count_notifications = 0;
    int title_notifications = 0;
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::countChanged,
                     [&count_notifications]() { ++count_notifications; });
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::titleChanged,
                     [&title_notifications]() { ++title_notifications; });

    const int result =
        qmlsharp_sync_state_batch(fixture.instance_id().c_str(), "{\"count\":9,\"missing\":true,\"title\":\"bad\"}");
    const std::string error = read_last_error();
    if (result != -7 || error.find("missing") == std::string::npos) {
        return fail(test_name, "batch partial failure should report the unknown property.");
    }

    if (fixture.counter()->count() != 3 || fixture.counter()->title() != QStringLiteral("stable") ||
        count_notifications != 0 || title_notifications != 0) {
        return fail(test_name, "batch partial failure should leave all properties unchanged.");
    }

    return EXIT_SUCCESS;
}

int test_state_sync_unknown_instance_reports_error() {
    constexpr const char* test_name = "SSY-09 unknown instance state sync";
    const int result = qmlsharp_sync_state_int("missing-instance", "count", 1);
    const std::string error = read_last_error();

    if (result != -3 || error.find("missing-instance") == std::string::npos) {
        return fail(test_name, "unknown instance state sync should return the instance-not-found error.");
    }

    return EXIT_SUCCESS;
}

int test_state_sync_unknown_property_reports_error() {
    constexpr const char* test_name = "SSY-10 unknown property state sync";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    fixture.counter()->setCount(5);
    const int result = qmlsharp_sync_state_int(fixture.instance_id().c_str(), "missingProperty", 99);
    const std::string error = read_last_error();

    if (result != -7 || error.find("missingProperty") == std::string::npos) {
        return fail(test_name, "unknown property state sync should return the property-not-found error.");
    }

    if (fixture.counter()->count() != 5) {
        return fail(test_name, "unknown property state sync should leave existing properties unchanged.");
    }

    return EXIT_SUCCESS;
}

int test_state_sync_from_background_thread_marshal_to_qobject_thread() {
    constexpr const char* test_name = "SSY-01B state sync marshals to QObject thread";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    bool notify_on_object_thread = false;
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::countChanged,
                     [&fixture, &notify_on_object_thread]() {
                         notify_on_object_thread = QThread::currentThread() == fixture.counter()->thread();
                     });

    std::atomic_bool done = false;
    int result = -1;
    std::thread worker([&fixture, &done, &result]() {
        result = qmlsharp_sync_state_int(fixture.instance_id().c_str(), "count", 84);
        done = true;
    });

    for (int attempt = 0; attempt < 100 && !done; ++attempt) {
        QCoreApplication::processEvents(QEventLoop::AllEvents, 10);
        QThread::msleep(1);
    }

    worker.join();

    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    if (fixture.counter()->count() != 84 || !notify_on_object_thread) {
        return fail(test_name, "background state sync should update the QObject on its owning thread.");
    }

    return EXIT_SUCCESS;
}

int test_effect_dispatch_emits_signal_and_preserves_order() {
    constexpr const char* test_name = "EFF-01 effect dispatch emits signal";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    std::vector<EffectEvent> events;
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::effectDispatched,
                     [&events](const QString& effectName, const QString& payloadJson) {
                         events.push_back(EffectEvent{effectName.toStdString(), payloadJson.toStdString()});
                     });

    const int first = qmlsharp_dispatch_effect(fixture.instance_id().c_str(), "showToast", "{\"message\":\"first\"}");
    const int second = qmlsharp_dispatch_effect(fixture.instance_id().c_str(), "showToast", "{\"message\":\"second\"}");
    if (first != 0 || second != 0) {
        return fail(test_name, read_last_error());
    }

    if (events.size() != 2U || events[0].effect_name != "showToast" ||
        events[0].payload_json != "{\"message\":\"first\"}" || events[1].payload_json != "{\"message\":\"second\"}") {
        return fail(test_name, "effect dispatch should emit signals in payload order.");
    }

    return EXIT_SUCCESS;
}

int test_effect_dispatch_empty_payload_defaults_to_object() {
    constexpr const char* test_name = "EFF-02 effect dispatch empty payload";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    std::vector<EffectEvent> events;
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::effectDispatched,
                     [&events](const QString& effectName, const QString& payloadJson) {
                         events.push_back(EffectEvent{effectName.toStdString(), payloadJson.toStdString()});
                     });

    const int result = qmlsharp_dispatch_effect(fixture.instance_id().c_str(), "emptyPayload", "");
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    if (events.size() != 1U || events[0].payload_json != "{}") {
        return fail(test_name, "empty effect payload should dispatch as an empty JSON object.");
    }

    return EXIT_SUCCESS;
}

int test_effect_dispatch_unknown_instance_reports_error() {
    constexpr const char* test_name = "EFF-03 unknown instance effect dispatch";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    std::vector<EffectEvent> events;
    QObject::connect(fixture.counter(), &RegistrationCounterViewModel::effectDispatched,
                     [&events](const QString& effectName, const QString& payloadJson) {
                         events.push_back(EffectEvent{effectName.toStdString(), payloadJson.toStdString()});
                     });

    const int result = qmlsharp_dispatch_effect("missing-instance", "showToast", "{}");
    const std::string error = read_last_error();
    if (result != -3 || error.find("missing-instance") == std::string::npos) {
        return fail(test_name, "unknown instance effect dispatch should return the instance-not-found error.");
    }

    if (!events.empty()) {
        return fail(test_name, "unknown instance effect dispatch should not emit any signal.");
    }

    return EXIT_SUCCESS;
}

int test_effect_broadcast_emits_to_all_class_instances() {
    constexpr const char* test_name = "EFF-04 broadcast effect emits to all class instances";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    auto first = std::make_unique<RegistrationCounterViewModel>();
    auto second = std::make_unique<RegistrationCounterViewModel>();
    auto third = std::make_unique<RegistrationCounterViewModel>();
    std::vector<EffectEvent> first_events;
    std::vector<EffectEvent> second_events;
    std::vector<EffectEvent> third_events;
    QObject::connect(first.get(), &RegistrationCounterViewModel::effectDispatched,
                     [&first_events](const QString& effectName, const QString& payloadJson) {
                         first_events.push_back(EffectEvent{effectName.toStdString(), payloadJson.toStdString()});
                     });
    QObject::connect(second.get(), &RegistrationCounterViewModel::effectDispatched,
                     [&second_events](const QString& effectName, const QString& payloadJson) {
                         second_events.push_back(EffectEvent{effectName.toStdString(), payloadJson.toStdString()});
                     });
    QObject::connect(third.get(), &RegistrationCounterViewModel::effectDispatched,
                     [&third_events](const QString& effectName, const QString& payloadJson) {
                         third_events.push_back(EffectEvent{effectName.toStdString(), payloadJson.toStdString()});
                     });

    const int result = qmlsharp_broadcast_effect("RegistrationCounterViewModel", "broadcast", "{\"value\":1}");
    const std::string error = read_last_error();
    first.reset();
    second.reset();
    third.reset();
    qmlsharp_engine_shutdown(engine);

    if (result != 0) {
        return fail(test_name, error);
    }

    if (first_events.size() != 1U || second_events.size() != 1U || third_events.size() != 1U ||
        first_events[0].payload_json != "{\"value\":1}" || second_events[0].effect_name != "broadcast" ||
        third_events[0].effect_name != "broadcast") {
        return fail(test_name, "broadcast effect should emit to every active instance of the class.");
    }

    return EXIT_SUCCESS;
}

int test_effect_broadcast_with_no_active_instances_is_success() {
    constexpr const char* test_name = "EFF-05 broadcast effect without active instances";
    const int result = qmlsharp_broadcast_effect("RegistrationCounterViewModel", "broadcast", "{}");
    if (result != 0) {
        return fail(test_name, read_last_error());
    }

    const std::string error = read_last_error();
    if (!error.empty()) {
        return fail(test_name, "broadcast without active instances should not leave a native error.");
    }

    return EXIT_SUCCESS;
}

int test_effect_dispatch_invalid_payload_reports_json_error() {
    constexpr const char* test_name = "EFF-01B effect dispatch invalid payload";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    const int result = qmlsharp_dispatch_effect(fixture.instance_id().c_str(), "showToast", "{invalid");
    const std::string error = read_last_error();
    if (result != -8 || error.find("invalid JSON") == std::string::npos) {
        return fail(test_name, "invalid effect payload should return the JSON parse error.");
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
    result |= run_test(test_state_sync_int_updates_property_and_notify);
    result |= run_test(test_state_sync_double_updates_property_and_notify);
    result |= run_test(test_state_sync_bool_true_updates_property_and_notify);
    result |= run_test(test_state_sync_bool_false_updates_property_and_notify);
    result |= run_test(test_state_sync_string_updates_property_and_handles_null_as_empty);
    result |= run_test(test_state_sync_string_preserves_utf8);
    result |= run_test(test_state_sync_json_updates_complex_property);
    result |= run_test(test_state_sync_batch_updates_properties_deterministically);
    result |= run_test(test_state_sync_batch_partial_failure_is_all_or_nothing);
    result |= run_test(test_state_sync_unknown_instance_reports_error);
    result |= run_test(test_state_sync_unknown_property_reports_error);
    result |= run_test(test_state_sync_from_background_thread_marshal_to_qobject_thread);
    result |= run_test(test_effect_dispatch_emits_signal_and_preserves_order);
    result |= run_test(test_effect_dispatch_empty_payload_defaults_to_object);
    result |= run_test(test_effect_dispatch_unknown_instance_reports_error);
    result |= run_test(test_effect_broadcast_emits_to_all_class_instances);
    result |= run_test(test_effect_broadcast_with_no_active_instances_is_success);
    result |= run_test(test_effect_dispatch_invalid_payload_reports_json_error);

    return result;
}
