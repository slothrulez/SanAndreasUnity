#!/usr/bin/env python3
"""Converts an Overpass JSON export of Kochi into the world data used by the game.

Outputs (in Assets/Kochi/Resources/Kochi/OSM):
  kochi_world.json  roads, buildings (with roof triangles), points of interest, spawn
  ground.png        ground texture: land, water and parks, covering the data bounds
  minimap.png       minimap: 6000 m square centred on the world origin, north up

Map data (c) OpenStreetMap contributors, ODbL. Map colours after Subin Siby's
GTA Vice City style OpenStreetMap theme (public domain).
"""
import json, math, os, sys
from PIL import Image, ImageDraw

LAT0, LON0 = 9.965, 76.26
KX = 111320.0 * math.cos(math.radians(LAT0))
KZ = 110574.0
MINIMAP_EDGE, MINIMAP_PX = 6000.0, 1536
GROUND_PX = 4096

COL_LAND = (255, 255, 210)
COL_WATER = (90, 85, 99)
COL_PARK = (28, 134, 75)
COL_INDUSTRIAL = (210, 207, 210)
COL_ROAD = (7, 6, 11)

ROAD_WIDTH = {
    'motorway': 14, 'trunk': 13, 'primary': 12, 'secondary': 10, 'tertiary': 8,
    'motorway_link': 7, 'trunk_link': 7, 'primary_link': 7, 'secondary_link': 7, 'tertiary_link': 6,
    'residential': 6, 'unclassified': 6, 'living_street': 5, 'road': 6, 'service': 4,
}
PARK_TAGS = {('leisure', 'park'), ('leisure', 'garden'), ('leisure', 'pitch'), ('leisure', 'playground'),
             ('landuse', 'grass'), ('landuse', 'forest'), ('landuse', 'meadow'), ('natural', 'wood'),
             ('natural', 'scrub'), ('natural', 'grassland'), ('leisure', 'nature_reserve'), ('landuse', 'recreation_ground')}
INDUSTRIAL_TAGS = {('landuse', 'industrial'), ('landuse', 'port'), ('landuse', 'railway'), ('aeroway', 'aerodrome'),
                   ('landuse', 'military'), ('industrial', 'port'), ('man_made', 'pier')}
POI_TYPES = {'hospital': 'hospital', 'police': 'police', 'bank': 'bank', 'atm': 'bank', 'ferry_terminal': 'ferry',
             'cinema': 'cinema', 'post_office': 'post', 'bus_station': 'bus', 'fire_station': 'fire'}
COASTLINE_ORDER = [612863795, 827910128, 613000915, 722690671, 998074909]


def xz(p):
    return ((p['lon'] - LON0) * KX, (p['lat'] - LAT0) * KZ)


def ring_area(r):
    return 0.5 * sum(r[i][0] * r[(i + 1) % len(r)][1] - r[(i + 1) % len(r)][0] * r[i][1] for i in range(len(r)))


def assemble_rings(segments):
    """Joins way segments that share endpoints into closed rings."""
    segs = [list(s) for s in segments if len(s) >= 2]
    rings = []
    while segs:
        ring = segs.pop()
        changed = True
        while changed and ring[0] != ring[-1]:
            changed = False
            for i, s in enumerate(segs):
                if s[0] == ring[-1]:
                    ring += s[1:]
                elif s[-1] == ring[-1]:
                    ring += s[::-1][1:]
                elif s[-1] == ring[0]:
                    ring = s[:-1] + ring
                elif s[0] == ring[0]:
                    ring = s[::-1][:-1] + ring
                else:
                    continue
                segs.pop(i)
                changed = True
                break
        if len(ring) >= 4:
            rings.append(ring)
    return rings


