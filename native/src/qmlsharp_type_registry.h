#pragma once

#include "qmlsharp/qmlsharp_abi.h"
#include "qmlsharp/qmlsharp_export.h"

#include <string>

namespace qmlsharp {
struct RegisteredTypeMetadata {
    std::string module_uri;
    int version_major = 0;
    int version_minor = 0;
    std::string type_name;
    std::string schema_id;
    std::string compiler_slot_key;
    int qt_type_id = -1;
};

int register_type(void* engine, const char* module_uri, int version_major, int version_minor, const char* type_name,
                  const char* schema_id, const char* compiler_slot_key,
                  qmlsharp_type_registration_callback register_callback) noexcept;

int register_module(void* engine, const char* module_uri, int version_major, int version_minor,
                    const qmlsharp_type_registration_entry* entries, int entry_count) noexcept;

QMLSHARP_API const RegisteredTypeMetadata* find_registered_type(const std::string& module_uri, int version_major,
                                                                int version_minor, const std::string& type_name);
}  // namespace qmlsharp
