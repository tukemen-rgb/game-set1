from __future__ import annotations
import argparse, importlib.util, json, math, hashlib
from pathlib import Path
import numpy as np
import trimesh
from PIL import Image

ROOT = Path(__file__).resolve().parent
OWNER = ROOT / 'generate_faucet_hose.py'

def load_owner(path=OWNER):
    spec = importlib.util.spec_from_file_location('faucet_owner', path)
    mod = importlib.util.module_from_spec(spec); spec.loader.exec_module(mod)
    return mod

MATS = {
    'ChromeBrass': dict(base=(0.60,0.62,0.64), metallic=1.0, rough=0.20, normal=0.12, kind='fine circumferential machining and wipe scratches'),
    'WarmBrass': dict(base=(0.60,0.41,0.15), metallic=1.0, rough=0.29, normal=0.10, kind='fine turned brass machining'),
    'BlueIndexCap': dict(base=(0.035,0.15,0.40), metallic=0.0, rough=0.46, normal=0.08, kind='molded thermoset/plastic stipple'),
    'BlackEPDM': dict(base=(0.018,0.020,0.022), metallic=0.0, rough=0.78, normal=0.10, kind='matte rubber microtexture'),
}

def texture_set(name,size=256):
    s=MATS[name]
    yy,xx=np.mgrid[:size,:size]
    u=xx/size; v=yy/size
    rng=np.random.default_rng(9107 + sum(map(ord,name)))
    noise=rng.normal(0,1,(size,size))
    # Deterministic material-scale signal only; no directional light/highlight baked.
    if s['metallic']:
        scratches=(np.sin(2*math.pi*(41*v + .8*np.sin(2*math.pi*3*u))) + .35*np.sin(2*math.pi*97*v))*0.5
        h=.55*noise+.45*scratches
    else:
        h=.72*noise+.28*np.sin(2*math.pi*(31*u+23*v))
    h=(h-h.min())/(h.max()-h.min()+1e-9)*2-1
    base=np.clip(np.array(s['base'])[None,None,:]*(1+0.012*h[...,None]),0,1)
    rough=np.clip(s['rough']+0.035*h,0.04,0.98)
    mr=np.zeros((size,size,3),dtype=np.uint8)
    mr[...,1]=np.uint8(rough*255)
    mr[...,2]=np.uint8(s['metallic']*255)
    # Height-derived tangent-space normal; amplitude is low and materially plausible.
    gy,gx=np.gradient(h)
    strength=s['normal']
    nx=-gx*strength; ny=-gy*strength; nz=np.ones_like(nx)
    nrm=np.stack([nx,ny,nz],axis=-1)
    nrm/=np.linalg.norm(nrm,axis=-1,keepdims=True)
    nimg=np.uint8(np.clip(nrm*.5+.5,0,1)*255)
    return Image.fromarray(np.uint8(base*255)), Image.fromarray(mr), Image.fromarray(nimg)

def cylindrical_uv(mesh):
    v=np.asarray(mesh.vertices)
    ext=np.ptp(v,axis=0)
    major=int(np.argmax(ext))
    others=[i for i in range(3) if i!=major]
    c=(v.min(axis=0)+v.max(axis=0))/2
    a=v[:,others[0]]-c[others[0]]; b=v[:,others[1]]-c[others[1]]
    u=(np.arctan2(b,a)/(2*math.pi)+.5)%1.0
    vv=(v[:,major]-v[:,major].min())/max(ext[major],1e-9)
    # 4 repeats longitudinally, 3 around circumference: close-up microtexture, not macro weathering.
    return np.column_stack([u*3.0,vv*4.0])

def apply_hero_material(mesh,name):
    if name not in MATS:
        return mesh
    base,mr,norm=texture_set(name)
    spec=MATS[name]
    material=trimesh.visual.material.PBRMaterial(
        name=name,
        baseColorFactor=[1,1,1,1],
        metallicFactor=1.0,
        roughnessFactor=1.0,
        baseColorTexture=base,
        metallicRoughnessTexture=mr,
        normalTexture=norm,
    )
    mesh.visual=trimesh.visual.TextureVisuals(uv=cylindrical_uv(mesh),material=material)
    return mesh

