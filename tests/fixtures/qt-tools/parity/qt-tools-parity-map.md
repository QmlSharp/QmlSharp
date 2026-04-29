# Qt Tools Parity Map

Step: 04.00 - Audit QmlTS Qt Tools Behavior and Repair the Test Map

This file is the implementation-side parity audit for the 04-qt-tools wave. It is
kept under test fixtures, not a root docs tree, so future Qt-tools tests and closure
checks have one local reference without adding implementation-repository documentation.

## Source Inputs

Required design inputs reviewed:

- `ApiDesign/README.md`
- `ApiDesign/architecture.md`
- `ApiDesign/implementation-constraints.md`
- `ApiDesign/migration-plan.md`
- `ApiDesign/integration-test-plan.md`
- `ApiDesign/implementation-plan/implementation-repository-layout.md`
- `ApiDesign/implementation-plan/implementation-plan-review.md`
- `ApiDesign/implementation-plan/final-production-gate.md`
- `ApiDesign/implementation-plan/04-qt-tools.md`
- `ApiDesign/04-qt-tools/README.md`
- `ApiDesign/04-qt-tools/API-Design.md`
- `ApiDesign/04-qt-tools/test-spec.md`

Required QmlTS inputs reviewed:

- `QmlTS/tests/qt-tools/toolchain.test.ts`
- `QmlTS/tests/qt-tools/tool-runner.test.ts`
- `QmlTS/tests/qt-tools/diagnostic.test.ts`
- `QmlTS/tests/qt-tools/qmlformat.test.ts`
- `QmlTS/tests/qt-tools/qmllint.test.ts`
- `QmlTS/tests/qt-tools/qmlcachegen.test.ts`
- `QmlTS/tests/qt-tools/qmltc.test.ts`
- `QmlTS/tests/qt-tools/qmlimportscanner.test.ts`
- `QmlTS/tests/qt-tools/qmldom.test.ts`
- `QmlTS/tests/qt-tools/qml-runner.test.ts`
- `QmlTS/tests/qt-tools/rcc.test.ts`
- `QmlTS/tests/qt-tools/qmltyperegistrar.test.ts`
- `QmlTS/tests/qt-tools/quality-gate.test.ts`
- `QmlTS/tests/qt-tools/factories.test.ts`
- `QmlTS/tests/qt-tools/fixtures/*`

## Test Count Repair

`ApiDesign/04-qt-tools/test-spec.md` says the module has 112 tests across 12 suites,
but its listed suites currently enumerate only 106 tests. The implementation contract
for the 04 wave is the corrected 13-suite map below unless ApiDesign is updated later
with an equivalent correction. The missing suite is
`QmlTypeRegistrarTests`, which is required because:

- `ApiDesign/04-qt-tools/README.md` says all 9 Qt CLI tools are wrapped.
- `ApiDesign/04-qt-tools/API-Design.md` includes `IQmlTypeRegistrar`.
- `QmlTS/tests/qt-tools/qmltyperegistrar.test.ts` contains six registrar tests.
- `ApiDesign/implementation-plan/04-qt-tools.md` Step 04.10 explicitly requires six
  qmltyperegistrar tests.

Corrected suite count:

| Suite | IDs | Count | Tool coverage |
|---|---:|---:|---|
| `QtToolchainTests` | `TC-001` through `TC-009` | 9 | discovery and all tools |
| `ToolRunnerTests` | `TR-001` through `TR-006` | 6 | process runner |
| `QmlFormatTests` | `QF-001` through `QF-016` | 16 | `qmlformat` |
| `QmlLintTests` | `QL-001` through `QL-018` | 18 | `qmllint` |
| `QmlCachegenTests` | `QC-001` through `QC-010` | 10 | `qmlcachegen` |
| `QmltcTests` | `QT-001` through `QT-005` | 5 | `qmltc` |
| `QmlImportScannerTests` | `IS-001` through `IS-006` | 6 | `qmlimportscanner` |
| `QmlDomTests` | `QD-001` through `QD-005` | 5 | `qmldom` |
| `QmlRunnerTests` | `QR-001` through `QR-007` | 7 | `qml` |
| `RccTests` | `RC-001` through `RC-007` | 7 | `rcc` |
| `QmlTypeRegistrarTests` | `REG-001` through `REG-006` | 6 | `qmltyperegistrar` |
| `QualityGateTests` | `QG-001` through `QG-011` | 11 | aggregation |
| `DiagnosticParserTests` | `DP-001` through `DP-006` | 6 | parser |
| Total |  | 112 | all 9 CLI tools covered |

