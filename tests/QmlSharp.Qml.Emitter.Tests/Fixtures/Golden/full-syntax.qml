pragma Singleton
pragma ComponentBehavior: Bound
pragma NativeTextRendering

import QtQuick 6.0
import QtQuick.Controls
import "../components"
import "logic.js" as Logic

ApplicationWindow {
    id: root
    // QmlSharp full-syntax golden fixture
    property int count: 0
    property string title: "My App"
    property list<Item> items
    property alias mainContent: contentArea.data
    signal activated(x: int, y: int)
    onActivated: (x, y) => { console.log(x, y) }
    width: 800
    height: 600
    visible: true
    opacity: enabled ? 1.0 : 0.5
    anchors {
        fill: parent
    }
    Layout.fillWidth: true
    Layout.preferredHeight: 100
    states: [
        State { name: "idle" },
        State { name: "active" }
    ]
    Behavior on opacity {
        NumberAnimation {
            duration: 200
        }
    }
    function doStuff(x: int): bool {
        return x > 0
    }
    Rectangle {
        id: contentArea
        color: "white"
        Text {
            text: "Hello"
            font {
                pixelSize: 24
            }
        }
    }
    component MyButton: Button {
        text: "Click"
    }
    enum Status {
        Idle = 0,
        Loading = 1,
        Done = 2
    }
}
