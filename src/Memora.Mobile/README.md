# Memora Mobile

.NET MAUI capture client for Memora contribution packets.

This is a Milestone 13 draft. The packet format
(`docs/mobile-contribution-packet.md`) is the contract; this project
produces packets that match it.

## What It Is

A single-page Android app that:

- runs on Android 5.0 (API 21) and above
- requires no auth, no backend, and no network access at runtime
- supports the four packet intents from the format spec: `question`,
  `decision_draft`, `planning_note`, `proposal_draft`
- emits the packet markdown in a read-only preview area
- generates `packet_id` and `created_at` when the form loads
- enforces the canonical-safe envelope shape:
  - `source: mobile`
  - `canonical: false`
  - `lifecycle_target` constrained to non-canonical values
  - reserved canonical fields (`status`, `revision`, `project_id`, `id`,
    `approved_at`, `approved_by`) are never emitted

## What It Is Not (M13-02)

This project is the M13-02 capture slice only. The following are
deliberately out of scope and arrive in later issues:

- copy to clipboard (M13-03)
- file export (M13-03)
- local save and packet history (M13-03)
- desktop import path (M13-04 — already in `Memora.Import.Mobile`)
- shared-folder transfer (M13-05)
- same-network sync (M13-06 — deferred)

## Target Platforms

Initial release targets **Android only**:

- `net10.0-android` target framework
- Android API 21+ (`SupportedOSPlatformVersion = 21.0`)

iOS, MacCatalyst, and Windows targets are intentionally deferred. The
project file is structured so adding more `<TargetFrameworks>` entries
later is a one-line change once those platforms are exercised.

## Solution Membership

`Memora.Mobile` is **intentionally not included in `Memora.sln`**. The CI
workflow builds the solution on Linux (no MAUI workload, no Android SDK),
so adding this project to the solution would break every PR build.

Build it directly:

```pwsh
dotnet build src/Memora.Mobile/Memora.Mobile.csproj
```

A future CI lane can build this project once the build matrix has a job
with the MAUI workload + Android SDK installed.

## Build Prerequisites

This project requires a one-time local setup that is heavier than the
rest of the Memora solution.

1. **.NET 10 SDK** (already required by the rest of the repo).
2. **MAUI workload**:
   ```pwsh
   dotnet workload install maui
   ```
   Or, for the smallest Android-only install:
   ```pwsh
   dotnet workload install maui-android
   ```
3. **JDK 17+** — required by the Android tooling. Visual Studio's
   ".NET Multi-platform App UI development" workload installs a bundled
   OpenJDK at a known location. Without Visual Studio, install
   [Microsoft OpenJDK 17](https://learn.microsoft.com/en-us/java/openjdk/download)
   and set `JAVA_HOME`.
4. **Android SDK with API 36 platform**. Two options:
   - Run the bundled `InstallAndroidDependencies` target against a
     user-writable path (no admin required):
     ```pwsh
     dotnet build src/Memora.Mobile/Memora.Mobile.csproj `
       -t:InstallAndroidDependencies `
       -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk" `
       -p:AcceptAndroidSDKLicenses=true
     ```
   - Or install via Visual Studio Installer / Android Studio's SDK Manager.

## Building

After the prerequisites are in place:

```pwsh
dotnet build src/Memora.Mobile/Memora.Mobile.csproj `
  -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk"
```

If your Android SDK lives at the default system path
(`C:\Program Files (x86)\Android\android-sdk`) and includes API 36, the
`-p:AndroidSdkDirectory` flag is not needed; set `ANDROID_HOME` or rely
on the default discovery instead.

Running on a device or emulator follows the standard MAUI Android flow
through Visual Studio or
`dotnet build -t:Run src/Memora.Mobile/Memora.Mobile.csproj`.

## Project Structure

```text
src/Memora.Mobile/
  Memora.Mobile.csproj        // MAUI Android project
  MauiProgram.cs              // app builder
  App.xaml / App.xaml.cs      // application + resource dictionaries
  MainPage.xaml / .xaml.cs    // single capture page
  Models/
    MobileCaptureIntent.cs    // intent enum, intent catalog, proposed-type list
  Services/
    MobilePacketComposer.cs   // builds packet markdown from form state
  Resources/
    AppIcon/                  // app icon SVG pair
    Splash/                   // splash screen SVG
    Styles/                   // colors + control styles
    Raw/                      // placeholder bundled assets
  Platforms/Android/          // Android entry point + manifest
```

## Packet Format Reference

The single source of truth for the packet shape is
`docs/mobile-contribution-packet.md`. Any change here must keep the
emitted markdown valid against that spec; the M13-04 desktop importer
re-enforces the same rules on intake.
