#include "qmlsharp/qmlsharp_abi.h"

#include <QtQml/qqml.h>

#include <QByteArray>
#include <QCoreApplication>
#include <QDir>
#include <QEventLoop>
#include <QFile>
#include <QJsonDocument>
#include <QJsonObject>
#include <QMetaObject>
#include <QTemporaryDir>
#include <QThread>
#include <QtGlobal>
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <iostream>
#include <memory>
#include <numeric>
#include <string>
#include <thread>
#include <vector>

#include "RegistrationCounterViewModel.h"
#include "qmlsharp_errors.h"

namespace {
using Clock = std::chrono::steady_clock;

std::atomic<int> command_callback_count = 0;
std::atomic<int> reentrant_callback_result = 0;
std::atomic_bool reentrant_callback_entered = false;

int fail(const char* test_name, const std::string& message) {
    std::cerr << "[FAIL] " << test_name << ": " << message << '\n';
    return EXIT_FAILURE;
}

std::string read_last_error() {
    const char* value = qmlsharp_get_last_error();
    if (value == nullptr) {
        return {};
    }

    std::string result(value);
    qmlsharp_free_string(value);
    return result;
}

std::string take_native_string(const char* value) {
    if (value == nullptr) {
        return {};
    }

    std::string result(value);
    qmlsharp_free_string(value);
    return result;
}

QJsonDocument parse_json_text(const std::string& text, std::string& error) {
    QJsonParseError parse_error;
    const QJsonDocument document =
        QJsonDocument::fromJson(QByteArray(text.data(), static_cast<qsizetype>(text.size())), &parse_error);
    if (parse_error.error != QJsonParseError::NoError || document.isNull()) {
        error = parse_error.errorString().toStdString();
    }

    return document;
}

QJsonObject read_metrics_object(std::string& error) {
    const std::string json = take_native_string(qmlsharp_get_metrics());
    if (json.empty()) {
        error = read_last_error();
        if (error.empty()) {
            error = "metrics payload should not be empty";
        }

        return {};
    }

    const QJsonDocument document = parse_json_text(json, error);
    if (!document.isObject() && error.empty()) {
        error = "metrics payload should be a JSON object";
    }

    return document.object();
}

void configure_headless_qt() {
    if (qEnvironmentVariableIsEmpty("QT_QPA_PLATFORM")) {
        qputenv("QT_QPA_PLATFORM", "offscreen");
    }
}

template <typename Test>
int run_test(const char* name, Test test) {
    std::cout << "[RUN ] " << name << '\n';
    const int result = test();
    if (result == EXIT_SUCCESS) {
        std::cout << "[PASS] " << name << '\n';
    }

    return result;
}

double p99_microseconds(std::vector<double> samples) {
    std::sort(samples.begin(), samples.end());
    const std::size_t index = static_cast<std::size_t>(std::ceil(static_cast<double>(samples.size()) * 0.99)) - 1U;
    return samples[index];
}

std::string format_measurement(const char* metric, double actual, double budget) {
    return std::string(metric) + " P99 was " + std::to_string(actual) + " us, budget is " + std::to_string(budget) +
           " us.";
}

template <typename Operation>
std::vector<double> measure_operation(int iterations, Operation operation) {
    std::vector<double> samples;
    samples.reserve(static_cast<std::size_t>(iterations));
    for (int index = 0; index < iterations; ++index) {
        const Clock::time_point start = Clock::now();
        operation(index);
        const Clock::time_point end = Clock::now();
        const double micros =
            static_cast<double>(std::chrono::duration_cast<std::chrono::nanoseconds>(end - start).count()) / 1000.0;
        samples.push_back(micros);
    }

    return samples;
}

int assert_p99_under(const char* test_name, const char* metric, std::vector<double> samples, double budget) {
    const double measured = p99_microseconds(std::move(samples));
    std::cout << "[MEASURE] " << metric << " P99 = " << measured << " us\n";
    if (measured >= budget) {
        return fail(test_name, format_measurement(metric, measured, budget));
    }

    return EXIT_SUCCESS;
}

void QMLSHARP_CALL counting_command_callback(const char*, const char*, const char*) {
    command_callback_count.fetch_add(1, std::memory_order_relaxed);
}

void QMLSHARP_CALL reentrant_command_callback(const char* instance_id, const char*, const char*) {
    reentrant_callback_entered.store(true, std::memory_order_release);
    reentrant_callback_result.store(qmlsharp_sync_state_int(instance_id, "count", 321), std::memory_order_release);
}

void QMLSHARP_CALL posted_callback(void* user_data) {
    auto* called = static_cast<std::atomic_bool*>(user_data);
    called->store(true, std::memory_order_release);
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
        qmlsharp_set_command_callback(nullptr);
        counter_.reset();
        if (engine_ != nullptr) {
            qmlsharp_engine_shutdown(engine_);
        }
    }

