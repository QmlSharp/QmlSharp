# AGENTS.md — Reviewer Instructions for QmlSharp

These rules tell Codex / AI reviewers how to review pull requests in **QmlSharp**.
Review the code, tests, PR description, CI results, and any design excerpts that
the PR author includes. Do not assume access to private design repositories; if
essential context is missing, ask the author to include the relevant excerpt in
the PR.

Be specific. Cite file paths and line numbers. Prefer fewer, higher-signal comments
over many style nits.

---

## 1. Repository Overview

QmlSharp is a **C# + C++ framework and toolchain for building Qt/QML applications**.
It is the C# + C++ successor to QmlTS (the prior TypeScript + Rust prototype). The
final stack is **C# and C++ only** — no TypeScript, Rust, napi-rs, or cxx-qt.

- **C# (managed brain)** owns the compiler, registry, DSL generator, build
  orchestration, runtime coordination, and dev tools.
- **C++ (thin Qt shell)** owns only `QObject` declarations, QML type registration,
  `QQmlEngine` / `QGuiApplication` lifecycle, and a flat C ABI surface.
- **P/Invoke + flat C ABI** is the only managed↔native boundary. No COM, no
  C++/CLI, no mixed-mode assemblies.
- **`.schema.json` is the single source of truth** for every generated artifact.
  All downstream generators (C++ QObject `.h`/`.cpp`, `qmldir`, `.qmltypes`)
  read from schema, never from C# source directly.

The repository is organized into **9 modules** that mirror the project DAG. Not
all modules are implemented yet — the codebase is being built in small, scoped
PRs that should explain the step or feature they implement.

| # | Module | Purpose | Language |
|---|--------|---------|----------|
| 01 | `registry` | Qt type system: scan, parse, normalize, query qmltypes/qmldir/metatypes | C# |
| 02 | `qml-ast` | QML AST: builder, visitor, walker, transform, validator | C# |
| 03 | `qml-emitter` | QML source generation from AST | C# |
| 04 | `qt-tools` | Qt SDK tool wrappers (qmlformat, qmllint, qmlcachegen, moc, rcc) | C# |
| 05 | `dsl-generator` | C# fluent DSL → internal IR | C# |
| 06 | `compiler` | Analyze → extract → transform → post-process → emit pipeline | C# |
| 07 | `native-host` | C# coordinator + thin generated C++ Qt shell | C# + C++ |
| 08 | `build-system` | 8-stage build pipeline + CLI + artifact layout | C# |
| 09 | `dev-tools` | Hot reload, error overlay, REPL, dev server | C# |

### Repository Layout (current)

```
src/                        Managed projects
  QmlSharp.Core/            Shared primitives
  QmlSharp.Qml.Ast/         02-qml-ast implementation
  QmlSharp.Registry/        01-registry implementation
tests/                      xUnit test projects (one per shipping project)
tools/                      Buildable internal tools (e.g. registry snapshot generator)
native/                     C++ thin Qt shell
  include/qmlsharp/         Public ABI headers (handwritten, stable)
  src/                      Bootstrap + ABI implementations (handwritten)
cmake/                      Reusable CMake modules (warnings, sanitizers, clang-tidy)
data/                       Fixtures and snapshots (e.g. qt-registry-snapshots)
templates/                  Project / artifact templates (currently empty)
examples/                   Example apps (currently empty)
.github/workflows/          windows-ci, linux-ci, macos-ci, codeql
```

### Review Context

Use the context available in this repository and in the pull request:

- the PR description, linked issue, and any quoted design excerpts
- the changed code and nearby module code
- existing tests and fixtures
- CI workflow definitions and check results
- repository configuration (`Directory.Build.props`, `.editorconfig`,
  `.clang-format`, `.clang-tidy`, CMake presets)

If the PR depends on private or out-of-repo design context that is not visible
to reviewers, ask for the relevant excerpt to be included in the PR before
blocking on that point.

---

## 2. Architecture Principles

These are non-negotiable. A PR that violates any of them should be flagged P1.

