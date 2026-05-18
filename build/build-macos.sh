#!/usr/bin/env bash
# =============================================================================
#  build-macos.sh — Build and publish Local Whisper Transcriber for macOS
#                   (Mac Catalyst, arm64 + x64)
#
#  REQUIREMENTS (run on a Mac):
#    - macOS 13 or later
#    - Xcode 15 or later (xcode-select --install)
#    - .NET SDK 10.0+ with maui-maccatalyst workload
#        dotnet workload install maui-maccatalyst
#    - For signed distribution: Apple Developer Program membership,
#      valid signing certificate, and provisioning profile.
#
#  USAGE:
#    chmod +x build/build-macos.sh
#
#    # Unsigned local/test build:
#    ./build/build-macos.sh
#
#    # Signed distribution build (.pkg):
#    SIGN=1 \
#    SIGNING_IDENTITY="Apple Distribution: Simon Lau" \
#    INSTALLER_IDENTITY="3rd Party Mac Developer Installer: Simon Lau" \
#    PROVISIONING_PROFILE="/path/to/LocalWhisperTranscriber.provisionprofile" \
#    ./build/build-macos.sh
# =============================================================================
set -euo pipefail

# ── Configuration ─────────────────────────────────────────────────────────────
CONFIGURATION="${CONFIGURATION:-Release}"
TARGET_FRAMEWORK="net10.0-maccatalyst"
PROJECT_FILE="$(dirname "$0")/../src/LocalWhisperTranscriber/LocalWhisperTranscriber.csproj"
NATIVE_MACOS="$(dirname "$0")/../src/LocalWhisperTranscriber/Native/macos"
ARTIFACTS_DIR="$(dirname "$0")/../artifacts/macos"

# Signing (set via environment or override here)
SIGN="${SIGN:-0}"
SIGNING_IDENTITY="${SIGNING_IDENTITY:-}"
INSTALLER_IDENTITY="${INSTALLER_IDENTITY:-}"
PROVISIONING_PROFILE="${PROVISIONING_PROFILE:-}"

echo ""
echo "═══════════════════════════════════════════════════════════"
echo "  Local Whisper Transcriber — macOS Build Script"
echo "═══════════════════════════════════════════════════════════"
echo "  Configuration : $CONFIGURATION"
echo "  Target TFM    : $TARGET_FRAMEWORK"
echo "  Signed build  : $SIGN"
echo ""

# ── Prerequisites check ───────────────────────────────────────────────────────
if [[ "$(uname)" != "Darwin" ]]; then
  echo "❌ This script must be run on macOS."
  exit 1
fi

if ! command -v dotnet &>/dev/null; then
  echo "❌ dotnet not found. Install .NET SDK 10 from https://dot.net"
  exit 1
fi

mkdir -p "$ARTIFACTS_DIR"

# ── Fix native binary permissions ─────────────────────────────────────────────
echo "[1/4] Setting execute permissions on bundled native binaries…"
for bin in "$NATIVE_MACOS/whisper-cli" "$NATIVE_MACOS/ffmpeg"; do
  if [[ -f "$bin" ]]; then
	chmod +x "$bin"
	echo "      chmod +x $(basename "$bin")"
  else
	echo "      ⚠️  Not found (add before build): $bin"
  fi
done

# ── Build / Publish ───────────────────────────────────────────────────────────
echo ""
if [[ "$SIGN" == "1" ]]; then
  echo "[2/4] Publishing (signed — creates .pkg)…"

  if [[ -z "$SIGNING_IDENTITY" || -z "$INSTALLER_IDENTITY" ]]; then
	echo "❌ SIGNING_IDENTITY and INSTALLER_IDENTITY must be set for signed builds."
	exit 1
  fi

  # Build a signed .app + .pkg
  dotnet publish "$PROJECT_FILE" \
	-f "$TARGET_FRAMEWORK" \
	-c "$CONFIGURATION" \
	-p:CreatePackage=true \
	-p:EnableCodeSigning=true \
	-p:EnablePackageSigning=true \
	-p:CodesignKey="$SIGNING_IDENTITY" \
	-p:PackageSigningKey="$INSTALLER_IDENTITY" \
	${PROVISIONING_PROFILE:+-p:CodesignProvision="$PROVISIONING_PROFILE"} \
	--output "$ARTIFACTS_DIR/pkg-staging" \
	--nologo

  echo ""
  echo "      ✅ Signed build complete."
  echo "         .pkg location: $ARTIFACTS_DIR/pkg-staging/"
  echo ""
  echo "      ⚠️  Signed distribution also requires:"
  echo "         1. Notarization via xcrun notarytool"
  echo "         2. Stapling: xcrun stapler staple <your.pkg>"
  echo "         3. Submission to Mac App Store (if distributing via MAS)."

