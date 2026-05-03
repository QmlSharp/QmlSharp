#include "RegistrationStatusViewModel.h"

#include <QUuid>

#include "qmlsharp_instances.h"

RegistrationStatusViewModel::RegistrationStatusViewModel(QObject* parent)
    : QObject(parent),
      instance_id_(QUuid::createUuid().toString(QUuid::WithoutBraces)),
      compiler_slot_key_(QStringLiteral("RegistrationStatusView::__qmlsharp_vm0")) {
    qmlsharp::notify_instance_created(this, instance_id_, QStringLiteral("RegistrationStatusViewModel"),
                                      compiler_slot_key_);
}

RegistrationStatusViewModel::~RegistrationStatusViewModel() {
    qmlsharp::notify_instance_destroyed(instance_id_);
}

QString RegistrationStatusViewModel::instanceId() const {
    return instance_id_;
}

QString RegistrationStatusViewModel::compilerSlotKey() const {
    return compiler_slot_key_;
}

QString RegistrationStatusViewModel::status() const {
    return status_;
}

void RegistrationStatusViewModel::setStatus(const QString& value) {
    if (status_ == value) {
        return;
    }

    status_ = value;
    emit statusChanged();
}

void RegistrationStatusViewModel::markReady() {
    setStatus(QStringLiteral("ready"));
}