    CounterFixture(const CounterFixture&) = delete;
    CounterFixture& operator=(const CounterFixture&) = delete;

    bool valid() const noexcept { return engine_ != nullptr && counter_ != nullptr; }

    void* engine() const noexcept { return engine_; }

    RegistrationCounterViewModel* counter() const noexcept { return counter_.get(); }

    const std::string& instance_id() const noexcept { return instance_id_; }

private:
    void* engine_ = nullptr;
    std::unique_ptr<RegistrationCounterViewModel> counter_;
    std::string instance_id_;
};

int32_t QMLSHARP_CALL register_counter_type(const char* module_uri, int32_t version_major, int32_t version_minor,
                                            const char* type_name) {
    return qmlRegisterType<RegistrationCounterViewModel>(module_uri, version_major, version_minor, type_name);
}

int register_counter_type_for_test(void* engine, const char* module_uri, const char* type_name, const char* schema_id) {
    return qmlsharp_register_type(engine, module_uri, 1, 0, type_name, schema_id, "RegistrationView::__qmlsharp_vm0",
                                  register_counter_type);
}

QString write_qml_file(QTemporaryDir& directory, const QString& file_name, const QString& qml_source,
                       std::string& error) {
    const QString file_path = directory.filePath(file_name);
    QFile file(file_path);
    if (!file.open(QIODevice::WriteOnly | QIODevice::Truncate | QIODevice::Text)) {
        error = file.errorString().toStdString();
        return {};
    }

    const QByteArray source_utf8 = qml_source.toUtf8();
    if (file.write(source_utf8) != source_utf8.size()) {
        error = file.errorString().toStdString();
        return {};
    }

    return file_path;
}

int reload_qml_file(void* engine, const QString& qml_path) {
    const QByteArray qml_path_utf8 = qml_path.toUtf8();
    return qmlsharp_reload_qml(engine, qml_path_utf8.constData());
}

QString make_hot_reload_qml(int count_offset) {
    QString source = QStringLiteral("import QtQuick\nimport QmlSharp.NativePerf.HotReload 1.0\nItem {\n");
    source += QStringLiteral("  width: 10\n  height: 10\n");
    for (int index = 0; index < 10; ++index) {
        source += QStringLiteral("  PerfCounter { count: %1; title: \"counter-%2\"; extra0: %3; extra1: %4 }\n")
                      .arg(count_offset + index)
                      .arg(index)
                      .arg(index)
                      .arg(index + 1);
    }

    source += QStringLiteral("}\n");
    return source;
}

int test_sync_state_int_latency_prf_01() {
    constexpr const char* test_name = "PRF-01 sync_state_int P99";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    for (int index = 0; index < 128; ++index) {
        if (qmlsharp_sync_state_int(fixture.instance_id().c_str(), "count", index) != qmlsharp::QmlSharpSuccess) {
            return fail(test_name, read_last_error());
        }
    }

    const std::vector<double> samples = measure_operation(10000, [&fixture](int index) {
        const int result = qmlsharp_sync_state_int(fixture.instance_id().c_str(), "count", index);
        if (result != qmlsharp::QmlSharpSuccess) {
            std::cerr << read_last_error() << '\n';
            std::abort();
        }
    });

    return assert_p99_under(test_name, "sync_state_int", samples, 50.0);
}

