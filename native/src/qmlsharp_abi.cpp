#include "qmlsharp/qmlsharp_abi.h"

#include <cstdlib>
#include <cstring>
#include <new>
#include <string>

#include "qmlsharp_diagnostics.h"
#include "qmlsharp_effects.h"
#include "qmlsharp_engine.h"
#include "qmlsharp_error_overlay.h"
#include "qmlsharp_errors.h"
#include "qmlsharp_hot_reload.h"
#include "qmlsharp_instances.h"
#include "qmlsharp_state.h"
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

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_get_abi_version(void) {
    return QMLSHARP_ABI_VERSION;
}

extern "C" QMLSHARP_API void* QMLSHARP_CALL qmlsharp_engine_init(int32_t argc, const char** argv) {
    return qmlsharp::engine_init(argc, argv);
}

extern "C" QMLSHARP_API void QMLSHARP_CALL qmlsharp_engine_shutdown(void* engine) {
    qmlsharp::engine_shutdown(engine);
}

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_engine_exec(void* engine) {
    return qmlsharp::engine_exec(engine);
}

extern "C" QMLSHARP_API void QMLSHARP_CALL qmlsharp_post_to_main_thread(qmlsharp_main_thread_callback callback,
                                                                        void* user_data) {
    qmlsharp::post_to_main_thread(callback, user_data);
}

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_register_type(
    void* engine, const char* module_uri, int32_t version_major, int32_t version_minor, const char* type_name,
    const char* schema_id, const char* compiler_slot_key, qmlsharp_type_registration_callback register_callback) {
    return qmlsharp::register_type(engine, module_uri, version_major, version_minor, type_name, schema_id,
                                   compiler_slot_key, register_callback);
}

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_register_module(void* engine, const char* module_uri,
                                                                       int32_t version_major, int32_t version_minor,
                                                                       const qmlsharp_type_registration_entry* entries,
                                                                       int32_t entry_count) {
    return qmlsharp::register_module(engine, module_uri, version_major, version_minor, entries, entry_count);
}

extern "C" QMLSHARP_API void QMLSHARP_CALL
qmlsharp_set_instance_callbacks(qmlsharp_instance_created_cb on_created, qmlsharp_instance_destroyed_cb on_destroyed) {
    qmlsharp::set_instance_callbacks(on_created, on_destroyed);
}

extern "C" QMLSHARP_API void QMLSHARP_CALL qmlsharp_instance_ready(const char* instance_id) {
    qmlsharp::instance_ready(instance_id);
}

extern "C" QMLSHARP_API void QMLSHARP_CALL qmlsharp_set_command_callback(qmlsharp_command_cb callback) {
    qmlsharp::set_command_callback(callback);
}

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_sync_state_string(const char* instance_id,
                                                                         const char* property_name, const char* value) {
    return qmlsharp::sync_state_string(instance_id, property_name, value);
}

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_sync_state_int(const char* instance_id,
                                                                      const char* property_name, int32_t value) {
    return qmlsharp::sync_state_int(instance_id, property_name, value);
}

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_sync_state_double(const char* instance_id,
                                                                         const char* property_name, double value) {
    return qmlsharp::sync_state_double(instance_id, property_name, value);
}

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_sync_state_bool(const char* instance_id,
                                                                       const char* property_name, int32_t value) {
    return qmlsharp::sync_state_bool(instance_id, property_name, value);
}

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_sync_state_json(const char* instance_id,
                                                                       const char* property_name,
                                                                       const char* json_value) {
    return qmlsharp::sync_state_json(instance_id, property_name, json_value);
}

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_sync_state_batch(const char* instance_id,
                                                                        const char* properties_json) {
    return qmlsharp::sync_state_batch(instance_id, properties_json);
}

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_dispatch_effect(const char* instance_id, const char* effect_name,
                                                                       const char* payload_json) {
    return qmlsharp::dispatch_effect(instance_id, effect_name, payload_json);
}

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_broadcast_effect(const char* class_name, const char* effect_name,
                                                                        const char* payload_json) {
    return qmlsharp::broadcast_effect(class_name, effect_name, payload_json);
}

extern "C" QMLSHARP_API const char* QMLSHARP_CALL qmlsharp_capture_snapshot(void* engine) {
    return qmlsharp::capture_snapshot(engine);
}

extern "C" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_reload_qml(void* engine, const char* qml_source_path) {
    return qmlsharp::reload_qml(engine, qml_source_path);
}

extern "C" QMLSHARP_API void QMLSHARP_CALL qmlsharp_restore_snapshot(void* engine, const char* snapshot_json) {
    qmlsharp::restore_snapshot(engine, snapshot_json);
}

extern "C" QMLSHARP_API void QMLSHARP_CALL qmlsharp_show_error(void* engine, const char* title, const char* message,
                                                               const char* file_path, int32_t line, int32_t column) {
    qmlsharp::show_error(engine, title, message, file_path, line, column);
}

extern "C" QMLSHARP_API void QMLSHARP_CALL qmlsharp_hide_error(void* engine) {
    qmlsharp::hide_error(engine);
}

extern "C" QMLSHARP_API const char* QMLSHARP_CALL qmlsharp_get_instance_info(const char* instance_id) {
    return qmlsharp::get_instance_info(instance_id);
}

extern "C" QMLSHARP_API const char* QMLSHARP_CALL qmlsharp_get_all_instances(void) {
    return qmlsharp::get_all_instances();
}

extern "C" QMLSHARP_API const char* QMLSHARP_CALL qmlsharp_get_metrics(void) {
    return qmlsharp::get_metrics();
}

extern "C" QMLSHARP_API const char* QMLSHARP_CALL qmlsharp_get_last_error(void) {
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

extern "C" QMLSHARP_API void QMLSHARP_CALL qmlsharp_free_string(const char* str) {
    std::free(const_cast<char*>(str));
}
