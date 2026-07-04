# Releasing InfoOrganizer

1. Optionally bump `<Version>` in `Directory.Build.props`.
2. Commit the version bump and any release changes.
3. Create a version tag: `git tag v0.x.y`.
4. Push the branch and tags: `git push; git push --tags` (PowerShell) or `git push && git push --tags` (bash).
5. GitHub Actions runs tests on every push and pull request.
6. Tags matching `v*` run the release workflow on `windows-latest`.
7. The workflow strips the leading `v` and uses that version for publish and installer metadata.
8. It publishes the self-contained `win-x64` single-file app.
9. It uploads `InfoOrganizer-<version>-win-x64-portable.zip`.
10. It compiles and uploads `InfoOrganizer-<version>-Setup.exe`.
11. Find the generated release and attached files under GitHub Releases.
