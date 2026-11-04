# SOSCSRPG v1.0.0-rc1 Release Checklist

## Local Validation

- [x] Verify .NET SDK 10.
- [x] Build Tutorial 020 in Release configuration.
- [x] Pass all 25 regression tests.
- [x] Publish Linux x64 application.
- [x] Generate Linux release archive.
- [x] Repackage Tutorials 018–020.
- [x] Validate all three ZIP archives.
- [x] Stage Tutorial 020 README.
- [x] Validate staged Git changes with configured whitespace rules.

## Pending Validation

- [ ] Verify all 20 tutorials build from current staged source.
- [ ] Run the published Linux application and complete the Spider Silk quest.
- [ ] Confirm ZIP contents match the intended staged source.
- [ ] Review licensing and third-party attribution.
- [ ] Commit release candidate changes.
- [ ] Push release branch to GitHub.
- [ ] Confirm GitHub Actions passes.
- [ ] Create and push tag v1.0.0-rc1.
- [ ] Publish GitHub prerelease with verified archives.

## Release Artifacts

- SOSCSRPG-CSharp-Tutorial018-RC1.zip
- SOSCSRPG-CSharp-Tutorial019-RC1.zip
- SOSCSRPG-CSharp-Tutorial020-RC1.zip
- soscsrpg-terminal-v1.0.0-rc1-linux-x64.tar.gz

## Release Policy

Do not publish the final v1.0.0 tag during RC1 validation.

Release candidate publication requires successful local validation,
GitHub Actions verification, and release artifact inspection.
