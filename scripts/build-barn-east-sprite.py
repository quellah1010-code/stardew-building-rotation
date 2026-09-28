"""Finish a complete generated side elevation; never repeat roof texture strips.

The generated RGBA source was alpha-thresholded (192), cropped, and uniformly
reduced to 112 pixels high (92 wide after rounding). Keep that roof intact.
Only fit the wall width to the real footprint and locate the edge-on jamb.
Private game/player references are optional and never needed for repo assets.
"""
from pathlib import Path
import argparse
import hashlib
import json
from collections import deque
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--original-barn", type=Path)
parser.add_argument("--player-screenshot", type=Path)
parser.add_argument("--private-review", type=Path)
args = parser.parse_args()
source = Image.open(ROOT / "docs/art/runtime/barn-east-v2-source.png").convert("RGBA")
assert source.size == (92, 112)
sprite = source.copy()
# Eave ends at y=70. Fit only the wall/posts/base from 78 to 64 pixels wide.
# The roof is untouched; this small wall adjustment is explicitly not uniform.
wall = source.crop((7, 71, 85, 112)).resize((64, 41), Image.Resampling.NEAREST)
sprite.paste((0, 0, 0, 0), (0, 71, 92, 112))
sprite.paste(wall, (14, 71))
draw = ImageDraw.Draw(sprite)
# World-local threshold (64,88); source ground origin (14,0) gives (78,88).
draw.rectangle((74, 69, 77, 87), fill="#291b13")
draw.line((74, 69, 74, 86), fill="#cbb49a")
draw.line((75, 69, 77, 69), fill="#92704b")
draw.line((75, 86, 77, 86), fill="#bd9e70")
draw.line((75, 87, 77, 87), fill="#4c3320")
draw.point((76, 79), fill="#c29348")
alpha = sprite.getchannel("A").point(lambda a: 255 if a >= 192 else 0)
sprite = sprite.convert("RGB").quantize(colors=64, dither=Image.Dither.NONE).convert("RGBA")
sprite.putalpha(alpha)
for y in range(sprite.height):
    for x in range(sprite.width):
        if alpha.getpixel((x, y)) == 0:
            sprite.putpixel((x, y), (0, 0, 0, 0))
assert set(sprite.getchannel("A").tobytes()) == {0, 255}
assert sprite.getchannel("A").crop((0, 111, 92, 112)).getbbox() == (14, 0, 78, 1)
assert sprite.getpixel((78, 88))[3] == 0
anchors = json.loads((ROOT / "docs/art/templates/rotation-template-anchors.json").read_text())
barn = next(b for b in anchors["buildings"] if b["id"] == "Barn")
east = next(o for o in barn["orientations"] if o["Facing"] == "East")
old_origin = east["GroundOrigin"]
old_threshold = east["Doors"]["human"]["Threshold"]
assert (old_threshold["X"] - old_origin["X"], old_threshold["Y"] - old_origin["Y"]) == (64, 88)
asset = ROOT / "src/BuildingRotation.Smapi/assets/barn-east-v2.png"
sprite.save(asset, optimize=True)

def placed(board, im, left, bottom, scale=4):
    large = im.resize((im.width * scale, im.height * scale), Image.Resampling.NEAREST)
    board.paste(large, (left, bottom - large.height), large)

old = Image.open(ROOT / "src/BuildingRotation.Smapi/assets/barn-east-v1.png").convert("RGBA")
review = Image.new("RGB", (880, 760), "#d7ddc5")
d = ImageDraw.Draw(review)
d.text((32, 20), "REJECTED v1: 64 x 160", fill="#4c3025")
d.text((430, 20), "REBUILT v2: 92 x 112 / same 4 x 7 ground", fill="#273b29")
placed(review, old, 52, 704)
placed(review, sprite, 440, 704)
for ox in (52, 440 + 14 * 4):
    d.rectangle((ox, 256, ox + 255, 703), outline="#378371", width=2)
    d.rectangle((ox + 256, 576, ox + 319, 639), outline="#009e95", width=2)
d.text((32, 732), "4x nearest-neighbor. Green: unchanged collision bounds. Cyan: actual outside approach tile.", fill="#273b29")
review_path = ROOT / "docs/art/runtime/barn-east-v2-comparison.png"
review.save(review_path, optimize=True)

if args.private_review:
    if not args.original_barn or not args.player_screenshot:
        parser.error("Private review requires both reference paths.")
    # A cropped player reference from the user's 3x screenshot, converted to 4x.
    player = Image.open(args.player_screenshot).convert("RGBA").crop((88, 132, 140, 228))
    mask = Image.new("L", player.size, 255)
    queue = deque([(x, y) for x in range(player.width) for y in (0, player.height-1)] +
                  [(x, y) for y in range(player.height) for x in (0, player.width-1)])
    seen = set()
    while queue:
        x, y = queue.popleft()
        if (x, y) in seen or not (0 <= x < player.width and 0 <= y < player.height):
            continue
        seen.add((x, y))
        r, g, b, _ = player.getpixel((x, y))
        if r > 140 and g > 75 and b < 120 and r > g:
            mask.putpixel((x, y), 0)
            queue.extend(((x-1,y),(x+1,y),(x,y-1),(x,y+1)))
    player.putalpha(mask)
    player = player.resize((round(player.width*4/3), round(player.height*4/3)), Image.Resampling.NEAREST)
    original = Image.open(args.original_barn).convert("RGBA").crop((0, 0, 112, 112))
    board = Image.new("RGB", (1536, 800), "#89996b")
    bd = ImageDraw.Draw(board)
    for x, label in [(32,"ORIGINAL BARN / reference"),(600,"REJECTED v1"),(1050,"REBUILT v2")]:
        bd.text((x,24),label,fill="#162519")
    placed(board, original, 32, 720)
    placed(board, old, 600, 720)
    placed(board, sprite, 1050, 720)
    for x in (502, 896, 1434):
        board.paste(player, (x, 720-player.height), player)
    bd.rectangle((1050+78*4, 720-112*4+80*4, 1050+94*4-1, 720-112*4+96*4-1), outline="#66f2db", width=2)
    bd.text((32,768),"Same source scale (4x). Offline composition, not an in-game screenshot. Input/collision/door coordinates unchanged.",fill="#162519")
    args.private_review.parent.mkdir(parents=True, exist_ok=True)
    board.save(args.private_review, optimize=True)

print(json.dumps({"asset":str(asset.relative_to(ROOT)), "size":sprite.size,
    "ground_origin":[14,0], "threshold":[78,88], "bytes":asset.stat().st_size,
    "sha256":hashlib.sha256(asset.read_bytes()).hexdigest(),
    "preview":str(review_path.relative_to(ROOT))}, indent=2))