else
  echo "[2/4] Publishing (unsigned — local/test .app)…"

  dotnet publish "$PROJECT_FILE" \
	-f "$TARGET_FRAMEWORK" \
	-c "$CONFIGURATION" \
	-p:CreatePackage=false \
	--output "$ARTIFACTS_DIR/app-staging" \
	--nologo

  echo "      ✅ Unsigned build complete."
fi

# ── Copy native binaries into publish output ───────────────────────────────────
STAGING_DIR="$ARTIFACTS_DIR/${SIGN:+pkg-}${SIGN:-app-}staging"
echo ""
echo "[3/4] Copying native binaries into publish output…"

copy_if_exists() {
  local src="$1"
  local dst_dir="$2"
  if [[ -f "$src" ]]; then
	mkdir -p "$dst_dir"
	cp -f "$src" "$dst_dir/"
	echo "      Copied: $(basename "$src")"
  else
	echo "      ⚠️  Not found: $src"
  fi
}

copy_if_exists "$NATIVE_MACOS/whisper-cli" "$STAGING_DIR"
copy_if_exists "$NATIVE_MACOS/ffmpeg"      "$STAGING_DIR"

MODELS_SRC="$NATIVE_MACOS/models"
MODELS_DST="$STAGING_DIR/models"
if [[ -d "$MODELS_SRC" ]]; then
  model_count=$(find "$MODELS_SRC" -name "*.bin" | wc -l | tr -d ' ')
  if [[ "$model_count" -gt 0 ]]; then
	mkdir -p "$MODELS_DST"
	cp -f "$MODELS_SRC"/*.bin "$MODELS_DST/" 2>/dev/null || true
	echo "      Copied $model_count model file(s) → $MODELS_DST"
  else
	echo "      ⚠️  No .bin model files in $MODELS_SRC — add them before running the app."
  fi
fi

# ── Optional: create a simple .pkg from unsigned .app ─────────────────────────
if [[ "$SIGN" == "0" ]]; then
  APP_BUNDLE=$(find "$ARTIFACTS_DIR/app-staging" -name "*.app" -maxdepth 3 | head -1)
  if [[ -n "$APP_BUNDLE" && -d "$APP_BUNDLE" ]]; then
	PKG_OUT="$ARTIFACTS_DIR/LocalWhisperTranscriber-1.0.0-unsigned.pkg"
	echo ""
	echo "[4/4] Creating unsigned .pkg for local distribution…"
	pkgbuild \
	  --install-location "/Applications" \
	  --component "$APP_BUNDLE" \
	  "$PKG_OUT" \
	  --version "1.0.0" \
	  --identifier "com.simonhylau.localwhispertranscriber" \
	  2>/dev/null && echo "      ✅ .pkg → $PKG_OUT" \
			   || echo "      ⚠️  pkgbuild failed — install Xcode Command Line Tools."
  else
	echo "[4/4] No .app bundle found in staging — skipping .pkg creation."
  fi
else
  echo "[4/4] Signed .pkg already produced by dotnet publish."
fi

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
echo "═══════════════════════════════════════════════════════════"
echo "  macOS build complete!"
echo "  Artifacts: $ARTIFACTS_DIR"
echo ""
if [[ "$SIGN" == "0" ]]; then
  echo "  To run unsigned on your Mac:"
  echo "    1. Open Finder → $ARTIFACTS_DIR/app-staging/"
  echo "    2. Right-click the .app → Open → Open (bypasses Gatekeeper)"
  echo "    3. Or: sudo xattr -rd com.apple.quarantine <YourApp.app>"
fi
echo "═══════════════════════════════════════════════════════════"
echo ""
