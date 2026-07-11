"""
Drone Log Visualizer アイコン生成スクリプト
実行: python3 make_icon.py
出力: icon.icns
"""
import math
import os
import shutil
import subprocess
from PIL import Image, ImageDraw

ICONSET = "icon.iconset"


def draw_icon(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d   = ImageDraw.Draw(img)
    cx, cy = size / 2, size / 2

    # 背景の円
    r = size * 0.46
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(30, 30, 46, 255))

    accent = (94, 129, 244, 255)
    green  = (76, 175, 80,  255)
    white  = (220, 230, 255, 255)

    # グリッド線（薄く）
    grid_color = (60, 60, 90, 180)
    grid_w     = max(1, size // 130)
    margin     = size * 0.16
    left       = cx - size * 0.28
    right      = cx + size * 0.28
    top        = cy - size * 0.24
    bottom     = cy + size * 0.24

    for i in range(3):
        gy = top + (bottom - top) * i / 2
        d.line([left, gy, right, gy], fill=grid_color, width=grid_w)
    for i in range(4):
        gx = left + (right - left) * i / 3
        d.line([gx, top, gx, bottom], fill=grid_color, width=grid_w)

    # 飛行ルート（なめらかな折れ線）
    path_w = max(2, size // 55)
    pts = [
        (left,                    bottom),
        (left + (right-left)*0.2, bottom - (bottom-top)*0.3),
        (left + (right-left)*0.4, top    + (bottom-top)*0.25),
        (left + (right-left)*0.6, top    + (bottom-top)*0.1),
        (left + (right-left)*0.8, top    + (bottom-top)*0.3),
        (right,                   top    + (bottom-top)*0.15),
    ]
    for i in range(len(pts) - 1):
        d.line([pts[i], pts[i + 1]], fill=accent, width=path_w)

    # データ点
    dot_r = max(2, size // 65)
    for p in pts:
        d.ellipse([p[0]-dot_r, p[1]-dot_r, p[0]+dot_r, p[1]+dot_r], fill=white)

    # 先端の小さいドローン（▲）
    tip = pts[-1]
    dr  = size * 0.045
    tri = [
        (tip[0],      tip[1] - dr * 1.4),
        (tip[0] - dr, tip[1] + dr * 0.7),
        (tip[0] + dr, tip[1] + dr * 0.7),
    ]
    d.polygon(tri, fill=green)

    # 衝突マーカー（赤い×点）
    col = pts[3]
    cr  = size * 0.038
    col_color = (244, 67, 54, 230)
    lw = max(1, size // 80)
    d.line([col[0]-cr, col[1]-cr, col[0]+cr, col[1]+cr], fill=col_color, width=lw)
    d.line([col[0]+cr, col[1]-cr, col[0]-cr, col[1]+cr], fill=col_color, width=lw)

    return img


def make_icns():
    os.makedirs(ICONSET, exist_ok=True)

    sizes = [
        ("icon_16x16.png",        16),
        ("icon_16x16@2x.png",     32),
        ("icon_32x32.png",        32),
        ("icon_32x32@2x.png",     64),
        ("icon_128x128.png",     128),
        ("icon_128x128@2x.png",  256),
        ("icon_256x256.png",     256),
        ("icon_256x256@2x.png",  512),
        ("icon_512x512.png",     512),
        ("icon_512x512@2x.png", 1024),
    ]

    base = draw_icon(1024)
    for fname, px in sizes:
        resized = base.resize((px, px), Image.LANCZOS)
        resized.save(os.path.join(ICONSET, fname))
        print(f"  {fname}")

    subprocess.run(["iconutil", "-c", "icns", ICONSET], check=True)
    shutil.rmtree(ICONSET)
    print("icon.icns を生成しました。")


if __name__ == "__main__":
    make_icns()
