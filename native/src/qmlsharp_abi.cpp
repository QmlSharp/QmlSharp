#include "qmlsharp/qmlsharp_abi.h"

#include <cstdlib>
#include <cstring>
#include <new>
#include <string>

#include "qmlsharp_errors.h"

namespace {
const char* qmlsharp_allocate_string(const std::string& value) noexcept {
    char* buffer = static_cast<char*>(std::malloc(value.size() + 1U));
    if (buffer == nullptr) {
        qmlsharp::set_last_error("Failed to allocate native string.");
        return nullptr;
    }

    std::memcpy(buffer, value.c_str(), value.size() + 1U);
    return buffer;
}
}  // namespace

extern "C" QMLSHARP_API int32_t qmlsharp_get_abi_version(void) {
    return QMLSHARP_ABI_VERSION;
}

extern "C" QMLSHARP_API const char* qmlsharp_get_last_error(void) {
    try {
        const std::string message = qmlsharp::last_error();
        if (message.empty()) {
            return nullptr;
        }

        return qmlsharp_allocate_string(message);
    } catch (const std::bad_alloc&) {
        return nullptr;
    } catch (...) {
        qmlsharp::set_last_error("Unknown failure while reading the last native error.");
        return nullptr;
    }
}

extern "C" QMLSHARP_API void qmlsharp_free_string(const char* str) {
    std::free(const_cast<char*>(str));
}
