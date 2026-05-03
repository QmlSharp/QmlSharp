#include "qmlsharp/qmlsharp_abi.h"

#include <cstdlib>
#include <cstring>
#include <new>
#include <string>

#include "qmlsharp_engine.h"
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

extern "C" QMLSHARP_API void* qmlsharp_engine_init(int32_t argc, const char** argv) {
    return qmlsharp::engine_init(argc, argv);
}

extern "C" QMLSHARP_API void qmlsharp_engine_shutdown(void* engine) {
    qmlsharp::engine_shutdown(engine);
}

extern "C" QMLSHARP_API int32_t qmlsharp_engine_exec(void* engine) {
    return qmlsharp::engine_exec(engine);
}

extern "C" QMLSHARP_API void qmlsharp_post_to_main_thread(void (*callback)(void* user_data), void* user_data) {
    qmlsharp::post_to_main_thread(callback, user_data);
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
