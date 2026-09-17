#!/usr/bin/env python3
"""Refine the existing rail-fitted summer sheet without replacing its owner.

This is geometry authoring, not a Unity renderer. It preserves the source sheet
triangles, UVs and materials, adds only small deterministic gravity/tension
deformation below the rail-contact zone, and optionally adds physical seam-thread
geometry at close LODs. No benchmark state is modified by this script.
"""
from __future__ import annotations
import argparse, copy, hashlib, json, math
from pathlib import Path
import numpy as np
import trimesh

SHEET_TOKEN = "SummerSheet1400x1900RailDrape_Fitted"


def smoothstep01(x):
    x=np.clip(x,0.0,1.0)
    return x*x*(3.0-2.0*x)


def sha256(path: Path) -> str:
    h=hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''): h.update(chunk)
    return h.hexdigest()


def tri_count(scene: trimesh.Scene) -> int:
    return int(sum(len(scene.geometry[g].faces) for _,g in [scene.graph[n] for n in scene.graph.nodes_geometry]))


def find_sheet(scene: trimesh.Scene):
    nodes=[n for n in scene.graph.nodes_geometry if SHEET_TOKEN in n]
    if len(nodes)!=1:
        raise RuntimeError(f'Expected exactly one fitted sheet node, found {nodes}')
    node=nodes[0]
    tf,gname=scene.graph[node]
    return node, np.asarray(tf,float), gname, scene.geometry[gname]


def deform_world(vertices: np.ndarray):
    """Deterministic sub-centimeter gravity/tension variation below contact zone.

    Y is up. Z spans the two hanging panels around the guardrail top. The top
    contact band is frozen so prior rail-clearance evidence is not invalidated.
    """
    v=np.asarray(vertices,float).copy()
    x,y,z=v.T
    xmin,xmax=float(x.min()),float(x.max())
    ymin,ymax=float(y.min()),float(y.max())
    u=(x-xmin)/max(xmax-xmin,1e-9)
    # Zero at/near rail contact, full influence lower down.
    t=np.clip((-0.18-y)/max((-0.18-ymin),1e-9),0.0,1.0)
    w=smoothstep01(t)
    amp=(0.30+0.70*w)

    # Distinguish the outward/front and inward/back hanging panels only after
    # leaving the rounded contact region. Their phases are intentionally related
    # but not mirrored to avoid a synthetic repeated-wave read.
    front=(z>1.030)
    phase=np.where(front,0.21,0.63)
    direction=np.where(front,1.0,-1.0)

    # Broad gravity folds + finer tension ripples; all are geometry, not baked light.
    dz=(
        0.0065*np.sin(2*math.pi*(2.65*u + 0.18*t + phase)) +
        0.0031*np.sin(2*math.pi*(5.85*u - 0.31*t + 0.55*phase)) +
        0.0018*np.sin(2*math.pi*(1.35*u + 1.75*t + 0.11))
    )
    # Local diagonal tension fans descending from two rail-over points.
    fan_l=np.exp(-((u-0.20)/0.19)**2)
    fan_r=np.exp(-((u-0.78)/0.17)**2)
    dz += 0.0026*np.sin(2*math.pi*(1.7*u+2.2*t+0.15))*fan_l
    dz += 0.0022*np.sin(2*math.pi*(2.0*u-1.9*t+0.41))*fan_r
    # The two hanging panels receive opposite small offsets so their negative
    # space varies physically without approaching each other by more than 20 mm.
    v[:,2] += w*amp*dz + w*0.0009*direction*np.sin(2*math.pi*(0.75*u+t))

    # Bottom hem uneven sag (<= 9 mm), fading in only over the last ~16% height.
    bottom=smoothstep01((t-0.84)/0.16)
    dy=-(0.0035 + 0.0045*(0.5+0.5*np.sin(2*math.pi*(2.15*u+0.17))))*bottom
    # Keep the very side walls coherent by tapering the extra sag close to x edges.
    edge=np.minimum(u,1-u)
    edge_w=smoothstep01(edge/0.035)
    v[:,1] += dy*(0.55+0.45*edge_w)

    # A subtle free-edge flutter in Z, strongest near bottom and sides; still dry,
    # no wind animation claim, simply an authored captured shape.
    side_w=np.clip(1.0-edge/0.08,0.0,1.0)
    v[:,2] += bottom*side_w*0.0022*np.sin(2*math.pi*(t*1.2 + u*3.0 + 0.2))*direction

    # Exact frozen contact zone check.
    frozen=y>=-0.18
    if np.any(np.linalg.norm(v[frozen]-vertices[frozen],axis=1)>1e-12):
        raise AssertionError('Rail-contact freeze violated')
    return v


def cylinder_between(a,b,radius,sections,material):
    a=np.asarray(a,float); b=np.asarray(b,float)
    if np.linalg.norm(b-a)<1e-8: return None
    m=trimesh.creation.cylinder(radius=radius, segment=np.vstack([a,b]), sections=sections)
    m.visual.material=copy.deepcopy(material)
    return m


def nearest_surface_z(vertices, side_front=True):
    # Fast brute-force helper closure; path point counts are small.
    vv=np.asarray(vertices,float)
    cand=vv[vv[:,2]>1.03] if side_front else vv[vv[:,2]<1.03]
    if len(cand)==0: raise RuntimeError('No hanging panel candidates')
    def sample(x,y):
        d=(cand[:,0]-x)**2 + 1.8*(cand[:,1]-y)**2
        q=cand[int(np.argmin(d))]
        return float(q[2])
    return sample


