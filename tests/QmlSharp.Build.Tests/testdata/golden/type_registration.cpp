// GENERATED — DO NOT EDIT
#include "qmlsharp/qmlsharp_abi.h"

#include <qqml.h>
#include <stdint.h>

#include <QtGlobal>

#include "CounterViewModel.h"

namespace {
int32_t QMLSHARP_CALL registerCounterViewModel(const char* moduleUri, int32_t versionMajor, int32_t versionMinor,
                                               const char* typeName) {
    return qmlRegisterType<CounterViewModel>(moduleUri, versionMajor, versionMinor, typeName);
}

}  // namespace

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_register_generated_type(const char* moduleUri,
                                                                               int32_t versionMajor,
                                                                               int32_t versionMinor,
                                                                               const char* typeName) {
    if (moduleUri == nullptr || typeName == nullptr) {
        return -2;
    }

    if (qstrcmp(typeName, "CounterViewModel") == 0) {
        return registerCounterViewModel(moduleUri, versionMajor, versionMinor, typeName);
    }

    return -6;
}
