"""Natureza noturna do Bosque Selvagem: recolore tiles_nature.png (Ninja Adventure, CC0) para
a noite azul de lua — folhas verde-azuladas, troncos de ardósia, flores rosadas acesas, neve
em azul-luar. Uso: python gen_night_nature.py <tiles_nature.png> <tiles_nature_night.png>"""
import colorsys
import sys

from PIL import Image

im = Image.open(sys.argv[1]).convert('RGBA')
px = im.load()
for y in range(im.height):
    for x in range(im.width):
        r, g, b, a = px[x, y]
        if a == 0:
            continue
        h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
        deg = h * 360
        if (deg >= 330 or deg < 30) and 0.08 <= s < 0.5 and v > 0.75:   # flores (rosa e pêssego): rosa aceso
            h2, s2, v2 = 345 / 360 if deg < 30 and deg > 10 else h, min(1, s * 1.2 + 0.1), min(1, v * 1.02)
        elif s < 0.16 and v > 0.7:                         # neve / branco: luar
            h2, s2, v2 = 0.58, 0.16, v * 0.88
        elif s < 0.22:                                   # pedra / cinza: ardósia azulada
            h2, s2, v2 = 0.61, max(s, 0.14), v * 0.72
        elif 45 <= deg < 170:                            # folhas: verde-azulado da noite
            h2 = (172 + (deg - 45) * 0.28) / 360
            s2 = min(1, s * 0.78 + 0.05)
            v2 = v * (0.78 if v > 0.75 else 0.62) + 0.03
        elif deg >= 320 or deg < 8:                      # flores rosadas: acesas
            h2, s2, v2 = h, min(1, s * 1.05), min(1, v * 1.05)
        elif 8 <= deg < 45:                              # troncos e terra: violeta escuro
            h2, s2, v2 = 0.72, s * 0.32, v * 0.52
        else:                                            # azuis e roxos: um pouco mais fundos
            h2, s2, v2 = h, s, v * 0.85
        rr, gg, bb = colorsys.hsv_to_rgb(h2, max(0, min(1, s2)), max(0, min(1, v2)))
        px[x, y] = (int(rr * 255), int(gg * 255), int(bb * 255), a)
im.save(sys.argv[2])
print('ok', im.size)
