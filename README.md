# Local Whisper Transcriber

> Fully offline, cross-platform speech-to-text desktop application powered by **whisper.cpp** and **.NET MAUI**.  
> No cloud. No API keys. No Python runtime.

---

## Table of Contents

1. [What the app does](#what-the-app-does)
2. [Solution structure](#solution-structure)
3. [AudioTranscriber (WinForms)](#audiotranscriber-winforms)
4. [Running in Visual Studio](#running-in-visual-studio)
5. [Adding whisper.cpp binaries](#adding-whispercpp-binaries)
6. [Adding ffmpeg binaries](#adding-ffmpeg-binaries)
7. [Adding model files](#adding-model-files)
8. [Windows build](#windows-build)
9. [Windows MSI build](#windows-msi-build)
10. [macOS build](#macos-build)
11. [macOS signing requirements](#macos-signing-requirements)
12. [Known limitations](#known-limitations)
13. [Troubleshooting](#troubleshooting)

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
LocalWhisperTranscriber.slnx
├── AudioTranscriber/                            ← WinForms recorder + transcriber (.NET Framework 4.8)
│   ├── MainForm.cs / MainForm.Designer.cs
│   ├── Program.cs
│   ├── Resource/
│   │   ├── Record.png                           ← button icon (idle)
│   │   └── Stop.png                             ← button icon (recording)
│   └── CLI/                                     ← place whisper-cli.exe + DLLs here (gitignored binaries)
│       └── models/                              ← place ggml-*.bin here (gitignored)
├── src/
│   └── LocalWhisperTranscriber/
│       ├── LocalWhisperTranscriber.csproj       ← MAUI multi-target project (.NET 10)
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
│       │   ├── windows/                         ← place whisper-cli.exe, ffmpeg.exe, models/ here
│       │   └── macos/                           ← place whisper-cli, ffmpeg, models/ here
│       └── Resources/
├── LocalTranscriberInstaller/                   ← VS Setup Project (.msi)
├── build/
│   ├── build-windows.ps1
│   └── build-macos.sh
└── artifacts/                                   ← generated build output (gitignored)
```

---

## AudioTranscriber (WinForms)

**AudioTranscriber** is a lightweight Windows-only (.NET Framework 4.8) WinForms app that records microphone input and system audio output simultaneously, then transcribes the recording fully offline using **whisper.cpp**.

### Features

- 🎙️ **Records all microphone devices** — captures every active input device at once (44.1 kHz mono WAV per device).
- 🔊 **Records system audio output** — uses WASAPI loopback to capture what is playing through each active render endpoint.
- ⚠️ **No microphone? No problem** — if no microphone is detected the status bar notifies you and recording continues on system audio only.
- 🔀 **Mixes all sources** — all captured streams are resampled to 16 kHz mono and mixed into a single WAV before transcription.
- ✂️ **Chunked transcription** — the mixed WAV is split into 30-second segments (whisper.cpp's optimal context window) and transcribed in parallel.
- ⚡ **Parallel processing** — up to `ProcessorCount ÷ 2` whisper-cli processes run concurrently to speed up long recordings.
- 🖼️ **Image button UI** — the record button shows `Record.png` at rest and switches to `Stop.png` while recording; both images are scaled to fill the button width while preserving aspect ratio.
- ⏱️ **Live recording timer** — elapsed time updates every 500 ms in the status area while recording.
- 📋 **Transcription progress** — the status bar shows `Transcribing chunk N/Total...` for each chunk as it completes.
- 🛠️ **Whisper stderr surfaced** — if whisper-cli exits with an error the stderr output is shown directly in the transcript text box instead of silently returning nothing.

---

### How it works

```
Click Record
    │
    ├── Start WaveInEvent for every microphone    (44.1 kHz mono WAV)
    └── Start WasapiLoopbackCapture for every     (native format WAV)
        active render endpoint

Click Stop
    │
    ├── Stop & flush all captures
    ├── MixToWav() — resample all streams to 16 kHz mono, mix together
    ├── SplitWavToChunks() — split mixed WAV into 30-second chunks
    └── TranscribeAsync()
            └── whisper-cli.exe -m <model> -f <chunk> -l auto -otxt
                    (up to ProcessorCount÷2 running in parallel)
            └── Concatenate chunk transcripts in order → display
```

---

### Required files

These files are **not included in the repository** (they exceed GitHub's 100 MB file limit) and must be placed manually before running the app.

| File | Place at | Size | Required |
|------|----------|------|----------|
| `whisper-cli.exe` | `AudioTranscriber/CLI/whisper-cli.exe` | ~5 MB | ✅ Yes |
| `whisper.dll` | `AudioTranscriber/CLI/whisper.dll` | — | ✅ Yes (ships with whisper-cli) |
| `ggml.dll` + `ggml-*.dll` | `AudioTranscriber/CLI/` | — | ✅ Yes (ships with whisper-cli) |
| `ggml-base.bin` _(or any model)_ | `AudioTranscriber/CLI/models/ggml-base.bin` | ~142 MB | ✅ Yes (at least one) |
| `ffmpeg.exe` | `AudioTranscriber/CLI/ffmpeg.exe` | ~215 MB | ⚠️ Optional |

> The app displays `[Error: whisper-cli not found]` or `[Error: whisper model not found]` in the transcript box if either required file is missing.

---

### Downloading whisper-cli.exe

**Option A — Pre-built release (easiest)**

1. Go to **https://github.com/ggerganov/whisper.cpp/releases**
2. Download the latest **Windows x64** ZIP (e.g. `whisper-cpp-binaries-windows-x64.zip`).
3. Extract and copy `whisper-cli.exe`, `whisper.dll`, `ggml.dll`, `ggml-base.dll`, `ggml-cpu.dll` into `AudioTranscriber/CLI/`.

**Option B — Build from source**

```powershell
git clone https://github.com/ggerganov/whisper.cpp
cd whisper.cpp
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
copy build\Release\whisper-cli.exe ..\AudioTranscriber\CLI\
copy build\Release\*.dll           ..\AudioTranscriber\CLI\
```

---

### Downloading a Whisper model

Place at least one `.bin` model file in `AudioTranscriber/CLI/models/`. The app automatically picks the **first** `ggml-*.bin` file it finds in that folder.

| Model | File | Size | Notes |
|-------|------|------|-------|
| Tiny | `ggml-tiny.bin` | 75 MB | Fastest; lower accuracy |
| Base | `ggml-base.bin` | 142 MB | **Recommended** — good balance |
| Base EN | `ggml-base.en.bin` | 142 MB | English only; slightly faster |
| Small | `ggml-small.bin` | 466 MB | Higher accuracy; slower |

**Direct download links (Hugging Face):**

```
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin
```

**PowerShell one-liner (base model):**

```powershell
Invoke-WebRequest `
  -Uri "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin" `
  -OutFile "AudioTranscriber\CLI\models\ggml-base.bin"
```

---

### Downloading ffmpeg.exe (optional)

`ffmpeg.exe` is present in the CLI folder for potential future use but is **not called** by the current transcription pipeline.

1. Go to **https://www.gyan.dev/ffmpeg/builds/**
2. Download the **ffmpeg-release-essentials** ZIP.
3. Extract `bin/ffmpeg.exe` into `AudioTranscriber/CLI/`.

---

### Folder structure after setup

```
AudioTranscriber/
├── Resource/
│   ├── Record.png          ← button icon (idle state)
│   └── Stop.png            ← button icon (recording state)
└── CLI/
    ├── whisper-cli.exe     ← required
    ├── whisper.dll         ← required (bundled with whisper-cli)
    ├── ggml.dll            ← required (bundled with whisper-cli)
    ├── ggml-base.dll       ← required (bundled with whisper-cli)
    ├── ggml-cpu.dll        ← required (bundled with whisper-cli)
    ├── ffmpeg.exe          ← optional
    └── models/
        └── ggml-base.bin   ← required (or any other ggml-*.bin)
```

---

### Status bar messages reference

| Message | Meaning |
|---------|---------|
| `Ready` | Idle, waiting to record |
| `Recording...` | Capture started on all devices |
| `Recording N input(s) and N output(s)...` | Active device count |
| `No microphone found. Recording system audio only...` | No mic detected; loopback still active |
| `Stopping capture...` | Flushing audio writers |
| `Mixing audio...` | Resampling and merging all streams |
| `Transcribing chunk N/Total...` | Parallel whisper-cli progress |
| `Done.` | Transcript ready |
| `[Error: whisper-cli not found]` | `CLI/whisper-cli.exe` is missing |
| `[Error: whisper model not found]` | No `ggml-*.bin` found in `CLI/models/` |
| `[whisper error: ...]` | whisper-cli exited non-zero; stderr shown inline |

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
| AudioTranscriber shows `[Error: whisper-cli not found]` | Copy `whisper-cli.exe` into `AudioTranscriber/CLI/`. See [AudioTranscriber setup](#audiotranscriber-winforms). |
| AudioTranscriber shows `[Error: whisper model not found]` | Download a `ggml-*.bin` model and place it in `AudioTranscriber/CLI/models/`. See [Downloading a Whisper model](#downloading-a-whisper-model). |
| AudioTranscriber transcript is empty | Check that `whisper-cli.exe` and `ggml-*.bin` are both present and the recorded WAV contains audio. |
