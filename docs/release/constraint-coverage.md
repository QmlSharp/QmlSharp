# Constraint Coverage

| Constraint or Rule | Implementing Step | Verification Point | Status | Notes |
| --- | --- | --- | --- | --- |
| Repository uses C# + C++ only | 01.00 | `dotnet build QmlSharp.sln`; `cmake --build --preset debug` | Implemented | No Rust, Node.js, TypeScript, or cxx-qt dependencies are introduced. |
| Root solution and root CMake coexist | 01.00 | `dotnet restore QmlSharp.sln`; `cmake --preset windows-debug` | Implemented | C# and native entry points are separate and explicit. |
| Production projects treat warnings as errors | 01.00 | `dotnet build QmlSharp.sln` | Implemented | Configured in `Directory.Build.props`. |
| Test projects relax only documented analyzer rules | 01.00 | `dotnet build QmlSharp.sln` | Implemented | `CA1707` is relaxed only for test projects in `Directory.Build.targets`. |
| Generated and build outputs are ignored | 01.00 | `git status --short` after restore/build/test/cmake | Implemented | Root `.gitignore` covers bootstrap and generated output directories. |
| Step 01.00 native bootstrap must not require Qt | 01.00 | `cmake --preset windows-debug`; `cmake --build --preset debug` | Implemented | Placeholder static library keeps native bootstrap buildable before Qt integration work. |
