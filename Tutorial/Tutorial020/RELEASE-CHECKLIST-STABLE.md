# SOSCSRPG v1.0.0 Stable Release Checklist

## Build and Test

- [x] Pin MonoGame DesktopGL to 3.8.6.1.
- [x] Restrict root project compilation scope.
- [x] Build MonoGame project in Release configuration.
- [x] Launch MonoGame window on Linux.
- [x] Pass Tutorial020 regression tests (25/25).
- [x] Build Tutorial020 Release application.

## Packaging

- [x] Generate Linux x64 framework-dependent archive.
- [x] Include repository LICENSE.
- [x] Include THIRD-PARTY-NOTICES.md.
- [x] Inspect archive contents.
- [x] Calculate SHA-256 checksum.
- [x] Verify extracted archive on Linux.
- [ ] Complete third-party license review.
- [x] Verify release archives match final commit.

## Repository and Documentation

- [x] Update MonoGame dependency version in README.
- [ ] Update stable release documentation.
- [x] Run full tutorial audit (20/20 builds; interactive coverage partial).
- [ ] Review Git diff and working tree.
- [ ] Commit stable release changes.

## GitHub Release

- [ ] Push stable release branch.
- [ ] Open and merge pull request after CI passes.
- [ ] Verify main branch CI.
- [ ] Create annotated v1.0.0 tag from verified main.
- [ ] Publish GitHub stable release.
- [ ] Upload verified artifacts and checksums.
