import QtQuick
import QtQuick.Controls

Item {
    id: root
    property int count: 0
    signal clicked()
    width: 100
    onClicked: doStuff()
    function calc() {
        return 42
    }
    Rectangle {
        color: "red"
    }
    component Badge: Text {
        text: "!"
    }
    enum Direction {
        Left,
        Right
    }
}
