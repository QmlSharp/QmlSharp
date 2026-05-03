#include "qmlsharp_metrics.h"

#include <mutex>

#include "qmlsharp_instances.h"

namespace qmlsharp {
namespace {
std::mutex metrics_mutex;
int64_t type_registration_count = 0;
int64_t state_sync_count = 0;
int64_t command_dispatch_count = 0;
int64_t effect_dispatch_count = 0;
int64_t hot_reload_count = 0;
int64_t last_hot_reload_duration_ms = 0;
int64_t error_count = 0;
bool error_overlay_visible = false;
}  // namespace

void record_type_registration() noexcept {
    std::lock_guard<std::mutex> lock(metrics_mutex);
    ++type_registration_count;
}

void record_state_sync() noexcept {
    std::lock_guard<std::mutex> lock(metrics_mutex);
    ++state_sync_count;
}

void record_command_dispatch() noexcept {
    std::lock_guard<std::mutex> lock(metrics_mutex);
    ++command_dispatch_count;
}

void record_effect_dispatch() noexcept {
    std::lock_guard<std::mutex> lock(metrics_mutex);
    ++effect_dispatch_count;
}

void record_hot_reload(int64_t duration_ms) noexcept {
    std::lock_guard<std::mutex> lock(metrics_mutex);
    ++hot_reload_count;
    last_hot_reload_duration_ms = duration_ms < 0 ? 0 : duration_ms;
}

void record_error() noexcept {
    std::lock_guard<std::mutex> lock(metrics_mutex);
    ++error_count;
}

void set_error_overlay_visible(bool visible) noexcept {
    std::lock_guard<std::mutex> lock(metrics_mutex);
    error_overlay_visible = visible;
}

RuntimeMetricsSnapshot metrics_snapshot() noexcept {
    std::lock_guard<std::mutex> lock(metrics_mutex);
    return RuntimeMetricsSnapshot{
        active_instance_count(),
        type_registration_count,
        state_sync_count,
        command_dispatch_count,
        effect_dispatch_count,
        hot_reload_count,
        last_hot_reload_duration_ms,
        error_count,
        error_overlay_visible,
    };
}
}  // namespace qmlsharp
