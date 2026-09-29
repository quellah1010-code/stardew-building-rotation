"""Art-only comparison: closed side wall, equal visible height, free width.

The private original Barn reference is read only to compose the review board.
No runtime sprite, collision layout, or installable package is changed.
"""
import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--original-barn", type=Path, required=True)
parser.add_argument("--review", type=Path, required=True)
args = parser.parse_args()

front = Image.open(args.original_barn).convert("RGBA").crop((0, 0, 112, 112))
front_bounds = front.getchannel("A").getbbox()
front = front.crop(front_bounds)
source = Image.open(ROOT / "docs/art/runtime/barn-east-v2-source.png").convert("RGBA")
# Use the continuous source wall: no narrowed wall and no added edge-on jamb.
source = source.crop(source.getchannel("A").getbbox())
height = front.height
side = source.resize((round(source.width * height / source.height), height), Image.Resampling.NEAREST)
side.putalpha(side.getchannel("A").point(lambda a: 255 if a >= 192 else 0))
assert side.getchannel("A").getbbox()[1::2] == (0, height)
assert height == 107
asset = ROOT / "docs/art/runtime/barn-east-closed-wall-height-study.png"
side.save(asset, optimize=True)

board = Image.new("RGB", (1080, 736), "#e7e2d5")
draw = ImageDraw.Draw(board)
font_dir = Path("/usr/share/fonts/truetype/dejavu")
title_font = ImageFont.truetype(str(font_dir / "DejaVuSans.ttf"), 30)
label_font = ImageFont.truetype(str(font_dir / "DejaVuSans.ttf"), 22)
small_font = ImageFont.truetype(str(font_dir / "DejaVuSans.ttf"), 16)
ink, guide = "#382e26", "#58847d"
draw.text((56, 40), "BARN / height alignment", font=title_font, fill=ink)
draw.text((56, 86), "Closed side wall. Widths may differ.", font=label_font, fill=ink)
top, scale = 192, 4
bottom = top + height * scale
for x, im, label in ((88, front, "FRONT"), (652, side, "SIDE")):
    draw.text((x, 139), label, font=label_font, fill=ink)
    im = im.resize((im.width * scale, im.height * scale), Image.Resampling.NEAREST)
    board.paste(im, (x, top), im)
for y, label in ((top - 1, "TOP"), (bottom, "BASE")):
    draw.line((56, y, 1030, y), fill=guide, width=2)
    draw.text((56, y - 24), label, font=small_font, fill=guide)
draw.text((88, 648), f"{front.width} px wide", font=small_font, fill=ink)
draw.text((652, 648), f"{side.width} px wide", font=small_font, fill=ink)
draw.text((56, 697), f"Both silhouettes: {height} px high / shown at 4x. Art study, not an in-game screenshot.", font=small_font, fill=ink)
args.review.parent.mkdir(parents=True, exist_ok=True)
board.save(args.review, optimize=True)
print(f"Front bounds: {front_bounds}; front: {front.size}; side: {side.size}; guide span: {bottom-top}px")
print(asset)
print(args.review)
