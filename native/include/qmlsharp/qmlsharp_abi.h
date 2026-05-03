#pragma once

#include "qmlsharp/qmlsharp_export.h"

#include <stdint.h>

#define QMLSHARP_ABI_VERSION 1

#ifdef __cplusplus
extern "C" {
#endif

QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_get_abi_version(void);
QMLSHARP_API void* QMLSHARP_CALL qmlsharp_engine_init(int32_t argc, const char** argv);
QMLSHARP_API void QMLSHARP_CALL qmlsharp_engine_shutdown(void* engine);
QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_engine_exec(void* engine);

typedef void(QMLSHARP_CALL* qmlsharp_main_thread_callback)(void* user_data);

QMLSHARP_API void QMLSHARP_CALL qmlsharp_post_to_main_thread(qmlsharp_main_thread_callback callback, void* user_data);

typedef int32_t(QMLSHARP_CALL* qmlsharp_type_registration_callback)(const char* module_uri, int32_t version_major,
                                                                    int32_t version_minor, const char* type_name);

typedef struct qmlsharp_type_registration_entry {
    const char* type_name;
    const char* schema_id;
    const char* compiler_slot_key;
    qmlsharp_type_registration_callback register_callback;
} qmlsharp_type_registration_entry;

QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_register_type(void* engine, const char* module_uri, int32_t version_major,
                                                          int32_t version_minor, const char* type_name,
                                                          const char* schema_id, const char* compiler_slot_key,
                                                          qmlsharp_type_registration_callback register_callback);

QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_register_module(void* engine, const char* module_uri, int32_t version_major,
                                                            int32_t version_minor,
                                                            const qmlsharp_type_registration_entry* entries,
                                                            int32_t entry_count);

typedef void(QMLSHARP_CALL* qmlsharp_instance_created_cb)(const char* instance_id, const char* class_name,
                                                          const char* compiler_slot_key);
typedef void(QMLSHARP_CALL* qmlsharp_instance_destroyed_cb)(const char* instance_id);

QMLSHARP_API void QMLSHARP_CALL qmlsharp_set_instance_callbacks(qmlsharp_instance_created_cb on_created,
                                                                qmlsharp_instance_destroyed_cb on_destroyed);
QMLSHARP_API void QMLSHARP_CALL qmlsharp_instance_ready(const char* instance_id);

typedef void(QMLSHARP_CALL* qmlsharp_command_cb)(const char* instance_id, const char* command_name,
                                                 const char* args_json);

QMLSHARP_API void QMLSHARP_CALL qmlsharp_set_command_callback(qmlsharp_command_cb callback);

QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_sync_state_string(const char* instance_id, const char* property_name,
                                                              const char* value);
QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_sync_state_int(const char* instance_id, const char* property_name,
                                                           int32_t value);
QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_sync_state_double(const char* instance_id, const char* property_name,
                                                              double value);
QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_sync_state_bool(const char* instance_id, const char* property_name,
                                                            int32_t value);
QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_sync_state_json(const char* instance_id, const char* property_name,
                                                            const char* json_value);
QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_sync_state_batch(const char* instance_id, const char* properties_json);

QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_dispatch_effect(const char* instance_id, const char* effect_name,
                                                            const char* payload_json);
QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_broadcast_effect(const char* class_name, const char* effect_name,
                                                             const char* payload_json);

QMLSHARP_API const char* QMLSHARP_CALL qmlsharp_get_last_error(void);
QMLSHARP_API void QMLSHARP_CALL qmlsharp_free_string(const char* str);

#ifdef __cplusplus
}
#endif
