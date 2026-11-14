"""Folha das armadilhas samurai (Resources/Art/Extra/traps_samurai.png).

Base: "aMazing Maze Sprites" de TimberwolfGames (itch.io): as estacas que sobem e o totem,
recoloridos (bambu, pedra e oni) e redesenhados por cima. A katana, a tsuba, o fosso com
moldura de madeira e o chão chamuscado são desenhados aqui, no mesmo traço (contorno escuro,
três tons). Uso: python gen_traps.py <maze_sprites.png> <saida.png>

Layout (pixels, origem no canto superior esquerdo):
  y 0-31   takeyari: 6 quadros 16x32 (fosso; pontas; subindo x3; erguidas)
  y 32-39  katana: 4 quadros 48x8 (limpa -> ensanguentada), cabo à esquerda, ponta à direita
  y 40-55  x 0 tsuba (eixo que gira) / x 16 pedestal de pedra / x 32 chão chamuscado
  y 56-87  oni de pedra: 2 quadros 16x32 (adormecido, em brasa)
  y 88-103 fosso em peças (para fossos de várias células com uma moldura só por fora):
           x 0 fundo · x 16 fundo com a parede de dentro · x 32/48/64/80 moldura de cima/baixo/esquerda/direita
  As estacas (y 0-31) são desenhadas sem o fosso: o fosso vem das peças.
"""
import math
import random
import sys

from PIL import Image

src = Image.open(sys.argv[1]).convert('RGB')
BG = {(234, 234, 234), (225, 225, 225)}
OUT = Image.new('RGBA', (192, 104), (0, 0, 0, 0))


def tile(box, table=None):
    c = src.crop(box).convert('RGBA')
    px = c.load()
    for y in range(c.height):
        for x in range(c.width):
            p = px[x, y][:3]
            if p in BG:
                px[x, y] = (0, 0, 0, 0)
            elif table and p in table:
                px[x, y] = table[p] + (255,)
    return c


def put(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), c if len(c) == 4 else c + (255,))


# Takeyari: bambu afiado num fosso com moldura de madeira
BAMBOO = {(42, 42, 42): (26, 34, 20), (135, 97, 76): (204, 214, 128), (111, 75, 58): (156, 178, 84),
          (82, 54, 43): (106, 132, 58), (44, 28, 23): (62, 78, 36), (15, 15, 15): (18, 22, 12)}


def pit():
    p = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    plank, plank_d, edge = (104, 74, 50), (76, 52, 36), (22, 16, 12)
    for y in range(16):
        for x in range(16):
            border = x in (0, 15) or y in (0, 15)
            frame = x <= 1 or x >= 14 or y <= 1 or y >= 14
            if border:
                put(p, x, y, edge)
            elif frame:
                c = plank if (x + y * 3) % 7 else plank_d
                if y == 1 or x == 1:
                    c = (132, 98, 66)
                put(p, x, y, c)
            else:
                c = (52, 36, 28) if y <= 3 else (24, 16, 14) if y <= 6 else (14, 10, 10)
                put(p, x, y, c)
    for x in (1, 14):
        put(p, x, 1, (150, 150, 160))
        put(p, x, 14, (150, 150, 160))
    return p


PIT = pit()
for f in range(6):
    cell = Image.new('RGBA', (16, 32), (0, 0, 0, 0))
    if f > 0:
        k = 5 - f
        tall = tile((224 + 16 * k, 64, 240 + 16 * k, 96), BAMBOO)
        # Três estacas: a do meio (uma cópia da esquerda) um pouco atrás e mais alta.
        middle = tall.crop((2, 0, 6, 32))
        cell.alpha_composite(middle, (6, -2))
        cell.alpha_composite(tall, (0, 0))
    OUT.alpha_composite(cell, (f * 16, 0))

# Katana: 48x8, cabo à esquerda
STEEL_L, STEEL_M, STEEL_D, LINE = (222, 224, 232), (176, 180, 190), (128, 132, 146), (34, 34, 42)
GOLD, GOLD_D = (214, 170, 72), (150, 108, 40)
BLOOD = [(206, 48, 45), (199, 68, 65), (161, 30, 30), (120, 18, 22)]


def katana(level):
    k = Image.new('RGBA', (48, 8), (0, 0, 0, 0))
    for x in range(0, 11):
        for y in range(2, 6):
            if y in (2, 5):
                put(k, x, y, LINE)
            else:
                put(k, x, y, (232, 222, 196) if (x + y) % 3 == 0 else (46, 30, 54))
    put(k, 0, 3, GOLD)
    put(k, 0, 4, GOLD_D)
    for y in range(0, 8):
        put(k, 11, y, GOLD_D if y in (0, 7) else GOLD)
        put(k, 12, y, LINE if y in (0, 7) else GOLD_D)
    for y in range(2, 6):
        put(k, 13, y, GOLD)
    for x in range(14, 47):
        top = 2 if x < 42 else 2 + (x - 41) // 2
        for y in range(top, 6):
            if y == top:
                c = LINE
            elif y == 5:
                c = STEEL_L
            elif y == 4:
                c = STEEL_M if (x * 7) % 9 else STEEL_L
            else:
                c = STEEL_D
            put(k, x, y, c)
        put(k, x, 6, LINE)
    put(k, 47, 5, LINE)
    rnd = random.Random(7)
    for x in range(14, 47):
        t = (x - 14) / 32.0
        for y in range(3, 7):
            p = k.getpixel((x, y))
            if p[3] == 0 or p[:3] == LINE:
                continue
            if rnd.random() < level * 0.18 * (0.3 + t):
                put(k, x, y, BLOOD[rnd.randrange(3)])
        if level >= 2 and rnd.random() < 0.05 * level:
            put(k, x, 7, BLOOD[3])
    return k


