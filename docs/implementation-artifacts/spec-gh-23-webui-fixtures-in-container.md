---
title: 'GH#23 — Ship PRP1 fixtures into the Web.UI image so document-processing fixtures load'
type: 'bugfix'
created: '2026-07-01'
status: 'done'
baseline_commit: '65d82dc66f40ae3f85d4d6555ce892ecd26d0bf5'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** On `/document-processing` in the containerized Web.UI, every fixture button fails (`Error loading fixture: Failed to load fixture: 222AAA-44444444442025.xml`). The `Prisma/Fixtures/` tree is excluded by `.dockerignore` and never copied into the image, so `FixtureFinder` finds no `Fixtures/` dir at runtime. Two buttons additionally reference names that don't exist on a case-sensitive (Linux) filesystem.

**Approach:** Bake the PRP1 fixture set into the runtime image (un-ignore + `COPY`) so `FixtureFinder` resolves `/app/Fixtures/PRP1` with no code change; and fix the two button references that point at non-existent/wrong-case files.

## Boundaries & Constraints

**Always:** Keep the fix infra-first (`.dockerignore` + `Dockerfile`); land fixtures at `/app/Fixtures/PRP1` so both `FixtureFinder` strategies (CWD and `AppDomain.BaseDirectory`, both `/app`) resolve them. Only re-include the specific `Prisma/Fixtures/PRP1/` subtree in `.dockerignore`, not all `Fixtures/`.

**Ask First:** RESOLVED (hebelmx, 2026-07-01): the *original* fixtures use the 7-digit `555CCC-6666662025` stem (matched `.xml` + `.pdf` pair). Both `555CCC` buttons (XML and PDF) point to that 7-digit original pair.

**Never:** Do NOT modify `ExxerCube.Prisma.CrossConcerns/FixtureFinder.cs` (shared by ~10 test projects). Do NOT un-ignore the whole `**/Fixtures/` tree (bloats context/image). Do NOT rename or move the fixture files on disk.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Load present fixture | Click `222AAA-44444444442025.xml` in container | Bytes loaded; processing proceeds | N/A |
| Wrong-case PDF | Click `333CCC` PDF button on Linux | Button targets `333ccc-6666666662025.pdf`; loads | Was: FileNotFound |
| 555CCC pair | Click `555CCC` XML/PDF buttons | Both target 7-digit `555CCC-6666662025.{xml,pdf}`; load | Was: XML FileNotFound |
| Fixtures dir absent | Misbuilt image without COPY | `DirectoryNotFoundException` logged clearly | Surfaced, not swallowed |

</frozen-after-approval>

## Code Map

- `.dockerignore` -- line 12 `**/Fixtures/` excludes fixtures from build context; needs negation for PRP1.
- `Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/Dockerfile` -- runtime stage; add `COPY` of PRP1 fixtures after publish copy.
- `Prisma/Fixtures/PRP1/` -- canonical fixture source (49 files); COPY source.
- `.../Web.UI/Services/FixtureLoaderService.cs` -- loader; no change needed (relies on `FixtureFinder`).
- `.../Web.UI/Components/DocumentProcessing/PdfProcessingSection.razor` -- `333CCC` PDF button (wrong case).
- `.../Web.UI/Components/DocumentProcessing/XmlProcessingSection.razor` -- `555CCC` XML button (missing file).

## Tasks & Acceptance

**Execution:**
- [x] `.dockerignore` -- after `**/Fixtures/`, add negations re-including `Prisma/Fixtures/PRP1/` and `Prisma/Fixtures/PRP1/**` -- put the demo fixtures back into the build context.
- [x] `Web.UI/Dockerfile` -- in the runtime stage after `COPY --from=build /app/publish .`, add `COPY Prisma/Fixtures/PRP1/ ./Fixtures/PRP1/` -- fixtures land at `/app/Fixtures/PRP1`.
- [x] `PdfProcessingSection.razor` -- change the `333CCC-6666666662025.pdf` button target to `333ccc-6666666662025.pdf` -- match the actual (lowercase) file; fixes Linux case-sensitivity.
- [x] `XmlProcessingSection.razor` -- change the `555CCC-6666666662025.xml` button target to `555CCC-6666662025.xml` -- point to the original 7-digit fixture that exists.
- [x] `PdfProcessingSection.razor` -- change the `555CCC-6666666662025.pdf` button target to `555CCC-6666662025.pdf` -- align to the same 7-digit original pair.

**Acceptance Criteria:**
- Given the rebuilt Web.UI image, when `docker exec <web-ui> find /app/Fixtures/PRP1 -name '222AAA-44444444442025.xml'`, then the file is found.
- Given the running container, when each remaining fixture button is clicked, then the fixture loads (no `Failed to load fixture` snackbar).
- Given the build, when `dotnet build` runs, then it succeeds with 0 warnings/errors (warnings-as-errors).

## Verification

**Commands:**
- `docker compose -p prisma -f docker-compose.dev.yml -f docker-compose.staging.override.yml build web-ui` -- expected: build succeeds.
- `docker compose -p prisma -f docker-compose.dev.yml -f docker-compose.staging.override.yml up -d web-ui` then `docker exec $(docker compose -p prisma ps -q web-ui) sh -lc 'ls /app/Fixtures/PRP1 | head'` -- expected: fixture files listed.

**Manual checks:**
- Open `http://localhost:8085/document-processing`, click each XML and PDF fixture button -- expected: loads without the error snackbar; the two previously-broken buttons now work (or the 555CCC XML button reflects the Ask-First decision).

## Suggested Review Order

**Build-context provisioning (the core fix)**

- Entry point: re-includes ONLY `PRP1` into the build context; the `Prisma/Fixtures/*` line re-excludes 51M of siblings (proven via probe build).
  [`.dockerignore:19`](../../.dockerignore#L19)

- Bakes the fixture set into the runtime image where `FixtureFinder` looks (`/app/Fixtures/PRP1`); needs the ignore exception above to have a source.
  [`Dockerfile:59`](../../Prisma/Code/Src/CSharp/07%20UI/UI/ExxerCube.Prisma.Web.UI/Dockerfile#L59)

**Button-target corrections (Linux case-sensitivity + original 7-digit pair)**

- Lowercase `333ccc` so `File.Exists` (case-sensitive on Linux) resolves the actual file.
  [`PdfProcessingSection.razor:38`](../../Prisma/Code/Src/CSharp/07%20UI/UI/ExxerCube.Prisma.Web.UI/Components/DocumentProcessing/PdfProcessingSection.razor#L38)

- Point 555CCC PDF at the original 7-digit fixture pair.
  [`PdfProcessingSection.razor:45`](../../Prisma/Code/Src/CSharp/07%20UI/UI/ExxerCube.Prisma.Web.UI/Components/DocumentProcessing/PdfProcessingSection.razor#L45)

- Point 555CCC XML at the original 7-digit fixture that exists on disk.
  [`XmlProcessingSection.razor:65`](../../Prisma/Code/Src/CSharp/07%20UI/UI/ExxerCube.Prisma.Web.UI/Components/DocumentProcessing/XmlProcessingSection.razor#L65)
