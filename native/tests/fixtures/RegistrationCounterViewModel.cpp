#include "RegistrationCounterViewModel.h"

#include <QUuid>

RegistrationCounterViewModel::RegistrationCounterViewModel(QObject* parent)
    : QObject(parent),
      instance_id_(QUuid::createUuid().toString(QUuid::WithoutBraces)),
      compiler_slot_key_(QStringLiteral("RegistrationView::__qmlsharp_vm0")) {}

QString RegistrationCounterViewModel::instanceId() const {
    return instance_id_;
}

QString RegistrationCounterViewModel::compilerSlotKey() const {
    return compiler_slot_key_;
}

int RegistrationCounterViewModel::count() const {
    return count_;
}

void RegistrationCounterViewModel::setCount(int value) {
    if (count_ == value) {
        return;
    }

    count_ = value;
    emit countChanged();
}

void RegistrationCounterViewModel::increment() {
    setCount(count_ + 1);
}
