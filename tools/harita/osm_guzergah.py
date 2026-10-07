# OSM yol verisinden (osm/roads.json, Overpass API) Kizilay-Atakule guzergahini cikarir.
# Yalnizca referans icindir; oyun haritasi stilize tasarlanir.
import json, math, heapq
d = json.load(open('osm/roads.json'))
LAT0, LON0 = 39.9035, 32.856
def xy(lat, lon):
    return ((lon-LON0)*111320*math.cos(math.radians(LAT0)), (lat-LAT0)*110540)
G = {}; meta = {}
for e in d['elements']:
    if e['type'] != 'way': continue
    t = e.get('tags', {}); ow = False
    if t.get('name') not in ('Atatürk Bulvarı','Cinnah Caddesi','Kızılay Myd.',None): continue
    pts = [(round(p['lat'],7), round(p['lon'],7)) for p in e['geometry']]
    for a, b in zip(pts, pts[1:]):
        (x1,y1),(x2,y2) = xy(*a), xy(*b); w = math.hypot(x2-x1, y2-y1)
        G.setdefault(a, []).append((b, w, t.get('name')))
        if not ow: G.setdefault(b, []).append((a, w, t.get('name')))
nodes=list(G)
import itertools
for a in nodes:
    ax,ay=xy(*a)
    for b in nodes:
        if a<b:
            bx,by=xy(*b); w=math.hypot(ax-bx,ay-by)
            if w<25: G[a].append((b,w,'snap')); G[b].append((a,w,'snap'))
def near(lat, lon):
    return min(G, key=lambda n: math.hypot(*(p-q for p,q in zip(xy(*n), xy(lat,lon)))))
def path(s, g):
    dist = {s:0}; prev = {}; pq = [(0,s)]
    while pq:
        c,u = heapq.heappop(pq)
        if u == g: break
        if c > dist[u]: continue
        for v,w,n in G.get(u, []):
            if c+w < dist.get(v, 1e18): dist[v]=c+w; prev[v]=(u,n); heapq.heappush(pq,(c+w,v))
    out=[g]; names=[]
    while out[-1] != s:
        u,n = prev[out[-1]]; out.append(u); names.append(n)
    return out[::-1], names[::-1]
waypoints = [(39.9213,32.8541),(39.9131,32.8540),(39.9023,32.8603),(39.8950,32.8575),(39.8846,32.8557)]
full=[]; fnames=[]
for a,b in zip(waypoints, waypoints[1:]):
    p,n = path(near(*a), near(*b)); full += p if not full else p[1:]; fnames += n
pts=[xy(*p) for p in full]
L=0; seg=[]
cur=None
for i,((x1,y1),(x2,y2)) in enumerate(zip(pts,pts[1:])):
    w=math.hypot(x2-x1,y2-y1); L+=w
    if fnames[i]!=cur: seg.append([fnames[i],0]); cur=fnames[i]
    seg[-1][1]+=w
print('total m', round(L))
for n,l in seg: print(f'  {n}: {round(l)} m')
json.dump({'latlon':full,'names':fnames}, open('osm/route.json','w'))
stops = {'Kızılay AVM':(39.9213,32.8541),'Meclis':(39.91311,32.85419),'Kuğulu Park':(39.90234,32.86078),'Cinnah (Botanik Parkı)':(39.88709,32.85491),'Atakule':(39.88458,32.85575)}
for k,(la,lo) in stops.items():
    sx,sy=xy(la,lo); best=min(range(len(pts)),key=lambda i:math.hypot(pts[i][0]-sx,pts[i][1]-sy))
    print(k, 'km', round(sum(math.hypot(pts[j+1][0]-pts[j][0],pts[j+1][1]-pts[j][1]) for j in range(best))/1000,2))
