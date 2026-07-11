"""
Drone TCP Client アイコン生成スクリプト
実行: python3 make_icon.py
出力: icon.icns
"""
import math
import os
import shutil
import subprocess
from PIL import Image, ImageDraw

ICONSET = "icon.iconset"


def draw_drone(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d   = ImageDraw.Draw(img)
    cx, cy = size / 2, size / 2

    # 背景の円
    r = size * 0.46
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(30, 30, 46, 255))

    accent  = (94, 129, 244, 255)
    arm_len = size * 0.33
    arm_w   = max(2, size // 38)

    # 4本のアーム
    for deg in [45, 135, 225, 315]:
        rad = math.radians(deg)
        x1 = cx + math.cos(rad) * size * 0.09
        y1 = cy + math.sin(rad) * size * 0.09
        x2 = cx + math.cos(rad) * arm_len
        y2 = cy + math.sin(rad) * arm_len
        d.line([x1, y1, x2, y2], fill=accent, width=arm_w)

    # ローター（外輪）
    rotor_r   = size * 0.14
    rotor_w   = max(1, size // 55)
    for deg in [45, 135, 225, 315]:
        rad = math.radians(deg)
        rx  = cx + math.cos(rad) * arm_len
        ry  = cy + math.sin(rad) * arm_len
        d.ellipse([rx - rotor_r, ry - rotor_r, rx + rotor_r, ry + rotor_r],
                  outline=accent, width=rotor_w)

    # ボディ（中央の正方形）
    body_r = size * 0.09
    d.rectangle([cx - body_r, cy - body_r, cx + body_r, cy + body_r],
                fill=accent)

    # ボディ中心の小さい円（カメラ）
    cam_r = size * 0.035
    d.ellipse([cx - cam_r, cy - cam_r, cx + cam_r, cy + cam_r],
              fill=(200, 220, 255, 255))

    return img


def make_icns():
    os.makedirs(ICONSET, exist_ok=True)

    sizes = [
        ("icon_16x16.png",       16),
        ("icon_16x16@2x.png",    32),
        ("icon_32x32.png",       32),
        ("icon_32x32@2x.png",    64),
        ("icon_128x128.png",    128),
        ("icon_128x128@2x.png", 256),
        ("icon_256x256.png",    256),
        ("icon_256x256@2x.png", 512),
        ("icon_512x512.png",    512),
        ("icon_512x512@2x.png",1024),
    ]

    base = draw_drone(1024)
    for fname, px in sizes:
        resized = base.resize((px, px), Image.LANCZOS)
        resized.save(os.path.join(ICONSET, fname))
        print(f"  {fname}")

    subprocess.run(["iconutil", "-c", "icns", ICONSET], check=True)
    shutil.rmtree(ICONSET)
    print("icon.icns を生成しました。")


if __name__ == "__main__":
    make_icns()
