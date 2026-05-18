# Local Whisper Transcriber

> Fully offline, cross-platform speech-to-text desktop application powered by **whisper.cpp** and **.NET MAUI**.  
> No cloud. No API keys. No Python runtime.

---

## Table of Contents

1. [What the app does](#what-the-app-does)
2. [Solution structure](#solution-structure)
3. [Running in Visual Studio](#running-in-visual-studio)
4. [Adding whisper.cpp binaries](#adding-whispercpp-binaries)
5. [Adding ffmpeg binaries](#adding-ffmpeg-binaries)
6. [Adding model files](#adding-model-files)
7. [Windows build](#windows-build)
8. [Windows MSI build](#windows-msi-build)
9. [macOS build](#macos-build)
10. [macOS signing requirements](#macos-signing-requirements)
11. [Known limitations](#known-limitations)
12. [Troubleshooting](#troubleshooting)

---

## What the app does

Local Whisper Transcriber lets you:

- **Select** any audio file (WAV, MP3, MP4, M4A, FLAC, OGG, AAC, OPUS, and more).
- **Convert** non-WAV files automatically to 16 kHz mono WAV using bundled ffmpeg.
- **Transcribe** locally using whisper.cpp — no internet connection required.
- **Choose** the Whisper model size: `tiny`, `base`, or `small`.
- **Choose** the language: auto-detect, English, Chinese (Mandarin), or Cantonese (`yue`).
- **Choose** the output format: plain text (`.txt`), SubRip subtitles (`.srt`), or JSON (`.json`).
- **View** the transcript directly in the app.
- **Save** the transcript to a file anywhere on disk.
- **Open** the output folder in Explorer / Finder.

All audio stays on your device. Nothing is sent over the network.

---

## Solution structure

```
LocalWhisperTranscriber.sln
├── src/
│   └── LocalWhisperTranscriber/
│       ├── LocalWhisperTranscriber.csproj   # MAUI multi-target project
│       ├── MauiProgram.cs
│       ├── App.xaml / App.xaml.cs
│       ├── AppShell.xaml / AppShell.xaml.cs
│       ├── MainPage.xaml / MainPage.xaml.cs
│       ├── Models/
│       │   ├── TranscriptionOptions.cs
│       │   └── TranscriptionResult.cs
│       ├── Services/
│       │   ├── IWhisperService.cs
│       │   ├── WhisperCppService.cs
│       │   ├── AudioConversionService.cs
│       │   ├── FileDialogService.cs
│       │   └── FileSaveDialogHelper.Windows.cs
│       ├── Native/
│       │   ├── windows/        ← place whisper-cli.exe, ffmpeg.exe, models/ here
│       │   └── macos/          ← place whisper-cli, ffmpeg, models/ here
│       └── Resources/
├── installer/
│   └── windows/                ← WiX v5 MSI project
├── build/
│   ├── build-windows.ps1
│   └── build-macos.sh
└── artifacts/                  ← generated build output (gitignored)
```

---

## Running in Visual Studio

1. Open `LocalWhisperTranscriber.sln` in **Visual Studio 2022 / 2026** (17.8+ recommended).
2. Make sure the **MAUI** workload is installed  
   _(Tools → Get Tools and Features → .NET Multi-platform App UI development)_.
3. Set the startup project to **LocalWhisperTranscriber**.
4. Select the **Windows Machine** target from the debug toolbar.
5. Add the required native binaries (see sections below) — the app will show an error dialog if they are missing.
6. Press **F5** to run.

> **Note**: For macOS, open the solution on a Mac in Visual Studio for Mac or use `dotnet run`.

---

## Adding whisper.cpp binaries

### Windows (`src/LocalWhisperTranscriber/Native/windows/`)

| File | Source |
|------|--------|
| `whisper-cli.exe` | [whisper.cpp releases](https://github.com/ggerganov/whisper.cpp/releases) (Windows x64 ZIP) |

**Build from source (PowerShell):**
```powershell
git clone https://github.com/ggerganov/whisper.cpp
cd whisper.cpp
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
copy build\Release\whisper-cli.exe ..\src\LocalWhisperTranscriber\Native\windows\
```

### macOS (`src/LocalWhisperTranscriber/Native/macos/`)

```bash
# Homebrew:
brew install whisper-cpp
cp $(brew --prefix)/bin/whisper-cli src/LocalWhisperTranscriber/Native/macos/
chmod +x src/LocalWhisperTranscriber/Native/macos/whisper-cli

# Or build from source (Apple Silicon with Metal):
git clone https://github.com/ggerganov/whisper.cpp && cd whisper.cpp
cmake -B build -DCMAKE_BUILD_TYPE=Release -DGGML_METAL=ON
cmake --build build --config Release -j$(sysctl -n hw.ncpu)
cp build/bin/whisper-cli ../src/LocalWhisperTranscriber/Native/macos/
chmod +x ../src/LocalWhisperTranscriber/Native/macos/whisper-cli
```

---

## Adding ffmpeg binaries

### Windows

1. Download from [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) — choose the **essentials** build.
2. Extract `bin/ffmpeg.exe` into `src/LocalWhisperTranscriber/Native/windows/`.

### macOS

```bash
brew install ffmpeg
cp $(brew --prefix)/bin/ffmpeg src/LocalWhisperTranscriber/Native/macos/
chmod +x src/LocalWhisperTranscriber/Native/macos/ffmpeg
```

Or download a static build from [evermeet.cx/ffmpeg](https://evermeet.cx/ffmpeg/).

---

## Adding model files

Place Whisper model files in the `models/` subfolder for each platform:

```
Native/windows/models/ggml-base.bin
Native/macos/models/ggml-base.bin
```

### Recommended first model: `ggml-base.bin` (~142 MB)

| Model | Size | Speed | Accuracy |
|-------|------|-------|----------|
| `ggml-tiny.bin` | 75 MB | Fastest | Basic |
| `ggml-base.bin` | 142 MB | Fast | Good |
| `ggml-base.en.bin` | 142 MB | Fast | Good (English only) |
| `ggml-small.bin` | 466 MB | Slower | Better |

### Download via whisper.cpp helper scripts

```bash
# From within the whisper.cpp repo:
bash models/download-ggml-model.sh base
```

### Direct Hugging Face download

```
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin
```

---

## Windows build

```powershell
# From the repo root:
.\build\build-windows.ps1

# Skip MSI if WiX is not installed:
.\build\build-windows.ps1 -SkipInstaller
```

The script:
1. Publishes a self-contained, unpackaged Windows x64 app to `artifacts/windows/app/`.
2. Copies `whisper-cli.exe`, `ffmpeg.exe`, and model files into the publish folder.
3. Builds the WiX MSI (if WiX is installed).

The published app can be run directly from `artifacts/windows/app/LocalWhisperTranscriber.exe`.

---

## Windows MSI build

**Prerequisites:**
```powershell
dotnet tool install --global wix
wix extension add WixToolset.UI.wixext
```

**Build MSI separately:**
```powershell
wix build installer\windows\LocalWhisperTranscriber.Installer.wixproj `
	-p PublishDir=artifacts\windows\app\ `
	-o artifacts\windows\LocalWhisperTranscriber-1.0.0-x64.msi
```

> **Harvesting app files**: The `AppFiles.wxs` contains a minimal skeleton.  
> For production, run the WiX harvest command to enumerate all published files:
> ```powershell
> wix harvest dir artifacts\windows\app\ `
>     -componentGroupName AppFilesGroup `
>     -directoryRefId INSTALLFOLDER `
>     -out installer\windows\AppFiles.wxs
> ```
> Or enable `<HarvestFiles>` auto-harvest in `LocalWhisperTranscriber.Installer.wixproj`.

---

## macOS build

**Must be run on a Mac.**

```bash
chmod +x build/build-macos.sh

# Unsigned local test build:
./build/build-macos.sh

# Signed distribution build:
SIGN=1 \
SIGNING_IDENTITY="Apple Distribution: Simon Lau" \
INSTALLER_IDENTITY="3rd Party Mac Developer Installer: Simon Lau" \
PROVISIONING_PROFILE="/path/to/your.provisionprofile" \
./build/build-macos.sh
```

Output is placed in `artifacts/macos/`.

---

## macOS signing requirements

To distribute the app outside of a local machine, you need:

| Requirement | Notes |
|-------------|-------|
| **macOS development machine** | The entire Mac Catalyst + pkg pipeline must run on macOS |
| **Xcode 15+** | `xcode-select --install` |
| **Apple Developer Program** | $99/year membership at developer.apple.com |
| **Distribution certificate** | "Apple Distribution" (App Store) or "Developer ID Application" (direct) |
| **Installer certificate** | "3rd Party Mac Developer Installer" (App Store) or "Developer ID Installer" (direct) |
| **Provisioning profile** | Bound to your App ID (`com.simonhylau.localwhispertranscriber`) |
| **Entitlements** | May need `com.apple.security.cs.allow-unsigned-executable-memory` for whisper.cpp |
| **Notarization** | Required for Developer ID distribution (`xcrun notarytool submit`) |
| **Stapling** | `xcrun stapler staple YourApp.pkg` |

### App ID

Register `com.simonhylau.localwhispertranscriber` in the Apple Developer portal before signing.

### Mac Catalyst sandbox note

If the bundled whisper-cli or ffmpeg cannot execute due to sandbox restrictions,  
the app copies them to `~/Library/Application Support/LocalWhisperTranscriber/`  
on first run and sets execute permissions there.

---

## Known limitations

- **Cantonese (`yue`)**: support depends on the whisper.cpp version and model used;  
  older models may map it to Mandarin Chinese.
- **Long audio files**: whisper.cpp processes the full file in memory; very long files  
  (> 2 hours) may require significant RAM.
- **Mac Catalyst subprocess execution**: sandboxing may require disabling the app sandbox  
  entitlement or shipping binaries in a separate helper bundle.
- **No real-time microphone recording**: the UI has a file-selection workflow only.  
  Microphone recording can be added using `MediaElement` or platform audio APIs.
- **Model files are large**: they are not bundled in the installer by default.  
  The build script copies whatever is present in `Native/<platform>/models/`.

---

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| _"whisper-cli executable not found"_ | Place `whisper-cli.exe` / `whisper-cli` in `Native/windows/` or `Native/macos/`. |
| _"Model file not found"_ | Download a `.bin` file and place it in `Native/<platform>/models/`. |
| _"ffmpeg executable not found"_ | Place `ffmpeg.exe` / `ffmpeg` in `Native/windows/` or `Native/macos/`. |
| _"ffmpeg failed (exit code 1)"_ | Check the audio file is not corrupted. Run ffmpeg manually to see the error. |
| _"whisper-cli exited with code 1"_ | Check stderr in the transcript editor; usually a bad model path or corrupt WAV. |
| App does not launch on macOS | Run `sudo xattr -rd com.apple.quarantine LocalWhisperTranscriber.app` in Terminal. |
| MSI build fails with "component not found" | Harvest app files first; see [Windows MSI build](#windows-msi-build). |
| Build error: `MauiVersion property not set` | Ensure .NET MAUI workload is installed: `dotnet workload install maui-windows`. |
