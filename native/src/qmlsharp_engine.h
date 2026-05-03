#pragma once

#include <QObject>

namespace qmlsharp {
// Thread affinity: Qt main thread only. The behavior surface is added in later 07-native-host steps.
class QmlSharpEngine final : public QObject {
    Q_OBJECT

public:
    explicit QmlSharpEngine(QObject* parent = nullptr);
};
}  // namespace qmlsharp
