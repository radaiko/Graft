#!/bin/bash
set -euo pipefail

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

# Get latest CLI version from GitHub API
LATEST=$(curl -fsSL "https://api.github.com/repos/radaiko/Graft/releases" \
  | grep -o '"tag_name": "cli/v[^"]*"' | head -1 | sed 's/.*cli\/v//;s/"//')

if [ -z "$LATEST" ]; then
  echo "Failed to determine latest version" >&2
  exit 1
fi

echo "Installing Graft CLI v${LATEST} (${RID})..."

TMPDIR=$(mktemp -d)
trap 'rm -rf "$TMPDIR"' EXIT

curl -fsSL "https://github.com/radaiko/Graft/releases/download/cli/v${LATEST}/graft-cli-v${LATEST}-${RID}.tar.gz" \
  -o "$TMPDIR/graft.tar.gz"
tar xzf "$TMPDIR/graft.tar.gz" -C "$TMPDIR"

INSTALL_DIR="${HOME}/.local/bin"
mkdir -p "$INSTALL_DIR"
mv "$TMPDIR/graft" "$INSTALL_DIR/"

# Check if install dir is in PATH
case ":$PATH:" in
  *":${INSTALL_DIR}:"*) ;;
  *)
    export PATH="${INSTALL_DIR}:${PATH}"
    echo "NOTE: ${INSTALL_DIR} is not in your PATH."
    echo "Add it by appending this to your shell profile (~/.bashrc, ~/.zshrc, etc.):"
    echo "  export PATH=\"${INSTALL_DIR}:\$PATH\""
    ;;
esac

echo "Graft CLI v${LATEST} installed to ${INSTALL_DIR}/graft"

# Set up gt symlink and git alias
"${INSTALL_DIR}/graft" install
