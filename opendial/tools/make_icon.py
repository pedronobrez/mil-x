#!/usr/bin/env python3
"""Draws the OpenDIAL application icon (macOS .icns / PNG) from the OpenQuant palette.

The mark is two co-eluting chromatographic peaks, the rear one showing through the front
one: the deconvolution that gives OpenDIAL its name. Colours are the shared suite tokens
(accent #234b8c, light accent #6f9be0).

    python3 tools/make_icon.py --out docs/images/icon.png [--size 1024]
"""
import argparse
import math
import os

from PIL import Image, ImageDraw, ImageFilter

ACCENT_TOP = (58, 111, 191)      # #3a6fbf
ACCENT_BOTTOM = (26, 58, 110)    # #1a3a6e
REAR_PEAK = (143, 182, 234)      # #8fb6ea
WHITE = (255, 255, 255)

SS = 4  # supersampling factor


def gaussian_band(draw, cx, sigma, height, baseline, left, right, colour, alpha):
    """Fills a gaussian peak between `left` and `right` on the given baseline."""
    pts = []
    step = max(1, (right - left) // 512)
    for x in range(int(left), int(right) + 1, step):
        y = baseline - height * math.exp(-((x - cx) ** 2) / (2.0 * sigma ** 2))
        pts.append((x, y))
    pts.append((right, baseline))
    pts.append((left, baseline))
    draw.polygon(pts, fill=colour + (alpha,))


def render(size: int) -> Image.Image:
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))

    # macOS icon grid: the rounded square covers ~80 % of the canvas, centred.
    margin = int(s * 0.098)
    box = (margin, margin, s - margin, s - margin)
    side = box[2] - box[0]
    radius = int(side * 0.225)

    # vertical gradient inside the squircle
    grad = Image.new("RGBA", (1, side), (0, 0, 0, 0))
    gp = grad.load()
    for y in range(side):
        t = y / max(1, side - 1)
        gp[0, y] = (
            int(ACCENT_TOP[0] + (ACCENT_BOTTOM[0] - ACCENT_TOP[0]) * t),
            int(ACCENT_TOP[1] + (ACCENT_BOTTOM[1] - ACCENT_TOP[1]) * t),
            int(ACCENT_TOP[2] + (ACCENT_BOTTOM[2] - ACCENT_TOP[2]) * t),
            255,
        )
    grad = grad.resize((side, side), Image.BILINEAR)

    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle(box, radius=radius, fill=255)
    img.paste(grad, (box[0], box[1]), mask.crop(box))

    # soft highlight along the top edge
    hi = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(hi).rounded_rectangle(
        (box[0], box[1], box[2], box[1] + int(side * 0.42)), radius=radius, fill=(255, 255, 255, 26)
    )
    hi = hi.filter(ImageFilter.GaussianBlur(side * 0.05))
    img.alpha_composite(Image.composite(hi, Image.new("RGBA", (s, s), (0, 0, 0, 0)), mask))

    # ---- the mark: two overlapping chromatographic peaks -------------------------------
    art = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(art)
    left = box[0] + int(side * 0.17)
    right = box[2] - int(side * 0.17)
    baseline = box[1] + int(side * 0.76)
    span = right - left
    sigma = span * 0.115

    # rear (co-eluting) peak, then the front peak drawn over it
    gaussian_band(d, left + span * 0.63, sigma * 0.92, side * 0.42, baseline, left, right, REAR_PEAK, 235)
    gaussian_band(d, left + span * 0.40, sigma, side * 0.54, baseline, left, right, WHITE, 255)

    # baseline rule
    rule = max(2, int(side * 0.018))
    d.rounded_rectangle((left, baseline - rule / 2, right, baseline + rule / 2),
                        radius=rule / 2, fill=WHITE + (255,))

    img.alpha_composite(Image.composite(art, Image.new("RGBA", (s, s), (0, 0, 0, 0)), mask))
    return img.resize((size, size), Image.LANCZOS)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--size", type=int, default=1024)
    a = ap.parse_args()
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    render(a.size).save(a.out)
    print(f"[icon] {a.out} ({a.size}x{a.size})")


if __name__ == "__main__":
    main()