1. **Static MOC-processed `QObject` types only.** No `QMetaObjectBuilder`, no
   runtime `QObject` construction. Every QML-visible type is a generated, statically
   compiled C++ subclass.
2. **No numeric dispatch in QML-visible APIs.** Properties and commands are
   addressed by name at the QML boundary. Internal numeric IDs are allowed for
   the native bridge but must not leak into QML.
3. **C# contracts are native-implementation-neutral.** No `QObject`, no `Qt*`
   type names, no MOC concepts in C# public APIs.
4. **Fire-and-forget commands.** QML → command is one-way. No synchronous return
   values from QML to managed code. State flows back through property bindings.
5. **QML engine owns `QObject` instances.** C# registry holds **non-owning**
   references. Treat native pointers as borrowed and potentially invalidated.
6. **Process-global Qt type registration.** Design assumes one QML type
   namespace per process. Do not attempt per-engine scoping.
7. **C# owns all business logic; C++ owns only Qt integration.** Litmus test:
   if a C++ function does more than delegate to a Qt API or call through the C
   ABI, it is in the wrong place.
8. **Generation over handwriting.** Per-ViewModel `QObject` `.h`/`.cpp` are
   generated from `.schema.json`. Handwritten C++ target is **< 500 lines** for
   the entire native host (excluding generated code). New handwritten C++ must
   justify why it cannot be generated.
9. **Schema is the single source of truth.** Compiler emits `.schema.json`; all
   downstream generators (C++, qmldir, qmltypes) read from schema, not from C#
   source.
10. **V2 runtime model only.** No V1 compatibility, no singleton ViewModels.
    ViewModels are registered QML types with `instanceId` routing and
    `compilerSlotKey` for hot-reload matching.
11. **Compiler output is deterministic and is part of the public toolchain
    contract.** `.qml`, `.schema.json`, `event-bindings.json`, and `.qml.map`
    must be byte-stable for the same input. `<Deterministic>true</Deterministic>`
    is set repo-wide; honor it.

### P/Invoke Boundary Rules

- Flat C ABI only — no C++ classes, templates, RTTI, or exceptions across the
  boundary. Calling convention is `cdecl`.
- Strings are **UTF-8, null-terminated**. Binary buffers always carry an explicit
  length parameter.
- Complex data uses **JSON** (`System.Text.Json` on the C# side, Qt JSON or
  `nlohmann/json` on the C++ side). Validate JSON shape before use — never trust
  the other side.
- Memory ownership: C# allocates inputs (valid only for the call); C++ allocates
  return strings via `qmlsharp_*_alloc`-equivalent and the caller frees via
  `qmlsharp_free_string` (or the documented free function for that buffer).
- Error reporting: C++ catches all exceptions, returns an `int` error code
  (0 = success), and exposes the message via `qmlsharp_get_last_error()`.
  Managed wrappers translate non-zero codes into typed exceptions.

### Module Dependency DAG (do not introduce back-edges)

```
Layer 0:  01-registry    02-qml-ast    04-qt-tools
Layer 1:  03-qml-emitter (←02)         05-dsl-generator (←01,02)
Layer 2:  06-compiler (←01,02,03,04,05)   07-native-host (←01,06)
Layer 3:  08-build-system (←04,06,07)
Layer 4:  09-dev-tools (←06,07,08)
```

Reject any PR that adds an upward or sideways dependency that violates this DAG.

---

## 3. Build and Test Commands

Use the tools and presets that already exist in this repository. Do not invent
commands. The current CI workflows (`.github/workflows/{windows,linux,macos}-ci.yml`)
are the authoritative reference for what must pass.

### .NET (managed)

```powershell
dotnet restore QmlSharp.slnx
dotnet build   QmlSharp.slnx --configuration Debug --no-restore
dotnet test    QmlSharp.slnx --configuration Debug --no-build --verbosity normal
dotnet format  QmlSharp.slnx --verify-no-changes --verbosity minimal
```

- SDK is pinned by `global.json` (currently `10.0.203`, `latestPatch` rollforward).
- `TargetFramework` is `net10.0`. Treat warnings as errors for non-test projects
  (`Directory.Build.props`).
