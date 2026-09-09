#!/usr/bin/env python3
"""Draw source-estimated building volumes in a fixed oblique camera.

This is a geometry reference for art direction, not a game renderer, collision
model, exact source-pixel reconstruction, or a replacement for image generation.
Only Python's standard library is required. Original game images are not read.
"""

from dataclasses import dataclass, field
from pathlib import Path
from html import escape
import argparse


@dataclass
class Face:
    points: list
    color: str
    normal: tuple
    overlays: list = field(default_factory=list)


def quad(points, color, normal):
    return Face(points, color, normal)


def box(faces, x0, y0, z0, x1, y1, z1, color):
    faces.extend([
        quad([(x0,y1,z0),(x1,y1,z0),(x1,y1,z1),(x0,y1,z1)],color,(0,1,0)),
        quad([(x1,y0,z0),(x0,y0,z0),(x0,y0,z1),(x1,y0,z1)],color,(0,-1,0)),
        quad([(x0,y0,z0),(x0,y1,z0),(x0,y1,z1),(x0,y0,z1)],color,(-1,0,0)),
        quad([(x1,y1,z0),(x1,y0,z0),(x1,y0,z1),(x1,y1,z1)],color,(1,0,0)),
        quad([(x0,y0,z1),(x0,y1,z1),(x1,y1,z1),(x1,y0,z1)],color,(0,0,1)),
    ])


def doorway(face, x0, x1, y, height, color):
    face.overlays.append(([(x0,y,0),(x1,y,0),(x1,y,height),(x0,y,height)],color))


def gabled(kind, width, depth, profile, wall, roof):
    main_width = profile[-1][0]
    front = quad([(0,depth,0),(main_width,depth,0)] +
                 [(x,depth,z) for x,z in reversed(profile)],wall,(0,1,0))
    back = quad([(main_width,0,0),(0,0,0)] +
                [(x,0,z) for x,z in profile],wall,(0,-1,0))
    faces = [front,back,
        quad([(0,0,0),(0,depth,0),(0,depth,profile[0][1]),(0,0,profile[0][1])],wall,(-1,0,0)),
        quad([(main_width,depth,0),(main_width,0,0),(main_width,0,profile[-1][1]),(main_width,depth,profile[-1][1])],wall,(1,0,0))]
    for (x0,z0),(x1,z1) in zip(profile,profile[1:]):
        faces.append(quad([(x0,-.10,z0),(x0,depth+.10,z0),(x1,depth+.10,z1),(x1,-.10,z1)],roof,(-(z1-z0)/(x1-x0),0,1)))
    if kind == 'Barn':
        doorway(front,1,2,depth,1.3,'#94602f')
        doorway(front,3,5,depth,1.65,'#342821')
        # Estimated attached timber structure occupies the original right strip.
        box(faces,main_width,0,0,width,depth,1.05,'#997146')
        faces.append(quad([(main_width,0,1.65),(main_width,depth,1.65),(width,depth,1.05),(width,0,1.05)],'#aa8255',(0.75,0,1)))
    else:
        doorway(front,3,4,depth,1.65,'#923728')
        front.overlays.append(([(3,depth,2.15),(4,depth,2.15),(4,depth,2.65),(3,depth,2.65)],'#644528'))
    return width,depth,faces


def stable():
    width,depth = 4,2
    faces=[]
    height=lambda x: 3.0+x*.25
    faces.append(quad([(0,0,0),(4,0,0),(4,0,height(4)-.2),(0,0,height(0)-.2)],'#9c7142',(0,-1,0)))
    # Inside of the same back wall is visible through the open front.
    faces.append(quad([(0,.02,0),(4,.02,0),(4,.02,height(4)-.2),(0,.02,height(0)-.2)],'#7f552f',(0,1,0)))
    for x in [0,3.8]:
        for y in [0,1.8]:
            box(faces,x,y,0,x+.2,y+.2,height(x)-.2,'#b68547')
    for x in [0,3.9]:
        box(faces,x,0,.35,x+.1,2,1.25,'#95683e')
    faces.append(quad([(-.1,-.1,height(-.1)),(-.1,2.1,height(-.1)),(4.1,2.1,height(4.1)),(4.1,-.1,height(4.1))],'#bb5036',(-.25,0,1)))
    faces.append(quad([(-.1,2.1,height(-.1)-.2),(4.1,2.1,height(4.1)-.2),(4.1,2.1,height(4.1)),(-.1,2.1,height(-.1))],'#9c3c2c',(0,1,0)))
    faces.append(quad([(4.1,-.1,height(4.1)-.2),(-.1,-.1,height(-.1)-.2),(-.1,-.1,height(-.1)),(4.1,-.1,height(4.1))],'#9c3c2c',(0,-1,0)))
    return width,depth,faces


