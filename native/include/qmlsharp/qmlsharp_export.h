#pragma once

#if defined(_WIN32)
#if defined(QMLSHARP_NATIVE_BUILD)
#define QMLSHARP_API __declspec(dllexport)
#else
#define QMLSHARP_API __declspec(dllimport)
#endif
#define QMLSHARP_CALL __cdecl
#elif defined(__GNUC__) || defined(__clang__)
#define QMLSHARP_API __attribute__((visibility("default")))
#define QMLSHARP_CALL
#else
#define QMLSHARP_API
#define QMLSHARP_CALL
#endif
