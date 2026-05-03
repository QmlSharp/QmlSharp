#include "qmlsharp/qmlsharp_abi.h"

#include <cstdlib>
#include <cstring>
#include <new>
#include <string>

#include "qmlsharp_engine.h"
#include "qmlsharp_errors.h"
#include "qmlsharp_instances.h"
#include "qmlsharp_type_registry.h"

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

extern "C" QMLSHARP_API int32_t qmlsharp_register_type(void* engine, const char* module_uri, int32_t version_major,
                                                       int32_t version_minor, const char* type_name,
                                                       const char* schema_id, const char* compiler_slot_key,
                                                       qmlsharp_type_registration_callback register_callback) {
    return qmlsharp::register_type(engine, module_uri, version_major, version_minor, type_name, schema_id,
                                   compiler_slot_key, register_callback);
}

extern "C" QMLSHARP_API int32_t qmlsharp_register_module(void* engine, const char* module_uri, int32_t version_major,
                                                         int32_t version_minor,
                                                         const qmlsharp_type_registration_entry* entries,
                                                         int32_t entry_count) {
    return qmlsharp::register_module(engine, module_uri, version_major, version_minor, entries, entry_count);
}

extern "C" QMLSHARP_API void qmlsharp_set_instance_callbacks(qmlsharp_instance_created_cb on_created,
                                                             qmlsharp_instance_destroyed_cb on_destroyed) {
    qmlsharp::set_instance_callbacks(on_created, on_destroyed);
}

extern "C" QMLSHARP_API void qmlsharp_instance_ready(const char* instance_id) {
    qmlsharp::instance_ready(instance_id);
}

extern "C" QMLSHARP_API void qmlsharp_set_command_callback(qmlsharp_command_cb callback) {
    qmlsharp::set_command_callback(callback);
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
