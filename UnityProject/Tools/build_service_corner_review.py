from __future__ import annotations
import argparse, importlib.util, json, math, hashlib
from pathlib import Path
import numpy as np
import trimesh

ROOT=Path(__file__).resolve().parent

def load(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def add_prefixed(out,scene,prefix,T=None):
    T=np.eye(4) if T is None else T
    count=0
    for name,g in scene.geometry.items():
        q=g.copy();q.apply_transform(T);out.add_geometry(q,node_name=prefix+'__'+name,geom_name=prefix+'__'+name);count+=len(q.faces)
    return count

def tr(x=0,y=0,z=0,ry=0,rx=0,rz=0):
    M=np.eye(4)
    R=trimesh.transformations.euler_matrix(math.radians(rx),math.radians(ry),math.radians(rz),'sxyz')
    M[:3,:3]=R[:3,:3];M[:3,3]=[x,y,z];return M

def mat(name,color,metal=0,rough=.7):
    return trimesh.visual.material.PBRMaterial(name=name,baseColorFactor=[*color,1],metallicFactor=metal,roughnessFactor=rough)

def box(name,ext,c,color,metal=0,rough=.7):
    m=trimesh.creation.box(extents=ext);m.apply_translation(c);m.visual.material=mat(name,color,metal,rough);return m

def bounds_for_prefix(scene,prefix):
    gs=[g for n,g in scene.geometry.items() if n.startswith(prefix+'__')]
    lo=np.min([g.bounds[0] for g in gs],axis=0);hi=np.max([g.bounds[1] for g in gs],axis=0);return np.array([lo,hi])

def overlap(a,b,eps=.003):
    return bool(np.all(a[1]-eps>b[0]) and np.all(b[1]-eps>a[0]))

def build():
    hero=load('hero',ROOT/'refine_faucet_hero.py')
    hose_owner=load('hoseowner',ROOT/'generate_faucet_hose.py')
    hose_refine=load('hoserefine',ROOT/'refine_hose_nozzle_resting.py')
    life=load('life',ROOT/'generate_balcony_life.py')
    cleaning=load('clean',ROOT/'generate_balcony_cleaning_set.py')
    drainmod=load('drain',ROOT/'generate_balcony_floor_drain.py')
    out=trimesh.Scene(); groups={}
    groups['HeroFaucet']=add_prefixed(out,hero.refine('LOD0'),'HeroFaucet',tr(z=.035))
    hose,_=hose_refine.refine('LOD0')
    groups['Hose']=add_prefixed(out,hose,'Hose',tr(x=.20,z=.52,ry=12))
    can,_=life.watering(96)
    dy=-can.bounds[0,1]
    groups['WateringCan']=add_prefixed(out,can,'WateringCan',tr(x=-.37,y=dy,z=.47,ry=-22))
    brush,_=cleaning.scrubbrush(48,24);cleaning.normalize(brush)
    groups['ScrubBrush']=add_prefixed(out,brush,'ScrubBrush',tr(x=-.50,z=.22,ry=28))
    drain=drainmod.build('LOD0')
    groups['FloorDrain']=add_prefixed(out,drain,'FloorDrain',tr(x=-.48,z=.73,ry=5))
    # Reversible diagnostic architectural context; never a formal benchmark scene mutation.
    floor=box('DIAGNOSTIC_floor',[1.25,.025,1.05],[0,-.0125,.39],(.37,.38,.39),0,.86)
    wall=box('DIAGNOSTIC_wall',[1.25,1.15,.035],[0,.53,-.0175],(.58,.59,.56),0,.82)
    curb=box('DIAGNOSTIC_curb',[1.25,.10,.11],[0,.05,.84],(.43,.44,.43),0,.84)
    out.add_geometry(floor,node_name='DIAGNOSTIC_floor',geom_name='DIAGNOSTIC_floor')
    out.add_geometry(wall,node_name='DIAGNOSTIC_wall',geom_name='DIAGNOSTIC_wall')
    out.add_geometry(curb,node_name='DIAGNOSTIC_curb',geom_name='DIAGNOSTIC_curb')
    # Check independent hero object bounds only; floor/wall contacts are intentional and excluded.
    bs={k:bounds_for_prefix(out,k) for k in groups}
    collisions=[]
    keys=list(groups)
    for i,a in enumerate(keys):
        for b in keys[i+1:]:
            if overlap(bs[a],bs[b]):collisions.append([a,b])
    return out,groups,bs,collisions

def verify(scene):
    rows=[]
    for name,g in scene.geometry.items():
        assert np.isfinite(g.vertices).all() and np.isfinite(g.vertex_normals).all(),name
        assert np.all(g.area_faces>1e-14),name
        h=g.copy();h.merge_vertices(digits_vertex=8,merge_tex=True,merge_norm=True);h.remove_unreferenced_vertices();h.fix_normals(multibody=True)
        assert h.is_watertight and h.is_winding_consistent and h.volume>0,(name,h.is_watertight,h.volume)
        rows.append({'component':name,'triangles':int(len(g.faces))})
    return {'triangles':sum(x['triangles'] for x in rows),'components':len(rows),'boundsMetres':scene.bounds.tolist()}

def main(out):
    out.mkdir(parents=True,exist_ok=True)
    scene,groups,bounds,collisions=build();rep=verify(scene)
    glb=out/'BalconyServiceCornerReview_LOD0.glb';glb.write_bytes(scene.export(file_type='glb'))
    re=trimesh.load(glb,force='scene',process=False);assert sum(len(g.faces) for g in re.geometry.values())==rep['triangles']
    manifest={'id':'BalconyServiceCornerReview_LOD0','status':'EXTERNAL_DIAGNOSTIC_ASSEMBLY_NOT_UNITY_RENDER','triangles':rep['triangles'],'components':rep['components'],'groupTriangles':groups,'groupBoundsMetres':{k:v.tolist() for k,v in bounds.items()},'unexpectedIndependentAabbOverlaps':collisions,'formalBenchmarkSceneChanged':False,'unityVerified':False,'bucketStatus':'Existing GalvanizedGardenBucket10L formal asset is not duplicated here; this run had its ntprop metadata through GitHub but no matching external GLB binary in the working container. Add it when the owner/importer can be executed or a verified export is available.','expectedVisualImpactWithoutPoints':'Establishes real-world scale/grounding and relative placement for faucet, hose, watering can and scrub brush; no visual points without Unity pixels.','sha256':hashlib.sha256(glb.read_bytes()).hexdigest()}
    (out/'BalconyServiceCornerReview.manifest.json').write_text(json.dumps(manifest,indent=2))
    print(json.dumps({k:manifest[k] for k in ['triangles','components','unexpectedIndependentAabbOverlaps']},indent=2))

if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);a=ap.parse_args();main(a.output)
