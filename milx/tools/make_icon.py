#!/usr/bin/env python3
"""Draws the suite's application icons (PNG, macOS .iconset/.icns, Windows .ico, SVG).

Three marks share one tile — the rounded square with the blue gradient — and differ in what stands
on it:

  milx         the cow's head, face on, in white: the MIL-X mark since 1.0. The drawing is
               docs/brand/mil-x-front.svg, reproduced here shape by shape so the icon is built
               without an SVG rasteriser.
  milq         the same head inside a ring, on the MIL-Q green tile: docs/brand/mil-q-front.svg.
               A targeted assay is not a different animal.
  untargeted   a white chromatographic peak with a whole chromatogram behind it: OpenDIAL's mark,
               the one this program carried until 1.0.
  targeted     one co-eluting peak showing through the front one: OpenQuant's mark.

    python3 tools/make_icon.py --out icon.png [--size 1024] [--variant milx|untargeted|targeted]
    python3 tools/make_icon.py --export DIR --variant milx --name MIL-X   # every size, .iconset, .icns, .ico, .svg

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
Q_TOP = (30, 138, 121)           # #1e8a79, the MIL-Q green, lit
Q_BOTTOM = (11, 76, 67)          # #0b4c43
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


# ---- the MIL-X mark: docs/brand/mil-x-front.svg, on its 64-unit grid ----------------------------
# The SVG is one silhouette (two ears, two horns, the head) with the patch, the eyes and the muzzle
# knocked out through a mask, and the two nostrils put back. The same shapes, the same numbers.
HEAD = "M32 13 C41 13 47.5 18.5 47.5 26.5 C47.5 34 45 40.5 41 44.5 C38 47.5 35 49 32 49 C29 49 26 47.5 23 44.5 C19 40.5 16.5 34 16.5 26.5 C16.5 18.5 23 13 32 13 Z"
HORN_L = "M25 15 C21 10 17 9 14 11 C18 12 21 14 23.5 17.5 Z"
HORN_R = "M39 15 C43 10 47 9 50 11 C46 12 43 14 40.5 17.5 Z"
EAR_L = (13, 26, 8.5, 5, -20)     # cx, cy, rx, ry, rotation in degrees
EAR_R = (51, 26, 8.5, 5, 20)
PATCH = (23, 19, 8, 5.5, -20)     # knocked out, clipped to the head
EYES = [(25.5, 29, 2.3), (38.5, 29, 2.3)]
MUZZLE = (24.5, 37, 15, 10, 5)    # x, y, w, h, corner radius
NOSTRILS = [(28.7, 41.5, 1.4, 1.9), (35.3, 41.5, 1.4, 1.9)]
MARK_BOX = (4.5, 9, 59.5, 49)     # what the drawing actually spans, for centring it on the tile


def _svg_path_points(d, steps=48):
    """Flattens an SVG path made of M, C and Z into a polygon."""
    import re
    tokens = re.sub(r"([MCZ])", r" \1 ", d.replace(",", " ")).split()
    pts = []
    i = 0
    cur = None
    while i < len(tokens):
        t = tokens[i]
        if t == "M":
            cur = (float(tokens[i + 1]), float(tokens[i + 2])); pts.append(cur); i += 3
        elif t == "C":
            p1 = (float(tokens[i + 1]), float(tokens[i + 2]))
            p2 = (float(tokens[i + 3]), float(tokens[i + 4]))
            p3 = (float(tokens[i + 5]), float(tokens[i + 6]))
            p0 = cur
            for k in range(1, steps + 1):
                u = k / steps
                x = (1 - u) ** 3 * p0[0] + 3 * (1 - u) ** 2 * u * p1[0] + 3 * (1 - u) * u ** 2 * p2[0] + u ** 3 * p3[0]
                y = (1 - u) ** 3 * p0[1] + 3 * (1 - u) ** 2 * u * p1[1] + 3 * (1 - u) * u ** 2 * p2[1] + u ** 3 * p3[1]
                pts.append((x, y))
            cur = p3; i += 7
        elif t == "Z":
            i += 1
        else:
            raise ValueError("unsupported path command " + t)
    return pts


def _ellipse_points(cx, cy, rx, ry, angle=0.0, steps=96):
    a = math.radians(angle)
    ca, sa = math.cos(a), math.sin(a)
    out = []
    for k in range(steps):
        t = 2 * math.pi * k / steps
        x, y = rx * math.cos(t), ry * math.sin(t)
        out.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    return out


def cow_mask(s, box, side, shrink=1.0, ring=False):
    """The MIL-X mark as an 8-bit mask on a canvas of `s` pixels, centred in the tile's box.
    MIL-Q draws the same head at 0.74 of the size with a ring around it, as its SVG does."""
    span_x = MARK_BOX[2] - MARK_BOX[0]
    span_y = MARK_BOX[3] - MARK_BOX[1]
    scale = side * 0.66 / span_x * shrink
    ox = box[0] + (side - span_x * scale) / 2 - MARK_BOX[0] * scale
    oy = box[1] + (side - span_y * scale) / 2 - MARK_BOX[1] * scale

    def T(pts):
        return [(ox + x * scale, oy + y * scale) for x, y in pts]

    mark = Image.new("L", (s, s), 0)
    d = ImageDraw.Draw(mark)
    d.polygon(T(_ellipse_points(*EAR_L)), fill=255)
    d.polygon(T(_ellipse_points(*EAR_R)), fill=255)
    d.polygon(T(_svg_path_points(HORN_L)), fill=255)
    d.polygon(T(_svg_path_points(HORN_R)), fill=255)
    d.polygon(T(_svg_path_points(HEAD)), fill=255)

    # the patch is knocked out only where it lies on the head
    head_only = Image.new("L", (s, s), 0)
    ImageDraw.Draw(head_only).polygon(T(_svg_path_points(HEAD)), fill=255)
    patch = Image.new("L", (s, s), 0)
    ImageDraw.Draw(patch).polygon(T(_ellipse_points(*PATCH)), fill=255)
    patch = Image.composite(patch, Image.new("L", (s, s), 0), head_only)
    mark = Image.composite(Image.new("L", (s, s), 0), mark, patch)

    knock = Image.new("L", (s, s), 0)
    k = ImageDraw.Draw(knock)
    for cx, cy, r in EYES:
        k.polygon(T(_ellipse_points(cx, cy, r, r)), fill=255)
    mx, my, mw, mh, mr = MUZZLE
    (x0, y0), (x1, y1) = T([(mx, my), (mx + mw, my + mh)])
    k.rounded_rectangle((x0, y0, x1, y1), radius=mr * scale, fill=255)
    mark = Image.composite(Image.new("L", (s, s), 0), mark, knock)

    d = ImageDraw.Draw(mark)   # composite() returned a new image; draw on that one
    for cx, cy, rx, ry in NOSTRILS:
        d.polygon(T(_ellipse_points(cx, cy, rx, ry)), fill=255)
    if ring:
        # the SVG's ring: radius 29.5 and stroke 3 on the 64 grid, around the tile's centre
        unit = side * 0.66 / span_x     # one grid unit at full size, which the ring is drawn at
        cx, cy = box[0] + side / 2, box[1] + side / 2
        r_out, r_in = 31.0 * unit, 28.0 * unit
        d.ellipse((cx - r_out, cy - r_out, cx + r_out, cy + r_out), fill=255)
        d.ellipse((cx - r_in, cy - r_in, cx + r_in, cy + r_in), fill=0)
        # the head was knocked out by the inner disc: draw it once more on top
        head = cow_mask(s, box, side, shrink=shrink, ring=False)
        mark = Image.composite(Image.new("L", (s, s), 255), mark, head)
    return mark


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

    # vertical gradient inside the squircle: blue for MIL-X and the old marks, green for MIL-Q
    top, bottom = (Q_TOP, Q_BOTTOM) if variant == "milq" else (ACCENT_TOP, ACCENT_BOTTOM)
    grad = Image.new("RGBA", (1, side), (0, 0, 0, 0))
    gp = grad.load()
    for y in range(side):
        t = y / max(1, side - 1)
        gp[0, y] = (
            int(top[0] + (bottom[0] - top[0]) * t),
            int(top[1] + (bottom[1] - top[1]) * t),
            int(top[2] + (bottom[2] - top[2]) * t),
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
    if variant in ("milx", "milq"):
        white = Image.new("RGBA", (s, s), WHITE + (255,))
        mask_ = cow_mask(s, box, side) if variant == "milx" else cow_mask(s, box, side, shrink=0.74, ring=True)
        img.alpha_composite(Image.composite(white, Image.new("RGBA", (s, s), (0, 0, 0, 0)), mask_))
        return img.resize((size, size), Image.LANCZOS)

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
    if variant == "milq":
        parts[2] = '<linearGradient id="g" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1e8a79"/><stop offset="1" stop-color="#0b4c43"/></linearGradient>'
        span_x = MARK_BOX[2] - MARK_BOX[0]
        scale = side * 0.66 / span_x
        ox = box[0] + (side - 64 * scale) / 2
        oy = box[1] + (side - 64 * scale) / 2
        parts.append('<g transform="translate(%.2f %.2f) scale(%.4f)">' % (ox, oy, scale))
        parts.append('<mask id="knock" maskUnits="userSpaceOnUse" x="0" y="0" width="64" height="64">'
                     '<rect width="64" height="64" fill="#fff"/>'
                     '<clipPath id="body"><path d="%s"/></clipPath>' % HEAD +
                     '<ellipse cx="%s" cy="%s" rx="%s" ry="%s" transform="rotate(%s %s %s)" fill="#000" clip-path="url(#body)"/>' % (PATCH[0], PATCH[1], PATCH[2], PATCH[3], PATCH[4], PATCH[0], PATCH[1]) +
                     ''.join('<circle cx="%s" cy="%s" r="%s" fill="#000"/>' % e for e in EYES) +
                     '<rect x="%s" y="%s" width="%s" height="%s" rx="%s" fill="#000"/>' % MUZZLE +
                     ''.join('<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="#fff"/>' % n for n in NOSTRILS) +
                     '</mask>')
        parts.append('<g transform="translate(32,32) scale(0.74) translate(-32,-32)"><g mask="url(#knock)" fill="#ffffff">')
        for cx, cy, rx, ry, rot in (EAR_L, EAR_R):
            parts.append('<ellipse cx="%s" cy="%s" rx="%s" ry="%s" transform="rotate(%s %s %s)"/>' % (cx, cy, rx, ry, rot, cx, cy))
        parts.append('<path d="%s"/><path d="%s"/><path d="%s"/>' % (HORN_L, HORN_R, HEAD))
        parts.append('</g></g><circle cx="32" cy="32" r="29.5" fill="none" stroke="#ffffff" stroke-width="3"/></g></g></svg>')
        return "\n".join(parts)
    if variant == "milx":
        span_x = MARK_BOX[2] - MARK_BOX[0]
        span_y = MARK_BOX[3] - MARK_BOX[1]
        scale = side * 0.66 / span_x
        ox = box[0] + (side - span_x * scale) / 2 - MARK_BOX[0] * scale
        oy = box[1] + (side - span_y * scale) / 2 - MARK_BOX[1] * scale
        parts.append('<g transform="translate(%.2f %.2f) scale(%.4f)">' % (ox, oy, scale))
        parts.append('<mask id="knock" maskUnits="userSpaceOnUse" x="0" y="0" width="64" height="64">'
                     '<rect width="64" height="64" fill="#fff"/>'
                     '<clipPath id="body"><path d="%s"/></clipPath>' % HEAD +
                     '<ellipse cx="%s" cy="%s" rx="%s" ry="%s" transform="rotate(%s %s %s)" fill="#000" clip-path="url(#body)"/>' % (PATCH[0], PATCH[1], PATCH[2], PATCH[3], PATCH[4], PATCH[0], PATCH[1]) +
                     ''.join('<circle cx="%s" cy="%s" r="%s" fill="#000"/>' % e for e in EYES) +
                     '<rect x="%s" y="%s" width="%s" height="%s" rx="%s" fill="#000"/>' % MUZZLE +
                     ''.join('<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="#fff"/>' % n for n in NOSTRILS) +
                     '</mask>')
        parts.append('<g mask="url(#knock)" fill="#ffffff">')
        for cx, cy, rx, ry, rot in (EAR_L, EAR_R):
            parts.append('<ellipse cx="%s" cy="%s" rx="%s" ry="%s" transform="rotate(%s %s %s)"/>' % (cx, cy, rx, ry, rot, cx, cy))
        parts.append('<path d="%s"/><path d="%s"/><path d="%s"/>' % (HORN_L, HORN_R, HEAD))
        parts.append('</g></g></g></svg>')
        return "\n".join(parts)
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
    ap.add_argument("--variant", choices=["milx", "milq", "targeted", "untargeted"], default="milx")
    ap.add_argument("--export", metavar="DIR", help="every form of the mark into DIR")
    ap.add_argument("--name", help="file stem for --export (default: the variant's app)")
    a = ap.parse_args()
    if a.export:
        export(a.export, a.variant, a.name or {"targeted": "OpenQuant", "untargeted": "OpenDIAL", "milq": "MIL-Q"}.get(a.variant, "MIL-X"))
    if a.out:
        os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
        render(a.size, a.variant).save(a.out)
        print(f"[icon] {a.out} ({a.size}x{a.size}, {a.variant})")
    if not a.export and not a.out:
        ap.error("give --out or --export")
        sys.exit(2)


if __name__ == "__main__":
    main()
