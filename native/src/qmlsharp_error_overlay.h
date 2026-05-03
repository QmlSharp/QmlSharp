#pragma once

#include <stdint.h>

namespace qmlsharp {
void show_error(void* engine, const char* title, const char* message, const char* file_path, int32_t line,
                int32_t column) noexcept;
void hide_error(void* engine) noexcept;
}  // namespace qmlsharp
