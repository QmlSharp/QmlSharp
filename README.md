# QmlSharp

QmlSharp is a C# + C++ framework and toolchain for building Qt/QML applications. The managed
toolchain owns compiler, registry, runtime coordination, build orchestration, and dev tools;
the native side stays a thin Qt/QML shell.

## Step 01.00 bootstrap status

This branch bootstraps the implementation repository foundation and quality gates only. It does
not implement real `01-registry` behavior yet.

## Required local tools

- .NET SDK 10.0.202
- CMake 4.x or newer
- `pre-commit` (optional but recommended)
- `clang-format` (optional until native formatting hooks become mandatory)

## Bootstrap commands

```powershell
dotnet restore QmlSharp.sln
dotnet build QmlSharp.sln
dotnet test QmlSharp.sln
cmake --preset windows-debug
cmake --build --preset debug
dotnet format QmlSharp.sln --verify-no-changes
```

`cmake --preset windows-debug` and `cmake --build --preset debug` are intentionally Qt-free in
Step 01.00. Real Qt discovery starts in later native-host and build-system steps.

## Local quality workflow

```powershell
pre-commit install
pre-commit run --all-files
```
