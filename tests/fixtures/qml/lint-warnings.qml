import QtQuick
import QtQuick.Controls

Item {
    property int unusedValue: 1 + 2

    Text {
        text: parent.implicitWidth
    }
}