for lv in range(4):
    OUT.alpha_composite(katana(lv), (lv * 48, 32))


def disc(size, fill, rim, line, light):
    d = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    r = size / 2 - 0.5
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            dd = ((x - c) ** 2 + (y - c) ** 2) ** 0.5
            if dd > r + 0.3:
                continue
            col = line if dd > r - 0.7 else rim if (rim and dd > r - 2.0) else fill
            if col == fill and x + y < size * 0.75:
                col = light
            put(d, x, y, col)
    return d


ts = disc(16, (54, 50, 62), GOLD, LINE, (78, 74, 90))
for (x, y) in [(5, 5), (10, 5), (5, 10), (10, 10)]:
    put(ts, x, y, (16, 14, 20))
    put(ts, x + (1 if x < 8 else -1), y, (16, 14, 20))
for y in range(6, 10):
    put(ts, 7, y, (16, 14, 20))
    put(ts, 8, y, (16, 14, 20))
OUT.alpha_composite(ts, (0, 40))
st = disc(16, (88, 86, 98), None, (30, 30, 36), (116, 114, 126))
for (x, y) in [(4, 10), (5, 11), (11, 4), (10, 12)]:
    put(st, x, y, (70, 110, 72))
OUT.alpha_composite(st, (16, 40))
sc = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
rnd = random.Random(3)
for y in range(16):
    for x in range(16):
        dx, dy = (x - 7.5) / 8, (y - 7.5) / 6
        if dx * dx + dy * dy > 1 or rnd.random() < 0.18:
            continue
        put(sc, x, y, (32, 24, 22) if rnd.random() < 0.8 else (60, 44, 36))
for x in range(2, 14):
    y = 7 + int(2 * math.sin(x * 0.9))
    put(sc, x, y, (240, 120, 50))
    if x % 3 == 0:
        put(sc, x, y - 1, (255, 210, 120))
OUT.alpha_composite(sc, (32, 40))

# Oni de pedra: o totem do pacote com chifres, olhos e boca
STONE = {(42, 42, 42): (26, 24, 32), (111, 75, 58): (122, 120, 134), (135, 97, 76): (156, 154, 168),
         (44, 28, 23): (66, 64, 76), (82, 54, 43): (92, 90, 104),
         (102, 39, 34): (120, 30, 34), (131, 52, 45): (160, 44, 40), (159, 55, 45): (196, 64, 50)}
for f in range(2):
    o = tile((16, 208, 32, 240), STONE)
    for (x, y) in [(3, 8), (3, 9), (12, 8), (12, 9), (4, 7), (11, 7)]:
        put(o, x, y, (232, 222, 192))
    eye = (255, 224, 96) if f else (40, 12, 14)
    for x in (6, 9):
        put(o, x, 15, eye)
    mouth = (255, 150, 50) if f else (70, 22, 22)
    for x in range(6, 10):
        put(o, x, 16, mouth)
    OUT.alpha_composite(o, (f * 16, 56))

# Fosso em peças: fundo (com e sem a parede de dentro) e as quatro bordas da moldura.
def piece(keep, wall=True):
    q = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    for y in range(16):
        for x in range(16):
            if not keep(x, y):
                continue
            c = PIT.getpixel((x, y))
            if not wall and keep is HOLE:
                c = (14, 10, 10, 255) if y > 2 else (20, 14, 13, 255)
            q.putpixel((x, y), c)
    return q


def HOLE(x, y):
    return True


hole = Image.new('RGBA', (16, 16), (14, 10, 10, 255))
wall = Image.new('RGBA', (16, 16), (14, 10, 10, 255))
for y in range(16):
    for x in range(16):
        c = (52, 36, 28) if y <= 3 else (24, 16, 14) if y <= 6 else (14, 10, 10)
        wall.putpixel((x, y), c + (255,))
OUT.alpha_composite(hole, (0, 88))
OUT.alpha_composite(wall, (16, 88))
OUT.alpha_composite(piece(lambda x, y: y <= 1), (32, 88))
OUT.alpha_composite(piece(lambda x, y: y >= 14), (48, 88))
OUT.alpha_composite(piece(lambda x, y: x <= 1), (64, 88))
OUT.alpha_composite(piece(lambda x, y: x >= 14), (80, 88))

OUT.save(sys.argv[2])
print('ok', OUT.size)
