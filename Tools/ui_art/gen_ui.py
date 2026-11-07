"""Generates Shura's hand-painted UI kit: sumi-e ink on washi paper.

Usage: python -I gen_ui.py <out_dir> <fonts_dir>
Everything is procedural (brush strokes are simulated bristle by bristle), so the style is
the project's own. Outputs RGBA PNGs.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from brush3 import stroke  # noqa: E402

OUT, FONTS = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)

PAPER = np.array([0.925, 0.886, 0.800])
PAPER_DARK = np.array([0.105, 0.094, 0.098])
SHU = np.array([0.776, 0.192, 0.141])


def save(rgb, alpha, name):
    rgba = np.dstack([np.clip(rgb, 0, 1), np.clip(alpha, 0, 1)])
    Image.fromarray((rgba * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(OUT, name))
    print("  ", name, rgba.shape[1], "x", rgba.shape[0])


def noise(h, w, sigma, rng, wrap=True):
    n = rng.standard_normal((h, w))
    f = ndi.gaussian_filter(n, sigma, mode="wrap" if wrap else "reflect")
    return (f - f.mean()) / (f.std() + 1e-9)


def fbm(h, w, rng, sigmas=(48, 24, 12, 6, 3, 1.5), weights=(1, 0.6, 0.4, 0.25, 0.15, 0.08), wrap=True):
    acc = np.zeros((h, w))
    for s, k in zip(sigmas, weights):
        acc += noise(h, w, s, rng, wrap) * k
    return acc / sum(weights)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


# ── Paper ────────────────────────────────────────────────────────────────────────────

def fibers(h, w, rng, count, light, wrap=True):
    """Washi fibres: thin curly strands, drawn tile-safe."""
    img = Image.new("F", (w * 3, h * 3), 0.0)
    d = ImageDraw.Draw(img)
    for _ in range(count):
        x, y = rng.uniform(w, 2 * w), rng.uniform(h, 2 * h)
        a = rng.uniform(0, math.tau)
        pts = []
        L = rng.uniform(8, 40)
        for k in range(8):
            a += rng.normal(0, 0.35)
            x += math.cos(a) * L / 8
            y += math.sin(a) * L / 8
            pts.append((x, y))
        v = rng.uniform(0.3, 1.0) * (1 if light else -1)
        offsets = [(ox * w, oy * h) for ox in (-1, 0, 1) for oy in (-1, 0, 1)] if wrap else [(0, 0)]
        for ox, oy in offsets:
            d.line([(px + ox, py + oy) for px, py in pts], fill=v, width=1)
    arr = np.array(img)[h:2 * h, w:2 * w]
    return ndi.gaussian_filter(arr, 0.6, mode="wrap")


def paper(h, w, rng, base, dark=False, wrap=True):
    mottle = fbm(h, w, rng, wrap=wrap)
    grain = noise(h, w, 0.7, rng, wrap)
    f_light = fibers(h, w, rng, int(h * w / 500), True, wrap)
    f_dark = fibers(h, w, rng, int(h * w / 1400), False, wrap)
    k = 0.6 if dark else 1.0
    lum = 1 + mottle * 0.035 * k + grain * 0.012 + f_light * (0.05 if not dark else 0.08) + f_dark * 0.04
    rgb = base[None, None, :] * lum[..., None]
    if dark:
        rgb += f_light[..., None] * 0.02
    return rgb


# ── Brush ────────────────────────────────────────────────────────────────────────────

def brush(h, w, path, width, rng, bristles=70, dryness=1.0, steps=2400, taper=(0.06, 0.18), pressure=None):
    """Sumi-e stroke (see brush3.stroke); the old bristle parameters map onto dryness."""
    return stroke(h, w, path, width, rng, dryness=min(1.2, dryness * 0.75), steps=min(steps, 1600),
                  entry=max(0.02, taper[0]), exit_=max(0.08, taper[1]), pressure=pressure)

def line_path(x0, y0, x1, y1, wobble, rng, bow=0.0):
    seed = rng.uniform(0, 100)

    def p(t):
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t + math.sin(t * math.pi) * bow + math.sin(t * 7 + seed) * wobble
        return x, y
    return p


# ── Kit pieces ───────────────────────────────────────────────────────────────────────

def make_paper_tiles():
    rng = np.random.default_rng(11)
    p = paper(256, 256, rng, PAPER)
    save(p, np.ones((256, 256)), "paper_tile.png")
    d = paper(256, 256, rng, PAPER_DARK, dark=True)
    save(d, np.ones((256, 256)), "paper_dark_tile.png")


def torn_mask(h, w, margin, rng, roughness=5.0):
    """Rectangle with torn, fibrous edges: returns (alpha, distance-to-edge in px)."""
    yy, xx = np.mgrid[0:h, 0:w]
    dist = np.minimum.reduce([xx - margin, w - 1 - margin - xx, yy - margin, h - 1 - margin - yy]).astype(float)
    wob = noise(h, w, 3.5, rng, False) * roughness + noise(h, w, 1.2, rng, False) * roughness * 0.35
    d = dist + wob
    alpha = smoothstep(-0.8, 0.8, d)
    return alpha, d


def make_panels():
    rng = np.random.default_rng(23)
    # Light washi panel, 9-sliced with border 72 (its centre is drawn too, but plain paper).
    S = 384
    base = paper(S, S, rng, PAPER, wrap=False)
    alpha, d = torn_mask(S, S, 10, rng, 5.5)
    burn = 1 - smoothstep(0, 30, d)
    stain = fbm(S, S, rng, wrap=False)
    burn = np.clip(burn * (0.8 + 0.4 * stain), 0, 1)
    tone = np.array([0.55, 0.40, 0.26])
    rgb = base * (1 - 0.32 * burn[..., None]) + tone * 0.32 * burn[..., None] * 0.6
    # A dry ink border painted just inside the edge, broken in places.
    ink = np.zeros((S, S))
    for (x0, y0, x1, y1) in [(26, 24, S - 26, 26), (S - 24, 26, S - 26, S - 26), (S - 26, S - 24, 26, S - 26), (24, S - 26, 26, 26)]:
        ink = np.maximum(ink, brush(S, S, line_path(x0, y0, x1, y1, 0.8, rng), 5, rng, bristles=24, dryness=1.4, taper=(0.12, 0.25)))
    rgb = rgb * (1 - ink[..., None] * 0.75) + np.array([0.12, 0.1, 0.09]) * ink[..., None] * 0.75
    # The centre is left transparent: the game tiles paper there so fibres stay sharp at any size.
    yy, xx = np.mgrid[0:S, 0:S]
    edge_dist = np.minimum.reduce([xx, S - 1 - xx, yy, S - 1 - yy]).astype(float)
    alpha = alpha * (1 - smoothstep(46, 72, edge_dist))
    save(rgb, alpha, "panel_paper.png")

    # Dark ink-wash panel: a wide brush wash with uneven edges, translucent.
    W, H = 512, 256
    wash = np.zeros((H, W))
    rows = 7
    for i in range(rows):
        y = 34 + i * (H - 68) / (rows - 1)
        wash = np.maximum(wash, brush(H, W, line_path(18, y + rng.normal(0, 3), W - 18, y + rng.normal(0, 3), 1.5, rng),
                                      52, rng, bristles=90, dryness=0.35, taper=(0.03, 0.06)))
    wash = ndi.gaussian_filter(wash, 1.2)
    edge = torn_mask(H, W, 14, rng, 7)[0]
    a = np.clip(wash * 1.15, 0, 1) * edge * 0.9
    tex = paper(H, W, rng, PAPER_DARK, dark=True, wrap=False)
    save(tex, a, "panel_ink.png")


def make_strokes():
    rng = np.random.default_rng(31)
    W, H = 768, 128
    a = brush(H, W, line_path(30, 66, W - 30, 62, 2.0, rng, bow=-4), 74, rng, bristles=110, dryness=0.9, taper=(0.04, 0.22))
    save(np.zeros((H, W, 3)), a, "stroke_wide.png")
    W, H = 640, 72
    a = brush(H, W, line_path(14, 36, W - 14, 35, 1.0, rng), 40, rng, bristles=80, dryness=0.55, taper=(0.02, 0.08))
    save(np.zeros((H, W, 3)), a, "stroke_bar.png")
    W, H = 640, 72
    a = brush(H, W, line_path(14, 36, W - 14, 36, 0.6, rng), 44, rng, bristles=100, dryness=0.15, taper=(0.015, 0.03))
    save(np.zeros((H, W, 3)), a, "stroke_fill.png")
    W, H = 640, 28
    a = brush(H, W, line_path(10, 14, W - 10, 13, 0.6, rng), 7, rng, bristles=26, dryness=1.0, taper=(0.1, 0.35))
    save(np.zeros((H, W, 3)), a, "divider.png")


def make_enso():
    rng = np.random.default_rng(41)
    S = 384
    c = S / 2
    R = S * 0.38
    a0 = math.radians(115)
    span = math.radians(328)
    seed = rng.uniform(0, 10)

    def p(t):
        ang = a0 - span * t
        r = R * (1 + 0.025 * math.sin(t * 5 + seed) + 0.03 * t)
        return c + math.cos(ang) * r, c - math.sin(ang) * r

    def pressure(t):
        return 0.75 + 0.45 * np.exp(-((t - 0.1) / 0.12) ** 2) + 0.15 * np.sin(t * 9)
    a = brush(S, S, p, 40, rng, bristles=120, dryness=1.15, steps=3400, taper=(0.03, 0.12), pressure=pressure)
    save(np.zeros((S, S, 3)), a, "enso.png")


def make_dab_and_splats():
    rng = np.random.default_rng(53)
    S = 96
    yy, xx = np.mgrid[0:S, 0:S]
    r = np.hypot(xx - S / 2, yy - S / 2) + noise(S, S, 2.5, rng, False) * 3.5
    a = smoothstep(S * 0.36, S * 0.30, r)
    a *= 0.85 + 0.15 * noise(S, S, 1.0, rng, False).clip(-1, 1)
    save(np.zeros((S, S, 3)), np.clip(a, 0, 1), "dab.png")

    for v in range(3):
        S = 320
        acc = np.zeros((S, S))
        yy, xx = np.mgrid[0:S, 0:S]
        c = np.array([S / 2, S / 2]) + rng.normal(0, 8, 2)
        acc += smoothstep(S * 0.2, S * 0.12, np.hypot(xx - c[0], yy - c[1]) + noise(S, S, 6, rng, False) * 14)
        for _ in range(rng.integers(24, 40)):
            ang = rng.uniform(0, math.tau)
            dist = abs(rng.normal(S * 0.22, S * 0.1))
            rad = max(2.0, rng.gamma(1.6, 4.0) * (1 - dist / (S * 0.5)))
            px, py = c[0] + math.cos(ang) * dist, c[1] + math.sin(ang) * dist
            acc += smoothstep(rad, rad * 0.6, np.hypot(xx - px, yy - py))
            if rng.random() < 0.35:
                # A streak of thrown ink toward the drop.
                for s in np.linspace(0.6, 1.0, 10):
                    qx, qy = c[0] + math.cos(ang) * dist * s, c[1] + math.sin(ang) * dist * s
                    acc += smoothstep(rad * 0.5, rad * 0.2, np.hypot(xx - qx, yy - qy))
        a = np.clip(ndi.gaussian_filter(acc, 1.0) * 1.6, 0, 1)
        save(np.zeros((S, S, 3)), a, "splat_%d.png" % v)


def make_seal_and_logo():
    rng = np.random.default_rng(67)
    yuji = os.path.join(FONTS, "yujisyuku_YujiSyuku-Regular.ttf")

    # Hanko seal: vermilion square, 修羅 carved out in white, imperfect ink transfer.
    S = 200
    img = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype(yuji, 82)
    for i, ch in enumerate("修羅"):
        d.text((S / 2, 56 + i * 86), ch, font=font, fill=255, anchor="mm")
    glyph = np.array(img) / 255.0
    sq, dist = torn_mask(S, S, 14, rng, 3.0)
    border = smoothstep(0, 1.5, dist) * (1 - smoothstep(9, 10.5, dist))
    body = sq * (1 - glyph)
    body = np.maximum(body * (dist > 13), border)
    speck = noise(S, S, 1.3, rng, False)
    body *= (speck > -1.6) * (0.82 + 0.18 * smoothstep(-1, 1, noise(S, S, 5, rng, False)))
    rgb = np.ones((S, S, 3)) * SHU
    save(rgb, np.clip(body, 0, 1), "seal.png")

    # Logo: 修羅 in a big brush hand, with dry-brush streaks, bleed and splatter.
    W, H = 1100, 560
    img = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype(yuji, 420)
    d.text((W / 2, H / 2 + 10), "修羅", font=font, fill=255, anchor="mm")
    g = np.array(img) / 255.0
    yy, xx = np.mgrid[0:H, 0:W]
    # Dry streaks run diagonally like a brush dragged across the paper.
    streak = noise(H, W, 1.0, rng, False)
    streak = ndi.gaussian_filter(streak, (0.6, 9.0))
    streak = ndi.rotate(streak, -25, reshape=False, mode="reflect")
    dry = smoothstep(-2.3, -1.7, streak / (streak.std() + 1e-9))
    bleed = ndi.gaussian_filter(g, 2.2)
    edge = smoothstep(0.35, 0.65, bleed + noise(H, W, 1.5, rng, False) * 0.08)
    a = edge * (0.25 + 0.75 * dry)
    halo = ndi.gaussian_filter(g, 14) * 0.18
    a = np.clip(np.maximum(a, halo), 0, 1)
    save(np.zeros((H, W, 3)), a, "logo.png")


# ── Backdrop painting ────────────────────────────────────────────────────────────────

def ridge(width, rng, base, amp, rough=0.55, octaves=8):
    """1D midpoint-displacement mountain ridge."""
    n = 2 ** octaves + 1
    ys = np.zeros(n)
    ys[0], ys[-1] = rng.normal(0, amp * 0.3), rng.normal(0, amp * 0.3)
    step, scale = n - 1, amp
    while step > 1:
        half = step // 2
        for i in range(half, n - 1, step):
            ys[i] = (ys[i - half] + ys[i + half]) / 2 + rng.normal(0, scale)
        step = half
        scale *= rough
    xs = np.linspace(0, width - 1, n)
    return base + np.interp(np.arange(width), xs, ys)


def make_backdrop():
    rng = np.random.default_rng(97)
    W, H = 1920, 1080
    rgb = paper(H, W, rng, PAPER, wrap=False)
    yy, xx = np.mgrid[0:H, 0:W]
    # Warm light from the top, cooler and paler toward the horizon.
    rgb *= (1.0 - 0.06 * (yy / H))[..., None]

    # The red sun, painted with a soft uneven edge.
    sx, sy, sr = 1230, 330, 205
    r = np.hypot(xx - sx, yy - sy) + noise(H, W, 4, rng, False) * 4
    sun = smoothstep(sr, sr - 5, r) * (0.92 + 0.05 * noise(H, W, 2.5, rng, False))
    rim = smoothstep(sr - 26, sr - 2, r)
    sun_col = SHU[None, None, :] * (1.04 - 0.12 * rim[..., None])
    rgb = rgb * (1 - sun[..., None]) + sun_col * sun[..., None]

    ink = np.array([0.09, 0.08, 0.08])
    layers = [
        (520, 120, 0.16, 0.55),
        (610, 150, 0.30, 0.6),
        (720, 120, 0.52, 0.62),
        (850, 110, 0.85, 0.6),
    ]
    for i, (base, amp, strength, rough) in enumerate(layers):
        top = ridge(W, rng, base, amp, rough)
        depth = yy - top[None, :]
        inside = smoothstep(-1.5, 1.5, depth)
        # Ink pools at the ridge and washes out downward into mist.
        wash = np.exp(-np.clip(depth, 0, None) / (60 + 40 * i)) * 0.85 + 0.15
        tex = 0.85 + 0.15 * noise(H, W, 2.0 + i, rng, False)
        a = inside * wash * strength * tex
        if i == len(layers) - 1:
            a = inside * strength * (0.75 + 0.25 * wash) * tex
        rgb = rgb * (1 - a[..., None]) + ink * a[..., None]
        # Mist band floating above the next layer.
        mist = np.exp(-((yy - (base + amp * 0.9)) / 50.0) ** 2) * 0.35
        rgb = rgb * (1 - mist[..., None]) + PAPER * mist[..., None]

    # A pine leaning in from the left: trunk and branches as brush strokes, needles as dabs.
    tree = np.zeros((H, W))
    trunk = lambda t: (W - (150 + 250 * t + 60 * math.sin(t * 3.0)), 1080 - 760 * t)
    tree = np.maximum(tree, brush(H, W, trunk, 46, rng, bristles=90, dryness=0.5, taper=(0.0, 0.5)))
    for (t0, length, ang, wdt) in [(0.45, 330, -0.15, 18), (0.62, 380, 0.05, 16), (0.78, 300, -0.25, 14), (0.9, 240, 0.25, 11)]:
        bx, by = trunk(t0)

        def branch(t, bx=bx, by=by, length=length, ang=ang):
            return bx - length * t, by - length * t * math.tan(ang) - 30 * math.sin(t * 2.5)
        tree = np.maximum(tree, brush(H, W, branch, wdt, rng, bristles=50, dryness=0.9, taper=(0.0, 0.4)))
        for k in range(26):
            t = rng.uniform(0.2, 1.0)
            px, py = branch(t)
            px += rng.normal(0, 30)
            py += rng.normal(-20, 18)
            rx, ry = rng.uniform(18, 40), rng.uniform(6, 13)
            blob = smoothstep(1.0, 0.6, np.hypot((xx - px) / rx, (yy - py) / ry) + noise(H, W, 2, rng, False) * 0.12)
            tree = np.maximum(tree, blob * rng.uniform(0.75, 0.95))
    rgb = rgb * (1 - tree[..., None]) + ink * tree[..., None]

    # Birds crossing the sun.
    birds = np.zeros((H, W))
    for (bx, by, s) in [(1110, 290, 1.0), (1170, 262, 0.8), (1060, 320, 0.7), (1215, 305, 0.6)]:
        wing = lambda t, bx=bx, by=by, s=s: (bx - 22 * s + 44 * s * t, by - 10 * s * abs(math.sin(t * math.pi)) * (1 if t < 0.5 else 1) + 8 * s * (abs(t - 0.5) * 2))
        birds = np.maximum(birds, brush(H, W, wing, 4 * s + 2, rng, bristles=12, dryness=0.4, steps=300, taper=(0.2, 0.2)))
    rgb = rgb * (1 - birds[..., None]) + ink * birds[..., None]

    # Edge vignette of aged paper.
    v = np.hypot((xx - W / 2) / (W / 2), (yy - H / 2) / (H / 2))
    burn = smoothstep(0.8, 1.45, v + noise(H, W, 30, rng, False) * 0.06)
    rgb = rgb * (1 - 0.35 * burn[..., None]) + np.array([0.45, 0.33, 0.22]) * 0.35 * burn[..., None] * 0.5
    save(rgb, np.ones((H, W)), "backdrop.png")


def make_vignette():
    rng = np.random.default_rng(101)
    W, H = 640, 360
    yy, xx = np.mgrid[0:H, 0:W]
    v = np.maximum(np.abs(xx - W / 2) / (W / 2), np.abs(yy - H / 2) / (H / 2))
    v = v + noise(H, W, 6, rng, False) * 0.05 + noise(H, W, 1.5, rng, False) * 0.015
    a = smoothstep(0.62, 1.0, v)
    save(np.zeros((H, W, 3)), a, "vignette_ink.png")


def make_petal():
    S = 48
    yy, xx = np.mgrid[0:S, 0:S]
    x = (xx - S / 2) / (S * 0.42)
    y = (yy - S / 2) / (S * 0.3)
    shape = (x ** 2 + y ** 2) < 1
    notch = np.hypot(x - 1.0, y) < 0.35
    a = ndi.gaussian_filter((shape & ~notch).astype(float), 0.8)
    rgb = np.ones((S, S, 3)) * np.array([0.96, 0.78, 0.82])
    rgb *= (0.9 + 0.1 * (1 - np.abs(y)))[..., None]
    save(rgb, a, "petal.png")


def whiten(names):
    for n in names:
        path = os.path.join(OUT, n)
        im = np.array(Image.open(path).convert("RGBA"))
        im[..., :3] = 255
        Image.fromarray(im, "RGBA").save(path)


print("gerando kit de UI:")
make_paper_tiles()
make_panels()
make_strokes()
make_enso()
make_dab_and_splats()
make_seal_and_logo()
make_vignette()
make_petal()
make_backdrop()
whiten(["stroke_wide.png", "stroke_bar.png", "stroke_fill.png", "divider.png", "enso.png", "dab.png", "splat_0.png", "splat_1.png", "splat_2.png", "logo.png", "vignette_ink.png"])
print("ok")