def add_exposed_seam(scene, sheet_world, source_material, level):
    """Add actual thread geometry on the outward panel at close LODs only."""
    if level not in ('MASTER','LOD0','LOD1'):
        return 0, []
    xmin,ymin,_=sheet_world.min(0); xmax,ymax,_=sheet_world.max(0)
    samplez=nearest_surface_z(sheet_world, True)
    sections={'MASTER':8,'LOD0':8,'LOD1':6}[level]
    # Thread radius 0.32 mm: physically small, but readable in grazing closeup.
    radius=0.00032
    paths=[]
    # Double bottom hem stitch, 8 and 12 mm above the lowest finished edge.
    for off in (0.008,0.012):
        xs=np.linspace(xmin+0.010,xmax-0.010,34 if level!='LOD1' else 22)
        ys=np.full_like(xs,ymin+off)
        paths.append(list(zip(xs,ys)))
    # Side hem stitches 9 mm inboard. Stop before the rail-contact bend.
    for xx in (xmin+0.009,xmax-0.009):
        ys=np.linspace(ymin+0.015,min(-0.205,ymax-0.03),28 if level!='LOD1' else 18)
        xs=np.full_like(ys,xx)
        paths.append(list(zip(xs,ys)))
    pieces=[]
    for path in paths:
        pts=[]
        for x,y in path:
            z=samplez(float(x),float(y))+0.00038
            pts.append(np.array([x,y,z]))
        for a,b in zip(pts[:-1],pts[1:]):
            c=cylinder_between(a,b,radius,sections,source_material)
            if c is not None: pieces.append(c)
    if not pieces: return 0, []
    seam=trimesh.util.concatenate(pieces)
    seam.visual.material=copy.deepcopy(source_material)
    name=f'BalconyLaundryRailDrapeLifeSet_SummerSheet_StitchThread_{level}'
    scene.add_geometry(seam,node_name=name,geom_name=name,transform=np.eye(4))
    return int(len(seam.faces)), [name]


def refine_scene(input_path: Path, output_path: Path, level: str):
    scene=trimesh.load(input_path,force='scene',process=False)
    before=tri_count(scene)
    node,tf,gname,mesh=find_sheet(scene)
    src=mesh.copy()
    world=trimesh.transform_points(src.vertices,tf)
    world_new=deform_world(world)
    inv=np.linalg.inv(tf)
    local_new=trimesh.transform_points(world_new,inv)
    mesh.vertices=local_new
    # Force normal refresh without changing faces/UV/material ownership.
    mesh._cache.clear()
    _=mesh.vertex_normals
    source_mat=copy.deepcopy(getattr(mesh.visual,'material',None))
    if source_mat is None: raise RuntimeError('Sheet material missing')
    seam_tris,seam_nodes=add_exposed_seam(scene,world_new,source_mat,level)
    after=tri_count(scene)
    output_path.parent.mkdir(parents=True,exist_ok=True)
    scene.export(output_path)
    # Roundtrip with transforms and geometry intact.
    rt=trimesh.load(output_path,force='scene',process=False)
    rt_tri=tri_count(rt)
    _,rt_tf,rt_gn,rt_mesh=find_sheet(rt)
    rt_world=trimesh.transform_points(rt_mesh.vertices,rt_tf)
    report={
      'input':str(input_path),'inputSHA256':sha256(input_path),
      'output':str(output_path),'outputSHA256':sha256(output_path),
      'level':level,'sourceTriangles':before,'outputTriangles':after,
      'addedSeamTriangles':seam_tris,'roundtripTriangles':rt_tri,
      'sheetTrianglesPreserved':int(len(mesh.faces)),
      'sheetVertices':int(len(mesh.vertices)),
      'sheetWatertight':bool(rt_mesh.is_watertight),
      'sheetWindingConsistent':bool(rt_mesh.is_winding_consistent),
      'sheetVolume':float(rt_mesh.volume),
      'sheetPositiveVolume':bool(rt_mesh.volume>0),
      'degenerateFaces':int(np.sum(rt_mesh.area_faces<1e-12)),
      'finiteVertices':bool(np.isfinite(rt_mesh.vertices).all()),
      'finiteNormals':bool(np.isfinite(rt_mesh.vertex_normals).all()),
      'contactFreezeMaxDeltaMm':float(np.max(np.linalg.norm(world_new[world[:,1]>=-0.18]-world[world[:,1]>=-0.18],axis=1))*1000),
      'maxVertexDisplacementMm':float(np.max(np.linalg.norm(world_new-world,axis=1))*1000),
      'meanVertexDisplacementMm':float(np.mean(np.linalg.norm(world_new-world,axis=1))*1000),
      'boundsWorld':rt_world.min(0).tolist()+rt_world.max(0).tolist(),
      'seamNodes':seam_nodes,
      'material':getattr(source_mat,'name',None),
      'paletteChange':'none; source PaleYellowSheet material/maps retained; seam uses same source material',
    }
    if rt_tri!=after: raise AssertionError((after,rt_tri))
    if not report['sheetWatertight'] or not report['sheetPositiveVolume'] or report['degenerateFaces']:
        raise AssertionError(report)
    return report


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--input',required=True)
    ap.add_argument('--output',required=True)
    ap.add_argument('--level',required=True,choices=['MASTER','LOD0','LOD1','LOD2','LOD3'])
    ap.add_argument('--report')
    a=ap.parse_args()
    r=refine_scene(Path(a.input),Path(a.output),a.level)
    if a.report:
        Path(a.report).write_text(json.dumps(r,indent=2),encoding='utf-8')
    print(json.dumps(r,indent=2))
if __name__=='__main__': main()