def ear_clip(poly):
    """Triangulates a simple polygon (counter-clockwise). Returns index triples."""
    n = len(poly)
    idx = list(range(n))
    tris = []

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    def inside(p, a, b, c):
        return cross(a, b, p) >= 0 and cross(b, c, p) >= 0 and cross(c, a, p) >= 0

    guard = 0
    while len(idx) > 3 and guard < n * n:
        guard += 1
        m = len(idx)
        for k in range(m):
            i0, i1, i2 = idx[(k - 1) % m], idx[k], idx[(k + 1) % m]
            a, b, c = poly[i0], poly[i1], poly[i2]
            if cross(a, b, c) <= 1e-9:
                continue
            if any(inside(poly[j], a, b, c) for j in idx if j not in (i0, i1, i2)):
                continue
            tris.append((i0, i1, i2))
            idx.pop(k)
            break
        else:
            break
    if len(idx) == 3:
        tris.append(tuple(idx))
    return tris


def building_height(tags, osm_id):
    for key in ('height', 'building:height'):
        try:
            return max(3.0, min(120.0, float(tags[key].split()[0])))
        except (KeyError, ValueError):
            pass
    try:
        return max(3.0, min(120.0, float(tags['building:levels']) * 3.2))
    except (KeyError, ValueError):
        pass
    return 4.0 + (osm_id * 7919 % 5) * 1.6  # deterministic 1-3 storey variety


