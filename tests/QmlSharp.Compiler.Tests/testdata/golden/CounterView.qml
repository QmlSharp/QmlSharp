import QmlSharp.TestApp 1.0
import QtQml 1.0

Column {
    CounterViewModel {
        id: __qmlsharp_vm0
    }

    Text {
        text: __qmlsharp_vm0.count.toString()
    }

    Row {
        Button {
            text: "+"
            onClicked: {
                __qmlsharp_vm0.increment()
            }
        }

        Button {
            text: "-"
            onClicked: {
                __qmlsharp_vm0.decrement()
            }
        }
    }
    Component.onCompleted: { __qmlsharp_vm0.onMounted(); }
    Component.onDestruction: { __qmlsharp_vm0.onUnmounting(); }
}
