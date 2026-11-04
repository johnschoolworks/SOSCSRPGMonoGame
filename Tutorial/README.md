# Missing SOSCSRPG Tutorial Source Projects

These nine .NET 10 console projects were reconstructed from the C# code blocks in the supplied tutorial Markdown documents.

Chapters: 001–007, 012, 014.

Each directory contains its source files, `.csproj`, and original `TUTORIAL.md`.

## Build on Ubuntu

```bash
cd Tutorial001
env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build
env -u DOTNET_ROOT /usr/bin/dotnet run
```

Repeat for each chapter. Tutorial 012 and 014 may run interactively.

## Verification status

Source files were extracted from the teaching material. The .NET SDK is not installed in the packaging environment, so compilation and gameplay have **not** been verified. Treat this as a source reconstruction candidate, not a certified release. Compare failures against the corresponding tutorial before committing.
