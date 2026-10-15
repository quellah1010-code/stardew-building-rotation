"""Split project-owned North art and make its far-side entrance sprite.

No game textures or user screenshots are read. The unmodified North PNG is
reconstructed exactly by the three disjoint layers. Run from any directory.
"""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src/BuildingRotation.Smapi/assets"
ART = ROOT / "docs/art/runtime"
source = Image.open(ASSETS / "barn-north-white-trim.png").convert("RGBA")
assert source.size == (113, 107)

# Trace the eave of the existing North sprite; the annex lid ends at row 72.
# Roof gets 22% opacity, upper wall 38%, final six base rows stay opaque.
eave = [(0,72),(17,72),(18,76),(24,69),(32,62),(42,58),(50,56),
        (77,56),(85,58),(97,65),(106,75),(112,78)]
layers = {name: Image.new("RGBA", source.size) for name in ("roof", "wall", "base")}
for x in range(source.width):
    left, right = next((a,b) for a,b in zip(eave,eave[1:]) if a[0] <= x <= b[0])
    boundary = round(left[1] + (right[1]-left[1]) * (x-left[0]) / (right[0]-left[0]))
    for y in range(source.height):
        name = "base" if y >= 101 else "roof" if y < boundary else "wall"
        layers[name].putpixel((x,y), source.getpixel((x,y)))
rebuilt = Image.new("RGBA", source.size)
for name, im in layers.items():
    im.save(ASSETS / f"barn-north-{name}.png", optimize=True)
    rebuilt.alpha_composite(im)
assert all(a == b if b[3] else a[3] == 0 for a,b in zip(rebuilt.get_flattened_data(),source.get_flattened_data())), "Layers changed visible pixels"

# A solid timber leaf belongs to a real far wall. Unlike v1's empty frame,
# guide planks cannot show through it like ladder rungs. Pixel edits use only
# the project's chosen wall texture and palette (no game assets).
portal = Image.new("RGBA", (16,28)); d = ImageDraw.Draw(portal)
dark=(57,36,27,255); wood=(111,64,35,255); light=(159,100,49,255)
white=(232,221,193,255); shade=(166,149,125,255)
d.rectangle((1,3,14,27), fill=dark)
d.rectangle((4,6,11,25), fill=wood)
for x in (4,7,10):
    d.line((x,7,x,24), fill=light)
    if x > 4: d.line((x-1,7,x-1,24), fill=dark)
d.rectangle((2,4,3,25), fill=white); d.line((3,5,3,25), fill=shade)
d.rectangle((12,4,13,25), fill=white); d.line((13,5,13,25), fill=shade)
d.rectangle((2,3,13,5), fill=white)
d.rectangle((10,16,11,18),fill=dark);d.point((10,16),fill=(223,178,76,255))
d.rectangle((0,26,15,27), fill=dark); d.line((1,26,14,26), fill=(195,151,93,255))
portal.save(ASSETS / "barn-north-entrance.png", optimize=True)

# The door now has a continuous red wall around it. Its sill and this wall's
# base share the first solid row; the open rear row stays outside this wall.
far_wall = Image.new("RGBA", (112,28)); f = ImageDraw.Draw(far_wall)
for y in range(4,26):
    for x in range(112):
        far_wall.putpixel((x,y),source.getpixel((30+x%70,78+(y-4)%22)))
f.rectangle((0,2,111,3),fill=white)
f.line((0,4,111,4),fill=shade)
f.rectangle((0,25,111,27),fill=dark)
f.line((0,25,111,25),fill=(147,95,58,255))
# Reserve the exact human-door span for the separate leaf and frame.
f.rectangle((80,2,95,27),fill=(0,0,0,0))
far_wall.save(ASSETS / "barn-north-far-wall.png", optimize=True)

facts = {"source_sha256":hashlib.sha256((ASSETS/'barn-north-white-trim.png').read_bytes()).hexdigest(),
         "layers_reconstruct_visible_pixels_exactly":True,"eave_vertices":eave,"base_first_row":101,
         "roof_opacity":.22,"wall_opacity":.38,"base_opacity":1,
         "entrance_size":[16,28],"entrance_threshold_anchor":[8,26],
         "human_door_north_local_tile":[5,1],"approach_local_tile":[5,0],
         "reserved_size":[7,4],"collision_rows":["OOOOOOO","XXXXXXX","XXXXXXX","XXXXXXX"],
         "ground_origin_in_north_sprite":[.5,43],"entrance_sill_in_north_sprite":[88.5,59],"far_wall_size":[112,28]}
(ART / "north-reveal-layers.json").write_text(json.dumps(facts,indent=2)+"\n")
print("North layers reconstruct source exactly; far-door sill anchors at (88.5,59); rear row is passable.")

# Offline compositing QA only: plain ground and a generic actor marker, not a
# game screenshot. Native-size placement is the same as the runtime renderer.
def faded(im, opacity):
    im = im.copy()
    im.putalpha(im.getchannel("A").point(lambda a: round(a * opacity)))
    return im

font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 18)
small = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 14)
board = Image.new("RGB", (1452, 540), "#eae5d9"); text = ImageDraw.Draw(board)
text.text((24,14), "NORTH REVEAL / offline layer check (not a game screenshot)", fill="#382e27", font=font)
text.text((24,42), "Rear row is walkable. Door and red wall share the solid boundary; foundation stays opaque.", fill="#52463b", font=small)
for index, (label, amount) in enumerate((("Far / normal",0),("Transition / 50%",.5),("Near / revealed",1))):
    scene = Image.new("RGBA", (152,140), "#a49464")
    ground = ImageDraw.Draw(scene)
    for y in range(0,140,16):
        for x in range(0,152,16):
            if (x//16+y//16)%2: ground.rectangle((x,y,x+15,y+15),fill="#a99a6c")
    left,top=20,17; gx,gy=left+.5,top+59; sill_x=gx+88
    if amount:
        cues=Image.new("RGBA",scene.size); c=ImageDraw.Draw(cues); ink=(109,78,48,round(204*amount))
        c.line((gx,gy,gx+80,gy),fill=ink);c.line((gx+96,gy,gx+112,gy),fill=ink)
        c.line((gx,gy,gx,gy+48,gx+112,gy+48,gx+112,gy),fill=ink)
        for y in (gy-12,gy-8,gy-4):c.rectangle((sill_x-6,y,sill_x+5,y+1),fill=(160,119,66,round(255*amount)))
        scene.alpha_composite(cues)
        scene.alpha_composite(faded(far_wall,amount),(round(gx),round(gy-26)))
        scene.alpha_composite(faded(portal,amount),(round(sill_x-8),round(gy-26)))
    # Marker stands beside the threshold so both the marker and door can be judged.
    actor=Image.new("RGBA",(8,24)); d=ImageDraw.Draw(actor)
    d.rectangle((2,0,5,5),fill="#dfb48a");d.rectangle((1,6,6,16),fill="#426fa9")
    d.rectangle((1,17,3,23),fill="#303f67");d.rectangle((5,17,6,23),fill="#303f67")
    scene.alpha_composite(actor,(round(sill_x-23),round(gy-8-24)))
    if amount:
        for name, opacity in (("roof",1-.78*amount),("wall",1-.62*amount),("base",1)):
            scene.alpha_composite(faded(layers[name],opacity),(left,top))
    else:scene.alpha_composite(source,(left,top))
    x=24+index*476
    text.text((x,80),label,fill="#382e27",font=font)
    board.paste(scene.convert("RGB").resize((456,420),Image.Resampling.NEAREST),(x,108))
board.save(ART / "barn-north-reveal-runtime-v2.png",optimize=True)
