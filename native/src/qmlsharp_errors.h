#pragma once

#include <string>

namespace qmlsharp {
constexpr int QmlSharpSuccess = 0;
constexpr int QmlSharpGeneralFailure = -1;
constexpr int QmlSharpInvalidArgument = -2;
constexpr int QmlSharpInstanceNotFound = -3;
constexpr int QmlSharpEngineNotInitialized = -4;
constexpr int QmlSharpQmlLoadFailure = -5;
constexpr int QmlSharpTypeRegistrationFailure = -6;
constexpr int QmlSharpPropertyNotFound = -7;
constexpr int QmlSharpJsonParseFailure = -8;

void set_last_error(std::string message);
void clear_last_error();
std::string last_error();
}  // namespace qmlsharp
