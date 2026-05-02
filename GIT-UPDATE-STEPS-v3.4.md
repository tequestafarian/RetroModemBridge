# Git update steps for v3.4

From the root of your RetroModemBridge repo:

```bat
git status
```

Copy the v3.4 files into your repo, then run:

```bat
git add .
git status
git commit -m "Release RetroModem Bridge v3.4 with local door games"
git tag v3.4
git push origin main
git push origin v3.4
```

If your branch is not named `main`, replace `main` with your branch name.

To build the Windows EXE before attaching it to a GitHub release:

```bat
publish-exe-v3.4.bat
```

Attach the published ZIP or EXE from:

```text
publish\v3.4\
```
