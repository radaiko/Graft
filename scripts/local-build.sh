#!/bin/bash
set -euo pipefail

VERSION="0.0.0-local"
INSTALL=false

usage() {
  echo "Usage: $0 [-v VERSION] [--install]"
  echo ""
  echo "Build a native AOT binary of graft for the current platform."
  echo ""
  echo "Options:"
  echo "  -v VERSION    Set the version (default: 0.0.0-local)"
  echo "  --install     Copy binary to ~/.local/bin/graft (backs up existing)"
  echo "  -h, --help    Show this help"
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -v)
      VERSION="$2"
      shift 2
      ;;
    --install)
      INSTALL=true
      shift
      ;;
    -h|--help)
      usage
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      ;;
  esac
done

# Detect OS
OS="$(uname -s)"
case "$OS" in
  Linux)  os="linux" ;;
  Darwin) os="osx" ;;
  *)      echo "Unsupported OS: $OS" >&2; exit 1 ;;
esac

# Detect architecture
ARCH="$(uname -m)"
case "$ARCH" in
  x86_64|amd64)  arch="x64" ;;
  arm64|aarch64)  arch="arm64" ;;
  *)              echo "Unsupported architecture: $ARCH" >&2; exit 1 ;;
esac

RID="${os}-${arch}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$REPO_ROOT/src/Graft.Cli/Graft.Cli.csproj"
PUBLISH_DIR="$REPO_ROOT/src/Graft.Cli/bin/Release/net10.0/$RID/publish"
BINARY="$PUBLISH_DIR/graft"

echo "Building graft $VERSION for $RID..."
echo ""

echo "  AOT publish: building... (this takes ~30s)"
dotnet publish "$PROJECT" -c Release -r "$RID" /p:Version="$VERSION"
echo ""

echo "Build complete:"
echo "  $BINARY"
echo ""
echo "Run directly:"
echo "  $BINARY version"

if [[ "$INSTALL" == true ]]; then
  INSTALL_DIR="$HOME/.local/bin"
  mkdir -p "$INSTALL_DIR"

  if [[ -f "$INSTALL_DIR/graft" ]]; then
    cp "$INSTALL_DIR/graft" "$INSTALL_DIR/graft.bak"
    echo ""
    echo "Backed up existing graft to $INSTALL_DIR/graft.bak"
  fi

  cp "$BINARY" "$INSTALL_DIR/graft"
  echo "Installed graft $VERSION to $INSTALL_DIR/graft"
fi
