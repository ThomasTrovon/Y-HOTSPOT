# Y-HOTSPOT

Y-HOTSPOT is a native Windows hotspot manager built for stability-focused local networking.  
It combines a WinUI 3 desktop interface with a background Windows Service to manage **Windows Mobile Hotspot** reliably.

## Table of Contents

- [Project Context](#project-context)
- [Features](#features)
- [Support and Test Scope](#support-and-test-scope)
- [Quick Start](#quick-start)
- [Build From Source](#build-from-source)
- [Architecture Overview](#architecture-overview)
- [Limitations](#limitations)
- [Bug Reports](#bug-reports)
- [License](#license)

## Features

- Runs as a background service with auto-start and restart recovery
- Configures hotspot settings (SSID, passphrase, authentication mode, band, client limit)
- Supports optional LAN fallback adapter path (Wintun-based)
- Supports optional Wi-Fi disconnect policy while hotspot is active
- Applies hotspot-focused system tuning with backup and restore
- Provides status and diagnostics pages with live refresh
- Includes troubleshooting actions for common setup failures
- Adds tray menu quick actions
- Includes reset flow to remove service, restore settings, and clear app config

## Support and Test Scope

The app is provided **as-is**. Support is currently limited.

Validated environment:

- Windows 11 build `26100+`
- `amd64` CPU architecture
- Wi-Fi 7 hardware

Other hardware or OS configurations may work, but are not guaranteed.

## Quick Start

1. Download the latest release build.
2. Run `Y-HOTSPOT.exe` as administrator.
3. Open **Settings** and configure your hotspot options.
4. Enable service mode if you want persistent background management.
5. Open **Status** to confirm service, hotspot, and client state.
6. Use **Troubleshooting** if setup appears incomplete or inconsistent.

## Build From Source

From the repository root in PowerShell:

```powershell
.\restore.ps1
.\build.ps1
```

Build output:

- `.\Publish\Y-HOTSPOT.exe`

Helpful scripts:

- `.\format.ps1` - Run dotnet format and csharpier
- `.\update.ps1` - Update packages and tools
- `.\cleanup.ps1` - Clean build artifacts

## Architecture Overview

### Runtime Model

- **UI mode**: WinUI 3 desktop app
- **Service mode**: same executable launched with `--service`
- Single-instance guards for both UI and service contexts
- UI and service communicate through a named pipe

### Service Lifecycle

When service mode is enabled, the worker loop:

1. Loads encrypted config from `%ProgramData%\Y-HOTSPOT\config.bin`
2. Applies hotspot-focused Windows optimizations
3. Initializes network profile and fallback adapter path
4. Configures and starts native Windows tethering
5. Monitors state and performs automatic recovery after stop/failure

### Reversible System Tuning

Before tuning, Y-HOTSPOT stores a snapshot in `%ProgramData%\Y-HOTSPOT\system_config_backup.json`.  
When disabled or reset, it restores backed-up settings and removes the backup file.

Tuning includes:

- ICS registry values (timeouts and max peers)
- Adapter power-related PnP value
- Optional WLAN AutoConfig disable per adapter
- Service restarts (`icssvc`, `FDResPub`, `SSDPSRV`, `upnphost`)
- TCP global autotuning normalization

## Limitations

- Designed for Windows-only tethering workflows
- Primarily tested on one hardware/software profile
- May behave differently on older builds, different chipsets, or non-`amd64` systems

## Bug Reports

When reporting a bug, include:

- Full system specifications
- Exact reproduction steps
- Fresh logs captured after reproducing

Before collecting logs, set log detail level to **All** and clear old logs so the reproduction session is isolated.

## License

Licensed under the **Apache License 2.0**.

You may use, modify, and redistribute this project, including for commercial use, with attribution preserved.  
See [LICENSE](./LICENSE) and [NOTICE](./NOTICE).
