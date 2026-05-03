#pragma once

#include <string>

namespace qmlsharp {
constexpr int QmlSharpSuccess = 0;
constexpr int QmlSharpGeneralFailure = -1;
constexpr int QmlSharpInvalidArgument = -2;
constexpr int QmlSharpEngineNotInitialized = -4;

void set_last_error(std::string message);
void clear_last_error();
std::string last_error();
}  // namespace qmlsharp
