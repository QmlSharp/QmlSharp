#include "qmlsharp_type_registry.h"

#include <cctype>
#include <exception>
#include <map>
#include <mutex>
#include <set>
#include <sstream>
#include <string>
#include <tuple>
#include <utility>

#include "qmlsharp_engine.h"
#include "qmlsharp_errors.h"

namespace qmlsharp {
namespace {
using TypeKey = std::tuple<std::string, int, int, std::string>;

std::mutex registry_mutex;
std::map<TypeKey, RegisteredTypeMetadata> registered_types;

bool is_blank(const char* value) noexcept {
    if (value == nullptr) {
        return true;
    }

    while (*value != '\0') {
        if (!std::isspace(static_cast<unsigned char>(*value))) {
            return false;
        }

        ++value;
    }

    return true;
}

bool is_valid_qml_type_name(const char* value) noexcept {
    if (is_blank(value)) {
        return false;
    }

    const auto first = static_cast<unsigned char>(value[0]);
    if (first < static_cast<unsigned char>('A') || first > static_cast<unsigned char>('Z')) {
        return false;
    }

    for (const char* cursor = value + 1; *cursor != '\0'; ++cursor) {
        const auto character = static_cast<unsigned char>(*cursor);
        if (!std::isalnum(character) && character != static_cast<unsigned char>('_')) {
            return false;
        }
    }

    return true;
}

std::string make_key_display(const TypeKey& key) {
    std::ostringstream builder;
    builder << std::get<0>(key) << ' ' << std::get<1>(key) << '.' << std::get<2>(key) << '/' << std::get<3>(key);
    return builder.str();
}

bool validate_common(void* engine, const char* module_uri, int version_major, int version_minor,
                     const char* operation) {
    if (!validate_engine_call(engine, operation)) {
        return false;
    }

    if (is_blank(module_uri)) {
        set_last_error(std::string(operation) + " requires a non-empty module URI.");
        return false;
    }

    if (version_major < 0 || version_minor < 0) {
        set_last_error(std::string(operation) + " requires a non-negative module version.");
        return false;
    }

    return true;
}

bool validate_type_entry(const char* type_name, const char* schema_id, const char* compiler_slot_key,
                         qmlsharp_type_registration_callback register_callback, const char* operation) {
    if (!is_valid_qml_type_name(type_name)) {
        set_last_error(std::string(operation) + " requires a QML type name that starts with an uppercase letter.");
        return false;
    }

    if (is_blank(schema_id)) {
        set_last_error(std::string(operation) + " requires a non-empty schema ID.");
        return false;
    }

    if (is_blank(compiler_slot_key)) {
        set_last_error(std::string(operation) + " requires a non-empty compilerSlotKey.");
        return false;
    }

    if (register_callback == nullptr) {
        set_last_error(std::string(operation) + " requires a generated type registration callback.");
        return false;
    }

    return true;
}

TypeKey make_type_key(const char* module_uri, int version_major, int version_minor, const char* type_name) {
    return TypeKey{std::string(module_uri), version_major, version_minor, std::string(type_name)};
}

int register_validated_type(const TypeKey& key, const char* schema_id, const char* compiler_slot_key,
                            qmlsharp_type_registration_callback register_callback) {
    std::lock_guard<std::mutex> lock(registry_mutex);
    if (registered_types.contains(key)) {
        set_last_error("QML type registration already exists for " + make_key_display(key) + '.');
        return QmlSharpTypeRegistrationFailure;
    }

    const int qt_type_id =
        register_callback(std::get<0>(key).c_str(), std::get<1>(key), std::get<2>(key), std::get<3>(key).c_str());
    if (qt_type_id < 0) {
        set_last_error("Generated registration callback failed for " + make_key_display(key) + '.');
        return QmlSharpTypeRegistrationFailure;
    }

    RegisteredTypeMetadata metadata;
    metadata.module_uri = std::get<0>(key);
    metadata.version_major = std::get<1>(key);
    metadata.version_minor = std::get<2>(key);
    metadata.type_name = std::get<3>(key);
    metadata.schema_id = schema_id;
    metadata.compiler_slot_key = compiler_slot_key;
    metadata.qt_type_id = qt_type_id;

    registered_types.emplace(key, std::move(metadata));
    return QmlSharpSuccess;
}
}  // namespace

int register_type(void* engine, const char* module_uri, int version_major, int version_minor, const char* type_name,
                  const char* schema_id, const char* compiler_slot_key,
                  qmlsharp_type_registration_callback register_callback) noexcept {
    try {
        constexpr const char* operation = "qmlsharp_register_type";
        if (!validate_common(engine, module_uri, version_major, version_minor, operation)) {
            return is_blank(module_uri) || version_major < 0 || version_minor < 0 ? QmlSharpInvalidArgument
                                                                                  : QmlSharpEngineNotInitialized;
        }

        if (!validate_type_entry(type_name, schema_id, compiler_slot_key, register_callback, operation)) {
            return QmlSharpInvalidArgument;
        }

        const int result = register_validated_type(make_type_key(module_uri, version_major, version_minor, type_name),
                                                   schema_id, compiler_slot_key, register_callback);
        if (result == QmlSharpSuccess) {
            clear_last_error();
        }

        return result;
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_register_type failed: ") + error.what());
        return QmlSharpTypeRegistrationFailure;
    } catch (...) {
        set_last_error("qmlsharp_register_type failed due to an unknown native exception.");
        return QmlSharpTypeRegistrationFailure;
    }
}

int register_module(void* engine, const char* module_uri, int version_major, int version_minor,
                    const qmlsharp_type_registration_entry* entries, int entry_count) noexcept {
    try {
        constexpr const char* operation = "qmlsharp_register_module";
        if (!validate_common(engine, module_uri, version_major, version_minor, operation)) {
            return is_blank(module_uri) || version_major < 0 || version_minor < 0 ? QmlSharpInvalidArgument
                                                                                  : QmlSharpEngineNotInitialized;
        }

        if (entry_count < 0) {
            set_last_error("qmlsharp_register_module requires a non-negative entry count.");
            return QmlSharpInvalidArgument;
        }

        if (entry_count > 0 && entries == nullptr) {
            set_last_error("qmlsharp_register_module requires entries when entry count is greater than zero.");
            return QmlSharpInvalidArgument;
        }

        if (entry_count == 0) {
            clear_last_error();
            return QmlSharpSuccess;
        }

        std::set<TypeKey> module_keys;
        for (int index = 0; index < entry_count; ++index) {
            const qmlsharp_type_registration_entry& entry = entries[index];
            if (!validate_type_entry(entry.type_name, entry.schema_id, entry.compiler_slot_key, entry.register_callback,
                                     operation)) {
                return QmlSharpInvalidArgument;
            }

            TypeKey key = make_type_key(module_uri, version_major, version_minor, entry.type_name);
            if (!module_keys.insert(key).second) {
                set_last_error("qmlsharp_register_module received duplicate entry " + make_key_display(key) + '.');
                return QmlSharpTypeRegistrationFailure;
            }
        }

        {
            std::lock_guard<std::mutex> lock(registry_mutex);
            for (const TypeKey& key : module_keys) {
                if (registered_types.contains(key)) {
                    set_last_error("QML type registration already exists for " + make_key_display(key) + '.');
                    return QmlSharpTypeRegistrationFailure;
                }
            }
        }

        for (int index = 0; index < entry_count; ++index) {
            const qmlsharp_type_registration_entry& entry = entries[index];
            const int result =
                register_validated_type(make_type_key(module_uri, version_major, version_minor, entry.type_name),
                                        entry.schema_id, entry.compiler_slot_key, entry.register_callback);
            if (result != QmlSharpSuccess) {
                return result;
            }
        }

        clear_last_error();
        return QmlSharpSuccess;
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_register_module failed: ") + error.what());
        return QmlSharpTypeRegistrationFailure;
    } catch (...) {
        set_last_error("qmlsharp_register_module failed due to an unknown native exception.");
        return QmlSharpTypeRegistrationFailure;
    }
}

const RegisteredTypeMetadata* find_registered_type(const std::string& module_uri, int version_major, int version_minor,
                                                   const std::string& type_name) {
    std::lock_guard<std::mutex> lock(registry_mutex);
    const TypeKey key{module_uri, version_major, version_minor, type_name};
    const auto iterator = registered_types.find(key);
    if (iterator == registered_types.end()) {
        return nullptr;
    }

    return &iterator->second;
}
}  // namespace qmlsharp
