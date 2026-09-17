"""Build the east-only test sprite from the project's generated Barn v6 board.

Programmatic image processing was explicitly authorized by the user. The board
is a material reference, not a native sprite: shingle courses and wall sections
are reassembled at source scale, rather than stretching the whole elevation.
No extracted game PNG or rejected checkerboard image is required.
"""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
source = ROOT / "docs/art/turnarounds/barn-four-views-v6.png"
board = Image.open(source).convert("RGB")
assert hashlib.sha256(source.read_bytes()).hexdigest() == "96c643f5b0879f329543942ee7288a8d2c7ae8d2c8a2a392b64bd2c60fde390d"
anchors = json.loads((ROOT / "docs/art/templates/rotation-template-anchors.json").read_text())
barn = next(b for b in anchors["buildings"] if b["id"] == "Barn")
east = next(o for o in barn["orientations"] if o["Facing"] == "East")
assert (east["CanvasWidth"], east["CanvasHeight"]) == (64, 160)
assert east["GroundOrigin"] == {"X": 0, "Y": 48}
assert east["Doors"]["human"]["Threshold"] == {"X": 64, "Y": 136}
approach = east["Doors"]["human"]["Approach"]
threshold = east["Doors"]["human"]["Threshold"]

# The LEFT board panel is the building facing East in game coordinates.
# Reduce each material at the same approximately 0.24 scale; extend courses,
# not individual pixels, to span the rotated 4 x 7 tile ground plane.
roof = board.crop((275, 576, 543, 810)).resize((64, 56), Image.Resampling.NEAREST)
wall = board.crop((286, 810, 532, 968)).resize((58, 38), Image.Resampling.NEAREST)
sprite = Image.new("RGBA", (64, 160))
sprite.paste(roof.crop((0, 0, 64, 6)), (0, 0))
y = 6
for top, bottom, count in [(6, 12, 3), (12, 16, 2), (16, 21, 2),
                            (21, 29, 2), (29, 37, 2), (37, 46, 2), (46, 55, 2)]:
    for _ in range(count):
        sprite.paste(roof.crop((0, top, 64, bottom)), (0, y))
        y += bottom - top
assert y == 110  # Two-pixel eave below the last complete shingle course.
sprite.paste(wall.crop((0, 0, 58, 18)), (3, 112))
sprite.paste(wall.crop((0, 12, 58, 22)), (3, 130))
sprite.paste(wall.crop((0, 18, 58, 38)), (3, 140))

# Deliberate pixel silhouette removes the pale review-board edge completely.
d = ImageDraw.Draw(sprite)
outline, shade, trim, gold = "#28170e", "#55321b", "#cab38a", "#ae7739"
d.line((0, 2, 0, 111), fill=outline)
d.line((63, 2, 63, 111), fill=outline)
d.line((1, 0, 62, 0), fill=outline)
d.rectangle((0, 110, 63, 111), fill=outline)
for x in (0, 63):
    for cy in (0, 1):
        sprite.putpixel((x, cy), (0, 0, 0, 0))
d.line((3, 112, 3, 158), fill=outline)
d.line((60, 112, 60, 158), fill=outline)
d.line((3, 159, 60, 159), fill=outline)

# Thin edge-on entrance on the east edge; its sill ends at the exact ground
# threshold (64, 136). This is a visual trial, not a change to game door data.
d.rectangle((60, 112, 63, 135), fill=outline)
d.line((60, 112, 60, 133), fill=trim)
d.line((63, 113, 63, 133), fill=shade)
d.line((61, 112, 63, 112), fill=gold)
d.point((62, 124), fill=gold)
d.line((61, 134, 63, 134), fill=trim)
d.line((61, 135, 63, 135), fill=shade)

# A compact palette and strictly binary alpha keep clean source-scale pixels.
alpha = sprite.getchannel("A")
sprite = sprite.convert("RGB").quantize(colors=48, dither=Image.Dither.NONE).convert("RGBA")
sprite.putalpha(alpha)
for cy in range(160):
    for cx in range(64):
        if alpha.getpixel((cx, cy)) == 0:
            sprite.putpixel((cx, cy), (0, 0, 0, 0))
assert set(sprite.getchannel("A").tobytes()) == {0, 255}
assert sprite.getpixel((63, 135))[3] == 255
assert sprite.getpixel((63, 136))[3] == 0

asset = ROOT / "src/BuildingRotation.Smapi/assets/barn-east-v1.png"
asset.parent.mkdir(parents=True, exist_ok=True)
sprite.save(asset, optimize=True)

# Four-times preview: real alpha composited on two solid backgrounds; guide
# is generated from the same source-coordinate contract as the game renderer.
preview = Image.new("RGB", (768, 760), "#f2eddf")
pd = ImageDraw.Draw(preview)
pd.text((24, 16), "BARN EAST - source 64 x 160 / displayed 4x", fill="#30231c")
scaled = sprite.resize((256, 640), Image.Resampling.NEAREST)
for ox, background in [(64, "#778a51"), (448, "#252d35")]:
    pd.rectangle((ox - 16, 54, ox + 272, 724), fill=background)
    preview.paste(scaled, (ox, 70), scaled)
    pd.rectangle((ox + approach["X"] * 4, 70 + approach["Y"] * 4,
                  ox + approach["Right"] * 4 - 1, 70 + approach["Bottom"] * 4 - 1), outline="#69e7d3", width=2)
    pd.line((ox + threshold["X"] * 4 - 8, 70 + threshold["Y"] * 4,
             ox + threshold["X"] * 4 + 12, 70 + threshold["Y"] * 4), fill="#69e7d3", width=2)
pd.text((24, 740), "Mint outline: real outside approach tile. Door sill: (64, 136). Art trial, not final.", fill="#30231c")
review = ROOT / "docs/art/runtime/barn-east-v1-preview.png"
review.parent.mkdir(parents=True, exist_ok=True)
preview.save(review, optimize=True)
print(json.dumps({"asset":str(asset.relative_to(ROOT)),"size":sprite.size,"mode":sprite.mode,
                  "bytes":asset.stat().st_size,"sha256":hashlib.sha256(asset.read_bytes()).hexdigest(),
                  "preview":str(review.relative_to(ROOT))}, indent=2))