Required qmltyperegistrar suite to add to the C# test-spec or implement directly if the
design document is still stale:

| ID | Test Case | Expected Result | QmlTS source |
|---|---|---|---|
| `REG-001` | Register with metatypes JSON input | `TypeRegistrarResult.Success = true`, output registration source is reported | `REG-01` |
| `REG-002` | Register with JS root or equivalent QmlSharp option when supported | Command arguments preserve the Qt option intent, or the case is marked not applicable if absent from QmlSharp API | `REG-02` |
| `REG-003` | Generate `.qmltypes` convenience output when the QmlSharp API exposes a convenience helper | Generated output file is reported; otherwise fold into `RegisterAsync` output reporting | `REG-03` |
| `REG-004` | Result includes command and timing metadata | `ToolResult.Command` is non-empty and `DurationMs > 0` | `REG-04` |
| `REG-005` | Namespace option is passed to the CLI | Registrar command includes namespace option and output succeeds or mocked args match | `REG-05` |
| `REG-006` | Invalid metatypes JSON fails gracefully | `Success = false` or deterministic diagnostics without unhandled exceptions | `REG-06` |

## Environment Contract Note

Some older ApiDesign Qt-tools text still refers to `QMLSHARP_QT_DIR`. The active
implementation contract for this wave is `QT_DIR`, matching current QmlSharp registry
tests and CI. When Step 04.01 and Step 04.03 implement Qt test guards and discovery,
test names and skip messages must use `QT_DIR`. `QMLSHARP_QT_DIR` must not be added as
a fallback or competing discovery path unless a later explicit design-update step changes
the project-wide Qt SDK contract. Any stale design-doc reference should be treated as a
design correction item, not copied into product code.

## Tool Coverage Check

All 9 Qt CLI tools have explicit C# test coverage after the count repair:

| Tool | Required wrapper | Required suite |
|---|---|---|
| `qmlformat` | `IQmlFormat` | `QmlFormatTests` |
| `qmllint` | `IQmlLint` | `QmlLintTests` |
| `qmlcachegen` | `IQmlCachegen` | `QmlCachegenTests` |
| `qmltc` | `IQmltc` | `QmltcTests` |
| `qmlimportscanner` | `IQmlImportScanner` | `QmlImportScannerTests` |
| `qmldom` | `IQmlDom` | `QmlDomTests` |
| `qml` | `IQmlRunner` | `QmlRunnerTests` |
| `rcc` | `IRcc` | `RccTests` |
| `qmltyperegistrar` | `IQmlTypeRegistrar` | `QmlTypeRegistrarTests` |

## QmlTS Test Classification

Classification values:

- Preserved exactly
- Preserved with C# naming or API adaptation
- Intentionally changed by QmlSharp design
- Not applicable to QmlSharp
- Missing from current C# test-spec and proposed as an added test

