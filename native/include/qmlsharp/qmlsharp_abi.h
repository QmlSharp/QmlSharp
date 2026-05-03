#pragma once

#include "qmlsharp/qmlsharp_export.h"

#include <stdint.h>

#define QMLSHARP_ABI_VERSION 1

#ifdef __cplusplus
extern "C" {
#endif

QMLSHARP_API int32_t qmlsharp_get_abi_version(void);
QMLSHARP_API void* qmlsharp_engine_init(int32_t argc, const char** argv);
QMLSHARP_API void qmlsharp_engine_shutdown(void* engine);
QMLSHARP_API int32_t qmlsharp_engine_exec(void* engine);
QMLSHARP_API void qmlsharp_post_to_main_thread(void (*callback)(void* user_data), void* user_data);
QMLSHARP_API const char* qmlsharp_get_last_error(void);
QMLSHARP_API void qmlsharp_free_string(const char* str);

#ifdef __cplusplus
}
#endif
