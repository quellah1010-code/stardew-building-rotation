"""Compose the selected Barn side and two new elevation studies.

Art only: no runtime assets or private game textures are read or written.
The front is a reference extracted from our existing generated v6 board.
"""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "docs/art/runtime"
TURN = ROOT / "docs/art/turnarounds"
HEIGHT = 107


def rgba(path):
    return Image.open(path).convert("RGBA")


def crisp_alpha(im):
    im = im.copy()
    im.putalpha(im.getchannel("A").point(lambda a: 255 if a >= 192 else 0))
    return im


east_path = ART / "barn-east-closed-wall-height-study.png"
east_hash_before = hashlib.sha256(east_path.read_bytes()).hexdigest()
east = rgba(east_path)
assert east.size == (88, HEIGHT)

# Preserve the chosen roof, eave, wall and visible white posts exactly.
# Only overlay the generated near-side annex on the lower part of this side.
# Its X-strapped lid is compressed as one plane; timber rows are never repeated.
west_source = rgba(ART / "barn-west-white-trim-v1-source.png")
assert west_source.size == (88, 112)
west = east.copy()
lid = west_source.crop((10, 79, 79, 92)).resize((74, 7), Image.Resampling.NEAREST)
face = west_source.crop((10, 92, 79, 112)).resize((74, 21), Image.Resampling.NEAREST)
west.alpha_composite(crisp_alpha(lid), (7, 79))
west.alpha_composite(crisp_alpha(face), (7, 86))
west = crisp_alpha(west)
west.save(ART / "barn-west-white-trim-v1.png", optimize=True)

north = crisp_alpha(rgba(ART / "barn-north-white-trim-v1-source.png"))
assert north.size == (113, HEIGHT)
north.save(ART / "barn-north-white-trim-v1.png", optimize=True)

# Public front reference is generated v6 artwork, NOT the original game PNG.
front = rgba(TURN / "barn-four-views-v6.png").crop((190, 52, 630, 470))
front.putalpha(Image.eval(front.convert("RGB").getchannel("G"), lambda g: 255 if g < 185 else 0))
front = front.crop(front.getchannel("A").getbbox())
front = front.resize((round(front.width * HEIGHT / front.height), HEIGHT), Image.Resampling.NEAREST)

entries = [(front, "FRONT / SOUTH", "v6 structure reference"),
           (east, "LEFT / EAST", "Selected side - unchanged"),
           (west, "RIGHT / WEST", "New near-side timber annex"),
           (north, "BACK / NORTH", "New enclosed rear wall")]
scale, margin, gap = 4, 48, 56
width = margin * 2 + gap * 3 + sum(im.width * scale for im, _, _ in entries)
board = Image.new("RGB", (width, 720), "#e9e4da")
draw = ImageDraw.Draw(board)
font_path = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
title = ImageFont.truetype(font_path, 30)
label = ImageFont.truetype(font_path, 23)
small = ImageFont.truetype(font_path, 17)
ink, accent = "#382e26", "#4c8078"
draw.text((margin, 30), "BARN / bright white trim", font=title, fill=ink)
draw.text((margin, 79), "Same visible height. Closed side walls. Existing front shown for comparison.", font=label, fill=ink)
top, base, x = 192, 192 + HEIGHT * scale, margin
for im, heading, note in entries:
    draw.text((x, 133), heading, font=label, fill=ink)
    draw.text((x, 163), note, font=small, fill=ink)
    large = im.resize((im.width * scale, im.height * scale), Image.Resampling.NEAREST)
    board.paste(large, (x, top), large)
    draw.text((x, 641), f"{im.width} x {im.height} px", font=small, fill=ink)
    x += im.width * scale + gap
for y in (top - 2, base + 1):
    draw.line((margin - 12, y, width - margin + 12, y), fill=accent, width=2)
draw.text((margin, 689), "Art review v1 / 4x pixels / runtime unchanged / no Reveal layer yet", font=small, fill=ink)
board_path = TURN / "barn-white-trim-four-views-v1.png"
board.save(board_path, optimize=True)

assert east_hash_before == hashlib.sha256(east_path.read_bytes()).hexdigest()
assert west.crop((0, 0, 88, 79)).tobytes() == east.crop((0, 0, 88, 79)).tobytes()
for im, _, _ in entries:
    assert im.getchannel("A").getbbox()[1::2] == (0, HEIGHT)
for im in (west, north):
    assert set(im.getchannel("A").tobytes()) == {0, 255}
report = {
    "date": "2026-10-12", "status": "art studies for review; not runtime sprites",
    "selected_east_sha256": east_hash_before,
    "selected_east_unchanged": True,
    "west_upper_79_rows_match_selected_east": True,
    "visible_height_all_views": HEIGHT,
    "dimensions": {heading: list(im.size) for im, heading, _ in entries},
    "front_source": "existing generated barn-four-views-v6.png; not original game PNG",
    "new_sources": {
        "west": {"generation_crop": [33, 12, 1105, 1373], "normalized_size": [88, 112]},
        "north": {"generation_crop": [24, 22, 1253, 1190], "normalized_size": [113, 107]}
    },
    "processing": "Nearest-neighbor uniform source reduction; alpha threshold 192. West annex lid compressed separately; roof/eave retained verbatim from selected East. No palette quantization.",
    "not_validated": ["user approval of new sides", "runtime anchors", "character occlusion", "door visual cues", "Reveal"]
}
(ART / "inspection-barn-white-trim-v1.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(report, indent=2))
print(board_path)
