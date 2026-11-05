# Phase 8 — SOSCSRPG Completion Audit

## Scope and evidence

Source reviewed: Phase 7 local project, including `Engine/`, `Game1.cs`, `SoscMonoGame.EngineChecks/`, and `run-phase7.sh`. This is a **source-level audit**, not a certified equivalence review of Scott Lilly's upstream SOSCSRPG revision. Upstream source was not pinned or diffed in this audit. This container has no `dotnet` executable, so compilation and runtime verification must be done on the user's Ubuntu machine.

## Findings

| ID | Finding | Status | Severity |
|---|---|---|---|
| A01 | Core engine is plain C# without MonoGame/Myra dependencies; EngineChecks compiles engine sources independently. | Source verified | Pass |
| A02 | Models, Factories, Actions and GameSession are separated. | Source verified | Pass |
| A03 | Game1.cs still constructs the complete Myra widget tree and handles events; dedicated UI screen/presenter not yet extracted. | Open | Medium |
| A04 | Phase 7 EngineChecks previously checked only the location ID after a blocked movement, so it could pass without proving movement was rejected. Phase 8 checks the `Move` return value explicitly. | Test corrected | Medium |
| A05 | Phase 7 Save/Load validates schema version, player stats, item IDs, quests, monster and merchant stock. | Source verified | Pass |
| A06 | Save/Load does not persist the PRNG internal state; replay is not deterministic after loading. | Open / documented limitation | Low |
| A07 | Save writes `.tmp` then replaces destination, but there is no multi-process locking or recovery for orphaned temporary files. | Open / documented limitation | Low |
| A08 | UI refreshes on `StateChanged`, but domain models are publicly mutable and do not notify the session for every direct property mutation. | Open | Medium |
| A09 | Phase 8 adds tests for movement rejection, healing, trading safety, state notifications and malformed save fields. | Added / execution pending | Medium |
| A10 | No authoritative upstream SOSCSRPG commit, curriculum lesson inventory or behavior matrix has been pinned for strict 100% parity. | Open | High |
| A11 | Build and graphical smoke test on Ubuntu 26.04 + .NET 10 + MonoGame + Myra are pending. | Pending user run | High |

## Completion criteria

1. `run-phase8.sh` completes restore, build and EngineChecks with no failures.
2. Game opens under X11, UI renders correctly, and user can move, fight, heal, trade, complete both quests, save and load.
3. A specific upstream SOSCSRPG version/commit is chosen, with a lesson-by-lesson comparison matrix.
4. A03/A08 are resolved if strict UI-engine decoupling is required.
5. Release signoff records both automated and manual results.

**Verdict: not yet certified as a complete SOSCSRPG port.** Phase 8 provides an audit baseline and stronger regression checks; it does not assert 100% parity.