### `toolchain.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `TC-01` explicit `qtDir` discovery | Preserved with C# naming or API adaptation | `TC-001` |
| `TC-02` invalid path throws | Preserved with C# naming or API adaptation | `TC-002` |
| `TC-03` discovery from `QT_DIR` | Preserved exactly | `TC-003`, using `QT_DIR` |
| `TC-04` available tools reported | Preserved with C# naming or API adaptation | `TC-004` |
| `TC-05` `getToolPath(qmlformat)` | Preserved with C# naming or API adaptation | `TC-006` |
| `TC-06` unknown tool throws | Preserved with C# naming or API adaptation | Fold into `TC-005`/`TC-006` error-path checks |
| `TC-07` import paths include QML directory | Preserved with C# naming or API adaptation | `TC-001`/`TC-009` |
| `TC-08` version parsing | Preserved with C# naming or API adaptation | `TC-007` |
| `TC-09` platform detection | Preserved with C# naming or API adaptation | `TC-008` |
| `TC-10` tools include `moc` and `qmlaotstats` | Preserved with C# naming or API adaptation | `TC-004` |
| `TC-11` extra import paths append | Preserved with C# naming or API adaptation | `TC-009` |
| `TC-12` discovery via PATH | Preserved with C# naming or API adaptation | Step 04.03 additional discovery test |
| `TC-13` `QMLTS_QT_DIR` preferred over `QT_DIR` | Intentionally changed by QmlSharp design | Use `QT_DIR`; do not preserve QmlTS-specific env precedence |
| `TC-14` explicit `qtDir` highest priority | Preserved with C# naming or API adaptation | `TC-003` precedence assertion |

### `tool-runner.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `TR-01` successful tool run | Preserved with C# naming or API adaptation | `TR-001` |
| `TR-02` missing tool throws | Preserved with C# naming or API adaptation | `TR-002` |
| `TR-03` timeout throws | Preserved with C# naming or API adaptation | `TR-003` |
| `TR-05` command string recorded | Preserved with C# naming or API adaptation | `TR-005` |
| `TR-06` duration tracked | Preserved with C# naming or API adaptation | `TR-006` |
| stdin coverage absent in QmlTS numbering | Missing from QmlTS but required by C# test-spec | `TR-004` |

### `diagnostic.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `DP-01` parse single stderr diagnostic | Preserved with C# naming or API adaptation | `DP-001` |
| `DP-02` parse multiple stderr lines | Preserved with C# naming or API adaptation | `DP-002` |
| `DP-03` default file for bracket format | Preserved with C# naming or API adaptation | `DP-003` filename override |
| `DP-04` parse qmllint JSON | Preserved with C# naming or API adaptation | `DP-004` |
| `DP-05` parse JSON suggestion | Preserved with C# naming or API adaptation | `DP-005` |
| `DP-06` empty input returns no diagnostics | Preserved with C# naming or API adaptation | `DP-006` |

### `qmlformat.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `FMT-01` format valid file | Preserved with C# naming or API adaptation | `QF-001` |
| `FMT-02` syntax-error file fails | Preserved with C# naming or API adaptation | `QF-002` |
| `FMT-03` format string | Preserved with C# naming or API adaptation | `QF-003` |
| `FMT-04` 2-space indent | Preserved with C# naming or API adaptation | `QF-004` |
| `FMT-05` tab indent | Preserved with C# naming or API adaptation | `QF-005` |
| `FMT-06` normalize reorders properties | Preserved with C# naming or API adaptation | `QF-006` |
| `FMT-07` sort imports | Preserved with C# naming or API adaptation | `QF-007` |
| `FMT-08` semicolon add/always | Preserved with C# naming or API adaptation | `QF-008` |
| `FMT-10` newline unix | Preserved with C# naming or API adaptation | `QF-010` |
| `FMT-13` format batch | Preserved with C# naming or API adaptation | `QF-013` |
| `FMT-14` already formatted has no changes | Preserved with C# naming or API adaptation | `QF-014` |
| `FMT-15` default options JSON | Intentionally changed by QmlSharp design | C# option defaults covered by `QF-015`, no TS `getDefaultOptions` JSON API |
| `FMT-16` force option | Preserved with C# naming or API adaptation | `QF-016` |
| `FMT-17` in-place mode | Preserved with C# naming or API adaptation | Add as `QF-001`/`QF-013` subcase if `InPlace` is exposed |
| `FMT-18` write defaults | Not applicable to QmlSharp | No `writeDefaults` API in current design |
| semicolon remove behavior absent from QmlTS numbering | Missing from QmlTS but required by C# test-spec | `QF-009` |
| column width and ignore settings absent from QmlTS numbering | Missing from QmlTS but required by C# test-spec | `QF-011`, `QF-012` |

