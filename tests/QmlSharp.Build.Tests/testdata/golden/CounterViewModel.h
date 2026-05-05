// GENERATED — DO NOT EDIT
#pragma once

#include "qmlsharp/qmlsharp_abi.h"

#include <qqml.h>

#include <QColor>
#include <QDate>
#include <QObject>
#include <QPointF>
#include <QRectF>
#include <QSizeF>
#include <QString>
#include <QUrl>
#include <QVariant>
#include <QVariantList>

class CounterViewModel : public QObject {
    Q_OBJECT
    QML_ELEMENT

    Q_PROPERTY(QString instanceId READ instanceId CONSTANT)
    Q_PROPERTY(QString compilerSlotKey READ compilerSlotKey CONSTANT)
    Q_PROPERTY(int count READ count WRITE setCount NOTIFY countChanged)

public:
    explicit CounterViewModel(QObject* parent = nullptr);
    ~CounterViewModel() override;

    QString instanceId() const;
    QString compilerSlotKey() const;
    int count() const;
    void setCount(int value);

    void setPropertyFromManaged(const char* propertyName, const char* jsonValue);
    void setPropertyFromManagedInt(const char* propertyName, int value);
    void setPropertyFromManagedDouble(const char* propertyName, double value);
    void setPropertyFromManagedBool(const char* propertyName, bool value);
    void setPropertyFromManagedString(const char* propertyName, const char* value);

    Q_INVOKABLE void increment();

    Q_INVOKABLE void emitEffectDispatched(const QString& effectName, const QString& payloadJson);

signals:
    void countChanged();
    void effectDispatched(const QString& effectName, const QString& payloadJson);

private:
    QString m_instanceId;
    QString m_compilerSlotKey;
    int m_count = 0;
};
