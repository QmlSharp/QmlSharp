#pragma once

#include <QObject>
#include <QString>
#include <QVariant>

// Generated-test-only QObject fixture for native registration tests.
class RegistrationCounterViewModel : public QObject {
    Q_OBJECT
    Q_PROPERTY(QString instanceId READ instanceId CONSTANT)
    Q_PROPERTY(QString compilerSlotKey READ compilerSlotKey CONSTANT)
    Q_PROPERTY(int count READ count WRITE setCount NOTIFY countChanged)
    Q_PROPERTY(double ratio READ ratio WRITE setRatio NOTIFY ratioChanged)
    Q_PROPERTY(bool enabled READ enabled WRITE setEnabled NOTIFY enabledChanged)
    Q_PROPERTY(QString title READ title WRITE setTitle NOTIFY titleChanged)
    Q_PROPERTY(QVariant metadata READ metadata WRITE setMetadata NOTIFY metadataChanged)

public:
    explicit RegistrationCounterViewModel(QObject* parent = nullptr);
    ~RegistrationCounterViewModel() override;

    QString instanceId() const;
    QString compilerSlotKey() const;
    int count() const;
    void setCount(int value);
    double ratio() const;
    void setRatio(double value);
    bool enabled() const;
    void setEnabled(bool value);
    QString title() const;
    void setTitle(const QString& value);
    QVariant metadata() const;
    void setMetadata(const QVariant& value);

    Q_INVOKABLE void increment();
    Q_INVOKABLE void reset(int value);
    Q_INVOKABLE void commandNoArgs();
    Q_INVOKABLE void commandInt(int value);
    Q_INVOKABLE void commandString(const QString& value);
    Q_INVOKABLE void commandMixed(int number, const QString& text, bool enabled);
    Q_INVOKABLE void emitEffectDispatched(const QString& effectName, const QString& payloadJson);

signals:
    void countChanged();
    void ratioChanged();
    void enabledChanged();
    void titleChanged();
    void metadataChanged();
    void effectDispatched(const QString& effectName, const QString& payloadJson);

private:
    QString instance_id_;
    QString compiler_slot_key_;
    int count_ = 0;
    double ratio_ = 0.0;
    bool enabled_ = false;
    QString title_;
    QVariant metadata_;
};