### `qmllint.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `LNT-01` valid file | Preserved with C# naming or API adaptation | `QL-001` |
| `LNT-02` syntax error diagnostics | Preserved with C# naming or API adaptation | `QL-002` |
| `LNT-03` type error diagnostics | Preserved with C# naming or API adaptation | `QL-003` |
| `LNT-04` lint string | Preserved with C# naming or API adaptation | `QL-004` |
| `LNT-05` JSON output structured | Preserved with C# naming or API adaptation | `QL-005` |
| `LNT-06` warning levels accepted | Preserved with C# naming or API adaptation | `QL-006` |
| `LNT-07` warning disabled suppresses diagnostics | Preserved with C# naming or API adaptation | `QL-006` |
| `LNT-12` batch lint | Preserved with C# naming or API adaptation | `QL-011` |
| `LNT-13` line numbers are 1-based | Preserved with C# naming or API adaptation | `QL-012` |
| `LNT-14` category field | Preserved with C# naming or API adaptation | `QL-013` |
| `LNT-15` summary counts | Preserved with C# naming or API adaptation | `QL-014` |
| `LNT-16` plugin list | Preserved with C# naming or API adaptation | `QL-015` |
| `LNT-17` parse JSON helper | Preserved with C# naming or API adaptation | `DP-004` plus parser unit tests |
| `LNT-18` write defaults | Not applicable to QmlSharp | No `writeDefaults` API in current design |
| max warnings, silent, import paths, bare, fix, module, stderr fallback absent from QmlTS numbering | Missing from QmlTS but required by C# test-spec | `QL-007` through `QL-010`, `QL-016` through `QL-018` |

### `qmlcachegen.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `CG-01` option-to-args mapping | Preserved with C# naming or API adaptation | Wrapper argument unit tests under Step 04.07 |
| `CG-02` valid file compiles | Preserved with C# naming or API adaptation | `QC-001` |
| `CG-03` syntax error fails | Preserved with C# naming or API adaptation | `QC-002` |
| `CG-04` compile string | Preserved with C# naming or API adaptation | `QC-003` |
| `CG-05` bytecode only | Preserved with C# naming or API adaptation | `QC-004` |
| `CG-06` AOT stats | Preserved with C# naming or API adaptation | `QC-005` |
| `CG-07` complex QML compiles or fails gracefully | Preserved with C# naming or API adaptation | Edge fixture subcase in Step 04.07 |
| `CG-08` type-error behavior | Intentionally changed by QmlSharp design | Prefer explicit diagnostics according to current Qt behavior, not QmlTS assumption |
| `CG-09` batch compile | Preserved with C# naming or API adaptation | `QC-009` |
| `CG-10` aggregate stats | Preserved with C# naming or API adaptation | `QC-010` |
| warnings-as-errors, verbose, import paths absent from QmlTS numbering | Missing from QmlTS but required by C# test-spec | `QC-006` through `QC-008` |

### `qmltc.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `QTC-01` compile valid file to C++ | Preserved with C# naming or API adaptation | `QT-001` |
| `QTC-02` syntax error fails | Preserved with C# naming or API adaptation | `QT-005` |
| `QTC-03` compile string | Intentionally changed by QmlSharp design | Current `IQmltc` design is file-only |
| `QTC-04` namespace option | Preserved with C# naming or API adaptation | `QT-002` |
| `QTC-05` timing info | Preserved with C# naming or API adaptation | `ToolResult` assertions in `QT-001`/`QT-005` |
| `QTC-06` complex QML compiles or fails gracefully | Preserved with C# naming or API adaptation | Edge fixture subcase in Step 04.07 |
| module and export macro options absent from QmlTS numbering | Missing from QmlTS but required by C# test-spec | `QT-003`, `QT-004` |

