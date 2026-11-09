"""
Paints the Shura application icon: an ink ensō on a night-ink tile, cut through by a
katana, with a drop of blood and the red seal 修羅. Writes the 1024 px master PNG (for
Unity's player icon) and a multi-size .ico (for the launcher executable).

    python -I make_icon.py <out_dir>
"""
import math
import random
import sys
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

S = 1024
random.seed(7)


def rounded_mask(size, radius):
    m = Image.new("L", (size, size), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, size - 1, size - 1], radius=radius, fill=255)
    return m


def background():
    bg = Image.new("RGBA", (S, S), (16, 13, 20, 255))
    # A crimson dusk glow behind the circle.
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    for r in range(520, 0, -8):
        a = int(150 * (1 - r / 520) ** 1.6)
        g.ellipse([S / 2 - r, S / 2 - r - 20, S / 2 + r, S / 2 + r - 20], fill=(150, 28, 30, a))
    glow = glow.filter(ImageFilter.GaussianBlur(40))
    bg = Image.alpha_composite(bg, glow)
    # Washi fibres.
    fib = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    f = ImageDraw.Draw(fib)
    for _ in range(900):
        x, y = random.uniform(0, S), random.uniform(0, S)
        ang = random.uniform(0, math.pi)
        ln = random.uniform(6, 26)
        f.line([x, y, x + math.cos(ang) * ln, y + math.sin(ang) * ln], fill=(235, 220, 200, random.randint(6, 18)), width=1)
    bg = Image.alpha_composite(bg, fib)
    return bg


def enso(img):
    """A dry-brush ensō: each bristle a stroke of its own, thick and wet where the brush lands,
    breaking into streaks as the ink runs out."""
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    cx, cy, R = S * 0.5, S * 0.47, S * 0.33
    start, sweep = math.radians(118), math.radians(322)
    bristles = 70
    steps = 420
    for b in range(bristles):
        o = random.uniform(-1, 1)
        # Bristles at the edges run dry first.
        end = min(1.0, random.uniform(0.55, 1.05) - 0.25 * abs(o) ** 2)
        thick = random.uniform(3.0, 7.5)
        tone = random.randint(205, 245)
        prev = None
        skip = 0
        for i in range(steps):
            t = i / steps
            if t > end:
                break
            a = start + sweep * t
            half = S * (0.07 * (1 - t) ** 0.55 + 0.014)
            wob = math.sin(t * 8 + b * 0.3) * 3 + math.sin(t * 21) * 1.5
            r = R + wob + o * half
            x, y = cx + math.cos(a) * r, cy + math.sin(a) * r
            # Dry streaks: gaps open up as the bristle runs out.
            if skip > 0:
                skip -= 1
                prev = None
                continue
            if t > end * 0.6 and random.random() < 0.06 + 0.25 * (t / end):
                skip = random.randint(1, 5)
                prev = None
                continue
            alpha = int(255 * (0.82 + 0.18 * random.random()) * (1 - 0.45 * (t / end) ** 3))
            col = (tone, tone - 10, tone - 30, alpha)
            if prev is not None:
                d.line([prev, (x, y)], fill=col, width=max(1, int(thick * (1 - 0.5 * t))))
            prev = (x, y)
    layer = layer.filter(ImageFilter.GaussianBlur(0.7))
    return Image.alpha_composite(img, layer)


