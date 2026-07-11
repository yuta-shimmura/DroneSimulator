#!/bin/bash
# Build visualizer.py into a macOS .app with PyInstaller
set -e

cd "$(dirname "$0")"

echo "==> Generating app icon ..."
pip3 install pillow -q
python3 make_icon.py

echo "==> Cleaning previous build cache ..."
rm -rf build/ DroneLogVisualizer.spec

echo "==> Building DroneSimulator Log Visualizer.app ..."

pyinstaller \
  --noconfirm \
  --windowed \
  --name "DroneLogVisualizer" \
  --icon icon.icns \
  --osx-bundle-identifier "com.dronesimulator.logvisualizer" \
  --hidden-import AppKit \
  --hidden-import objc \
  visualizer.py

ln -sf "$(pwd)/dist/DroneLogVisualizer.app" ../DroneLogVisualizer.app

echo "==> Refreshing icon cache ..."
APP="$(pwd)/dist/DroneLogVisualizer.app"
touch "$APP"
xattr -cr "$APP" 2>/dev/null || true
rm -rf ~/Library/Caches/com.apple.iconservices.store 2>/dev/null || true
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "$APP"
killall iconservicesd 2>/dev/null || true
killall Dock 2>/dev/null || true

echo ""
echo "==> Done!"
echo "    App: $APP"
echo ""
echo "    To run: open dist/DroneLogVisualizer.app"
echo "    Or double-click the .app in Finder."
