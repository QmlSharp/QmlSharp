#pragma once

#include <stdint.h>

namespace qmlsharp {
int sync_state_string(const char* instance_id, const char* property_name, const char* value) noexcept;
int sync_state_int(const char* instance_id, const char* property_name, int32_t value) noexcept;
int sync_state_double(const char* instance_id, const char* property_name, double value) noexcept;
int sync_state_bool(const char* instance_id, const char* property_name, int32_t value) noexcept;
int sync_state_json(const char* instance_id, const char* property_name, const char* json_value) noexcept;
int sync_state_batch(const char* instance_id, const char* properties_json) noexcept;
}  // namespace qmlsharp