def katana(img):
    """A blade cutting through the circle, edge catching the light."""
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    ang = math.radians(-38)
    ux, uy = math.cos(ang), math.sin(ang)
    nx, ny = -uy, ux
    # Grip at the bottom left, tip past the top right.
    gx, gy = S * 0.25, S * 0.79
    blade_len = S * 0.78
    tip = (gx + ux * blade_len, gy + uy * blade_len)

    def pt(along, side):
        return (gx + ux * along + nx * side, gy + uy * along + ny * side)

    # Handle (tsuka): dark wrap with diamonds.
    d.polygon([pt(-170, -21), pt(40, -21), pt(40, 21), pt(-170, 21)], fill=(28, 22, 26, 255))
    for k in range(-160, 30, 30):
        d.polygon([pt(k, 0), pt(k + 15, -16), pt(k + 30, 0), pt(k + 15, 16)], fill=(160, 32, 36, 255))
    kc = pt(-178, 0)
    d.ellipse([kc[0] - 22, kc[1] - 22, kc[0] + 22, kc[1] + 22], fill=(201, 158, 70, 255))
    # Tsuba.
    c = pt(48, 0)
    d.ellipse([c[0] - 56, c[1] - 56, c[0] + 56, c[1] + 56], fill=(214, 168, 72, 255), outline=(90, 60, 20, 255), width=7)
    d.ellipse([c[0] - 14, c[1] - 14, c[0] + 14, c[1] + 14], fill=(60, 40, 14, 255))
    # Blade: spine, body and a bright edge, curving very slightly.
    body = []
    edge = []
    n = 40
    for i in range(n + 1):
        t = i / n
        along = 60 + t * (blade_len - 60)
        curve = -18 * math.sin(t * math.pi * 0.9)
        half = 21 * (1 - t ** 3) + 2
        body.append(pt(along, curve - half))
        edge.append(pt(along, curve + half))
    d.polygon(body + edge[::-1], fill=(196, 204, 214, 255))
    d.line(edge, fill=(255, 255, 255, 255), width=5)
    d.line(body, fill=(110, 118, 130, 255), width=4)
    # Hamon shimmer.
    hamon = [pt(60 + t / n * (blade_len - 120), -18 * math.sin(t / n * math.pi * 0.9) + 5 + 3 * math.sin(t * 1.7)) for t in range(n + 1)]
    d.line(hamon, fill=(235, 240, 248, 200), width=3)
    shadow = layer.filter(ImageFilter.GaussianBlur(10))
    shadow = Image.eval(shadow, lambda v: v)
    sh = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sh.paste((0, 0, 0, 150), (14, 18), shadow.split()[3])
    img = Image.alpha_composite(img, sh)
    img = Image.alpha_composite(img, layer)
    return img, tip


def blood(img, tip):
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    # A spray off the edge, near where the blade leaves the circle.
    cx, cy = S * 0.66, S * 0.36
    for _ in range(70):
        r = random.uniform(2, 16) * random.choice([1, 1, 1, 1.8])
        x = cx + random.gauss(0, 70) + 60
        y = cy + random.gauss(0, 50) - 30
        d.ellipse([x - r, y - r, x + r, y + r], fill=(178, 22, 30, random.randint(200, 255)))
    # One fat drop running down from the blade.
    x, y = S * 0.6, S * 0.47
    d.ellipse([x - 20, y - 6, x + 20, y + 40], fill=(190, 24, 32, 255))
    d.polygon([(x - 13, y + 4), (x, y - 40), (x + 13, y + 4)], fill=(190, 24, 32, 255))
    d.ellipse([x - 8, y + 6, x, y + 16], fill=(255, 140, 130, 200))
    return Image.alpha_composite(img, layer.filter(ImageFilter.GaussianBlur(0.6)))


def seal(img):
    """The red hanko 修羅, carved white, a little rough at the edges."""
    size = 250
    stamp = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(stamp)
    d.rounded_rectangle([8, 8, size - 8, size - 8], radius=26, fill=(196, 34, 30, 255))
    font = None
    for name in ("C:/Windows/Fonts/YuGothB.ttc", "C:/Windows/Fonts/msgothic.ttc"):
        try:
            font = ImageFont.truetype(name, 104)
            break
        except OSError:
            continue
    for i, ch in enumerate("修羅"):
        bbox = d.textbbox((0, 0), ch, font=font)
        w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
        d.text(((size - w) / 2 - bbox[0], 22 + i * 108 - bbox[1] + (100 - h) / 2), ch, font=font, fill=(246, 232, 214, 255))
    # Worn stamp: speckles of missing ink.
    px = stamp.load()
    for _ in range(2600):
        x, y = random.randrange(size), random.randrange(size)
        if px[x, y][3] > 0 and random.random() < 0.55:
            px[x, y] = (px[x, y][0], px[x, y][1], px[x, y][2], random.randint(60, 200))
    stamp = stamp.rotate(-6, resample=Image.BICUBIC, expand=True)
    x, y = S - stamp.width - 46, S - stamp.height - 40
    img.alpha_composite(stamp, (x, y))
    return img


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    img = background()
    img = enso(img)
    img, tip = katana(img)
    img = blood(img, tip)
    img = seal(img)
    # Rounded tile with a thin gold rim.
    rim = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(rim).rounded_rectangle([6, 6, S - 7, S - 7], radius=200, outline=(201, 158, 70, 200), width=10)
    img = Image.alpha_composite(img, rim)
    mask = rounded_mask(S, 206)
    final = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    final.paste(img, (0, 0), mask)
    final.save(os.path.join(out, "shura_icon_1024.png"))
    # Small sizes: the seal and the fine bristles vanish; the ensō, blade and blood stay readable.
    final.save(os.path.join(out, "shura.ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    final.resize((256, 256), Image.LANCZOS).save(os.path.join(out, "shura_icon_256.png"))
    print("ok", out)


if __name__ == "__main__":
    main()
