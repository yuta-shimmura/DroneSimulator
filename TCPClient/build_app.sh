#!/bin/bash
set -e

cd "$(dirname "$0")"

echo "==> Generating app icon ..."
pip3 install pillow -q
python3 make_icon.py

echo "==> Building Drone TCP Client.app ..."

pyinstaller \
  --noconfirm \
  --windowed \
  --name "DroneTCPClient" \
  --icon icon.icns \
  --osx-bundle-identifier "com.dronesimulator.tcpclient" \
  client.py

ln -sf "$(pwd)/dist/DroneTCPClient.app" ../DroneTCPClient.app

echo "==> Refreshing icon cache ..."
APP="$(pwd)/dist/DroneTCPClient.app"
touch "$APP"
xattr -cr "$APP" 2>/dev/null || true
rm -rf ~/Library/Caches/com.apple.iconservices.store 2>/dev/null || true
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "$APP"
killall iconservicesd 2>/dev/null || true
killall Dock 2>/dev/null || true

echo ""
echo "==> Done!"
echo "    App: $(pwd)/../DroneTCPClient.app"
echo ""
echo "    To run: open ../DroneTCPClient.app"
