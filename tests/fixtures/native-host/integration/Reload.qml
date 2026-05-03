import QtQuick
import QmlSharp.Integration.Tests 1.0

Item {
    id: root
    objectName: "roundTripRootReloaded"
    width: 320
    height: 240

    RegistrationCounterViewModel {
        id: __qmlsharp_vm0
        objectName: "counterVmReloaded"
        title: "reloaded"
        onCountChanged: title = "count:" + count
        onEffectDispatched: function(effectName, payloadJson) {
            title = effectName + ":" + payloadJson
        }
    }
}
