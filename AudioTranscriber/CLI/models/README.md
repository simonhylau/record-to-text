# Native Windows Model Files — Placeholder

Place Whisper model `.bin` files in this folder.

| File | Size | Notes |
|------|------|-------|
| `ggml-tiny.bin` | ~75 MB | Fastest; lower accuracy |
| `ggml-base.bin` | ~142 MB | **Recommended starting point** |
| `ggml-base.en.bin` | ~142 MB | English-only; slightly faster |
| `ggml-small.bin` | ~466 MB | Better accuracy; slower |

## Download Commands

### Using whisper.cpp helper scripts (from the whisper.cpp repo)
```bash
bash models/download-ggml-model.sh tiny
bash models/download-ggml-model.sh base
bash models/download-ggml-model.sh small
```

### Direct Hugging Face URLs
```
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin
```
