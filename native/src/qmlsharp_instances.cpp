#include "qmlsharp_instances.h"

#include <QByteArray>
#include <QPointer>
#include <exception>
#include <map>
#include <mutex>
#include <string>
#include <utility>
#include <vector>

#include "qmlsharp_errors.h"

namespace qmlsharp {
namespace {
struct QueuedCommand {
    std::string command_name;
    std::string args_json;
};

struct InstanceRecord {
    QPointer<QObject> object;
    std::string class_name;
    std::string compiler_slot_key;
    bool ready = false;
    std::vector<QueuedCommand> queued_commands;
};

std::mutex instance_mutex;
std::map<std::string, InstanceRecord> instances;
qmlsharp_instance_created_cb instance_created_callback = nullptr;
qmlsharp_instance_destroyed_cb instance_destroyed_callback = nullptr;
qmlsharp_command_cb command_callback = nullptr;

bool is_blank(const char* value) noexcept {
    if (value == nullptr) {
        return true;
    }

    while (*value != '\0') {
        if (*value != ' ' && *value != '\t' && *value != '\r' && *value != '\n') {
            return false;
        }

        ++value;
    }

    return true;
}

std::string to_utf8_string(const QString& value) {
    const QByteArray utf8 = value.toUtf8();
    return std::string(utf8.constData(), static_cast<std::size_t>(utf8.size()));
}

void invoke_instance_created(qmlsharp_instance_created_cb callback, const std::string& instance_id,
                             const std::string& class_name, const std::string& compiler_slot_key) noexcept {
    if (callback == nullptr) {
        clear_last_error();
        return;
    }

    try {
        callback(instance_id.c_str(), class_name.c_str(), compiler_slot_key.c_str());
        clear_last_error();
    } catch (const std::exception& error) {
        set_last_error(std::string("Instance created callback failed: ") + error.what());
    } catch (...) {
        set_last_error("Instance created callback failed due to an unknown native exception.");
    }
}

void invoke_instance_destroyed(qmlsharp_instance_destroyed_cb callback, const std::string& instance_id) noexcept {
    if (callback == nullptr) {
        clear_last_error();
        return;
    }

    try {
        callback(instance_id.c_str());
        clear_last_error();
    } catch (const std::exception& error) {
        set_last_error(std::string("Instance destroyed callback failed: ") + error.what());
    } catch (...) {
        set_last_error("Instance destroyed callback failed due to an unknown native exception.");
    }
}

bool invoke_command(qmlsharp_command_cb callback, const std::string& instance_id, const std::string& command_name,
                    const std::string& args_json) noexcept {
    if (callback == nullptr) {
        clear_last_error();
        return true;
    }

    try {
        callback(instance_id.c_str(), command_name.c_str(), args_json.c_str());
        clear_last_error();
        return true;
    } catch (const std::exception& error) {
        set_last_error(std::string("Command callback failed for '") + command_name + "': " + error.what());
        return false;
    } catch (...) {
        set_last_error(std::string("Command callback failed for '") + command_name +
                       "' due to an unknown native exception.");
        return false;
    }
}
}  // namespace

void set_instance_callbacks(qmlsharp_instance_created_cb on_created,
                            qmlsharp_instance_destroyed_cb on_destroyed) noexcept {
    std::lock_guard<std::mutex> lock(instance_mutex);
    instance_created_callback = on_created;
    instance_destroyed_callback = on_destroyed;
    clear_last_error();
}

void set_command_callback(qmlsharp_command_cb callback) noexcept {
    std::lock_guard<std::mutex> lock(instance_mutex);
    command_callback = callback;
    clear_last_error();
}

void notify_instance_created(QObject* object, const QString& instance_id, const QString& class_name,
                             const QString& compiler_slot_key) noexcept {
    try {
        const std::string instance_id_utf8 = to_utf8_string(instance_id);
        const std::string class_name_utf8 = to_utf8_string(class_name);
        const std::string compiler_slot_key_utf8 = to_utf8_string(compiler_slot_key);
        qmlsharp_instance_created_cb callback = nullptr;

        if (instance_id_utf8.empty()) {
            set_last_error("Generated QObject instance creation requires a non-empty instanceId.");
            return;
        }

        {
            std::lock_guard<std::mutex> lock(instance_mutex);
            instances[instance_id_utf8] = InstanceRecord{
                object, class_name_utf8, compiler_slot_key_utf8, false, {},
            };
            callback = instance_created_callback;
        }

        invoke_instance_created(callback, instance_id_utf8, class_name_utf8, compiler_slot_key_utf8);
    } catch (const std::exception& error) {
        set_last_error(std::string("Instance creation notification failed: ") + error.what());
    } catch (...) {
        set_last_error("Instance creation notification failed due to an unknown native exception.");
    }
}

void notify_instance_destroyed(const QString& instance_id) noexcept {
    try {
        const std::string instance_id_utf8 = to_utf8_string(instance_id);
        qmlsharp_instance_destroyed_cb callback = nullptr;
        bool found = false;

        if (instance_id_utf8.empty()) {
            set_last_error("Generated QObject instance destruction requires a non-empty instanceId.");
            return;
        }

        {
            std::lock_guard<std::mutex> lock(instance_mutex);
            const auto existing = instances.find(instance_id_utf8);
            if (existing != instances.end()) {
                instances.erase(existing);
                callback = instance_destroyed_callback;
                found = true;
            }
        }

        if (!found) {
            clear_last_error();
            return;
        }

        invoke_instance_destroyed(callback, instance_id_utf8);
    } catch (const std::exception& error) {
        set_last_error(std::string("Instance destruction notification failed: ") + error.what());
    } catch (...) {
        set_last_error("Instance destruction notification failed due to an unknown native exception.");
    }
}

void instance_ready(const char* instance_id) noexcept {
    try {
        if (is_blank(instance_id)) {
            set_last_error("qmlsharp_instance_ready requires a non-empty instanceId.");
            return;
        }

        std::vector<QueuedCommand> queued_commands;
        qmlsharp_command_cb callback = nullptr;
        const std::string instance_id_utf8(instance_id);

        {
            std::lock_guard<std::mutex> lock(instance_mutex);
            const auto existing = instances.find(instance_id_utf8);
            if (existing == instances.end()) {
                clear_last_error();
                return;
            }

            InstanceRecord& record = existing->second;
            if (record.ready) {
                clear_last_error();
                return;
            }

            record.ready = true;
            queued_commands.swap(record.queued_commands);
            callback = command_callback;
        }

        bool all_dispatched = true;
        std::string first_failure;
        for (const QueuedCommand& queued_command : queued_commands) {
            if (!invoke_command(callback, instance_id_utf8, queued_command.command_name, queued_command.args_json)) {
                all_dispatched = false;
                if (first_failure.empty()) {
                    first_failure = last_error();
                }
            }
        }

        if (all_dispatched) {
            clear_last_error();
        } else if (!first_failure.empty()) {
            set_last_error(first_failure);
        }
    } catch (const std::exception& error) {
        set_last_error(std::string("qmlsharp_instance_ready failed: ") + error.what());
    } catch (...) {
        set_last_error("qmlsharp_instance_ready failed due to an unknown native exception.");
    }
}

void dispatch_command(const QString& instance_id, const QString& command_name, const QString& args_json) noexcept {
    try {
        const std::string instance_id_utf8 = to_utf8_string(instance_id);
        const std::string command_name_utf8 = to_utf8_string(command_name);
        std::string args_json_utf8 = to_utf8_string(args_json);
        qmlsharp_command_cb callback = nullptr;
        bool should_dispatch_now = false;

        if (args_json_utf8.empty()) {
            args_json_utf8 = "[]";
        }

        if (instance_id_utf8.empty() || command_name_utf8.empty()) {
            set_last_error("Generated command dispatch requires non-empty instanceId and commandName.");
            return;
        }

        {
            std::lock_guard<std::mutex> lock(instance_mutex);
            const auto existing = instances.find(instance_id_utf8);
            if (existing == instances.end()) {
                clear_last_error();
                return;
            }

            InstanceRecord& record = existing->second;
            if (!record.ready) {
                record.queued_commands.push_back(QueuedCommand{command_name_utf8, args_json_utf8});
                clear_last_error();
                return;
            }

            callback = command_callback;
            should_dispatch_now = true;
        }

        if (should_dispatch_now) {
            invoke_command(callback, instance_id_utf8, command_name_utf8, args_json_utf8);
        }
    } catch (const std::exception& error) {
        set_last_error(std::string("Command dispatch failed: ") + error.what());
    } catch (...) {
        set_last_error("Command dispatch failed due to an unknown native exception.");
    }
}

QObject* find_instance_object(const char* instance_id) noexcept {
    if (is_blank(instance_id)) {
        return nullptr;
    }

    std::lock_guard<std::mutex> lock(instance_mutex);
    const auto existing = instances.find(instance_id);
    if (existing == instances.end()) {
        return nullptr;
    }

    return existing->second.object.data();
}

std::vector<QPointer<QObject>> find_instance_objects_by_class(const char* class_name) noexcept {
    std::vector<QPointer<QObject>> matches;
    if (is_blank(class_name)) {
        return matches;
    }

    std::lock_guard<std::mutex> lock(instance_mutex);
    for (const auto& [unused_instance_id, record] : instances) {
        Q_UNUSED(unused_instance_id);
        if (record.class_name == class_name && !record.object.isNull()) {
            matches.push_back(record.object);
        }
    }

    return matches;
}

int active_instance_count() noexcept {
    std::lock_guard<std::mutex> lock(instance_mutex);
    return static_cast<int>(instances.size());
}
}  // namespace qmlsharp
