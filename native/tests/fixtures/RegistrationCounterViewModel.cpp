#include "RegistrationCounterViewModel.h"

#include <QJsonArray>
#include <QJsonDocument>
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
