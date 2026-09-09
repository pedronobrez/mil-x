#!/usr/bin/env python3
"""Draws the suite's application icons (PNG, macOS .iconset/.icns, Windows .ico, SVG).

Two marks share one identity — the rounded square, the blue gradient, the white chromatographic
peak on a baseline — and differ in what stands behind the peak:

  targeted     one co-eluting peak showing through the front one: the deconvolution of a known
               analyte. OpenQuant's mark, and OpenDIAL's original.
  untargeted   a whole chromatogram behind the front peak, a skyline of smaller peaks across the
               baseline: everything the run contains, not one thing looked for. OpenDIAL's mark.

    python3 tools/make_icon.py --out icon.png [--size 1024] [--variant untargeted|targeted]
    python3 tools/make_icon.py --export DIR --variant targeted     # every size, .iconset, .icns, .ico, .svg

Colours are the shared suite tokens (accent #234b8c, light accent #6f9be0).
"""
import argparse
import math
import os
import shutil
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFilter

ACCENT_TOP = (58, 111, 191)      # #3a6fbf
ACCENT_BOTTOM = (26, 58, 110)    # #1a3a6e
REAR_PEAK = (143, 182, 234)      # #8fb6ea
WHITE = (255, 255, 255)

SS = 4  # supersampling factor

# the skyline of the untargeted mark: (position along the span, height as a share of the front
# peak's, width as a share of the front peak's sigma), left to right — a real-looking chromatogram
# with the front peak standing out of it, and nothing taller than the front peak
SKYLINE = [
    (0.06, 0.22, 0.55), (0.14, 0.38, 0.62), (0.22, 0.30, 0.55), (0.30, 0.52, 0.65),
    (0.55, 0.62, 0.72), (0.66, 0.36, 0.60), (0.75, 0.48, 0.65), (0.84, 0.27, 0.55), (0.93, 0.34, 0.60),
]
FRONT_AT = 0.40   # where the front peak stands, as a share of the span


def gaussian_points(cx, sigma, height, baseline, left, right, steps=512):
    pts = []
    for i in range(steps + 1):
        x = left + (right - left) * i / steps
        y = baseline - height * math.exp(-((x - cx) ** 2) / (2.0 * sigma ** 2))
        pts.append((x, y))
    return pts


def gaussian_band(draw, cx, sigma, height, baseline, left, right, colour, alpha):
    """Fills a gaussian peak between `left` and `right` on the given baseline."""
    pts = gaussian_points(cx, sigma, height, baseline, left, right)
    pts.append((right, baseline))
    pts.append((left, baseline))
    draw.polygon(pts, fill=colour + (alpha,))


def geometry(s):
    """The squircle and the mark's frame for a canvas of `s` pixels."""
    margin = int(s * 0.098)
    box = (margin, margin, s - margin, s - margin)
    side = box[2] - box[0]
    radius = int(side * 0.225)
    left = box[0] + int(side * 0.17)
    right = box[2] - int(side * 0.17)
    baseline = box[1] + int(side * 0.76)
    span = right - left
    sigma = span * 0.115
    return box, side, radius, left, right, baseline, span, sigma


def render(size: int, variant: str = "untargeted") -> Image.Image:
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    box, side, radius, left, right, baseline, span, sigma = geometry(s)

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

    # ---- the mark ------------------------------------------------------------------------
    art = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(art)
    front_height = side * 0.54
    if variant == "targeted":
        # the rear (co-eluting) peak, then the front peak drawn over it
        gaussian_band(d, left + span * 0.63, sigma * 0.92, side * 0.42, baseline, left, right, REAR_PEAK, 235)
    else:
        # the skyline: every smaller peak of the run, faint towards the edges, the front peak's
        # neighbours a little stronger so the whole reads as one chromatogram
        for at, h, w in SKYLINE:
            alpha = 150 + int(70 * (1 - abs(at - FRONT_AT) / 0.6))
            gaussian_band(d, left + span * at, sigma * w, front_height * h, baseline, left, right, REAR_PEAK, max(120, min(235, alpha)))
    gaussian_band(d, left + span * FRONT_AT, sigma, front_height, baseline, left, right, WHITE, 255)

    # baseline rule
    rule = max(2, int(side * 0.018))
    d.rounded_rectangle((left, baseline - rule / 2, right, baseline + rule / 2),
                        radius=rule / 2, fill=WHITE + (255,))

    img.alpha_composite(Image.composite(art, Image.new("RGBA", (s, s), (0, 0, 0, 0)), mask))
    return img.resize((size, size), Image.LANCZOS)