int test_sync_state_string_latency_prf_02() {
    constexpr const char* test_name = "PRF-02 sync_state_string P99";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    std::vector<std::string> values;
    values.reserve(10000U);
    for (int index = 0; index < 10000; ++index) {
        values.push_back("state-value-" + std::to_string(index) + "-abcdefghijklmnopqrstuvwxyz-0123456789");
    }

    const std::vector<double> samples = measure_operation(10000, [&fixture, &values](int index) {
        const int result = qmlsharp_sync_state_string(fixture.instance_id().c_str(), "title",
                                                      values[static_cast<std::size_t>(index)].c_str());
        if (result != qmlsharp::QmlSharpSuccess) {
            std::cerr << read_last_error() << '\n';
            std::abort();
        }
    });

    return assert_p99_under(test_name, "sync_state_string", samples, 100.0);
}

int test_sync_state_batch_latency_prf_03() {
    constexpr const char* test_name = "PRF-03 sync_state_batch P99";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    std::vector<std::string> payloads;
    payloads.reserve(1000U);
    for (int index = 0; index < 1000; ++index) {
        QJsonObject payload;
        payload.insert(QStringLiteral("count"), index);
        payload.insert(QStringLiteral("ratio"), static_cast<double>(index) / 10.0);
        payload.insert(QStringLiteral("enabled"), index % 2 == 0);
        payload.insert(QStringLiteral("title"), QStringLiteral("batch-%1").arg(index));
        payload.insert(QStringLiteral("metadata"), QJsonObject{{QStringLiteral("index"), index}});
        payload.insert(QStringLiteral("extra0"), index);
        payload.insert(QStringLiteral("extra1"), index + 1);
        payload.insert(QStringLiteral("extra2"), index + 2);
        payload.insert(QStringLiteral("extra3"), index + 3);
        payload.insert(QStringLiteral("extra4"), index + 4);
        const QByteArray json = QJsonDocument(payload).toJson(QJsonDocument::Compact);
        payloads.emplace_back(json.constData(), static_cast<std::size_t>(json.size()));
    }

    const std::vector<double> samples = measure_operation(1000, [&fixture, &payloads](int index) {
        const int result =
            qmlsharp_sync_state_batch(fixture.instance_id().c_str(), payloads[static_cast<std::size_t>(index)].c_str());
        if (result != qmlsharp::QmlSharpSuccess) {
            std::cerr << read_last_error() << '\n';
            std::abort();
        }
    });

    return assert_p99_under(test_name, "sync_state_batch", samples, 500.0);
}

int test_command_dispatch_latency_prf_04() {
    constexpr const char* test_name = "PRF-04 command dispatch P99";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    command_callback_count.store(0, std::memory_order_relaxed);
    qmlsharp_set_command_callback(counting_command_callback);
    qmlsharp_instance_ready(fixture.instance_id().c_str());

    const std::vector<double> samples =
        measure_operation(10000, [&fixture](int) { fixture.counter()->commandNoArgs(); });
    qmlsharp_set_command_callback(nullptr);
    if (command_callback_count.load(std::memory_order_relaxed) != 10000) {
        return fail(test_name, "command callback count did not match the measured dispatch count.");
    }

    return assert_p99_under(test_name, "command dispatch", samples, 100.0);
}

int test_instance_creation_latency_prf_05() {
    constexpr const char* test_name = "PRF-05 instance creation P99";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    std::vector<std::unique_ptr<RegistrationCounterViewModel>> instances;
    instances.reserve(1000U);
    const std::vector<double> samples = measure_operation(
        1000, [&instances](int) { instances.push_back(std::make_unique<RegistrationCounterViewModel>()); });

    instances.clear();
    qmlsharp_engine_shutdown(engine);
    return assert_p99_under(test_name, "instance creation", samples, 1000.0);
}

