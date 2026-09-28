from pathlib import Path
from collections import deque
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent
X0, X1, Y0, Y1 = -65, 115, -46, 19
PLAYER_WIDTH = 1.7525
PLAYER_HEIGHT = 2.695
CORRIDOR_RADIUS = 3.5
ROCK_EDGE_DEPTH = 2

H,W=Y1-Y0+1,X1-X0+1
out=np.zeros((H,W),dtype=np.uint8)
def disk(cx,cy,r):
    rr=int(np.ceil(r))
    for dy in range(-rr,rr+1):
        yy=cy+dy
        if yy<Y0 or yy>Y1:continue
        for dx in range(-rr,rr+1):
            xx=cx+dx
            if xx<X0 or xx>X1 or dx*dx+dy*dy>r*r:continue
            out[Y1-yy,xx-X0]=255

def carve_route(points, radius=CORRIDOR_RADIUS):
    for (ax,ay),(bx,by) in zip(points,points[1:]):
        count=max(1,int(np.ceil(max(abs(bx-ax),abs(by-ay))*3)))
        for step in range(count+1):
            t=step/count
            disk(round(ax+(bx-ax)*t),round(ay+(by-ay)*t),radius)

# Trace the sketch's upper, middle and deep cave bands as a single traversable
# switchback. Gradual changes in elevation fit the saved player's jump rise.
# Broad source silhouettes are intentionally not retained: their high one-way
# drops and empty chambers were the specific player-scale problem.
main_route=[
    (29,2),(43,2),(55,1),(67,-1),(80,-3),(92,-6),(103,-9),(109,-12),
    (99,-15),(85,-17),(65,-18),(45,-19),(25,-20),(5,-21),(-15,-22),(-35,-23),(-47,-25),
    (-37,-29),(-20,-30),(0,-31),(20,-32),(40,-33),(60,-34),(80,-35),(100,-36),(110,-38),
]
carve_route(main_route)

# Keep the saved forest surface and the terrain directly visible beneath it.
for y in range(-10,Y1+1):
    out[Y1-y,:27-X0]=0

# Player-sized floor clearance at the mouth and a short landing strip.
for x in range(27,34):
    for y in range(0,7):
        out[Y1-y,x-X0]=255

# Main connected component: trim any skeleton artifacts after scaling.
seen=np.zeros_like(out,dtype=bool);components=[]
for sy in range(H):
    for sx in range(W):
        if out[sy,sx]==0 or seen[sy,sx]:continue
        q=deque([(sy,sx)]);seen[sy,sx]=True;cells=[]
        while q:
            cy,cx=q.popleft();cells.append((cy,cx))
            for ny,nx in ((cy-1,cx),(cy+1,cx),(cy,cx-1),(cy,cx+1)):
                if 0<=ny<H and 0<=nx<W and out[ny,nx] and not seen[ny,nx]:
                    seen[ny,nx]=True;q.append((ny,nx))
        components.append(cells)
components.sort(key=len,reverse=True)
print('components', [len(c) for c in components[:8]])
if len(components)<1:raise RuntimeError('No cave corridor')
connected=np.zeros_like(out)
for y,x in components[0]:connected[y,x]=255
out=connected

# At the left hairpin two short footholds bridge the 8-unit merged chamber.
# Each rise is inside the player's 3.06-unit jump envelope.
for left,right,tile_y in [(-31,-28,-29),(-37,-34,-31)]:
    for x in range(left,right+1):
        if out[Y1-tile_y,x-X0]:out[Y1-tile_y,x-X0]=0

# Close a low pocket beneath the first foothold and the single-cell tip at the
# deep endpoint; a full-size player cannot enter those cells from the route.
for x in range(-32,-28):out[Y1-(-32),x-X0]=0
out[Y1-(-39),112-X0]=0

# Rock only borders playable space. A single solid-color sprite fills the far
# impassable mass without repeated rock tiles across the whole camera area.
border=np.zeros_like(out)
for dy in range(-ROCK_EDGE_DEPTH,ROCK_EDGE_DEPTH+1):
    for dx in range(-ROCK_EDGE_DEPTH,ROCK_EDGE_DEPTH+1):
        if dx*dx+dy*dy>ROCK_EDGE_DEPTH*ROCK_EDGE_DEPTH:continue
        ys=slice(max(0,dy),min(H,H+dy));xs=slice(max(0,dx),min(W,W+dx))
        srcys=slice(max(0,-dy),min(H,H-dy));srcxs=slice(max(0,-dx),min(W,W-dx))
        border[ys,xs]=np.maximum(border[ys,xs],out[srcys,srcxs])
border[(out>0)]=0
for y in range(-10,Y1+1):border[Y1-y,:27-X0]=0

Image.fromarray(out).save(ROOT/'player-fit-corridor-mask.png')
Image.fromarray(border).save(ROOT/'player-fit-rock-border.png')
silhouette=np.zeros((H,W,4),dtype=np.uint8)
silhouette[:,:,:3]=[34,30,30]
silhouette[:,:,3]=np.where(out>0,0,255)
silhouette[border>0,:3]=[91,67,55]
for y in range(-15,Y1+1):silhouette[Y1-y,:27-X0,3]=0
Image.fromarray(silhouette).save(ROOT/'player-fit-wall-silhouette.png')
preview=np.zeros((H,W,3),dtype=np.uint8)
preview[:]=[34,30,30];preview[out>0]=[35,57,66];preview[border>0]=[103,70,56]
for y in range(-10,Y1+1):preview[Y1-y,:27-X0]=[98,132,113]
Image.fromarray(preview).resize((W*10,H*10),Image.Resampling.NEAREST).save(ROOT/'player-fit-preview.png')
print('corridor',int((out>0).sum()),'rock edge',int((border>0).sum()),'wall',int((out==0).sum()))
