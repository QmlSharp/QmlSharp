#include "RegistrationCounterViewModel.h"

#include <QJsonArray>
#include <QJsonDocument>
#include <QThread>
#include <QUuid>
#include <QtGlobal>

#include "qmlsharp_instances.h"

namespace {
QString json_args(const QJsonArray& values) {
    return QString::fromUtf8(QJsonDocument(values).toJson(QJsonDocument::Compact));
}
}  // namespace

RegistrationCounterViewModel::RegistrationCounterViewModel(QObject* parent)
    : QObject(parent),
      instance_id_(QUuid::createUuid().toString(QUuid::WithoutBraces)),
      compiler_slot_key_(QStringLiteral("RegistrationView::__qmlsharp_vm0")) {
    qmlsharp::notify_instance_created(this, instance_id_, QStringLiteral("RegistrationCounterViewModel"),
                                      compiler_slot_key_);
}

RegistrationCounterViewModel::~RegistrationCounterViewModel() {
    qmlsharp::notify_instance_destroyed(instance_id_);
}

QString RegistrationCounterViewModel::instanceId() const {
    return instance_id_;
}

QString RegistrationCounterViewModel::compilerSlotKey() const {
    return compiler_slot_key_;
}

int RegistrationCounterViewModel::count() const {
    count_read_on_owner_thread_ = QThread::currentThread() == thread();
    return count_;
}

void RegistrationCounterViewModel::setCount(int value) {
    if (count_ == value) {
        return;
    }

    count_ = value;
    emit countChanged();
}

double RegistrationCounterViewModel::ratio() const {
    return ratio_;
}

void RegistrationCounterViewModel::setRatio(double value) {
    if (qFuzzyCompare(ratio_, value)) {
        return;
    }

    ratio_ = value;
    emit ratioChanged();
}

bool RegistrationCounterViewModel::enabled() const {
    return enabled_;
}

void RegistrationCounterViewModel::setEnabled(bool value) {
    if (enabled_ == value) {
        return;
    }

    enabled_ = value;
    emit enabledChanged();
}

QString RegistrationCounterViewModel::title() const {
    return title_;
}

void RegistrationCounterViewModel::setTitle(const QString& value) {
    if (title_ == value) {
        return;
    }

    title_ = value;
    emit titleChanged();
}

QVariant RegistrationCounterViewModel::metadata() const {
    return metadata_;
}

void RegistrationCounterViewModel::setMetadata(const QVariant& value) {
    if (metadata_ == value) {
        return;
    }

    metadata_ = value;
    emit metadataChanged();
}

int RegistrationCounterViewModel::extra0() const {
    return extra0_;
}

void RegistrationCounterViewModel::setExtra0(int value) {
    if (extra0_ == value) {
        return;
    }

    extra0_ = value;
    emit extra0Changed();
}

int RegistrationCounterViewModel::extra1() const {
    return extra1_;
}

void RegistrationCounterViewModel::setExtra1(int value) {
    if (extra1_ == value) {
        return;
    }

    extra1_ = value;
    emit extra1Changed();
}

int RegistrationCounterViewModel::extra2() const {
    return extra2_;
}

void RegistrationCounterViewModel::setExtra2(int value) {
    if (extra2_ == value) {
        return;
    }

    extra2_ = value;
    emit extra2Changed();
}

int RegistrationCounterViewModel::extra3() const {
    return extra3_;
}

void RegistrationCounterViewModel::setExtra3(int value) {
    if (extra3_ == value) {
        return;
    }

    extra3_ = value;
    emit extra3Changed();
}

int RegistrationCounterViewModel::extra4() const {
    return extra4_;
}

void RegistrationCounterViewModel::setExtra4(int value) {
    if (extra4_ == value) {
        return;
    }

    extra4_ = value;
    emit extra4Changed();
}

bool RegistrationCounterViewModel::wasCountReadOnOwnerThread() const {
    return count_read_on_owner_thread_;
}

void RegistrationCounterViewModel::resetCountReadProbe() {
    count_read_on_owner_thread_ = false;
}

void RegistrationCounterViewModel::increment() {
    setCount(count_ + 1);
    qmlsharp::dispatch_command(instance_id_, QStringLiteral("increment"), QStringLiteral("[]"));
}

void RegistrationCounterViewModel::reset(int value) {
    setCount(value);
    qmlsharp::dispatch_command(instance_id_, QStringLiteral("reset"), json_args(QJsonArray{value}));
}

void RegistrationCounterViewModel::commandNoArgs() {
    qmlsharp::dispatch_command(instance_id_, QStringLiteral("commandNoArgs"), QStringLiteral("[]"));
}

void RegistrationCounterViewModel::commandInt(int value) {
    qmlsharp::dispatch_command(instance_id_, QStringLiteral("commandInt"), json_args(QJsonArray{value}));
}

void RegistrationCounterViewModel::commandString(const QString& value) {
    qmlsharp::dispatch_command(instance_id_, QStringLiteral("commandString"), json_args(QJsonArray{value}));
}

void RegistrationCounterViewModel::commandMixed(int number, const QString& text, bool enabled) {
    qmlsharp::dispatch_command(instance_id_, QStringLiteral("commandMixed"),
                               json_args(QJsonArray{number, text, enabled}));
}

void RegistrationCounterViewModel::emitEffectDispatched(const QString& effectName, const QString& payloadJson) {
    emit effectDispatched(effectName, payloadJson);
}
