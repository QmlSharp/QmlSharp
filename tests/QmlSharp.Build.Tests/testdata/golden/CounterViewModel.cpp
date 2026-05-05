// GENERATED — DO NOT EDIT
#include "CounterViewModel.h"

#include <QByteArray>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonParseError>
#include <QJsonValue>
#include <QUuid>
#include <QtGlobal>

#include "qmlsharp_instances.h"

CounterViewModel::CounterViewModel(QObject* parent)
    : QObject(parent),
      m_instanceId(QUuid::createUuid().toString(QUuid::WithoutBraces)),
      m_compilerSlotKey(QStringLiteral("CounterView::__qmlsharp_vm0")),
      m_count(0) {
    qmlsharp::notify_instance_created(this, m_instanceId, QStringLiteral("CounterViewModel"), m_compilerSlotKey);
}

CounterViewModel::~CounterViewModel() {
    qmlsharp::notify_instance_destroyed(m_instanceId);
}

QString CounterViewModel::instanceId() const {
    return m_instanceId;
}

QString CounterViewModel::compilerSlotKey() const {
    return m_compilerSlotKey;
}

int CounterViewModel::count() const {
    return m_count;
}

void CounterViewModel::setCount(int value) {
    if (m_count == value) {
        return;
    }

    m_count = value;
    emit countChanged();
}

void CounterViewModel::increment() {
    qmlsharp::dispatch_command(m_instanceId, QStringLiteral("increment"), QStringLiteral("[]"));
}

void CounterViewModel::setPropertyFromManaged(const char* propertyName, const char* jsonValue) {
    if (propertyName == nullptr || jsonValue == nullptr) {
        return;
    }

    QJsonParseError parseError;
    const QJsonDocument document = QJsonDocument::fromJson(QByteArray(jsonValue), &parseError);
    if (parseError.error != QJsonParseError::NoError || document.isNull()) {
        return;
    }

    const QVariant value = document.toVariant();
    if (qstrcmp(propertyName, "count") == 0) {
        setCount(value.toInt());
        return;
    }
}

void CounterViewModel::setPropertyFromManagedInt(const char* propertyName, int value) {
    if (propertyName == nullptr) {
        return;
    }

    if (qstrcmp(propertyName, "count") == 0) {
        setCount(value);
        return;
    }
}

void CounterViewModel::setPropertyFromManagedDouble(const char* propertyName, double value) {
    if (propertyName == nullptr) {
        return;
    }

    Q_UNUSED(value);
}

void CounterViewModel::setPropertyFromManagedBool(const char* propertyName, bool value) {
    if (propertyName == nullptr) {
        return;
    }

    Q_UNUSED(value);
}

void CounterViewModel::setPropertyFromManagedString(const char* propertyName, const char* value) {
    if (propertyName == nullptr) {
        return;
    }

    const QString qmlsharpValue = QString::fromUtf8(value == nullptr ? "" : value);
    Q_UNUSED(qmlsharpValue);
}

void CounterViewModel::emitEffectDispatched(const QString& effectName, const QString& payloadJson) {
    emit effectDispatched(effectName, payloadJson);
}
