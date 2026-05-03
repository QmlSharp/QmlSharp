#pragma once

#include <QObject>
#include <QString>

// Generated-test-only QObject fixture for native registration tests.
class RegistrationCounterViewModel : public QObject {
    Q_OBJECT
    Q_PROPERTY(QString instanceId READ instanceId CONSTANT)
    Q_PROPERTY(QString compilerSlotKey READ compilerSlotKey CONSTANT)
    Q_PROPERTY(int count READ count WRITE setCount NOTIFY countChanged)

public:
    explicit RegistrationCounterViewModel(QObject* parent = nullptr);

    QString instanceId() const;
    QString compilerSlotKey() const;
    int count() const;
    void setCount(int value);

    Q_INVOKABLE void increment();

signals:
    void countChanged();

private:
    QString instance_id_;
    QString compiler_slot_key_;
    int count_ = 0;
};
