# Changelog

## [1.0.0-rc1] — 2026-11-04

### Added

- Complete SOSCSRPG C# tutorial series (Tutorial 001–020).
- Modular RPG engine with terminal-based interface.
- World navigation, quests, dialogue, combat, inventory, and trading.
- Save/load support with validation.
- 25 automated regression tests.
- GitHub Actions build, regression test, and Linux publish workflow.
- Linux x64 release packaging scripts.
- Release candidate archives for Tutorials 018–020.

### Changed

- Refactored Tutorials 018–020 into maintainable source structures.
- Separated RPG engine, terminal application, and regression tests.
- Removed previously tracked build artifacts from Git.
- Replaced legacy tutorial ZIP distribution with source directories.

### Validation

- .NET 10 SDK build: PASS
- Tutorial 020 regression tests: 25 passed, 0 failed
- Linux x64 publish: PASS
- Tutorial 018–020 ZIP integrity: PASS

### Scope

This release candidate covers the educational C# terminal RPG.

The repository's MonoGame/Myra graphical application is a separate
project and is not included in these terminal tutorial release packages.
