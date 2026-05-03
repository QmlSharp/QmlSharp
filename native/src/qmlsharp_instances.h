#pragma once

#include "qmlsharp/qmlsharp_abi.h"

#include <QObject>
#include <QString>

namespace qmlsharp {
void set_instance_callbacks(qmlsharp_instance_created_cb on_created,
                            qmlsharp_instance_destroyed_cb on_destroyed) noexcept;
void instance_ready(const char* instance_id) noexcept;
void set_command_callback(qmlsharp_command_cb callback) noexcept;

void notify_instance_created(QObject* object, const QString& instance_id, const QString& class_name,
                             const QString& compiler_slot_key) noexcept;
void notify_instance_destroyed(const QString& instance_id) noexcept;
void dispatch_command(const QString& instance_id, const QString& command_name, const QString& args_json) noexcept;

QObject* find_instance_object(const char* instance_id) noexcept;
int active_instance_count() noexcept;
}  // namespace qmlsharp
