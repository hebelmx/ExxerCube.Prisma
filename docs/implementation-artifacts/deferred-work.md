# Deferred work

Findings surfaced during reviews that are pre-existing / out of scope for the
triggering story. Not caused by the change under review.

## From GH#23 review (2026-07-01) — spec-gh-23-webui-fixtures-in-container

- **[low] Coarse missing-directory error path.** If the PRP1 fixtures fail to land,
  `FixtureFinder.FindFixturesPath` throws `DirectoryNotFoundException`, which the razor
  `LoadFixture` handlers do NOT catch in their `catch (FileNotFoundException)` branch —
  it falls through to the generic `catch (Exception)` and shows "Error processing PDF/XML: …"
  instead of a clear "fixture directory not found". Degrades gracefully; pre-existing.
  Files: `PdfProcessingSection.razor` / `XmlProcessingSection.razor` LoadFixture catch blocks.
- **[low] Fixture-name maintenance trap.** The old 10-digit `555CCC-6666666662025.pdf`
  remains in `Prisma/Fixtures/PRP1` unreferenced, and near-identical stems now coexist
  (`555CCC-6666662025.*` 7-digit vs `555CCC-66666662025.*` 8-digit vs the former 10-digit).
  Future mis-wire hazard; corpus-hygiene cleanup, owner-gated.
- **[low] CWD assumption in FixtureFinder.** Strategy 1 (`Directory.GetCurrentDirectory()`)
  equals `/app` only because Docker defaults CWD to WORKDIR; a compose `working_dir:` /
  k8s `workingDir:` override would break it. Currently robust by redundancy (Strategy 2 =
  `AppDomain.BaseDirectory` = `/app`). Noting only.