int test_hot_reload_total_latency_prf_06() {
    constexpr const char* test_name = "PRF-06 hot reload total P99";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    if (register_counter_type_for_test(engine, "QmlSharp.NativePerf.HotReload", "PerfCounter",
                                       "schema-native-perf-hot-reload") != qmlsharp::QmlSharpSuccess) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, read_last_error());
    }

    QTemporaryDir directory(QDir::tempPath() + QStringLiteral("/qmlsharp perf spaces XXXXXX"));
    std::string error;
    const QString initial_path =
        write_qml_file(directory, QStringLiteral("PerfReload.qml"), make_hot_reload_qml(0), error);
    if (initial_path.isEmpty() || reload_qml_file(engine, initial_path) != qmlsharp::QmlSharpSuccess) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, error.empty() ? read_last_error() : error);
    }

    const std::vector<double> samples = measure_operation(100, [&directory, engine](int index) {
        const std::string snapshot = take_native_string(qmlsharp_capture_snapshot(engine));
        if (snapshot.empty()) {
            std::cerr << read_last_error() << '\n';
            std::abort();
        }

        std::string write_error;
        const QString path =
            write_qml_file(directory, QStringLiteral("PerfReload.qml"), make_hot_reload_qml(index + 100), write_error);
        if (path.isEmpty() || reload_qml_file(engine, path) != qmlsharp::QmlSharpSuccess) {
            std::cerr << (write_error.empty() ? read_last_error() : write_error) << '\n';
            std::abort();
        }

        qmlsharp_restore_snapshot(engine, snapshot.c_str());
        const std::string restore_error = read_last_error();
        if (!restore_error.empty()) {
            std::cerr << restore_error << '\n';
            std::abort();
        }
    });

    qmlsharp_engine_shutdown(engine);
    return assert_p99_under(test_name, "hot reload total", samples, 70000.0);
}

int test_state_sync_throughput_prf_07() {
    constexpr const char* test_name = "PRF-07 sustained state sync throughput";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    constexpr int test_duration_seconds = 60;
    constexpr int target_operations_per_second = 15000;
    const Clock::time_point start = Clock::now();
    int operation_count = 0;
    for (int second = 0; second < test_duration_seconds; ++second) {
        for (int index = 0; index < target_operations_per_second; ++index) {
            if (qmlsharp_sync_state_int(fixture.instance_id().c_str(), "count", operation_count) !=
                qmlsharp::QmlSharpSuccess) {
                return fail(test_name, read_last_error());
            }

            ++operation_count;
        }

        std::this_thread::sleep_until(start + std::chrono::seconds(second + 1));
    }

    const Clock::time_point end = Clock::now();
    const double seconds = std::chrono::duration<double>(end - start).count();
    const double ops_per_second = static_cast<double>(operation_count) / seconds;
    std::cout << "[MEASURE] sync_state_int throughput = " << ops_per_second << " ops/sec\n";
    if (ops_per_second <= 10000.0) {
        return fail(test_name, "state sync throughput was below 10,000 ops/sec.");
    }

    return EXIT_SUCCESS;
}

int test_concurrent_instances_prf_08() {
    constexpr const char* test_name = "PRF-08 100 concurrent instances";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    std::vector<std::unique_ptr<RegistrationCounterViewModel>> instances;
    std::vector<std::string> instance_ids;
    instances.reserve(100U);
    instance_ids.reserve(100U);
    for (int index = 0; index < 100; ++index) {
        instances.push_back(std::make_unique<RegistrationCounterViewModel>());
        instance_ids.push_back(instances.back()->instanceId().toStdString());
    }

    std::string metrics_error;
    const QJsonObject metrics = read_metrics_object(metrics_error);
    if (!metrics_error.empty()) {
        instances.clear();
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, metrics_error);
    }

    if (metrics.value(QStringLiteral("activeInstanceCount")).toInt() < 100) {
        instances.clear();
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, "native registry did not track all 100 active instances.");
    }

    const std::vector<double> samples = measure_operation(10000, [&instance_ids](int index) {
        const std::size_t instance_index = static_cast<std::size_t>(index % 100);
        const int result = qmlsharp_sync_state_int(instance_ids[instance_index].c_str(), "count", index);
        if (result != qmlsharp::QmlSharpSuccess) {
            std::cerr << read_last_error() << '\n';
            std::abort();
        }
    });

    instances.clear();
    qmlsharp_engine_shutdown(engine);
    return assert_p99_under(test_name, "100-instance sync_state_int", samples, 100.0);
}

