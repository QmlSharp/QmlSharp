# QmlSharp

QmlSharp is a C# + C++ framework and toolchain for building Qt/QML applications. The managed
toolchain owns compiler, registry, runtime coordination, build orchestration, and dev tools;
the native side stays a thin Qt/QML shell.

## Step 01.00 bootstrap status

This branch bootstraps the implementation repository foundation and quality gates only. It does
not implement real `01-registry` behavior yet.

## Required local tools

- .NET SDK 10.0.202
- CMake 4.2 or newer
- `pre-commit` (optional but recommended)
- `clang-format` (optional until native formatting hooks become mandatory)

## Bootstrap commands

```powershell
dotnet restore QmlSharp.slnx
dotnet build QmlSharp.slnx
dotnet test QmlSharp.slnx
cmake --preset windows-debug
cmake --build --preset debug
$env:MSBUILDDISABLENODEREUSE = "1"
dotnet format QmlSharp.slnx --verify-no-changes --no-restore --verbosity minimal
```

`cmake --preset windows-debug` and `cmake --build --preset debug` are intentionally Qt-free in
Step 01.00. Real Qt discovery starts in later native-host and build-system steps.

On Windows, run the native preset commands from a Visual Studio developer shell or by calling
`vcvars64.bat` first so `clang-cl` and `FBuild.exe` are available in `PATH`.

## RequiresQt test policy

Real Qt SDK integration tests use the stable xUnit trait `[Trait("Category", "RequiresQt")]`.
They discover Qt through `QT_DIR` first and then a Qt `bin` directory on `PATH`; the
legacy `QMLSHARP_QT_DIR` name is not a QmlSharp discovery contract.

To run the mocked/default test set without real Qt:

```powershell
dotnet test QmlSharp.slnx --configuration Debug --filter "Category!=RequiresQt"
```

To run only real Qt tests on a machine with Qt installed:

```powershell
$env:QT_DIR = "C:\Qt\6.11.0\msvc2022_64"
dotnet test QmlSharp.slnx --configuration Debug --filter "Category=RequiresQt"
```

CI jobs that install Qt 6.11.0 may run the full test suite or the `Category=RequiresQt`
filter explicitly. CI jobs without Qt can use `Category!=RequiresQt` without changing test
code.

## Implementation traceability

Module step audits, parity notes, and final-gate traceability live in PR descriptions or
linked GitHub issues. The implementation repository does not keep a committed root `docs/`
tree for these records. Later implementation reviews should treat the relevant step audit
PR body or linked issue as the review contract for that wave.

## Local quality workflow

```powershell
pre-commit install
pre-commit run --all-files
```
