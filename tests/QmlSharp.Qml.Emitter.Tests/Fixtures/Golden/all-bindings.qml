import QtQuick

Item {
    width: 400
    opacity: 0.5
    x: -10
    text: "hello"
    visible: true
    enabled: false
    model: null
    fillMode: Image.Stretch
    color: enabled ? "blue" : "gray"
    onCompleted: {
        count++
        ready = true
    }
    font: Font { pixelSize: 14 }
    metadata: ["idle", 1, root.width]
    anchors {
        left: parent.left
        right: parent.right
    }
    Layout.fillWidth: true
    states: [
        State { name: "idle" },
        State { name: "active" }
    ]
    Behavior on opacity {
        NumberAnimation {
            duration: 200
        }
    }
}
