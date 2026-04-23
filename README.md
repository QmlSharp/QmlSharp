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
dotnet format QmlSharp.slnx --verify-no-changes
```

`cmake --preset windows-debug` and `cmake --build --preset debug` are intentionally Qt-free in
Step 01.00. Real Qt discovery starts in later native-host and build-system steps.

On Windows, run the native preset commands from a Visual Studio developer shell or by calling
`vcvars64.bat` first so `clang-cl` and `FBuild.exe` are available in `PATH`.

## Local quality workflow

```powershell
pre-commit install
pre-commit run --all-files
```
