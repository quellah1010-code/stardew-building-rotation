"""Refine generated materials while retaining the accepted side silhouette.

Input is the complete generated sprite, alpha-cropped and reduced to 88x107.
The original game image is optional, used only in a private comparison board.
No game assets are written into the repository and no runtime asset is changed.
"""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "docs/art/runtime"
parser = argparse.ArgumentParser()
parser.add_argument("--original-barn", type=Path)
parser.add_argument("--private-review", type=Path)
args = parser.parse_args()
source = Image.open(ART / "barn-east-material-v3-source.png").convert("RGBA")
previous = Image.open(ART / "barn-east-closed-wall-height-study.png").convert("RGBA")
assert source.size == previous.size == (88, 107)
mask = previous.getchannel("A")
aligned = Image.new("RGBA", source.size)

# Generated eave was 3 px too high. Fit complete roof/eave/wall regions to the
# approved divisions. Map each scanline to its approved extent; do not tile rows.
for y0, y1, s0, s1 in [(0, 64, 0, 61), (64, 66, 61, 63), (66, 107, 63, 107)]:
    region = source.crop((0, s0, 88, s1)).resize((88, y1-y0), Image.Resampling.NEAREST)
    for dy in range(y1-y0):
        row = region.crop((0, dy, 88, dy+1))
        bounds = row.getchannel("A").getbbox()
        target = mask.crop((0, y0+dy, 88, y0+dy+1)).getbbox()
        if bounds and target:
            strip = row.crop(bounds).resize((target[2]-target[0], 1), Image.Resampling.NEAREST)
            aligned.paste(strip, (target[0], y0+dy))
aligned.putalpha(mask)

def colors(hexes):
    return [tuple(bytes.fromhex(h)) for h in hexes]

# Four browns and a separate silhouette outline match the reference's restrained
# roof palette. Interior seams do not get the nearly black silhouette color.
roof = colors(["52352a", "704b2d", "9b6f41", "c28d54"])
wall = colors(["3e0316", "4e0f00", "651914", "671902", "8e2100", "ab320c", "bf320f", "ea4f12"])
trim = colors(["3e0316", "651914", "996b68", "b68b87", "d9b3af", "efd6d0"])

def closest(rgb, palette):
    return min(palette, key=lambda c: sum((c[i]-rgb[i])**2 for i in range(3)))

sprite = aligned.copy()
for y in range(107):
    for x in range(88):
        r, g, b, a = aligned.getpixel((x, y))
        if not a:
            sprite.putpixel((x, y), (0, 0, 0, 0))
            continue
        if y < 64:
            color = closest((r, g, b), roof)
            neighbors = [(x-1, y), (x+1, y), (x, y-1), (x, y+1)]
            if any(not (0 <= nx < 88 and 0 <= ny < 107) or mask.getpixel((nx, ny)) == 0 for nx, ny in neighbors):
                color = (40, 11, 9)
        elif y >= 103 and r-g < 70 and g > b*1.15:
            color = closest((r, g, b), roof)
        elif r-g < 65 and g > 65:
            color = closest((r, g, b), trim)
        else:
            color = closest((r, g, b), wall)
        sprite.putpixel((x, y), (*color, 255))

# Remove isolated one/two-pixel roof speckles, keeping large material clusters
# and the silhouette. Use the unmodified snapshot to avoid cascading edits.
snapshot, seen = sprite.copy(), set()
for y in range(1, 63):
    for x in range(1, 87):
        if (x, y) in seen:
            continue
        color = snapshot.getpixel((x, y))
        if not color[3] or color[:3] == (40, 11, 9):
            continue
        queue, component, adjacent = [(x, y)], [], []
        while queue:
            px, py = queue.pop()
            if (px, py) in seen:
                continue
            seen.add((px, py))
            component.append((px, py))
            for nx, ny in [(px-1, py), (px+1, py), (px, py-1), (px, py+1)]:
                if not (0 < nx < 87 and 0 < ny < 63):
                    continue
                c = snapshot.getpixel((nx, ny))
                if c == color:
                    if (nx, ny) not in seen:
                        queue.append((nx, ny))
                elif c[3] and c[:3] != (40, 11, 9):
                    adjacent.append(c)
        if len(component) < 3 and adjacent:
            replacement = Counter(adjacent).most_common(1)[0][0]
            for pixel in component:
                sprite.putpixel(pixel, replacement)

assert sprite.getchannel("A").tobytes() == mask.tobytes()
assert sprite.getchannel("A").getbbox() == (0, 0, 88, 107)
assert set(mask.tobytes()) == {0, 255}
asset = ART / "barn-east-material-v3.png"
sprite.save(asset, optimize=True)

font_dir = Path("/usr/share/fonts/truetype/dejavu")
title_font = ImageFont.truetype(str(font_dir / "DejaVuSans.ttf"), 28)
label_font = ImageFont.truetype(str(font_dir / "DejaVuSans.ttf"), 21)
small_font = ImageFont.truetype(str(font_dir / "DejaVuSans.ttf"), 16)

def comparison(path, items, width):
    board = Image.new("RGB", (width, 720), "#e7e2d5")
    draw = ImageDraw.Draw(board)
    draw.text((40, 35), "BARN / material revision", font=title_font, fill="#382e26")
    top, bottom, scale = 172, 600, 4
    for x, im, label in items:
        assert im.height == 107
        draw.text((x, 112), label, font=label_font, fill="#382e26")
        enlarged = im.resize((im.width*scale, im.height*scale), Image.Resampling.NEAREST)
        board.paste(enlarged, (x, top), enlarged)
    for y in (top-1, bottom):
        draw.line((40, y, width-40, y), fill="#58847d", width=2)
    draw.text((40, 640), "107 px high / 4x view", font=small_font, fill="#382e26")
    draw.text((40, 675), "Same side silhouette, closed wall and eave position. Offline art review.", font=small_font, fill="#382e26")
    path.parent.mkdir(parents=True, exist_ok=True)
    board.save(path, optimize=True)

comparison(ART / "barn-east-material-v3-comparison.png", [(48, previous, "BEFORE"), (488, sprite, "AFTER")], 896)
if args.private_review:
    if not args.original_barn:
        parser.error("--private-review requires --original-barn")
    front = Image.open(args.original_barn).convert("RGBA").crop((0, 0, 112, 112))
    front = front.crop(front.getchannel("A").getbbox())
    comparison(args.private_review, [(56, front, "ORIGINAL FRONT"), (592, previous, "PREVIOUS SIDE"), (1072, sprite, "REVISED SIDE")], 1488)

print(json.dumps({"asset": str(asset.relative_to(ROOT)), "size": sprite.size,
    "alpha_matches_accepted_silhouette": True,
    "opaque_colors": len(set(sprite.convert("RGB").get_flattened_data()))-1,
    "sha256": hashlib.sha256(asset.read_bytes()).hexdigest()}, indent=2))