### `qmlimportscanner.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `IMP-01` scan file finds QtQuick import | Preserved with C# naming or API adaptation | `IS-002` |
| `IMP-02` complex file finds multiple imports | Preserved with C# naming or API adaptation | `IS-002` fixture subcase |
| `IMP-03` scan string | Preserved with C# naming or API adaptation | `IS-003` |
| `IMP-04` scan directory | Preserved with C# naming or API adaptation | `IS-001` |
| `IMP-05` import type field | Preserved with C# naming or API adaptation | `IS-004` |
| `IMP-06` path field | Preserved with C# naming or API adaptation | `IS-005` |
| `IMP-07` timing info | Preserved with C# naming or API adaptation | `ToolResult` assertion in scanner tests |
| `IMP-08` multiple files in one scan | Preserved with C# naming or API adaptation | `IS-002` |
| exclude directories absent from QmlTS numbering | Missing from QmlTS but required by C# test-spec | `IS-006` |

### `qmldom.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `DOM-01` dump DOM JSON | Preserved with C# naming or API adaptation | `QD-001` |
| `DOM-02` dump AST JSON | Preserved with C# naming or API adaptation | `QD-002` |
| `DOM-03` syntax error handled gracefully | Preserved with C# naming or API adaptation | Edge fixture subcase in `QD-001`/`QD-002` |
| `DOM-04` dump string | Preserved with C# naming or API adaptation | `QD-003` |
| `DOM-05` dump string AST mode | Preserved with C# naming or API adaptation | `QD-002`/`QD-003` combination |
| `DOM-06` DOM contains file info | Preserved with C# naming or API adaptation | `QD-001` fixture assertion |
| `DOM-07` timing info | Preserved with C# naming or API adaptation | `ToolResult` assertion in DOM tests |
| filter fields and no dependencies absent from QmlTS numbering | Missing from QmlTS but required by C# test-spec | `QD-004`, `QD-005` |

### `qml-runner.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `RUN-01` minimal app exits cleanly | Preserved with C# naming or API adaptation | `QR-001` |
| `RUN-02` timeout | Preserved with C# naming or API adaptation | `QR-003` |
| `RUN-03` run string with `Qt.quit()` | Preserved with C# naming or API adaptation | `QR-004` |
| `RUN-04` smoke test loaded | Preserved with C# naming or API adaptation | `QR-001` |
| `RUN-05` smoke test syntax error | Preserved with C# naming or API adaptation | `QR-002` |
| `RUN-06` list configs | Preserved with C# naming or API adaptation | `QR-007` |
| `RUN-07` timeout but loaded | Preserved with C# naming or API adaptation | `QR-003`/`QR-001` stable-period semantics |
| `RUN-08` command and timing | Preserved with C# naming or API adaptation | `ToolResult` assertion in runner tests |
| app type and runtime stderr parsing absent from QmlTS numbering | Missing from QmlTS but required by C# test-spec | `QR-005`, `QR-006` |

### `rcc.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `RCC-01` create QRC XML | Preserved with C# naming or API adaptation | `RC-004` |
| `RCC-02` default prefix | Preserved with C# naming or API adaptation | `RC-004` subcase |
| `RCC-02b` XML escaping | Preserved with C# naming or API adaptation | `RC-004` subcase |
| `RCC-03` compile to C++ | Preserved with C# naming or API adaptation | `RC-001` |
| `RCC-04` compile binary | Preserved with C# naming or API adaptation | `RC-005` |
| `RCC-05` list entries | Preserved with C# naming or API adaptation | `RC-002` |
| `RCC-06` list mappings | Preserved with C# naming or API adaptation | `RC-003` |
| `RCC-07` project QRC generation | Preserved with C# naming or API adaptation | `RC-004` |
| `RCC-08` no-compress | Preserved with C# naming or API adaptation | `RC-006` |
| `RCC-09` Python generator | Preserved with C# naming or API adaptation | `RC-007` |
| `RCC-10` timing info | Preserved with C# naming or API adaptation | `ToolResult` assertion in RCC tests |