int test_background_thread_state_sync_and_command_dispatch() {
    constexpr const char* test_name = "background thread state sync and command dispatch";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    command_callback_count.store(0, std::memory_order_relaxed);
    qmlsharp_set_command_callback(counting_command_callback);
    qmlsharp_instance_ready(fixture.instance_id().c_str());

    std::atomic_bool done = false;
    std::string worker_error;
    std::thread worker([&fixture, &done, &worker_error]() {
        if (qmlsharp_sync_state_int(fixture.instance_id().c_str(), "count", 444) != qmlsharp::QmlSharpSuccess) {
            worker_error = read_last_error();
            done.store(true, std::memory_order_release);
            return;
        }

        const bool invoked =
            QMetaObject::invokeMethod(fixture.counter(), "commandNoArgs", Qt::BlockingQueuedConnection);
        if (!invoked) {
            worker_error = "worker command dispatch did not marshal to the QObject thread.";
        }

        done.store(true, std::memory_order_release);
    });

    for (int attempt = 0; attempt < 200 && !done.load(std::memory_order_acquire); ++attempt) {
        QCoreApplication::processEvents(QEventLoop::AllEvents, 10);
        QThread::msleep(1);
    }

    worker.join();
    qmlsharp_set_command_callback(nullptr);
    if (!worker_error.empty()) {
        return fail(test_name, worker_error);
    }

    if (fixture.counter()->count() != 444 || command_callback_count.load(std::memory_order_relaxed) != 1) {
        return fail(test_name, "background work did not update state and dispatch one command.");
    }

    return EXIT_SUCCESS;
}

int test_command_callback_reentry_does_not_deadlock() {
    constexpr const char* test_name = "command callback reentry does not deadlock";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    reentrant_callback_entered.store(false, std::memory_order_release);
    reentrant_callback_result.store(-1, std::memory_order_release);
    qmlsharp_set_command_callback(reentrant_command_callback);
    qmlsharp_instance_ready(fixture.instance_id().c_str());
    fixture.counter()->commandNoArgs();
    qmlsharp_set_command_callback(nullptr);

    if (!reentrant_callback_entered.load(std::memory_order_acquire) ||
        reentrant_callback_result.load(std::memory_order_acquire) != qmlsharp::QmlSharpSuccess ||
        fixture.counter()->count() != 321) {
        return fail(test_name, "reentrant state sync from the command callback did not complete.");
    }

    return EXIT_SUCCESS;
}

int test_repeated_init_shutdown_releases_resources() {
    constexpr const char* test_name = "repeated init shutdown releases resources";
    for (int index = 0; index < 25; ++index) {
        void* engine = qmlsharp_engine_init(0, nullptr);
        if (engine == nullptr) {
            return fail(test_name, read_last_error());
        }

        {
            std::vector<std::unique_ptr<RegistrationCounterViewModel>> instances;
            instances.reserve(4U);
            for (int instance_index = 0; instance_index < 4; ++instance_index) {
                instances.push_back(std::make_unique<RegistrationCounterViewModel>());
            }
        }

        qmlsharp_engine_shutdown(engine);
        std::string metrics_error;
        const QJsonObject metrics = read_metrics_object(metrics_error);
        if (!metrics_error.empty()) {
            return fail(test_name, metrics_error);
        }

        if (metrics.value(QStringLiteral("activeInstanceCount")).toInt() != 0) {
            return fail(test_name, "native registry retained instances after shutdown cycle.");
        }
    }

    return EXIT_SUCCESS;
}

