# ADR-009: Real-Time Communication / Unified Hub Extracted to IndFusion.Ember

**Date**: 2026-06-07
**Status**: Accepted
**Deciders**: Development Team
**Tags**: signalr, real-time-communication, nuget, repo-structure, extraction

## Context and Problem Statement

Two project trees lived at the repository root and were **not referenced** by the
main solution (`ExxerCube.Prisma.sln`) or any production/test source:

- `ExxerAICode/` — `ExxerAI.RealTimeCommunication` (+ Tests), the SignalR/real-time
  communication abstraction and adapters (see ADR-001, ADR-017).
- `UnifiedHub/` — `ExxerAI.RealTimeCommunication.Client/Server`,
  `ExxerCube.Prisma.SignalR.Abstractions` (+ Tests), its own `.sln`, and
  `NUGET-PACKAGING.md`.

These were always intended to become a standalone, reusable NuGet package. That
extraction has since **happened**: the code was lifted out, refactored, and
published as the **`IndFusion.Ember`** package.

## Decision

Remove `ExxerAICode/` and `UnifiedHub/` from this repository. The canonical,
maintained home for this functionality is now `IndFusion.Ember`.

- **Repo**: https://github.com/hebelmx/IndFusion.Ember (branch `katV`)
- **Local**: `E:\Dynamic\IndFusion\IndFusion.Ember\IndFusion.Ember`
- **Mapping**: namespaces evolved from `ExxerAI.RealTimeCommunication.*` /
  `ExxerCube.Prisma.SignalR.Abstractions.*` to `IndFusion.Ember.Abstractions.*`
  (e.g. the hub abstraction is now `IExxerHub` / `ExxerHub`).

## Rationale

1. **Already superseded** — `IndFusion.Ember` is the evolved, published successor;
   the in-repo copies are stale predecessors.
2. **Unreferenced** — nothing in Prisma consumes them (verified: zero references
   to `RealTimeCommunication` / `SignalR.Abstractions` in `Prisma/Code/Src`).
3. **Avoid future "digital archaeology"** — keeping dead duplicates invites
   confusion about which copy is authoritative.

## Consequences

- If Prisma needs real-time communication, it should reference the
  `IndFusion.Ember` NuGet package rather than reviving the in-repo code.
- History is preserved: the removed files remain recoverable from git history
  prior to the `chore/repo-reorg` cleanup.
- A parallel drifted clone (`github.com/hebelmx/Veriqan`) still contains the old
  in-tree copies; that repo is a separate snapshot and out of scope here.

## Related

- ADR-001 (SignalR Unified Hub Abstraction)
- ADR-017 (Unified Hub Abstraction — Hexagonal Architecture) *(removed with `ExxerAICode/`; preserved in history)*
