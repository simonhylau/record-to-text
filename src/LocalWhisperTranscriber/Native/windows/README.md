# Native Windows Binaries — Placeholder

This folder must contain the Windows native binaries before building or publishing.

## Required Files

| File | Description |
|------|-------------|
| `whisper-cli.exe` | whisper.cpp command-line tool for Windows x64 |
| `ffmpeg.exe` | FFmpeg for Windows x64 (audio conversion) |
| `models/ggml-tiny.bin` | Tiny Whisper model (~75 MB) |
| `models/ggml-base.bin` | Base Whisper model (~142 MB) — **recommended first model** |
| `models/ggml-base.en.bin` | English-only base model (~142 MB) |
| `models/ggml-small.bin` | Small Whisper model (~466 MB) |

## Where to Get whisper-cli.exe

### Option A — Pre-built Release (easiest)
1. Go to https://github.com/ggerganov/whisper.cpp/releases
2. Download the latest **Windows x64** release ZIP.
3. Extract `whisper-cli.exe` (or `main.exe` in older releases; rename to `whisper-cli.exe`).
4. Place it here.

### Option B — Build from source
```powershell
git clone https://github.com/ggerganov/whisper.cpp
cd whisper.cpp
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
# The binary will be at build/Release/whisper-cli.exe (or main.exe)
```

## Where to Get ffmpeg.exe

1. Go to https://www.gyan.dev/ffmpeg/builds/ or https://github.com/BtbN/FFmpeg-Builds/releases
2. Download the **Windows x64 essentials** build.
3. Extract `bin/ffmpeg.exe` and place it here.

## Where to Get Model Files

Run the whisper.cpp download script from the repo root, or download directly:

```powershell
# From within the whisper.cpp repo (PowerShell)
.\models\download-ggml-model.cmd base

# Or use bash via Git Bash:
bash models/download-ggml-model.sh base
```

### Direct download URLs
```
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin
```

Place downloaded `.bin` files in the `models/` subfolder.

## Folder Layout (after populating)

```
Native/windows/
├── whisper-cli.exe
├── ffmpeg.exe
└── models/
	├── ggml-tiny.bin
	├── ggml-base.bin
	├── ggml-base.en.bin
	└── ggml-small.bin
```
