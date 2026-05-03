#pragma once

#include "qmlsharp/qmlsharp_abi.h"

#include <QObject>
#include <QString>

namespace qmlsharp {
QMLSHARP_API void set_instance_callbacks(qmlsharp_instance_created_cb on_created,
                                         qmlsharp_instance_destroyed_cb on_destroyed) noexcept;
QMLSHARP_API void instance_ready(const char* instance_id) noexcept;
QMLSHARP_API void set_command_callback(qmlsharp_command_cb callback) noexcept;

QMLSHARP_API void notify_instance_created(QObject* object, const QString& instance_id, const QString& class_name,
                                          const QString& compiler_slot_key) noexcept;
QMLSHARP_API void notify_instance_destroyed(const QString& instance_id) noexcept;
QMLSHARP_API void dispatch_command(const QString& instance_id, const QString& command_name,
                                   const QString& args_json) noexcept;

QMLSHARP_API QObject* find_instance_object(const char* instance_id) noexcept;
QMLSHARP_API int active_instance_count() noexcept;
}  // namespace qmlsharp
