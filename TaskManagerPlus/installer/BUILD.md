# Build installer (Windows)

## 1) Build & stage files

From repo root:

```powershell
.\scripts\Build-Release.ps1
```

This creates:
- `dist\staging\` (input for installer)
- `dist\TaskManagerPlus-win-x64-Release-1.0.0-YYYY-MM-DD\` (portable folder)
- `dist\TaskManagerPlus-win-x64-Release-1.0.0-YYYY-MM-DD.zip`

## 2) Create the installer (.exe)

Install **Inno Setup**, then run:

```powershell
ISCC.exe .\installer\TaskManagerPlus.iss
```

Output:
- `dist\TaskManagerPlus-Setup-1.0.0.exe`

## Notes
- App targets **.NET Framework 4.7.2** (user machine must have it installed).
- App manifest requests admin (`requireAdministrator`), so the installer is configured with `PrivilegesRequired=admin`.

