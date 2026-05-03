#pragma once

#include <stdint.h>

namespace qmlsharp {
const char* capture_snapshot(void* engine) noexcept;
int32_t reload_qml(void* engine, const char* qml_source_path) noexcept;
void restore_snapshot(void* engine, const char* snapshot_json) noexcept;
}  // namespace qmlsharp