- Analyzers always on: `Microsoft.CodeAnalysis.NetAnalyzers`, `Meziantou.Analyzer`,
  and (where `PublicAPI.Shipped.txt` exists) `Microsoft.CodeAnalysis.PublicApiAnalyzers`.
- Package versions are managed centrally in `Directory.Packages.props`. Do not
  add `<Version>` attributes to `<PackageReference>` items.

### Native (C++ via CMake + FASTBuild)

```powershell
# Windows (run from a VS dev shell, or after vcvars64.bat)
cmake --preset windows-debug
cmake --build --preset debug

# Linux
cmake --preset linux-debug && cmake --build --preset linux-debug

# CI parity (warnings-as-errors)
cmake --preset windows-ci  && cmake --build --preset ci
cmake --preset linux-ci    && cmake --build --preset linux-ci
cmake --preset macos-ci    && cmake --build --preset macos-ci
```

- C++20 is required (`CMAKE_CXX_STANDARD 20`); design minimum is C++17, current
  builds use C++20.
- `USE_WERROR=ON` in CI presets. `ENABLE_ASAN` and `ENABLE_CLANG_TIDY` are
  available CMake options.
- Windows uses FASTBuild + `clang-cl`. Linux/macOS use FASTBuild + `clang`.
- Step 01.00 builds are intentionally **Qt-free**; later native-host work
  introduces real Qt discovery. If a PR claims to add Qt integration, verify
  that CI actually exercises it.

### Quality Gates (pre-commit)

```bash
pre-commit install
pre-commit run --all-files
```

Hooks include: trailing whitespace, EOL fixer, JSON/YAML/TOML check, codespell,
clang-format, clang-tidy (`-p=build`), and `dotnet format`.

### Generated Artifacts (when present)

- Compiler outputs: `*.qml`, `*.schema.json`, `event-bindings.json`, `*.qml.map`.
- Build outputs: `qmldir`, `*.qmltypes`, `manifest.json`, `qmlsharp_native.dll`/`.so`/`.dylib`.
- Generated C# files live under `**/Generated/**` and are flagged
  `generated_code = true` in `.editorconfig`. Generated C++ files must carry a
  `// GENERATED — DO NOT EDIT` header.

If a build/test command in this section is uncertain in your reviewing context,
say so in the review (e.g. "Could not verify locally; CI must pass").

---

## 4. Code Style Rules

Defer to repo configuration first (`.editorconfig`, `.clang-format`, `.clang-tidy`,
`Directory.Build.props`). The points below are house rules the configs already
encode but reviewers commonly forget.

### C\#

- 4-space indent, UTF-8, LF, final newline. Strings: 2-space for JSON/YAML/MD.
- **Do not use `var`.** Repo policy: `csharp_style_var_*` are all `false:warning`.
- `Nullable` and `ImplicitUsings` are enabled. Honor nullable annotations — do
  not bypass with `!` unless the invariant is documented in a comment.
- `Deterministic` is on; do not introduce non-deterministic output (timestamps,
  GUIDs, hash dictionaries with non-stable iteration) into anything that lands
  in a generated artifact.
- Prefer `record`, `readonly`, and `System.Collections.Immutable` types for
  data flowing across module boundaries. Mutable state requires a reason.
- Validate every P/Invoke input (null, length, JSON shape) **before** calling
  native code. Don't trust the C++ side.
- `TreatWarningsAsErrors=true` for non-test projects. Test projects relax
  `CA1707` only for sentence-style xUnit names; do not silence other rules
  globally.
- `PublicApiAnalyzers` runs where `PublicAPI.Shipped.txt` exists — public API
  changes require corresponding `PublicAPI.Unshipped.txt` updates.

### C++

- `clang-format` and `clang-tidy` are authoritative. If a comment is "style",
  it should be a config change, not a review nit.
- C++20, no exceptions across the C ABI. Use RAII (`std::unique_ptr`,
  `std::shared_ptr`, Qt parent ownership). No raw `new`/`delete` for ownership.
