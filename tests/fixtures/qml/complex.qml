import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

Rectangle {
    id: root
    width: 320
    height: 200
    color: "#20242a"

    property string title: "Complex"
    signal accepted(string value)

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 12

        Text {
            text: root.title
            color: "white"
            Layout.fillWidth: true
        }

        Repeater {
            model: 3

            delegate: Button {
                text: "Choice " + (index + 1)
                onClicked: root.accepted(text)
            }
        }
    }
}