def main(src, out_dir):
    els = json.load(open(src))['elements']
    roads, buildings, pois = [], [], []
    water_outer, water_inner, parks, industrial = [], [], [], []
    coast = {}
    for e in els:
        tags = e.get('tags', {})
        if e['type'] == 'relation':
            if tags.get('natural') == 'water' or 'water' in tags:
                outer = [[xz(p) for p in m['geometry']] for m in e['members'] if m.get('role') == 'outer' and 'geometry' in m]
                inner = [[xz(p) for p in m['geometry']] for m in e['members'] if m.get('role') == 'inner' and 'geometry' in m]
                water_outer += assemble_rings(outer)
                water_inner += assemble_rings(inner)
            continue
        geom = e.get('geometry')
        if not geom:
            continue
        pts = [xz(p) for p in geom]
        closed = len(pts) >= 4 and geom[0] == geom[-1]
        if tags.get('natural') == 'coastline':
            coast[e['id']] = pts
        elif 'highway' in tags and tags['highway'] in ROAD_WIDTH:
            w = ROAD_WIDTH[tags['highway']]
            try:
                w = max(w, float(tags['lanes']) * 3.2)
            except (KeyError, ValueError):
                pass
            roads.append({'w': round(w, 1), 'b': 1 if tags.get('bridge') else 0, 'n': tags.get('name', ''),
                          'p': [round(c, 1) for p in pts for c in p]})
        elif 'building' in tags and closed:
            ring = pts[:-1]
            if ring_area(ring) < 0:
                ring.reverse()
            if abs(ring_area(ring)) < 8:
                continue
            tris = ear_clip(ring)
            if not tris:
                continue
            buildings.append({'h': round(building_height(tags, e['id']), 1),
                              'p': [round(c, 1) for p in ring for c in p],
                              't': [i for t in tris for i in t]})
        elif closed and (tags.get('natural') == 'water' or 'water' in tags or tags.get('waterway') in ('dock', 'riverbank')):
            water_outer.append(pts)
        elif closed and any((k, tags.get(k)) in PARK_TAGS for k in ('leisure', 'landuse', 'natural')):
            parks.append(pts)
        elif closed and any((k, tags.get(k)) in INDUSTRIAL_TAGS for k in ('landuse', 'aeroway', 'industrial', 'man_made')):
            industrial.append(pts)
        elif tags.get('waterway') in ('river', 'canal') and not closed:
            roads.append({'w': 0, 'water': 1, 'p': [round(c, 1) for p in pts for c in p]})
        amenity = tags.get('amenity')
        if amenity in POI_TYPES:
            b = e.get('bounds')
            if b:
                c = xz({'lat': (b['minlat'] + b['maxlat']) / 2, 'lon': (b['minlon'] + b['maxlon']) / 2})
                pois.append({'t': POI_TYPES[amenity], 'x': round(c[0], 1), 'z': round(c[1], 1), 'n': tags.get('name', '')})

    # Sea: the coastline has land on its left; close it around the west edge.
    sea = []
    for cid in COASTLINE_ORDER:
        if cid in coast:
            sea += coast[cid] if not sea else coast[cid][1:]
    if sea:
        west = xz({'lat': LAT0, 'lon': 76.15})[0]
        south = xz({'lat': 9.90, 'lon': LON0})[1]
        north = xz({'lat': 10.03, 'lon': LON0})[1]
        sea += [(sea[-1][0], south), (west, south), (west, north), (sea[0][0], north)]
        water_outer.append(sea)

    drivable = [r for r in roads if not r.get('water')]
    allx = [v for r in drivable for v in r['p'][0::2]]
    allz = [v for r in drivable for v in r['p'][1::2]]
    bounds = [math.floor(min(allx)) - 200, math.floor(min(allz)) - 200, math.ceil(max(allx)) + 200, math.ceil(max(allz)) + 200]

    # Spawn on a road in Fort Kochi, near the Chinese fishing nets.
    target = xz({'lat': 9.9668, 'lon': 76.2425})
    best = None
    for r in drivable:
        if r['w'] < 6 or r['b']:
            continue
        p = r['p']
        for i in range(0, len(p) - 2, 2):
            d = (p[i] - target[0]) ** 2 + (p[i + 1] - target[1]) ** 2
            if best is None or d < best[0]:
                best = (d, p[i], p[i + 1], p[i + 2], p[i + 3], r['w'])
    _, x0, z0, x1, z1, w = best
    heading = math.degrees(math.atan2(x1 - x0, z1 - z0))
    spawn = {'x': round(x0, 1), 'z': round(z0, 1), 'heading': round(heading, 1), 'roadWidth': w}

    os.makedirs(out_dir, exist_ok=True)
    world = {'attribution': 'Map data \u00a9 OpenStreetMap contributors (ODbL)', 'origin': [LAT0, LON0],
             'bounds': bounds, 'spawn': spawn, 'roads': roads, 'buildings': buildings, 'pois': pois}
    with open(os.path.join(out_dir, 'kochi_world.json'), 'w') as f:
        json.dump(world, f, separators=(',', ':'))

    def render(path, x0, z0, x1, z1, px_w, px_h, draw_roads):
        img = Image.new('RGB', (px_w, px_h), COL_LAND)
        d = ImageDraw.Draw(img)
        sx, sz = px_w / (x1 - x0), px_h / (z1 - z0)
        to_px = lambda pts: [((x - x0) * sx, (z1 - z) * sz) for x, z in pts]
        for poly in industrial:
            d.polygon(to_px(poly), fill=COL_INDUSTRIAL)
        for poly in parks:
            d.polygon(to_px(poly), fill=COL_PARK)
        for poly in water_outer:
            d.polygon(to_px(poly), fill=COL_WATER)
        for poly in water_inner:
            d.polygon(to_px(poly), fill=COL_LAND)
        for r in roads:
            if r.get('water'):
                pts = list(zip(r['p'][0::2], r['p'][1::2]))
                d.line(to_px(pts), fill=COL_WATER, width=max(1, int(8 * sx)))
        if draw_roads:
            for r in sorted(drivable, key=lambda r: r['w']):
                pts = list(zip(r['p'][0::2], r['p'][1::2]))
                d.line(to_px(pts), fill=COL_ROAD, width=max(2, int(r['w'] * sx * 1.6)), joint='curve')
        img.save(path)

    b = bounds
    render(os.path.join(out_dir, 'ground.png'), b[0], b[1], b[2], b[3], GROUND_PX, int(GROUND_PX * (b[3] - b[1]) / (b[2] - b[0])), False)
    h = MINIMAP_EDGE / 2
    render(os.path.join(out_dir, 'minimap.png'), -h, -h, h, h, MINIMAP_PX, MINIMAP_PX, True)
    print('roads', len(drivable), 'buildings', len(buildings), 'water', len(water_outer), len(water_inner),
          'parks', len(parks), 'pois', len(pois), 'bounds', bounds, 'spawn', spawn)


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.expanduser('~/kochi-osm/kochi_merged.json'),
         sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(__file__), '../../Assets/Kochi/Resources/Kochi/OSM'))
