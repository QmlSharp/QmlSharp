import QtQuick
import QmlSharp.Integration.Tests 1.0

Item {
    RegistrationCounterViewModel {
        id: __qmlsharp_vm0
    }

    Timer {
        interval: 0
        running: true
        repeat: false
        onTriggered: Qt.quit()
    }
}
