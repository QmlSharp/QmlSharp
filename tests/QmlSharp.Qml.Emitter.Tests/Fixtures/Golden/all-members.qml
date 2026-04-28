import QtQuick

Item {
    id: root
    // property and signal members
    property int count: 0
    property string name
    readonly property real area: w * h
    required property string title
    default property Item content
    property alias text: label.text
    signal clicked()
    signal moved(x: int, y: int)
    onClicked: doStuff()
    onMoved: {
        console.log("moved")
    }
    onPressed: (event) => { handle(event) }
    function calculate(a: int, b: int): int {
        return a + b
    }

    function reset() {
        count = 0
    }
    width: 100
    height: 200
    Rectangle {
        color: "red"
    }
    component Badge: Text {
        text: "!"
    }
    enum Direction {
        Up,
        Down,
        Left,
        Right
    }
}
