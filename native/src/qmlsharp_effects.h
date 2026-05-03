#pragma once

namespace qmlsharp {
int dispatch_effect(const char* instance_id, const char* effect_name, const char* payload_json) noexcept;
int broadcast_effect(const char* class_name, const char* effect_name, const char* payload_json) noexcept;
}  // namespace qmlsharp