def turn(point, width, depth, facing):
    x,y,z=point
    return {'S':(x,y,z),'W':(depth-y,x,z),'E':(y,width-x,z),'N':(width-x,depth-y,z)}[facing]


def normal_turn(normal, facing):
    x,y,z=normal
    return {'S':(x,y,z),'W':(-y,x,z),'E':(y,-x,z),'N':(-x,-y,z)}[facing]


def polygon(points, color, project, opacity=1):
    coords=' '.join(f'{a:.2f},{b:.2f}' for a,b in map(project,points))
    return f'<polygon points="{coords}" fill="{color}" stroke="#4d4035" stroke-width="1.7" stroke-linejoin="round" opacity="{opacity}"/>'


def draw(name, model):
    width,depth,faces=model
    rows=['<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="1120" viewBox="0 0 1440 1120">',
          '<rect width="1440" height="1120" fill="#f2f0e9"/>',
          f'<text x="36" y="43" font-family="sans-serif" font-size="25" fill="#302d27">{escape(name)} / fixed-camera structure reference</text>']
    labels=[('S','FRONT / entrance faces DOWN'),('W','LEFT / entrance faces LEFT'),('E','RIGHT / entrance faces RIGHT'),('N','BACK / entrance faces UP')]
    scale=38
    for index,(facing,label) in enumerate(labels):
        left=(index%2)*720
        top=72+(index//2)*500
        fw,fd=(width,depth) if facing in 'SN' else (depth,width)
        def project(p):
            x,y,z=p
            return (left+360+(x-fw/2)*scale,top+440+(y-z-fd)*scale)
        rows.append(f'<text x="{left+40}" y="{top+32}" font-family="sans-serif" font-size="19" fill="#464139">{label}</text>')
        ground=[(0,0,0),(fw,0,0),(fw,fd,0),(0,fd,0)]
        rows.append(polygon(ground,'#dfd9ca',project))
        visible=[]
        for face in faces:
            normal=normal_turn(face.normal,facing)
            if normal[1]+normal[2]<=.00001:
                continue
            vertices=[turn(p,width,depth,facing) for p in face.points]
            distance=sum(p[1]+p[2] for p in vertices)/len(vertices)
            visible.append((distance,face,vertices))
        for _,face,vertices in sorted(visible,key=lambda entry:entry[0]):
            rows.append(polygon(vertices,face.color,project))
            for pts,color in face.overlays:
                rows.append(polygon([turn(p,width,depth,facing) for p in pts],color,project))
        rows.append(f'<text x="{left+40}" y="{top+480}" font-family="sans-serif" font-size="15" fill="#6b6459">Ground {fw} x {fd}; same scale; opaque structure</text>')
    rows.append('<text x="36" y="1098" font-family="sans-serif" font-size="15" fill="#6b6459">Estimated volumes from the front sprite, not an original 3D model. Hidden doors stay on their original wall.</text>')
    rows.append('</svg>')
    return '\n'.join(rows)+'\n'


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output',type=Path)
    args=parser.parse_args()
    models={
        'Barn':gabled('Barn',7,4,[(0,1.6),(.9,2.45),(3.1,3),(5.3,2.45),(6.2,1.6)],'#ac4933','#a78150'),
        'Shed':gabled('Shed',7,3,[(0,2.2),(.9,3.5),(2.1,4.5),(3.5,5),(4.9,4.5),(6.1,3.5),(7,2.2)],'#cf9b58','#b64027'),
        'Stable':stable(),
    }
    args.output.mkdir(parents=True,exist_ok=True)
    for name,model in models.items():
        path=args.output/(name.lower()+'-structure.svg')
        path.write_text(draw(name,model),encoding='utf-8')
        print(path)


if __name__=='__main__':
    main()
