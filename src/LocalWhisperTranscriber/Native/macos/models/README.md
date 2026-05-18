# Native macOS Model Files — Placeholder

Place Whisper model `.bin` files in this folder.

| File | Size | Notes |
|------|------|-------|
| `ggml-tiny.bin` | ~75 MB | Fastest; lower accuracy |
| `ggml-base.bin` | ~142 MB | **Recommended starting point** |
| `ggml-base.en.bin` | ~142 MB | English-only; slightly faster |
| `ggml-small.bin` | ~466 MB | Better accuracy; slower |

## Download

```bash
# From within the whisper.cpp repo:
bash models/download-ggml-model.sh base

# Or directly via curl:
curl -L -o ggml-base.bin \
  https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin
```

Place the downloaded `.bin` files in this `models/` folder.
