import QmlSharp.TestApp 1.0
import QtQml 1.0

Column {
    TodoViewModel {
        id: __qmlsharp_vm0
    }

    Text {
        text: __qmlsharp_vm0.title
    }

    Text {
        text: __qmlsharp_vm0.itemCount.toString()
    }

    Text {
        text: __qmlsharp_vm0.items.length.toString()
    }

    Row {
        Button {
            text: "Add"
            onClicked: {
                __qmlsharp_vm0.addItem()
            }
        }

        Button {
            text: "Remove"
            onClicked: {
                __qmlsharp_vm0.removeItem(0)
            }
        }
    }

    Connections {
        target: __qmlsharp_vm0
        function onEffectDispatched(effectName: string, payloadJson: string) {
            __qmlsharp_effect_router
            switch (effectName) {
                    case "showToast":
                        break;
            }
        }
    }
}