- Document thread affinity in every class header (Qt is strict about this).
- `extern "C"` ABI surface lives in `native/include/qmlsharp/` and
  `native/src/`. Keep it minimal and stable.
- New handwritten `.cpp` files must be justified ("why not generated?") in the
  PR description.

### QML (generated)

- QML files are **generated**; do not hand-edit. PRs that add handwritten QML
  in the build pipeline are wrong unless the file is a fixture, template, or
  test golden.
- QML stays presentation-focused. Bindings, layout, animations — yes. Business
  logic, validation, networking, persistence — no.
- ViewModel state and commands cross into QML by **name**, never by index.

### Files / Layout

- Honor the 9-module DAG. New code lives in the module that owns it.
- `**/Generated/**`, `tests/fixtures/**`, `native/generated/**`, and `tools/*/Generated/**`
  are excluded from `pre-commit`. Don't put hand-written code there.
- New scripts that change developer setup also need a README/CLAUDE/commit-note
  update — silent setup churn breaks contributors.

---

## 5. Review Guidelines

Read the **diff**, nearby code, tests, CI output, and PR-provided context. Don't
speculate from file names alone. Be terse and specific.

### P1 — block the PR until resolved

Treat every item below as a P1 issue. Comment with file:line and a concrete fix.

- **Correctness regressions** — behavior change vs. tests, PR-stated contracts,
  or prior commits.
- **Missing tests for behavior changes** — every new public method, every
  diagnostic code, every code path with a branch needs a test. New diagnostics
  (`QMLSHARP-*`) need at least one positive and one negative case.
- **Public API breaking changes** — including `PublicAPI.Shipped.txt` deltas,
  ABI exports in `native/include/qmlsharp/`, schema field renames in
  `.schema.json`, and CLI flag changes. Require explicit call-out and migration
  notes.
- **CI / build failures** — Windows, Linux, and macOS CI must all pass.
  `dotnet format --verify-no-changes` failures count.
- **Unsafe C# / C++ / Qt interop** — string encoding mistakes (must be UTF-8,
  null-terminated), missing length on binary buffers, exceptions allowed to
  cross the C ABI, ignored error codes, missing free of returned buffers.
- **`QObject` ownership mistakes** — C# code that assumes ownership of Qt
  objects, `QObject` retained beyond engine lifetime, `setParent` patterns that
  fight Qt's tree, missing `QML_ELEMENT` / wrong owner on QML-exposed types.
- **Signal/slot lifetime bugs** — connections to objects without explicit
  `QObject` context, lambdas capturing `this` without disconnect on destruction,
  cross-thread connection types that don't match the receiver's affinity.
- **QML binding / delegate performance regressions** — bindings that allocate in
  hot paths, expensive expressions inside `Repeater`/`ListView` delegates,
  per-frame property reads in animations, layout recomputation triggered by
  bindings on derived state.
- **Source generator or compiler edge cases** — non-deterministic output,
  symbol-name collisions, generic / nullable / partial / nested-type handling,
  `INamedTypeSymbol` equality assumptions, attribute lookups by string.
- **Generated QML / runtime contract / native integration mismatches** — any
  drift between the C# `[State]/[Command]/[Effect]` model, the emitted
  `.schema.json`, the generated C++ `QObject`, and the QML the runtime loads.
  These four must agree exactly.
- **Nondeterministic compiler output** — iteration over `Dictionary<>` with
  default ordering, `Guid.NewGuid()` in artifacts, timestamps in output, file
  enumeration without sort, hash IDs that depend on machine-local state.
- **Cross-platform path / build issues** — backslash literals, hard-coded
  `C:\` paths, case-sensitive filename mismatches, missing `Path.Combine`,
  preset assumptions that don't apply on macOS / Linux.
- **Packaging or generated-artifact regressions** — changes to `dist/` layout,
  `manifest.json`, `qmldir`, `.qmltypes`, schema field additions/removals,
  `event-bindings.json` shape. These are public toolchain contract.
- **Performance regressions in hot paths** — compiler `Compile()` /
  `IncrementalCompile()`, registry queries, P/Invoke marshaling per state
  update, JSON serialization on per-frame paths. Flag any allocation in a
  `for`/`foreach` over QML nodes, schema members, or registry entries.
- **Deviations from the stated PR scope or established architecture** — silent
  scope expansion, module boundary violations, dependency back-edges in the DAG,
  or "MVP" stubs that skip behavior the PR says it implements.

### P2 — request changes; not a hard block

- Missing or stale `///` XML docs on public types (`GenerateDocumentationFile`
  is on; `CS1591` is suppressed but docs are still expected on stable APIs).
