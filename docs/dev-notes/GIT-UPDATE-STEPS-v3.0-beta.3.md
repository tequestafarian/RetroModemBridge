# How to update the GitHub repo with v3 Beta 3

These steps assume your Git repo is here:

```powershell
C:\apps\coco\RetroModem Bridge\RetroModemBridge-no-detect-source\RetroModemBridge
```

and this release package was extracted here:

```powershell
C:\apps\coco\RetroModem Bridge\RetroModemBridge-v3.0-beta.3-commercial-release
```

Adjust paths if yours are different.

---

## 1. Go to your repo

```powershell
cd "C:\apps\coco\RetroModem Bridge\RetroModemBridge-no-detect-source\RetroModemBridge"
git status
```

Make sure there is nothing important uncommitted.

---

## 2. Make a backup

```powershell
Copy-Item "C:\apps\coco\RetroModem Bridge\RetroModemBridge-no-detect-source\RetroModemBridge" "C:\apps\coco\RetroModem Bridge\RetroModemBridge-backup-before-v3-beta-3" -Recurse
```

---

## 3. Create a working branch

```powershell
git checkout -b v3-beta-3
```

If the branch already exists:

```powershell
git checkout v3-beta-3
```

---

## 4. Copy the new files into your repo

Copy the **contents** of the extracted `RetroModemBridge-v3-beta` folder into your Git repo.

Example:

```powershell
Copy-Item "C:\apps\coco\RetroModem Bridge\RetroModemBridge-v3.0-beta.3-commercial-release\RetroModemBridge-v3-beta\*" "C:\apps\coco\RetroModem Bridge\RetroModemBridge-no-detect-source\RetroModemBridge" -Recurse -Force
```

Your repo should still directly contain:

```text
RetroModemBridge.sln
RetroModemBridge\
README.md
README-v3-beta.md
RELEASE-NOTES-v3.0-beta.3.md
GITHUB-RELEASE-BODY-v3.0-beta.3.md
```

Do not create this accidentally:

```text
RetroModemBridge\
  RetroModemBridge-v3-beta\
    RetroModemBridge.sln
```

---

## 5. Build it

```powershell
dotnet build
```

or:

```powershell
.\publish-exe-v3-beta.ps1
```

Your published EXE should appear under:

```text
RetroModemBridge-v3-beta\publish\v3-beta\
```

---

## 6. Test before committing

From the Windows app:

```text
Start Bridge
Mirror
Import Guide
Update / Merge BBS Guide
Guide filter: New only
Test All Favorites
Profiles
Export Support Bundle
```

From the retro computer:

```text
AT
ATDT MENU
ATDT GUIDE
ATDT UPDATEGUIDE
ATDT NEWS
ATDT RANDOM
ATDT FAVORITES
```

Test Monthly and Daily updates:

```text
ATDT UPDATEGUIDE
1
ATDT UPDATEGUIDE
2
```

---

## 7. Commit the changes

```powershell
git status
git add .
git commit -m "Release v3 beta 3 local BBS guide browser and updater"
```

---

## 8. Push the branch

```powershell
git push -u origin v3-beta-3
```

---

## 9. Merge into main

```powershell
git checkout main
git pull origin main
git merge v3-beta-3
git push origin main
```

---

## 10. Create the release tag

```powershell
git tag v3.0-beta.3
git push origin v3.0-beta.3
```

If the tag already exists and you need to recreate it:

```powershell
git tag -d v3.0-beta.3
git push origin :refs/tags/v3.0-beta.3
git tag v3.0-beta.3
git push origin v3.0-beta.3
```

Only recreate the tag if you are sure you want to replace the existing beta tag.

---

## 11. Create the GitHub release

On GitHub:

```text
Releases → Draft a new release
```

Use:

```text
Tag: v3.0-beta.3
Title: RetroModem Bridge v3 Beta 3 - Local BBS, Guide Browser, and Monthly/Daily Updates
```

Check:

```text
Set as a pre-release
```

Paste the contents of:

```text
GITHUB-RELEASE-BODY-v3.0-beta.3.md
```

Upload the compiled Windows ZIP, ideally named:

```text
RetroModemBridge-v3.0-beta.3-win-x64.zip
```

---

## 12. Suggested release asset

After publishing, ZIP the contents of:

```text
RetroModemBridge-v3-beta\publish\v3-beta\
```

Name it:

```text
RetroModemBridge-v3.0-beta.3-win-x64.zip
```

That compiled ZIP is what most users should download.
