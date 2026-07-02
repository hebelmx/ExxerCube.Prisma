---
title: 'GH#32 — Orphaned AddUnifiedMetadataRecords migration (no Designer) → UnifiedMetadataRecords table never created'
type: 'bugfix'
created: '2026-07-02'
status: 'done'
baseline_commit: 'a694f516'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `20260613200000_AddUnifiedMetadataRecords.cs` shipped with **no `.Designer.cs`**. The
`[DbContext]`/`[Migration]` attributes live in the Designer partial, so without it the migration carries
no `[Migration]` attribute, is invisible to the `PrismaDbContext` migrations assembly, and is never
applied — the `UnifiedMetadataRecords` table (used on-demand by `ManualReviewerService` /
`DecisionLogicService` via `IUnifiedMetadataStore`) is missing on any DB built by `MigrateAsync`.
(Discovered while wiring GH#26's startup migration: history had 8 migrations, not this one.)

**Approach:** Retire the orphan and re-author it as a **terminal** migration
(`20260702120000_AddUnifiedMetadataRecords`) whose `BuildTargetModel` copies the current terminal
migration's Designer — which is byte-identical to `PrismaDbContextModelSnapshot` and already includes
the entity. A terminal migration's target model == the snapshot == the current model, so it is fully
design-time consistent, and `MigrateAsync` creates the table on any DB lacking it. The orphan was never
applied anywhere (never in any `__EFMigrationsHistory`), so deleting it is safe.

## Boundaries & Constraints

**Always:** Keep the terminal migration's Designer == current snapshot (copied from
`20260624120000_WidenAuditErrorMessageColumn.Designer.cs`, verified byte-identical to the snapshot body).
`Up()` = `CreateTable UnifiedMetadataRecords` (+ `IX_..._UpdatedAt`); `Down()` = `DropTable`.

**Never:** Do NOT hand-craft a bespoke mid-history snapshot (corrupts future `migrations add` diffs). Do
NOT edit `PrismaDbContextModelSnapshot.cs` — it already contains the entity and is unchanged.

## I/O & Edge-Case Matrix

| Scenario | State | Expected |
|----------|-------|----------|
| Container DB (post-GH#26) | 8 migrations, table missing | terminal migration applies → table created |
| Fresh DB | none | full chain incl. terminal migration → table present |
| Healthy DB (re-boot) | terminal already applied | no-op |
| Future `migrations add` | model == snapshot | empty diff (correct) — snapshot unchanged |

</frozen-after-approval>

## Code Map

- DELETE `Migrations/20260613200000_AddUnifiedMetadataRecords.cs` (orphan, no Designer).
- ADD `Migrations/20260702120000_AddUnifiedMetadataRecords.cs` — terminal migration Up()/Down().
- ADD `Migrations/20260702120000_AddUnifiedMetadataRecords.Designer.cs` — copied from #9's Designer, class + `[Migration]` id retargeted.
- `PrismaDbContextModelSnapshot.cs` — unchanged (already has the entity).

## Tasks & Acceptance

- [x] Delete orphan; add terminal migration `.cs` + `.Designer.cs`.

**Acceptance / Verified 2026-07-02 (live container):** build 0/0; on web-ui recreate the startup runner
logs "PrismaDbContext migrations applied successfully"; `Prisma.sys.tables` now has
`UnifiedMetadataRecords`; `__EFMigrationsHistory` contains `20260702120000_AddUnifiedMetadataRecords`.