def svg(variant: str, size: int = 1024) -> str:
    """The same mark as vector art: the gradient squircle, the peaks as paths, the baseline."""
    box, side, radius, left, right, baseline, span, sigma = geometry(size)
    front_height = side * 0.54

    def path(cx, sg, h):
        pts = gaussian_points(cx, sg, h, baseline, left, right, steps=160)
        d = "M %.1f %.1f " % (left, baseline) + " ".join("L %.1f %.1f" % p for p in pts) + " L %.1f %.1f Z" % (right, baseline)
        return d

    rear = "#8fb6ea"
    parts = [
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" width="%d" height="%d">' % (size, size, size, size),
        "<defs>",
        '<linearGradient id="g" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3a6fbf"/><stop offset="1" stop-color="#1a3a6e"/></linearGradient>',
        '<linearGradient id="hi" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffffff" stop-opacity="0.12"/><stop offset="1" stop-color="#ffffff" stop-opacity="0"/></linearGradient>',
        '<clipPath id="c"><rect x="%d" y="%d" width="%d" height="%d" rx="%d"/></clipPath>' % (box[0], box[1], side, side, radius),
        "</defs>",
        '<rect x="%d" y="%d" width="%d" height="%d" rx="%d" fill="url(#g)"/>' % (box[0], box[1], side, side, radius),
        '<rect x="%d" y="%d" width="%d" height="%d" rx="%d" fill="url(#hi)"/>' % (box[0], box[1], side, int(side * 0.5), radius),
        '<g clip-path="url(#c)">',
    ]
    if variant == "targeted":
        parts.append('<path d="%s" fill="%s" fill-opacity="0.92"/>' % (path(left + span * 0.63, sigma * 0.92, side * 0.42), rear))
    else:
        for at, h, w in SKYLINE:
            alpha = 150 + int(70 * (1 - abs(at - FRONT_AT) / 0.6))
            parts.append('<path d="%s" fill="%s" fill-opacity="%.2f"/>' % (path(left + span * at, sigma * w, front_height * h), rear, max(120, min(235, alpha)) / 255))
    parts.append('<path d="%s" fill="#ffffff"/>' % path(left + span * FRONT_AT, sigma, front_height))
    rule = max(2, int(side * 0.018))
    parts.append('<rect x="%d" y="%.1f" width="%d" height="%d" rx="%.1f" fill="#ffffff"/>' % (left, baseline - rule / 2, right - left, rule, rule / 2))
    parts.append("</g></svg>")
    return "\n".join(parts)


ICONSET = [(16, "16x16"), (32, "16x16@2x"), (32, "32x32"), (64, "32x32@2x"), (128, "128x128"), (256, "128x128@2x"),
           (256, "256x256"), (512, "256x256@2x"), (512, "512x512"), (1024, "512x512@2x")]
PNG_SIZES = [16, 32, 48, 64, 128, 256, 512, 1024]


def export(folder: str, variant: str, name: str):
    """Every form of one mark: PNGs by size, the macOS iconset and .icns, the Windows .ico, the SVG."""
    os.makedirs(folder, exist_ok=True)
    master = render(1024, variant)
    png_dir = os.path.join(folder, "png")
    os.makedirs(png_dir, exist_ok=True)
    for size in PNG_SIZES:
        (master if size == 1024 else master.resize((size, size), Image.LANCZOS)).save(os.path.join(png_dir, f"{name}-{size}.png"))
    iconset = os.path.join(folder, f"{name}.iconset")
    shutil.rmtree(iconset, ignore_errors=True)
    os.makedirs(iconset)
    for size, label in ICONSET:
        (master if size == 1024 else master.resize((size, size), Image.LANCZOS)).save(os.path.join(iconset, f"icon_{label}.png"))
    icns = os.path.join(folder, f"{name}.icns")
    if shutil.which("iconutil"):
        subprocess.run(["iconutil", "--convert", "icns", iconset, "--output", icns], check=True)
    master.save(os.path.join(folder, f"{name}.ico"), sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    with open(os.path.join(folder, f"{name}.svg"), "w", encoding="utf-8") as handle:
        handle.write(svg(variant))
    print(f"[icon] {variant} -> {folder}: {len(PNG_SIZES)} PNGs, {name}.iconset, {name}.icns, {name}.ico, {name}.svg")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", help="one PNG")
    ap.add_argument("--size", type=int, default=1024)
    ap.add_argument("--variant", choices=["targeted", "untargeted"], default="untargeted")
    ap.add_argument("--export", metavar="DIR", help="every form of the mark into DIR")
    ap.add_argument("--name", help="file stem for --export (default: the variant's app)")
    a = ap.parse_args()
    if a.export:
        export(a.export, a.variant, a.name or ("OpenQuant" if a.variant == "targeted" else "OpenDIAL"))
    if a.out:
        os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
        render(a.size, a.variant).save(a.out)
        print(f"[icon] {a.out} ({a.size}x{a.size}, {a.variant})")
    if not a.export and not a.out:
        ap.error("give --out or --export")
        sys.exit(2)


if __name__ == "__main__":
    main()