- Test names that don't describe the behavior under test.
- Heavy logic that *could* be expressed as data (a table-driven test, a
  generator input) but was hand-rolled.

### P3 — suggest, do not block

- Naming consistency, comment grammar, redundant `using` directives that the
  formatter will fix on next run.

### Things to *not* comment on

- Style points the formatter / analyzer will catch.
- Personal taste re-orderings of independent statements.
- Speculative "could be slow" without a measurement or hot-path argument.
- Large-scale refactors outside the PR's stated scope (file an issue instead).

---

## 6. PR Expectations

Each PR should be reviewable in one sitting. Reject scope creep.

1. **One scoped change per PR.** A PR implements **one** step or feature
   end-to-end — code + tests + docs + (if needed) generator updates. Do not
   bundle unrelated steps.
2. **Branch from latest `main`.** `main` is protected; never push to it directly.
   Branch names follow existing conventions (e.g. `codex/stepNN-NN-…`,
   `chore/…`, `feat/…`).
3. **Title and body explain the *why*.** Title: imperative, ≤ 70 chars. Body:
   summary, relevant design or issue context available to reviewers, test plan,
   and any deviation called out explicitly.
4. **Tests in the same PR.** Behavior changes without tests are P1. Tests live
   next to the code (`tests/<Project>.Tests/`). Use xUnit; assert on observable
   behavior, not on private internals.
5. **No placeholders or TODO stubs for core behavior.** "Implemented in the
   next PR" is not acceptable for the step's listed deliverables. Out-of-scope
   follow-ups go to issues, not to TODOs in the diff.
6. **No generator output drift.** If a change affects generated artifacts
   (`.qml`, `.schema.json`, `event-bindings.json`, `.qml.map`, generated C++,
   `qmldir`, `.qmltypes`), regenerate goldens in the same PR and confirm the
   diff is intentional.
7. **CI must be green on all three OSes.** Windows, Linux, and macOS workflows
   are all required. CodeQL runs on relevant changes.
8. **Public API and ABI changes are explicit.** Call out updates to
   `PublicAPI.{Shipped,Unshipped}.txt`, `native/include/qmlsharp/*.h`, schema
   shape, CLI flags, or build-config keys in the PR body.
9. **Determinism is verified.** If the change touches code that emits artifacts,
   the test suite should include a "compile twice, byte-equal" assertion or
   equivalent. Don't merge if you can't show output is stable.
10. **Cross-platform paths.** Use `Path.Combine` / `Path.DirectorySeparatorChar`
    in C#; avoid hard-coded slash direction in CMake/scripts.

### Reviewer Checklist (paste into review when applicable)

- [ ] Change matches the stated PR scope and any design excerpt included in the PR.
- [ ] No DAG back-edges introduced.
- [ ] Tests added/updated for every behavior change; `dotnet test` passes locally or in CI.
- [ ] `dotnet format --verify-no-changes` passes.
- [ ] Native code (if any) builds under `windows-ci` / `linux-ci` / `macos-ci` presets with `USE_WERROR=ON`.
- [ ] No new handwritten C++ that could be generated; ABI surface unchanged or change called out.
- [ ] Generated artifacts (`.qml`, `.schema.json`, `event-bindings.json`, `.qml.map`) are deterministic and goldens updated.
- [ ] No `QObject` ownership / signal-slot lifetime issues.
- [ ] Public API / schema / ABI diffs explicitly noted.
- [ ] No scope creep beyond the stated step.

---

*If a rule here ever feels wrong for a real change, say so in the review.
Rules without rationale rot — push back, link the ADR, and we will update this
file.*
