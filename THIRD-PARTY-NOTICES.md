# Third-Party Notices

This document identifies third-party projects and dependencies used by SOSCSRPGMonoGame.

## Original Educational Project

**Scott's Open-Source C# Role-Playing Game (SOSCSRPG)**

- Author: Scott Lilly
- Copyright: © 2012–2021 Scott Lilly
- License: MIT
- Source: https://github.com/ScottLilly/SOSCSRPG
- Licensing information: https://soscsrpg.com/licensing/

This repository contains educational implementations inspired by SOSCSRPG. Any portions derived from the original source remain subject to the original applicable copyright and license notices.

## MonoGame and Myra Dependencies

The following versions were resolved for the MonoGame graphical application targeting .NET 10.

| Package | Version | Declared License |
|---|---|---|
| MonoGame.Framework.DesktopGL | 3.8.6.1 | MS-PL |
| Myra | 1.6.8 | MIT |
| AssetManagementBase | 0.7.3 | MIT |
| Cyotek.Drawing.BitmapFont | 2.0.4 | MIT |
| FontStashSharp.Base | 1.2.9 | Zlib |
| FontStashSharp.MonoGame | 1.6.2 | Zlib |
| FontStashSharp.Rasterizers.StbTrueTypeSharp | 1.2.9 | Zlib |
| MonoGame.Library.OpenAL | 1.24.3.4 | Package license file |
| MonoGame.Library.SDL | 2.32.10.2 | Zlib |
| NVorbis | 0.10.4 | MIT |
| StbImageSharp | 2.30.16 | Unlicense OR MIT |
| StbTrueTypeSharp | 1.26.13 | Unlicense OR MIT |
| XNAssets.FontStashSharp.MonoGame | 0.8.9 | MIT |
| XNAssets.MonoGame | 0.8.9 | MIT |

## Native Library Notices

### SDL

- Package: MonoGame.Library.SDL 2.32.10.2
- Copyright: © 1997–2025 Sam Lantinga
- License: Zlib
- License file: `LICENSE.txt` in the NuGet package

### OpenAL

- Package: MonoGame.Library.OpenAL 1.24.3.4
- License file: `LICENSE` in the NuGet package

The package's license file identifies portions of the software distributed under BSD 3-Clause terms, including copyright notices for Archontis Politis, Anis A. Hireche, and Christopher Robinson.

This statement does not characterize the complete OpenAL package as exclusively BSD 3-Clause licensed.

### NVorbis

- Package: NVorbis 0.10.4
- Copyright: © 2020 Andrew Ward
- License: MIT
- License file: `LICENSE` in the NuGet package

## License Compliance

Third-party software remains subject to its respective license terms.

Package license declarations and relevant license files were inspected during the v1.0.0 release audit.

When redistributing third-party components, the applicable copyright notices, license texts, disclaimers, and attribution requirements must be preserved.

This inventory does not replace the original license documents or constitute a complete legal compliance determination.

## Release Packaging

The Tutorial020 Linux x64 archive is a framework-dependent .NET 10 application.

It includes:

- The published application files
- The repository `LICENSE`
- This `THIRD-PARTY-NOTICES.md`

The graphical MonoGame application and its NuGet dependency graph are separate from the Tutorial020 terminal release archive.