def refine(level):
    o=load_owner()
    scene=o.build_faucet(level)
    sec={'MASTER':72,'LOD0':56,'LOD1':40,'LOD2':24,'LOD3':14}[level]
    small={'MASTER':18,'LOD0':14,'LOD1':10,'LOD2':8,'LOD3':6}[level]
    y=.55
    # Correct the owner's index cap orientation: hub spindle is Y-axis, so cap must face outward along Y.
    if 'BlueIndexCap' in scene.geometry:
        scene.delete_geometry('BlueIndexCap')
    cap=o.cylinder_between([0,y+.0675,.076],[0,y+.0703,.076],.0095,sec,'BlueIndexCapCorrected','BlueIndexCap')
    scene.add_geometry(cap,node_name='BlueIndexCapCorrected',geom_name='BlueIndexCapCorrected')
    # Real packing/sealing and junction details that catch close-range grazing highlights.
    wall=o.cylinder_between([0,y,-.0012],[0,y,.0002],.0355,sec,'EscutcheonEPDMGasket','BlueIndexCap')
    # Replace temporary material below after adding because owner has no EPDM.
    wall.metadata['heroMaterial']='BlackEPDM'; scene.add_geometry(wall,node_name='EscutcheonEPDMGasket',geom_name='EscutcheonEPDMGasket')
    ring=o.torus_component([0,y+.041,.076],.0128,.0010,sec,max(5,small//2),'BonnetPackingReveal','WarmBrass',axis='y')
    scene.add_geometry(ring,node_name='BonnetPackingReveal',geom_name='BonnetPackingReveal')
    gland=o.torus_component([0,y+.047,.076],.0052,.00075,sec,max(5,small//2),'SpindleEPDMPacking','BlueIndexCap',axis='y')
    gland.metadata['heroMaterial']='BlackEPDM'; scene.add_geometry(gland,node_name='SpindleEPDMPacking',geom_name='SpindleEPDMPacking')
    # Outlet collar and barb shoulder are separate manufactured forms rather than an abrupt primitive join.
    n={'MASTER':40,'LOD0':32,'LOD1':22,'LOD2':14,'LOD3':9}[level]
    t=np.linspace(0,1,n); path=[]
    for u in t:
        z=.082+.075*u; yy=y-.010-.040*u*u; path.append([0,yy,z])
    tip=np.array(path[-1]); base=tip+[0,-.029,0]
    collar=o.torus_component(tip+[0,-.001,0],.0106,.0012,sec,max(5,small//2),'SpoutOutletFillet','ChromeBrass',axis='y')
    scene.add_geometry(collar,node_name='SpoutOutletFillet',geom_name='SpoutOutletFillet')
    shoulder=o.cylinder_between(base+[0,.0015,0],base+[0,-.0035,0],.0123,max(12,sec//2),'HoseBarbShoulder','WarmBrass')
    scene.add_geometry(shoulder,node_name='HoseBarbShoulder',geom_name='HoseBarbShoulder')
    # Assign hero PBR maps to all camera-near faucet materials. Original owner material names are preserved.
    for name,g in list(scene.geometry.items()):
        mat_name=getattr(getattr(g.visual,'material',None),'name',None)
        hero=g.metadata.get('heroMaterial') if hasattr(g,'metadata') else None
        if hero:
            apply_hero_material(g,hero)
        elif mat_name in MATS:
            apply_hero_material(g,mat_name)
    return scene

def verify(scene):
    rows=[]
    textured=0; normalmapped=0
    for name,g in scene.geometry.items():
        assert np.isfinite(g.vertices).all() and np.isfinite(g.vertex_normals).all(), name
        assert np.all(g.area_faces>1e-14), name
        h=g.copy(); h.merge_vertices(digits_vertex=8,merge_tex=True,merge_norm=True); h.remove_unreferenced_vertices()
        assert h.is_watertight and h.is_winding_consistent and h.volume>0,(name,h.is_watertight,h.volume)
        mat=getattr(g.visual,'material',None)
        uv=getattr(g.visual,'uv',None)
        if uv is not None:
            assert len(uv)==len(g.vertices) and np.isfinite(uv).all(),name
            textured+=1
        if getattr(mat,'normalTexture',None) is not None: normalmapped+=1
        rows.append({'component':name,'triangles':int(len(g.faces)),'watertight':True,'winding':True,'uv':uv is not None,'normalTexture':getattr(mat,'normalTexture',None) is not None})
    return {'triangles':sum(r['triangles'] for r in rows),'components':len(rows),'texturedComponents':textured,'normalMappedComponents':normalmapped,'rows':rows,'boundsMetres':scene.bounds.tolist()}

def export(out):
    out.mkdir(parents=True,exist_ok=True)
    levels=[]
    for level in ['MASTER','LOD0','LOD1','LOD2','LOD3']:
        scene=refine(level); report=verify(scene)
        stem=f'BalconyFaucetG13Hero_{level}'
        glb=out/(stem+'.glb'); glb.write_bytes(scene.export(file_type='glb'))
        reload=trimesh.load(glb,force='scene',process=False)
        assert sum(len(g.faces) for g in reload.geometry.values())==report['triangles']
        # Verify material textures survive GLB roundtrip on camera-near components.
        rt_mats=[getattr(g.visual,'material',None) for g in reload.geometry.values()]
        assert sum(getattr(m,'normalTexture',None) is not None for m in rt_mats)>=report['normalMappedComponents']-2
        folder=out/stem;folder.mkdir(exist_ok=True)
        obj,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True)
        (folder/(stem+'.obj')).write_text(obj)
        for fn,data in files.items():
            p=folder/fn;p.write_text(data) if isinstance(data,str) else p.write_bytes(data)
        levels.append({'level':level,'sha256':hashlib.sha256(glb.read_bytes()).hexdigest(),**report,'glbRoundtrip':True})
        print(level,report['triangles'],report['components'],report['normalMappedComponents'])
    counts=[x['triangles'] for x in levels]
    assert all(a>b for a,b in zip(counts,counts[1:])),counts
    result={'asset':'BalconyFaucetG13','variant':'hero_refinement_reusing_generate_faucet_hose_owner','levels':levels,'visualFidelityScore':None,'unityRender':False,'unityImport':False,'implementationClaims':'external geometry and glTF material implementation only'}
    (out/'hero_faucet_verification.json').write_text(json.dumps(result,indent=2))
    return result

if __name__=='__main__':
    ap=argparse.ArgumentParser(); ap.add_argument('--output',type=Path,required=True); ap.add_argument('--owner',type=Path)
    args=ap.parse_args()
    if args.owner:
        globals()['OWNER']=args.owner.resolve()
    export(args.output)
