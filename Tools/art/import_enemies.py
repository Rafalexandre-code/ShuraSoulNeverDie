"""Copies the bestiary art from the Ninja Adventure pack (CC0) into Resources/Art/NA.

Usage: python -I Tools/art/import_enemies.py "<pack dir>" <project dir>

Characters come with Walk/Idle/Attack/Dead sheets. Monsters only have one 4x4 sheet
(columns = down, up, left, right; rows = frames), so their idle, strike and fallen
frames are cut or made from it: idle = first row, strike = second row, fallen = the
front frame squashed flat and darkened. Effects (explosion, magic circle, smoke...)
are copied as horizontal strips.
"""
import os
import shutil
import sys

from PIL import Image, ImageEnhance

pack, project = sys.argv[1], sys.argv[2]
out = os.path.join(project, "Assets", "_Shura", "Resources", "Art", "NA")
actor = os.path.join(pack, "Actor")

CHARACTERS = ["NinjaMasked", "NinjaBomb", "Tengu", "SorcererBlack", "Shaman", "Caveman",
              "NinjaThunder", "Vampire", "SkeletonDemon", "MonkeyBoxerRed"]
# name in the game -> sheet inside Actor/Monster
MONSTERS = {
    "LanternRed": "LanternRed/SpriteSheet.png",
    "Spirit": "Spirit/SpriteSheet.png",
    "KappaGreen": "KappaGreen/SpriteSheet.png",
    "Cyclope": "Cyclope/SpriteSheet.png",
    "Slime": "Slime/Slime.png",
    "Eye": "Eye/Eye.png",
    "Flam": "Flam/SpriteSheet.png",
}
FX = {
    "fx_explosion.png": "FX/Elemental/Explosion/SpriteSheet.png",
    "fx_circle.png": "FX/Magic/Circle/SpriteSheetOrange.png",
    "fx_aura.png": "FX/Magic/Aura/SpriteSheet.png",
    "fx_smoke.png": "FX/Smoke/Smoke/SpriteSheet.png",
    "fx_thunder.png": "FX/Elemental/Thunder/SpriteSheet.png",
    "fx_shuriken.png": "FX/Projectile/Shuriken.png",
    "fx_kunai.png": "FX/Projectile/Kunai.png",
    "fx_fireball.png": "FX/Projectile/Fireball.png",
    "item_bomb.png": "Items/Projectile/Bomb.png",
    "item_scroll_fire.png": "Items/Scroll/ScrollFire.png",
}

os.makedirs(out, exist_ok=True)
count = 0

for name in CHARACTERS:
    src = os.path.join(actor, "Character", name, "SeparateAnim")
    for part in ("Walk", "Idle", "Attack", "Dead"):
        dst = os.path.join(out, "%s_%s.png" % (name, part.lower()))
        path = os.path.join(src, part + ".png")
        if os.path.exists(path):
            shutil.copy2(path, dst)
        elif part == "Idle":
            # A few characters ship without an idle sheet: the first walk frame stands in.
            Image.open(os.path.join(src, "Walk.png")).convert("RGBA").crop((0, 0, 64, 16)).save(dst)
        else:
            raise SystemExit("missing %s for %s" % (part, name))
        count += 1


def fallen(sheet):
    """The front-facing frame lying on the floor: squashed to a third of its height, darker."""
    cell = sheet.crop((0, 0, 16, 16))
    box = cell.getbbox() or (0, 0, 16, 16)
    body = cell.crop(box)
    flat = body.resize((min(16, body.width + 2), max(3, body.height // 3)), Image.NEAREST)
    flat = ImageEnhance.Brightness(flat).enhance(0.6)
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    img.paste(flat, ((16 - flat.width) // 2, 16 - flat.height - (16 - box[3])), flat)
    return img


for name, rel in MONSTERS.items():
    sheet = Image.open(os.path.join(actor, "Monster", rel)).convert("RGBA")
    if sheet.size != (64, 64):
        raise SystemExit("unexpected sheet size for %s: %s" % (name, sheet.size))
    sheet.save(os.path.join(out, name + "_walk.png"))
    sheet.crop((0, 0, 64, 16)).save(os.path.join(out, name + "_idle.png"))
    sheet.crop((0, 16, 64, 32)).save(os.path.join(out, name + "_attack.png"))
    fallen(sheet).save(os.path.join(out, name + "_dead.png"))
    count += 4

for dst, rel in FX.items():
    shutil.copy2(os.path.join(pack, rel), os.path.join(out, dst))
    count += 1

print("%d arquivos em %s" % (count, out))
