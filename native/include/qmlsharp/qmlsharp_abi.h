#pragma once

#include "qmlsharp/qmlsharp_export.h"

#include <stdint.h>

#define QMLSHARP_ABI_VERSION 1

#ifdef __cplusplus
extern "C" {
#endif

QMLSHARP_API int32_t qmlsharp_get_abi_version(void);
QMLSHARP_API const char* qmlsharp_get_last_error(void);
QMLSHARP_API void qmlsharp_free_string(const char* str);

#ifdef __cplusplus
}
#endif
