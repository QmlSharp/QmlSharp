#pragma once

#include <string>

namespace qmlsharp {
void set_last_error(std::string message);
std::string last_error();
}  // namespace qmlsharp
