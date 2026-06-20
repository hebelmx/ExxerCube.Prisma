# ExxerCube.Prisma QA Harness

A first-class, compiled QA Harness for the ExxerCube.Prisma product. It encapsulates
the product-specific knowledge needed to **provision** an environment, **start** the
application, **run** representative workflows, **validate** outputs, **collect**
evidence, **trace** to requirements, and **report** — so an independent QA agent can
evaluate the product without knowing its internals.

> **The harness makes no PASS/FAIL or quality judgment.** It produces infrastructure,
> validation primitives, evidence, and structured reports. Verdicts are rendered by the
> independent Phase-2 QA review.

## Phase-1 deliverables (this is the index)
1. **Architecture** — [`ARCHITECTURE.md`](./ARCHITECTURE.md) (components, extension points, execution model, data flow).
2. **Implementation** — the three projects below (built, tested, live-proven).
3. **Capability Inventory** — [`CAPABILITY-INVENTORY.md`](./CAPABILITY-INVENTORY.md) (what it provides + maturity).
4. **Limitations** — [`LIMITATIONS.md`](./LIMITATIONS.md) (gates, unsupported scenarios, future work).

Execution tracker: [`QA-HARNESS-TRACKER.md`](./QA-HARNESS-TRACKER.md).

## Projects
```
Prisma/Code/Src/CSharp/09 Testing/02 QaHarness/
  ExxerCube.Prisma.QaHarness/        Core library (the harness API + implementations)
  ExxerCube.Prisma.QaHarness.Cli/    Shell front-end (run workflows, emit reports)
  ExxerCube.Prisma.QaHarness.Tests/  Self-test suite (116 tests; proves the harness, not the product)
```

## Programmatic use
```csharp
var services = new ServiceCollection();
services.AddLogging();
services.AddQaHarness();                       // registers provisioner, host controllers,
                                               // 6 validators, 6 workflows, runner, report writers
// extend BEFORE BuildServiceProvider (see LIMITATIONS §15 — additions after build need a rebuild):
//   .AddWorkflow<MyWorkflow>() / .AddValidator<TSubject,MyValidator>() / .AddReportWriter<MyWriter>()
var sp = services.BuildServiceProvider();

var provisioner = sp.GetRequiredService<IEnvironmentProvisioner>();
var runner      = sp.GetRequiredService<IWorkflowRunner>();
var writer      = sp.GetRequiredService<IEnumerable<IReportWriter>>().First(w => w.Format == "markdown");
// provision -> start host -> runner.RunAsync(workflow, context) -> writer.WriteAsync(summary, path)
```

## Shell use (CLI)
```bash
DOTNET="C:/Program Files/dotnet/dotnet.exe"
CLI="Prisma/Code/Src/CSharp/09 Testing/02 QaHarness/ExxerCube.Prisma.QaHarness.Cli/ExxerCube.Prisma.QaHarness.Cli.csproj"
REPO="E:/Dynamic/IndFusion/ExxerCube.Prisma/ExxerCube.Prisma"

"$DOTNET" run --project "$CLI" -- --help
"$DOTNET" run --project "$CLI" -- --provision-only --repo-root "$REPO"
"$DOTNET" run --project "$CLI" -- --workflow HealthCheckWorkflow --repo-root "$REPO" --report-format md
"$DOTNET" run --project "$CLI" -- --all-workflows --repo-root "$REPO" --report-format md,json
```
Flags: `--workflow <name>` · `--all-workflows` · `--provision-only` · `--report-format <md|html|json>` ·
`--output-dir <path>` (default `docs/qa/harness/runs/<runId>/`) · `--repo-root <path>` (or `PRISMA_REPO_ROOT`) ·
`--skip-docker` · `--no-seed-corpus` · `--help`. Exit code 0 = run completed (NOT a QA verdict).

## Running the self-tests
```bash
DOTNET="C:/Program Files/dotnet/dotnet.exe"
TESTS="Prisma/Code/Src/CSharp/09 Testing/02 QaHarness/ExxerCube.Prisma.QaHarness.Tests/ExxerCube.Prisma.QaHarness.Tests.csproj"

"$DOTNET" test "$TESTS"                                              # all 116 (fast + integration; Docker for integration)
"$DOTNET" test "$TESTS" --filter-query "/*/*/HarnessIntegrationTests/*"   # the live proof only (~3 min, needs Docker)
```
> MTP runner: do **not** pass `--nologo`. Filter syntax is `/Assembly/Namespace/Class/Method`, not `[category=fast]`.

## Environment notes
- Docker (29.5+) enables container provisioning + the live integration test.
- The SIARA corpus is absent (see Limitations §5) — ingestion/export workflows abort `CorpusAbsent` until restored.
- `playwright install chromium` is required for the browser workflows.
