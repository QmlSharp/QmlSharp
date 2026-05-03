#pragma once

#include "qmlsharp/qmlsharp_abi.h"

#include <QObject>
#include <QPointer>
#include <QString>
#include <string>
#include <vector>

namespace qmlsharp {
struct NativeInstanceRecordSnapshot {
    std::string instance_id;
    std::string class_name;
    std::string compiler_slot_key;
    bool ready = false;
    QPointer<QObject> object;
};

void set_instance_callbacks(qmlsharp_instance_created_cb on_created,
                            qmlsharp_instance_destroyed_cb on_destroyed) noexcept;
void instance_ready(const char* instance_id) noexcept;
void set_command_callback(qmlsharp_command_cb callback) noexcept;

void notify_instance_created(QObject* object, const QString& instance_id, const QString& class_name,
                             const QString& compiler_slot_key) noexcept;
void notify_instance_destroyed(const QString& instance_id) noexcept;
void dispatch_command(const QString& instance_id, const QString& command_name, const QString& args_json) noexcept;

QObject* find_instance_object(const char* instance_id) noexcept;
std::vector<QPointer<QObject>> find_instance_objects_by_class(const char* class_name) noexcept;
std::vector<NativeInstanceRecordSnapshot> snapshot_instances() noexcept;
std::string find_instance_id_by_class_slot(const char* class_name, const char* compiler_slot_key) noexcept;
int active_instance_count() noexcept;
}  // namespace qmlsharp