### `qmltyperegistrar.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `REG-01` register with MOC JSON | Missing from current C# test-spec and proposed as an added test | `REG-001` |
| `REG-02` `--jsroot` mode | Missing from current C# test-spec and proposed as an added test | `REG-002`, unless absent from QmlSharp API |
| `REG-03` generate `.qmltypes` helper | Missing from current C# test-spec and proposed as an added test | `REG-003`, if helper exists |
| `REG-04` timing and command | Missing from current C# test-spec and proposed as an added test | `REG-004` |
| `REG-05` namespace option | Missing from current C# test-spec and proposed as an added test | `REG-005` |
| `REG-06` invalid MOC JSON graceful failure | Missing from current C# test-spec and proposed as an added test | `REG-006` |

### `quality-gate.test.ts`

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `QG-01` syntax valid | Preserved with C# naming or API adaptation | `QG-001` |
| `QG-02` syntax error | Preserved with C# naming or API adaptation | `QG-002` |
| `QG-03` lint valid | Preserved with C# naming or API adaptation | `QG-003` |
| `QG-04` lint type error | Preserved with C# naming or API adaptation | `QG-004` |
| `QG-05` compile result | Preserved with C# naming or API adaptation | `QG-005` |
| `QG-06` syntax error skips later stages | Preserved with C# naming or API adaptation | `QG-010` |
| `QG-07` string input | Preserved with C# naming or API adaptation | `QG-007` |
| `QG-08` batch files | Preserved with C# naming or API adaptation | `QG-008` |
| `QG-09` progress callback | Preserved with C# naming or API adaptation | `QG-009` |
| `QG-10` lint failure skips compile | Preserved with C# naming or API adaptation | `QG-010` |
| `QG-11` duration | Preserved with C# naming or API adaptation | `QG-011` |
| `QG-12` full includes smoke test | Preserved with C# naming or API adaptation | `QG-006` |
| `QG-13` full syntax failure skips smoke | Preserved with C# naming or API adaptation | `QG-010` |

### `factories.test.ts`

Factory helpers are not currently part of `ApiDesign/04-qt-tools/API-Design.md`. If Step
04.01 introduces factory or dependency-injection helpers, add smoke tests adapted from
these cases. Otherwise they are not part of the 112-test target.

| QmlTS case | Classification | QmlSharp mapping |
|---|---|---|
| `FAC-01` create diagnostic parser without Qt | Missing from current C# test-spec and proposed as an added test | Add DI/factory smoke if factories exist |
| `FAC-02` create qmlformat | Missing from current C# test-spec and proposed as an added test | Add DI/factory smoke if factories exist |
| `FAC-03` create qmllint | Missing from current C# test-spec and proposed as an added test | Add DI/factory smoke if factories exist |
| `FAC-04` create quality gate | Missing from current C# test-spec and proposed as an added test | Add DI/factory smoke if factories exist |
| `FAC-05` create RCC pure XML helper | Missing from current C# test-spec and proposed as an added test | Add DI/factory smoke if factories exist |
| `FAC-06` create qml runner | Missing from current C# test-spec and proposed as an added test | Add DI/factory smoke if factories exist |
| `FAC-07` create tool runner | Missing from current C# test-spec and proposed as an added test | Add DI/factory smoke if factories exist |
| `FAC-08` qmlformat write defaults | Not applicable to QmlSharp | No `writeDefaults` API in current design |
| `FAC-09` qmllint write defaults | Not applicable to QmlSharp | No `writeDefaults` API in current design |
| `FAC-10` qmllint parse JSON helper | Missing from current C# test-spec and proposed as an added test | Add DI/factory smoke if factories exist; parser behavior already covered by `DP-004` |

## Final-Gate Impact

This step enables later final-gate traceability by making the 04-qt-tools module count
mechanical before product code exists. It also prevents `qmltyperegistrar` from silently
falling out of the toolchain, which would later affect generated `.qmltypes`, module
metadata, and `INT-1-04`/`INT-3-03` style validation.