int test_post_to_main_thread_after_shutdown_rejects_and_drops_queued_work() {
    constexpr const char* test_name = "post to main thread after shutdown rejects and drops queued work";
    void* engine = qmlsharp_engine_init(0, nullptr);
    if (engine == nullptr) {
        return fail(test_name, read_last_error());
    }

    std::atomic_bool called = false;
    qmlsharp_post_to_main_thread(posted_callback, &called);
    if (!read_last_error().empty()) {
        qmlsharp_engine_shutdown(engine);
        return fail(test_name, "posting before shutdown should succeed.");
    }

    qmlsharp_engine_shutdown(engine);
    if (called.load(std::memory_order_acquire)) {
        return fail(test_name, "queued callback should not run after immediate shutdown.");
    }

    qmlsharp_post_to_main_thread(posted_callback, &called);
    if (read_last_error().empty()) {
        return fail(test_name, "posting after shutdown should report an error.");
    }

    if (called.load(std::memory_order_acquire)) {
        return fail(test_name, "post-after-shutdown callback should not run.");
    }

    return EXIT_SUCCESS;
}

int test_utf8_strings_round_trip_through_abi() {
    constexpr const char* test_name = "UTF-8 ABI string round trip";
    CounterFixture fixture;
    if (!fixture.valid()) {
        return fail(test_name, read_last_error());
    }

    const QString expected = QString::fromUtf8(
        "Gr\xc3\xbc\xc3\x9f"
        "e \xe6\x9d\xb1\xe4\xba\xac tea");
    const QByteArray expected_utf8 = expected.toUtf8();
    if (qmlsharp_sync_state_string(fixture.instance_id().c_str(), "title", expected_utf8.constData()) !=
        qmlsharp::QmlSharpSuccess) {
        return fail(test_name, read_last_error());
    }

    if (fixture.counter()->title() != expected) {
        return fail(test_name, "UTF-8 title did not round-trip through qmlsharp_sync_state_string.");
    }

    const char* json_payload =
        "{\"text\":\"Gr\xc3\xbc\xc3\x9f"
        "e \xe6\x9d\xb1\xe4\xba\xac tea\"}";
    if (qmlsharp_sync_state_json(fixture.instance_id().c_str(), "metadata", json_payload) !=
        qmlsharp::QmlSharpSuccess) {
        return fail(test_name, read_last_error());
    }

    const QJsonObject metadata = fixture.counter()->metadata().toJsonObject();
    if (metadata.value(QStringLiteral("text")).toString() != expected) {
        return fail(test_name, "UTF-8 JSON payload did not round-trip through qmlsharp_sync_state_json.");
    }

    return EXIT_SUCCESS;
}
}  // namespace

int main() {
    configure_headless_qt();

    int result = EXIT_SUCCESS;
    result |= run_test("PRF-01 sync_state_int P99", test_sync_state_int_latency_prf_01);
    result |= run_test("PRF-02 sync_state_string P99", test_sync_state_string_latency_prf_02);
    result |= run_test("PRF-03 sync_state_batch P99", test_sync_state_batch_latency_prf_03);
    result |= run_test("PRF-04 command dispatch P99", test_command_dispatch_latency_prf_04);
    result |= run_test("PRF-05 instance creation P99", test_instance_creation_latency_prf_05);
    result |= run_test("PRF-06 hot reload total P99", test_hot_reload_total_latency_prf_06);
    result |= run_test("PRF-07 sustained state sync throughput", test_state_sync_throughput_prf_07);
    result |= run_test("PRF-08 100 concurrent instances", test_concurrent_instances_prf_08);
    result |= run_test("background thread state sync and command dispatch",
                       test_background_thread_state_sync_and_command_dispatch);
    result |= run_test("command callback reentry does not deadlock", test_command_callback_reentry_does_not_deadlock);
    result |= run_test("repeated init shutdown releases resources", test_repeated_init_shutdown_releases_resources);
    result |= run_test("post to main thread after shutdown rejects and drops queued work",
                       test_post_to_main_thread_after_shutdown_rejects_and_drops_queued_work);
    result |= run_test("UTF-8 ABI string round trip", test_utf8_strings_round_trip_through_abi);
    return result;
}
