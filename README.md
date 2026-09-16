# SMC Wireless Studio

Native Windows desktop application and MSI installer, based on the approved dark SMC-blue UI concept. **Version 0.1.0 is a working demo, not a hardware controller.** It never writes to or connects to an actual EXW1 base. EXW1-specific I/O requires the exact base/remote types, firmware and verified data mapping before commissioning.

## Download

Download the x64 MSI from [Releases](https://github.com/Csomaika/SMC-Wireless-Studio/releases/latest). The .NET runtime is included. Install from the MSI and launch the desktop or Start-menu shortcut. Administrator approval is required for installation/update into Program Files. Windows 10/11 x64 is the intended desktop target; CI currently validates Windows Server 2022.

## Working features

- Seven Hungarian screens: overview, network, I/O monitor, logic, event log, projects, settings.
- Ten initial remote units; add/remove demo devices, search, offline filtering, rename devices and I/O channels.
- Separate background demo loop with 10/20/50/100 ms target and measured timing/overrun statistics. Timing is not a hard real-time guarantee.
- Manual outputs, manual/automatic sensor simulation and simulated connection loss.
- Editable sequences with input conditions, output operations, delays and input deadlines. Timeout/stop clears simulated outputs.
- Atomic project save/import/export, schema validation, multiple projects, CSV event export.
- Real original SMC product images, no generated device renders. See asset notice for provenance.

## Updates

The MSI has **Install**, **Update**, **Repair**, and **Cancel** buttons. Update becomes available when an older version with the fixed UpgradeCode is detected. Install is disabled in this state. You do not have to manually uninstall the old version. Older-version downgrades are blocked.

The application's **Update** action checks this repository's latest stable GitHub Release. It validates the version and download origin, downloads the MSI, checks its SHA-256 against the matching release checksum, stops the demo, saves the project and launches Windows Installer. The user confirms downloading/installing the new version. Updates do not run automatically in the background. HTTPS and checksum verification are implemented; packages are not yet Authenticode-signed, so Windows may display an unknown-publisher prompt.

Project data is stored separately in `%LOCALAPPDATA%\SMC Wireless Studio\Projects`; session logs are in `Logs`. Neither upgrading nor uninstalling the app deletes these folders. Keep exported project backups as usual. Never change the MSI UpgradeCode in future versions. Increment all three-part release versions monotonically in `version.txt`.

## Build and verification

Requires Windows, .NET 10 SDK and network access to NuGet. `scripts/Build.ps1` pins WiX 5.0.2, publishes a self-contained x64 app and creates the MSI and checksum.

```powershell
dotnet run --project tests/Studio.Tests/Studio.Tests.csproj -c Release
./scripts/Build.ps1
```

GitHub Actions runs core behavior tests, builds the MSI, starts the desktop in `--smoke-test` mode and renders all seven screens. It installs a genuine earlier-version fixture, upgrades it, checks installed EXE version and preservation of user data, checks downgrade rejection, runs the installed app and tests uninstallation. Successful builds on `main` publish a versioned release if that version does not already exist. Published versions are not overwritten.

## Layout

`Studio.Core`: validated project model, atomic persistence, isolated demo engine and release validation. `Studio.Desktop`: WPF views, native project dialogs and updater. `installer`: Windows Installer UI and stable upgrade identity. `scripts`: build and installer lifecycle validation.

This is a community prototype created for Csomaika's workflow, not an official SMC Corporation product. The existing repository license applies to the software; SMC product imagery and trademarks remain with their rights holders.
