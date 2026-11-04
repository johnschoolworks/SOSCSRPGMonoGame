# SOSCSRPG — Tutorial 020

## Release Candidate v1.0.0-rc1

Tutorial 020 is the final stage of the SOSCSRPG C# tutorial series.

It demonstrates a modular, terminal-based role-playing game developed with C# and .NET 10.

This project is independent of the repository's MonoGame/Myra graphical application.

## Features

- Reusable RPG engine
- Terminal-based gameplay
- Player movement and world navigation
- NPC dialogue and quest interactions
- Turn-based combat
- Weapons, inventory, and equipment
- Shops and trading
- Loot and rewards
- Save/load persistence
- Automated regression testing

## Requirements

- Linux (Ubuntu recommended)
- .NET 10 SDK
- Visual Studio Code or Vim
- Bash

## Project Structure

```text
Tutorial020/
├── RpgEngine/
│   ├── Combat/
│   ├── Dialogue/
│   ├── Economy/
│   ├── Engine/
│   ├── Factories/
│   ├── Models/
│   ├── Persistence/
│   ├── World/
│   └── RpgEngine.csproj
├── TerminalRpg/
│   ├── ConsoleUI/
│   ├── Program.cs
│   └── TerminalRpg.csproj
├── RegressionTests/
│   ├── Program.cs
│   └── RegressionTests.csproj
├── scripts/
│   ├── verify.sh
│   └── release.sh
├── Directory.Build.props
├── CHANGELOG.md
└── RELEASE-CHECKLIST.md
```

## Build

From the repository root:

```bash
dotnet build Tutorial/Tutorial020/TerminalRpg/TerminalRpg.csproj -c Release
```

## Run

```bash
dotnet run --project Tutorial/Tutorial020/TerminalRpg/TerminalRpg.csproj -c Release
```

## Regression Tests

Run the automated verification script:

```bash
bash Tutorial/Tutorial020/scripts/verify.sh
```

The RC1 verification run completed with 25 passing regression tests and zero failures.

## Build a Linux Release

```bash
bash Tutorial/Tutorial020/scripts/release.sh
```

The script verifies the project, publishes a framework-dependent Linux x64 application, and creates:

```text
Tutorial/Tutorial020/artifacts/soscsrpg-terminal-v1.0.0-rc1-linux-x64.tar.gz
```

The published application requires a compatible .NET runtime on the target machine.

## Continuous Integration

The repository includes a GitHub Actions workflow:

```text
.github/workflows/soscsrpg-ci.yml
```

It builds the project, runs regression tests, publishes the Linux x64 application, and uploads the build artifact.

## Release Status

Version: `1.0.0-rc1`

This is a release candidate intended for validation before the stable v1.0.0 release.

## License

Refer to the repository's license information, if provided.
