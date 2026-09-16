"""Existing BalconyDrainageIntegration floor owner, recovered as an importable module.

The archived exporter passed (x0,x1,z0,z1) to shapely.box(minx,miny,maxx,maxy),
removing a large rectangle rather than the small rainwater notch. This module
uses the existing authoring contract's explicit XZ ordering. Drain/hose meshes
are reused from the verified original GLBs by build_balcony_structural_contact_repair.
"""
import numpy as np
import shapely
from shapely.geometry import Point, box
from shapely.ops import unary_union
import trimesh

QUADS={'MASTER':24,'LOD0':18,'LOD1':12,'LOD2':8,'LOD3':5}


def finished_floor_y(z):
    return -1.2470-.015*z+.0008


def floor_polygon(level):
    # Bounds are (min X, min Z, max X, max Z), never (X0, X1, Z0, Z1).
    outer=box(-1.8,0.,1.8,.970)
    rainwater_notch=box(1.600,0.000,1.780,.180)
    drain=Point(.450,.820).buffer(.0434,quad_segs=QUADS[level])
    return outer.difference(unary_union([rainwater_notch,drain]))


def sloped_solid(poly,bottom,top,name,material):
    vertices=[];faces=[];lookup={}
    def index(x,z,y):
        key=(round(float(x),10),round(float(y),10),round(float(z),10))
        if key not in lookup:lookup[key]=len(vertices);vertices.append([x,y,z])
        return lookup[key]
    for triangle in shapely.constrained_delaunay_triangles(poly).geoms:
        if not poly.covers(triangle.representative_point()):continue
        points=list(triangle.exterior.coords)[:3]
        a=[index(x,z,top(z)) for x,z in points]
        b=[index(x,z,bottom(z)) for x,z in points]
        faces.extend(([a[0],a[2],a[1]],b))
    for ring in (poly.exterior,*poly.interiors):
        for (x0,z0),(x1,z1) in zip(ring.coords,list(ring.coords)[1:]):
            a=index(x0,z0,bottom(z0));b=index(x1,z1,bottom(z1))
            c=index(x1,z1,top(z1));d=index(x0,z0,top(z0))
            faces.extend(([a,b,c],[a,c,d]))
    mesh=trimesh.Trimesh(vertices=np.asarray(vertices),faces=np.asarray(faces),process=False)
    mesh.fix_normals(multibody=True)
    mesh.metadata.update(name=name,material=material)
    return mesh


def floor_parts(level):
    polygon=floor_polygon(level)
    # Keep the measured finished surface and 4 mm membrane, and make the mortar
    # meet its underside rather than overlap the membrane by 3.2 mm.
    join=lambda z:finished_floor_y(z)-.004
    return [sloped_solid(polygon,lambda z:-1.335,join,
                         'SlopedMortarWithRealDrainCutout','CementMortar'),
            sloped_solid(polygon,join,finished_floor_y,
                         'WaterproofSkinWithRealDrainCutout','WaterproofCoating')]
