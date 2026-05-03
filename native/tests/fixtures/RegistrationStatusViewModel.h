#pragma once

#include <QObject>
#include <QString>

// Generated-test-only QObject fixture for native module registration tests.
class RegistrationStatusViewModel : public QObject {
    Q_OBJECT
    Q_PROPERTY(QString instanceId READ instanceId CONSTANT)
    Q_PROPERTY(QString compilerSlotKey READ compilerSlotKey CONSTANT)
    Q_PROPERTY(QString status READ status WRITE setStatus NOTIFY statusChanged)

public:
    explicit RegistrationStatusViewModel(QObject* parent = nullptr);
    ~RegistrationStatusViewModel() override;

    QString instanceId() const;
    QString compilerSlotKey() const;
    QString status() const;
    void setStatus(const QString& value);

    Q_INVOKABLE void markReady();

signals:
    void statusChanged();

private:
    QString instance_id_;
    QString compiler_slot_key_;
    QString status_ = QStringLiteral("pending");
};
