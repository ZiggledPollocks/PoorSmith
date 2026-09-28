from pathlib import Path
from collections import deque
import sys
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parent
if len(sys.argv) != 2:
    raise SystemExit('Usage: python PrepareCaveMask.py <source-sketch.jpg>')
SOURCE = Path(sys.argv[1])
X_MIN, X_MAX = -100, 160
Y_MIN, Y_MAX = -110, 32
PX_ENTRANCE, WORLD_ENTRANCE = 1175, 28
PX_GROUND = 346
SX, SY = .0912, .0825

im = np.asarray(Image.open(SOURCE).convert('RGB'))
height, width, _ = im.shape
mask = np.zeros((Y_MAX-Y_MIN+1,X_MAX-X_MIN+1), dtype=np.uint8)
for y in range(Y_MIN,Y_MAX+1):
    py = int(round(PX_GROUND - (y+.5)/SY))
    if py<0 or py>=height: continue
    for x in range(X_MIN,X_MAX+1):
        px = int(round(PX_ENTRANCE+(x+.5-WORLD_ENTRANCE)/SX))
        if px<0 or px>=width: continue
        patch = im[max(0,py-5):min(height,py+6),max(0,px-5):min(width,px+6)]
        color = np.median(patch.reshape(-1,3),axis=0)
        low,high = color.min(),color.max()
        if high-low<=18 and 62<=color.mean()<=232:
            mask[Y_MAX-y,x-X_MIN] = 255

# Remove small sketch labels/objects and bridge JPEG/outline pinholes.
image = Image.fromarray(mask).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3))
mask = np.asarray(image).copy() > 0
seen = np.zeros(mask.shape,dtype=bool)
components=[]
for sy in range(mask.shape[0]):
    for sx in range(mask.shape[1]):
        if not mask[sy,sx] or seen[sy,sx]: continue
        todo=deque([(sy,sx)]);seen[sy,sx]=True;nodes=[]
        while todo:
            cy,cx=todo.popleft();nodes.append((cy,cx))
            for ny,nx in ((cy-1,cx),(cy+1,cx),(cy,cx-1),(cy,cx+1)):
                if 0<=ny<mask.shape[0] and 0<=nx<mask.shape[1] and mask[ny,nx] and not seen[ny,nx]:
                    seen[ny,nx]=True;todo.append((ny,nx))
        components.append(nodes)
components.sort(key=len,reverse=True)
print('components', [len(x) for x in components[:10]])
main = np.zeros(mask.shape,dtype=np.uint8)
for cy,cx in components[0]: main[cy,cx]=255

# User-requested wider walkable corridors: two additional world tiles on each
# exposed edge. Major white rock islands remain several tiles thick.
main = np.asarray(Image.fromarray(main).filter(ImageFilter.MaxFilter(5))).copy()

# The top-left above-ground forest is outside the cave operation.
for y in range(-15,Y_MAX+1):
    for x in range(X_MIN,27):
        main[Y_MAX-y,x-X_MIN]=0

Image.fromarray(main).save(ROOT/'cave-corridor-mask.png')
preview=np.zeros((main.shape[0],main.shape[1],3),dtype=np.uint8)
preview[:]=[80,65,58]              # wall
preview[main>0]=[36,48,55]         # exposed cave background
for y in range(-15,Y_MAX+1):
    preview[Y_MAX-y,:27-X_MIN]=[98,132,113] # protected forest
preview=Image.fromarray(preview).resize((preview.shape[1]*8,preview.shape[0]*8),Image.Resampling.NEAREST)
preview.save(ROOT/'cave-mask-preview.png')
print('corridor cells',int((main>0).sum()),'total',main.size)
