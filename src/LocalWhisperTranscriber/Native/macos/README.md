# Native macOS Binaries — Placeholder

This folder must contain macOS native binaries before building or publishing the Mac Catalyst app.

## Required Files

| File | Description |
|------|-------------|
| `whisper-cli` | whisper.cpp CLI for macOS (arm64 or universal binary) |
| `ffmpeg` | FFmpeg for macOS (audio conversion) |
| `models/ggml-tiny.bin` | Tiny Whisper model (~75 MB) |
| `models/ggml-base.bin` | Base Whisper model (~142 MB) — **recommended first model** |
| `models/ggml-base.en.bin` | English-only base model |
| `models/ggml-small.bin` | Small Whisper model (~466 MB) |

## Where to Get whisper-cli (macOS)

### Option A — Homebrew (easiest)
```bash
brew install whisper-cpp
# The binary is at $(brew --prefix)/bin/whisper-cli
cp $(brew --prefix)/bin/whisper-cli Native/macos/
```

### Option B — Build from source (supports Apple Silicon natively)
```bash
git clone https://github.com/ggerganov/whisper.cpp
cd whisper.cpp
# For Apple Silicon (arm64):
cmake -B build -DCMAKE_BUILD_TYPE=Release -DGGML_METAL=ON
cmake --build build --config Release -j$(sysctl -n hw.ncpu)
cp build/bin/whisper-cli ../../Native/macos/
```

### Option C — Pre-built GitHub release
https://github.com/ggerganov/whisper.cpp/releases

## Where to Get ffmpeg (macOS)

```bash
brew install ffmpeg
cp $(brew --prefix)/bin/ffmpeg Native/macos/
```

Or download a static build from:
https://evermeet.cx/ffmpeg/

## Important: Execute Permissions

After copying the binaries, you MUST set execute permissions:
```bash
chmod +x src/LocalWhisperTranscriber/Native/macos/whisper-cli
chmod +x src/LocalWhisperTranscriber/Native/macos/ffmpeg
```

The application attempts to do this automatically at runtime via `chmod +x`, but setting
permissions before build is recommended.

## Mac Catalyst Sandbox Note

Mac Catalyst apps run in a sandboxed environment. If you encounter "Operation not permitted"
when executing bundled binaries, the app will automatically copy them to the app-support
directory under `~/Library/Application Support/LocalWhisperTranscriber/` and execute from there.

## Folder Layout (after populating)

```
Native/macos/
├── whisper-cli       (chmod +x required)
├── ffmpeg            (chmod +x required)
└── models/
	├── ggml-tiny.bin
	├── ggml-base.bin
	├── ggml-base.en.bin
	└── ggml-small.bin
```
