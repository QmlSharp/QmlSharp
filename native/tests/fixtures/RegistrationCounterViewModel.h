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
    ~RegistrationCounterViewModel() override;

    QString instanceId() const;
    QString compilerSlotKey() const;
    int count() const;
    void setCount(int value);

    Q_INVOKABLE void increment();
    Q_INVOKABLE void reset(int value);
    Q_INVOKABLE void commandNoArgs();
    Q_INVOKABLE void commandInt(int value);
    Q_INVOKABLE void commandString(const QString& value);
    Q_INVOKABLE void commandMixed(int number, const QString& text, bool enabled);

signals:
    void countChanged();

private:
    QString instance_id_;
    QString compiler_slot_key_;
    int count_ = 0;
};
