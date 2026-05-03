#pragma once

#include <stdint.h>

namespace qmlsharp {
struct RuntimeMetricsSnapshot {
    int32_t active_instance_count = 0;
    int64_t type_registration_count = 0;
    int64_t state_sync_count = 0;
    int64_t command_dispatch_count = 0;
    int64_t effect_dispatch_count = 0;
    int64_t hot_reload_count = 0;
    int64_t last_hot_reload_duration_ms = 0;
    int64_t error_count = 0;
    bool error_overlay_visible = false;
};

void record_type_registration() noexcept;
void record_state_sync() noexcept;
void record_command_dispatch() noexcept;
void record_effect_dispatch() noexcept;
void record_hot_reload(int64_t duration_ms) noexcept;
void record_error() noexcept;
void set_error_overlay_visible(bool visible) noexcept;
RuntimeMetricsSnapshot metrics_snapshot() noexcept;
}  // namespace qmlsharp
