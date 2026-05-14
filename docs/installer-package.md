# Windows Installer Package

Memora currently ships best as a local Windows portable package: self-contained
UI and API binaries plus install, uninstall, and launch scripts. This keeps the
package honest while the app is still early and unsigned.

## Build The Package

From the repository root:

```powershell
.\build\package-windows.ps1
```

The script creates:

- `artifacts/memora-<version>-win-x64/`
- `artifacts/memora-<version>-win-x64.zip`

The package contains:

- `app/Memora.Ui/Memora.Ui.exe`
- `app/Memora.Api/Memora.Api.exe`
- `scripts/Start-Memora.ps1`
- `scripts/Start-MemoraUi.ps1`
- `scripts/Start-MemoraApi.ps1`
- `install-memora.ps1`
- `uninstall-memora.ps1`

## Install Locally

Extract the zip and run:

```powershell
.\install-memora.ps1
```

By default, the installer copies Memora to:

```text
%LOCALAPPDATA%\Memora
```

and creates the workspace root:

```text
%USERPROFILE%\memora-workspaces
```

It also creates Start Menu shortcuts under `Memora`.
The installer creates a shared local access token under the workspace root and
prints a one-time URL shaped like:

```text
http://127.0.0.1:5080/?localToken=<token>
```

Open that URL once after starting Memora to set the local browser session.

## Run

Use the Start Menu shortcut named `Start Memora`, or run:

```powershell
%LOCALAPPDATA%\Memora\scripts\Start-Memora.ps1
```

Open:

```text
http://127.0.0.1:5080
```

First-run project setup is available at:

```text
http://127.0.0.1:5080/get-started
```

Use that page to create a Memora workspace and attach a local Git checkout.
Memora is local-first, so it does not upload the repository.

The API is available at:

```text
http://127.0.0.1:5081
```

For GitHub-backed workflows, log in with GitHub CLI:

```powershell
gh auth login
```

## Notes

- The package is unsigned. Windows may warn before first run.
- The installer does not register a Windows service.
- Workspace files are intentionally left in place during uninstall.
- The package does not make Memora a hosted MCP transport. It runs the current
  local UI and companion API.
