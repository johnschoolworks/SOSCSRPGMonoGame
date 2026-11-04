# Release checklist — v1.0.0

- [ ] `dotnet --info` shows .NET SDK 10.x.
- [ ] `./scripts/verify.sh` passes all 10 tests.
- [ ] `./scripts/release.sh` creates a Linux x64 archive.
- [ ] Run the published app and finish the Spider Silk quest.
- [ ] Confirm GitHub Actions succeeds after pushing.
- [ ] Review attribution, repository name, and licensing before publication.
- [ ] Tag and publish v1.0.0 only after validation.

Commands:

```bash
git init
git add .
git commit -m "Release SOSCSRPG terminal tutorial v1.0.0"
git tag v1.0.0
# Configure your remote before pushing:
git remote add origin git@github.com:YOUR-ACCOUNT/YOUR-REPO.git
git push -u origin HEAD
git push origin v1.0.0
```

Do not run `git remote add origin` if your repository already has a remote. A license is deliberately not assigned automatically; choose one only after reviewing applicable third-party code and your publishing intentions.
