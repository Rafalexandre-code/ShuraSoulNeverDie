"""Copies the art, sounds, music, fonts and UI pieces the browser client needs into
Assets/StreamingAssets/web (shipped as-is with the build) and writes the sfx manifest.

Usage: python -I Tools/web/sync_web_assets.py <project_dir>
Run again whenever art or sounds in Resources change.
"""
import json
import os
import shutil
import sys

project = sys.argv[1]
res = os.path.join(project, "Assets", "_Shura", "Resources")
web = os.path.join(project, "Assets", "StreamingAssets", "web")


def fresh(path):
    """Empties a folder but keeps Unity's .meta files (their GUIDs must not change)."""
    if not os.path.isdir(path):
        os.makedirs(path)
        return
    for root, dirs, files in os.walk(path, topdown=False):
        for f in files:
            if not f.endswith(".meta"):
                os.remove(os.path.join(root, f))


art = os.path.join(web, "art")
fresh(art)
for f in os.listdir(os.path.join(res, "Art", "NA")):
    if f.endswith(".png"):
        shutil.copy2(os.path.join(res, "Art", "NA", f), art)

sfx = os.path.join(web, "sfx")
fresh(sfx)
manifest = {}
sfx_root = os.path.join(res, "Audio", "Sfx")
for cat in sorted(os.listdir(sfx_root)):
    src = os.path.join(sfx_root, cat)
    if not os.path.isdir(src):
        continue
    files = sorted(f for f in os.listdir(src) if f.endswith(".ogg"))
    if not files:
        continue
    os.makedirs(os.path.join(sfx, cat), exist_ok=True)
    for f in files:
        shutil.copy2(os.path.join(src, f), os.path.join(sfx, cat, f))
    manifest[cat] = files
with open(os.path.join(sfx, "manifest.json"), "w", encoding="utf-8") as fh:
    json.dump(manifest, fh)

music = os.path.join(web, "music")
fresh(music)
playlists = {}
for mood in ("Calm", "Fight"):
    src = os.path.join(res, "Audio", "Music", mood)
    os.makedirs(os.path.join(music, mood.lower()), exist_ok=True)
    files = sorted(f for f in os.listdir(src) if f.endswith(".ogg"))
    for f in files:
        shutil.copy2(os.path.join(src, f), os.path.join(music, mood.lower(), f))
    playlists[mood.lower()] = files
with open(os.path.join(music, "playlists.json"), "w", encoding="utf-8") as fh:
    json.dump(playlists, fh)

fonts = os.path.join(web, "fonts")
fresh(fonts)
for f in os.listdir(os.path.join(res, "UI", "Fonts")):
    if f.endswith(".ttf"):
        shutil.copy2(os.path.join(res, "UI", "Fonts", f), fonts)

ui = os.path.join(web, "ui")
fresh(ui)
for f in ("paper_tile.png", "logo.png", "seal.png", "stroke_wide.png", "enso.png", "dab.png", "panel_paper.png", "backdrop.png", "petal.png"):
    shutil.copy2(os.path.join(res, "UI", f), ui)

total = sum(os.path.getsize(os.path.join(d, f)) for d, _, fs in os.walk(web) for f in fs)
print("web assets: %.1f MB, %d categorias de som" % (total / 1048576.0, len(manifest)))
