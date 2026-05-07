# QmlSharp

**Build Qt/QML applications in C#.**

QmlSharp is a managed framework and toolchain for Qt 6. Views, ViewModels, and
application logic are written in C# through a fluent, QML-style DSL. The
toolchain compiles those declarations to QML, schema contracts, native QObject
glue, and source maps; a thin Qt host loads and runs the generated app.

## A glance

A view, written in C#:

```csharp
public static View Counter(CounterViewModel vm) =>
    Column(
        Text(vm.State(s => s.Count)).FontSize(24),
        Button("Increment").OnClicked(vm.Increment)
    );
```

A ViewModel that exposes state, commands, and effects:

```csharp
[ViewModel]
public partial class CounterViewModel
{
    [State]   public int  Count { get; set; }
    [Command] public void Increment() => Count++;
    [Effect]  public void OnCountChanged(int next) { /* ... */ }
}
```

The QML the compiler emits, loaded by the Qt host:

```qml
ColumnLayout {
    Text   { text: vm.count; font.pixelSize: 24 }
    Button { text: qsTr("Increment"); onClicked: vm.increment() }
}
```

> Examples are conceptual. The public API is still evolving.

## How it works

- **C# is the source of truth.** The fluent DSL describes views; `[State]`,
  `[Command]`, and `[Effect]` describe ViewModels.
- **The compiler emits everything else.** QML files, `.schema.json` runtime
  contracts, native QObject bindings, metadata, and source maps are generated
  artifacts, not hand-written.
- **Interop is flat C ABI + P/Invoke.** The native side is a thin Qt shell
  around `QQmlEngine` and registered types — no COM, no C++/CLI.
- **Qt 6.11 is the runtime baseline.** The toolchain discovers Qt through
  `QT_DIR`.

## Status

QmlSharp is in early development. The compiler, runtime contracts, and public
APIs are still stabilizing and may change between releases.

## Documentation

Full documentation will be published on the QmlSharp GitHub Pages site. Until
then, this README covers the project at a glance.

Documentation: GitHub Pages site coming soon.

## License

[MIT](LICENSE)
