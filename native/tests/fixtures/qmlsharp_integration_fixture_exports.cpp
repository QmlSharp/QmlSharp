#include "qmlsharp/qmlsharp_export.h"

#include <qqml.h>
#include <stdint.h>

#include "RegistrationCounterViewModel.h"

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_test_register_registration_counter_view_model(
    const char* module_uri, int32_t version_major, int32_t version_minor, const char* type_name) {
    if (module_uri == nullptr || type_name == nullptr) {
        return -1;
    }

    return qmlRegisterType<RegistrationCounterViewModel>(module_uri, version_major, version_minor, type_name);
}
