# SOSCSRPG C# Tutorial 020 — Final Integration and GitHub Release

**Series:** SOSCSRPG C# Tutorials (20/20)  
**Platform:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 019 — Testing, Refactoring, and Stabilization  
**Language:** English · **Version target:** 1.0.0

> This is an original educational adaptation inspired by [Scott Lilly's SOSCSRPG](https://github.com/ScottLilly/SOSCSRPG). It is not an official Scott Lilly release. This distribution is a **terminal RPG**, not a MonoGame + Myra GUI application.

## 1. Objectives

1. Retain the tested gameplay engine and terminal UI from Tutorial 019.
2. Automate builds and regression testing.
3. Produce a portable Linux x64, framework-dependent distribution.
4. Document versioning, release procedures, and limitations.
5. Prepare a stable boundary for future MonoGame + Myra integration.

## 2. Architecture

```text
Tutorial020/
├── RpgEngine/                  # UI-independent rules, world, combat, quests
│   ├── GameEngine.cs
│   └── RpgEngine.csproj
├── TerminalRpg/                # Terminal adapter
│   ├── Program.cs
│   └── TerminalRpg.csproj
├── RegressionTests/           # 10 deterministic tests, no test NuGet packages
│   ├── Program.cs
│   └── RegressionTests.csproj
├── .github/workflows/ci.yml   # GitHub Actions
├── scripts/verify.sh          # Restore, build, test
├── scripts/release.sh         # Verify, publish, package
├── Directory.Build.props      # Version and deterministic build properties
├── .gitignore
├── CHANGELOG.md
├── RELEASE-CHECKLIST.md
└── README.md
```

## 3. Requirements

Install the **.NET 10 SDK** on Ubuntu (using Microsoft's official Linux instructions) and optionally VS Code with the C# extension. Verify:

```bash
dotnet --info
code --version
```

The build uses the installed .NET SDK and does not depend on MonoGame, Myra, or an external test framework.

## 4. Open in VS Code

```bash
cd Tutorial020
code .
```

Use VS Code's integrated terminal for all following commands.

## 5. Run the game

```bash
dotnet restore TerminalRpg/TerminalRpg.csproj
dotnet run --project TerminalRpg/TerminalRpg.csproj
```

Available exploration commands: `help`, `look`, `go west`, `go east`, `go north`, `go south`, `talk`, `quest`, `inventory`, `status`, `fight`, `buy potion`, `use potion`, `quit`. During combat: `attack`, `defend`, `potion`, `flee`.

## 6. Automated tests

```bash
./scripts/verify.sh
```

This runs **10 regression tests**: initial state, invalid travel, connected world, quest acceptance, guaranteed Spider Silk, one-time quest rewards, merchant constraints, invalid combat actions, safe zones, and fleeing without loot.

The tests use `FixedRandomSource` to make combat deterministic and avoid flaky outcomes.

Expected final test summary:

```text
Results: 10 passed, 0 failed
Verification passed.
```

This is an **expected result**, not a claim that the tests were run in this authoring environment.

## 7. Gameplay acceptance test

1. `go west`, `talk` — accept the Spider Silk quest.
2. `go east`, `go east`, `go south` — enter the Spider Cave.
3. `fight`, then `attack` until the spider is defeated. Repeat for three victories.
4. `go north`, `go west`, `go west`, `talk` — turn in the quest.
5. `quest`, `status` — verify completion and one-time reward.

Three cave victories guarantee three Spider Silk. Completing the quest consumes the silk and awards 20 gold. If no other transactions occur, total gold is 29 (9 combat + 20 quest).

## 8. Publish a portable Linux build

```bash
./scripts/release.sh
```

Produces `artifacts/soscsrpg-terminal-v1.0.0-linux-x64.tar.gz`. The publish uses `--self-contained false`, so the target machine **must have the .NET 10 runtime** installed.

Test the published output:

```bash
dotnet artifacts/linux-x64/TerminalRpg.dll
```

The GitHub Actions workflow also builds, tests, publishes, and uploads a Linux x64 artifact on push and pull request.

## 9. GitHub release

Review `RELEASE-CHECKLIST.md`. Once the tests and acceptance walkthrough pass, commit the project, configure the GitHub remote, tag `v1.0.0`, and push the tag. On GitHub, create a release from that tag and attach the generated archive.

**Do not claim the release is validated until the commands have succeeded on your system or CI.**

## 10. Known limitations

- This release has **no MonoGame + Myra window**; only a terminal adapter is implemented.
- Save/load and JSON-defined world data are not implemented.
- Repeated combat encounters are possible in dangerous locations.
- The regression runner is a lightweight console executable, not xUnit or NUnit.
- The publish script targets Linux x64 only and is framework-dependent.
- A LICENSE file is not included: choose a license after reviewing the project's code provenance and your distribution goals.

## 11. Future expansion: MonoGame + Myra

Build a new `MonoGameFrontend` project referencing `RpgEngine`, then map UI buttons to engine actions and bind engine state to labels, inventory lists, and quest panels. Do not move game rules into UI event handlers. Add tests for state transitions and introduce serialization separately.

## 12. Series completion

Tutorial 020 concludes the **20-chapter SOSCSRPG C# educational series**. It is a source-and-release-workflow milestone, not a claim of feature parity with Scott Lilly's full SOSCSRPG project.

## Attribution

Educational adaptation inspired by Scott Lilly's [SOSCSRPG](https://github.com/ScottLilly/SOSCSRPG). Not affiliated with or endorsed by the original author.
