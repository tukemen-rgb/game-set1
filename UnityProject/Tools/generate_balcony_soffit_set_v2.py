from __future__ import annotations
from pathlib import Path
import argparse, json, math, hashlib
import numpy as np
import trimesh
from PIL import Image

LEVELS = {
    'MASTER': {'radial':24,'fastener_step':0.30,'joint_detail':True,'bevel':0.0025},
    'LOD0':   {'radial':16,'fastener_step':0.45,'joint_detail':True,'bevel':0.0018},
    'LOD1':   {'radial':10,'fastener_step':0.90,'joint_detail':False,'bevel':0.0010},
    'LOD2':   {'radial':8, 'fastener_step':1.80,'joint_detail':False,'bevel':0.0},
    'LOD3':   {'radial':6, 'fastener_step':9.99,'joint_detail':False,'bevel':0.0},
}

MATERIALS = {
    'PaintedFiberCement': {'base':[0.70,0.69,0.64],'metallic':0.0,'roughness':0.78,'normalScale':0.30,'micro':'fine painted cement-board tooth','wetness':0.0,'uv':'slight generic chalking proxy only','fresnel':'dielectric F0~0.04'},
    'JointSealant': {'base':[0.20,0.20,0.19],'metallic':0.0,'roughness':0.70,'normalScale':0.12,'micro':'tooled elastomer skin','wetness':0.0,'uv':'none baked','fresnel':'dielectric F0~0.04'},
    'ClosedCellFoam': {'base':[0.08,0.08,0.075],'metallic':0.0,'roughness':0.88,'normalScale':0.35,'micro':'closed-cell foam proxy','wetness':0.0,'uv':'hidden installation layer','fresnel':'dielectric F0~0.04'},
    'PowderCoatedAluminum': {'base':[0.36,0.37,0.36],'metallic':0.0,'roughness':0.44,'normalScale':0.14,'micro':'fine powder-coat orange peel','wetness':0.0,'uv':'subtle top-side aging only','fresnel':'dielectric coating over aluminum'},
    'StainlessFastener': {'base':[0.56,0.58,0.59],'metallic':1.0,'roughness':0.37,'normalScale':0.08,'micro':'machined stainless','wetness':0.0,'uv':'none baked','fresnel':'conductor'},
    'EPDM': {'base':[0.025,0.026,0.025],'metallic':0.0,'roughness':0.78,'normalScale':0.24,'micro':'fine rubber grain','wetness':0.0,'uv':'minor compression represented in geometry','fresnel':'dielectric F0~0.04'},
    'DarkCavity': {'base':[0.012,0.012,0.012],'metallic':0.0,'roughness':0.92,'normalScale':0.0,'micro':'actual recessed shadow cavity','wetness':0.0,'uv':'n/a','fresnel':'dielectric'}
}

ASSETS = ['PaintedFiberCementSoffit3600x1200','BalconySoffitAccessHatch450','SoffitDripEdgeFlashing3600']

def box(extents, center, name, material):
    m=trimesh.creation.box(extents=np.asarray(extents,float)); m.apply_translation(center); m.metadata.update(name=name,material=material); return m

def cyl(radius,height,center,axis,sections,name,material):
    m=trimesh.creation.cylinder(radius=radius,height=height,sections=sections)
    axis=np.asarray(axis,float); axis/=np.linalg.norm(axis)
    if not np.allclose(axis,[0,0,1]): m.apply_transform(trimesh.geometry.align_vectors([0,0,1],axis))
    m.apply_translation(center); m.metadata.update(name=name,material=material); return m

def annulus(outer_r,inner_r,height,center,axis,sections,name,material):
    th=np.arange(sections)*2*np.pi/sections; verts=[]
    for z in (-height/2,height/2):
        for r in (outer_r,inner_r):
            for a in th: verts.append([r*np.cos(a),r*np.sin(a),z])
    faces=[]
    def ix(zi,ri,j): return zi*2*sections+ri*sections+(j%sections)
    for j in range(sections):
        k=j+1
        faces += [[ix(0,0,j),ix(0,0,k),ix(1,0,k)],[ix(0,0,j),ix(1,0,k),ix(1,0,j)]]
        faces += [[ix(0,1,j),ix(1,1,k),ix(0,1,k)],[ix(0,1,j),ix(1,1,j),ix(1,1,k)]]
        faces += [[ix(1,0,j),ix(1,0,k),ix(1,1,k)],[ix(1,0,j),ix(1,1,k),ix(1,1,j)]]
        faces += [[ix(0,0,j),ix(0,1,k),ix(0,0,k)],[ix(0,0,j),ix(0,1,j),ix(0,1,k)]]
    m=trimesh.Trimesh(vertices=np.asarray(verts),faces=np.asarray(faces),process=False);m.fix_normals()
    axis=np.asarray(axis,float);axis/=np.linalg.norm(axis)
    if not np.allclose(axis,[0,0,1]):m.apply_transform(trimesh.geometry.align_vectors([0,0,1],axis))
    m.apply_translation(center);m.metadata.update(name=name,material=material);return m

def make_textures(out):
    out.mkdir(parents=True,exist_ok=True);rng=np.random.default_rng(26091641);res={};n=256;yy,xx=np.mgrid[:n,:n]
    for name,p in MATERIALS.items():
        noise=rng.normal(0,1,(n,n))
        if name=='PaintedFiberCement': field=.65*noise+.35*np.sin(xx*.11)*np.sin(yy*.09); amp=.018
        elif name=='PowderCoatedAluminum': field=.75*noise+.25*np.sin(xx*.21+yy*.17); amp=.012
        elif name=='ClosedCellFoam': field=noise; amp=.025
        elif name=='EPDM': field=noise; amp=.016
        else: field=noise; amp=.009
        base=np.clip(np.array(p['base'])[None,None,:]*(1+amp*field[:,:,None]),0,1)
        mr=np.zeros((n,n,3),dtype=np.uint8);mr[:,:,1]=np.uint8(np.clip(p['roughness']+amp*field,0,1)*255);mr[:,:,2]=np.uint8(p['metallic']*255)
        gx=np.gradient(field,axis=1);gy=np.gradient(field,axis=0);N=np.dstack([-gx*p['normalScale'],-gy*p['normalScale'],np.ones_like(gx)]);N/=np.linalg.norm(N,axis=2)[:,:,None];N=np.uint8(np.clip(N*.5+.5,0,1)*255)
        bc=Image.fromarray(np.uint8(base*255));mi=Image.fromarray(mr);ni=Image.fromarray(N)
        bc.save(out/f'{name}_base.png');mi.save(out/f'{name}_mr.png');ni.save(out/f'{name}_normal.png');res[name]=(bc,mi,ni)
    return res

def uv_for(g,scale):
    v=g.vertices; span=np.ptp(v,axis=0); drop=int(np.argmin(span)); axes=[i for i in range(3) if i!=drop]; return v[:,axes]/scale

def compact(parts,textures):
    groups={}
    for p in parts:groups.setdefault(p.metadata['material'],[]).append(p)
    scene=trimesh.Scene()
    for mat,arr in groups.items():
        V=[];F=[];N=[];UV=[];off=0
        for g in arr:
            V.append(g.vertices);F.append(g.faces+off);N.append(g.vertex_normals);UV.append(uv_for(g,.20 if mat=='PaintedFiberCement' else .08));off+=len(g.vertices)
        m=trimesh.Trimesh(vertices=np.vstack(V),faces=np.vstack(F),vertex_normals=np.vstack(N),process=False)
        bc,mr,nm=textures[mat];pbr=trimesh.visual.material.PBRMaterial(name=mat,baseColorTexture=bc,metallicRoughnessTexture=mr,normalTexture=nm,metallicFactor=1.0,roughnessFactor=1.0,doubleSided=False)
        m.visual=trimesh.visual.TextureVisuals(uv=np.vstack(UV),material=pbr);scene.add_geometry(m,node_name=mat,geom_name=mat)
    return scene

def fasteners_along_x(parts, y, z, width, step, radial, prefix):
    xs=np.arange(-width/2+0.12,width/2-0.12+1e-9,step)
    for i,x in enumerate(xs):
        parts.append(cyl(.0048,.0028,[float(x),y,z],[0,1,0],radial,f'{prefix}_Head_{i:02d}','StainlessFastener'))
        parts.append(cyl(.0021,.010,[float(x),y+.005,z],[0,1,0],radial,f'{prefix}_Shank_{i:02d}','StainlessFastener'))

def soffit_parts(level):
    d=LEVELS[level];parts=[];W=3.6;D=1.2;t=.008;gap=.006;panel=(W-3*gap)/4
    for i in range(4):
        x=-W/2 + panel/2 + i*(panel+gap)
        parts.append(box([panel,t,D],[x,t/2,0],f'Panel_{i}','PaintedFiberCement'))
        if d['joint_detail'] and i<3:
            jx=-W/2+(i+1)*panel+i*gap+gap/2
            parts.append(box([gap*.80,.003,D-.030],[jx,-.0015,0],f'JointSeal_{i}','JointSealant'))
            parts.append(cyl(gap*.42,D-.050,[jx,.004,0],[0,0,1],d['radial'],f'BackerRod_{i}','ClosedCellFoam'))
    parts.append(box([W,.004,.010],[0,-.002,-D/2+.005],'WallPerimeterSeal','JointSealant'))
    parts.append(box([W,.006,.012],[0,-.003,D/2-.006],'OuterShadowReveal','DarkCavity'))
    if level in ('MASTER','LOD0'):
        for z in (-.45,0,.45):parts.append(box([W-.08,.018,.030],[0,.017,z],f'Furring_{z:+.2f}','PowderCoatedAluminum'))
    if level!='LOD3':
        fasteners_along_x(parts,-.001,D/2-.085,W,d['fastener_step'],d['radial'],'OuterFastener')
        fasteners_along_x(parts,-.001,-D/2+.085,W,d['fastener_step'],d['radial'],'WallFastener')
    return parts

def hatch_parts(level):
    d=LEVELS[level];parts=[];S=.45;rail=.030;depth=.022
    frame=[
        box([S,depth,rail],[0,0,S/2-rail/2],'HatchFrameTop','PowderCoatedAluminum'),
        box([S,depth,rail],[0,0,-S/2+rail/2],'HatchFrameBottom','PowderCoatedAluminum'),
        box([rail,depth,S-2*rail],[-S/2+rail/2,0,0],'HatchFrameLeft','PowderCoatedAluminum'),
        box([rail,depth,S-2*rail],[S/2-rail/2,0,0],'HatchFrameRight','PowderCoatedAluminum'),
        box([S-.070,.008,S-.070],[0,.004,0],'HatchLeaf','PaintedFiberCement'),
    ]
    if level=='LOD3': return frame
    parts += frame + [
        box([S-.050,.006,.007],[0,.010,S/2-.034],'GasketTop','EPDM'),
        box([S-.050,.006,.007],[0,.010,-S/2+.034],'GasketBottom','EPDM'),
        box([.007,.006,S-.064],[-S/2+.034,.010,0],'GasketLeft','EPDM'),
        box([.007,.006,S-.064],[S/2-.034,.010,0],'GasketRight','EPDM'),
    ]
    parts.append(box([.055,.013,.018],[0,-.006,-.145],'FingerCupCavity','DarkCavity'))
    if level in ('MASTER','LOD0','LOD1'):
        parts.append(annulus(.017,.011,.003,[0,-.015,-.145],[0,1,0],d['radial'],'FingerCupRim','PowderCoatedAluminum'))
    corners=[(-.195,-.195),(.195,-.195),(.195,.195),(-.195,.195)]
    use=corners if level in ('MASTER','LOD0') else corners[::2] if level=='LOD1' else []
    for i,(x,z) in enumerate(use):parts.append(cyl(.0045,.003,[x,-.013,z],[0,1,0],d['radial'],f'HatchFastener_{i}','StainlessFastener'))
    if level in ('MASTER','LOD0'):
        for x in (-.12,.12):
            parts.append(cyl(.006,.075,[x,.014,.225],[1,0,0],d['radial'],'HingeKnuckle','PowderCoatedAluminum'))
            parts.append(cyl(.0025,.082,[x,.014,.225],[1,0,0],d['radial'],'HingePin','StainlessFastener'))
    return parts

def flashing_parts(level):
    d=LEVELS[level];parts=[];W=3.6
    parts += [box([W,.002,.055],[0,.001,-.0275],'MountingFlange','PowderCoatedAluminum'),box([W,.055,.002],[0,-.0265,-.056],'VerticalDrop','PowderCoatedAluminum'),box([W,.002,.028],[0,-.054,-.069],'DripKick','PowderCoatedAluminum')]
    if level != 'LOD3':parts.append(box([W,.008,.006],[0,-.050,-.084],'HemmedEdge','PowderCoatedAluminum'))
    if level in ('MASTER','LOD0','LOD1'):
        xs=np.arange(-W/2+.15,W/2-.15+1e-9,d['fastener_step'])
        for i,x in enumerate(xs):
            parts.append(cyl(.004,.0025,[float(x),.004,-.025],[0,1,0],d['radial'],f'FlashingFastener_{i}','StainlessFastener'))
            parts.append(annulus(.0065,.0042,.0012,[float(x),.002,-.025],[0,1,0],d['radial'],f'FlashingWasher_{i}','EPDM'))
    if level in ('MASTER','LOD0'):
        parts.append(box([.014,.060,.060],[-W/2+.007,-.025,-.050],'EndDamL','PowderCoatedAluminum'))
        parts.append(box([.014,.060,.060],[W/2-.007,-.025,-.050],'EndDamR','PowderCoatedAluminum'))
    return parts

BUILDERS={'PaintedFiberCementSoffit3600x1200':soffit_parts,'BalconySoffitAccessHatch450':hatch_parts,'SoffitDripEdgeFlashing3600':flashing_parts}

def validate(parts):
    rows=[]
    for p in parts:
        assert np.isfinite(p.vertices).all(),p.metadata['name'];assert np.isfinite(p.vertex_normals).all(),p.metadata['name'];assert np.all(p.area_faces>1e-14),p.metadata['name']
        q=p.copy();q.merge_vertices(digits_vertex=9);assert q.is_watertight,p.metadata['name'];assert q.is_winding_consistent,p.metadata['name'];assert q.volume>0,(p.metadata['name'],q.volume)
        _,counts=np.unique(q.edges_sorted,axis=0,return_counts=True);assert np.all(counts==2),p.metadata['name']
        rows.append({'part':p.metadata['name'],'material':p.metadata['material'],'triangles':int(len(p.faces)),'volumeM3':float(q.volume)})
    return rows

def export_asset(asset,level,parts,out,textures):
    rows=validate(parts);scene=compact(parts,textures);ad=out/asset;ad.mkdir(parents=True,exist_ok=True);glb=ad/f'{asset}_{level}.glb';glb.write_bytes(scene.export(file_type='glb'))
    expected=sum(r['triangles'] for r in rows);rd=trimesh.load(glb,force='scene',process=False);assert sum(len(g.faces) for g in rd.geometry.values())==expected;assert np.allclose(rd.bounds,scene.bounds,atol=1e-6)
    od=ad/f'{asset}_{level}_OBJ';od.mkdir(exist_ok=True);obj,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True);(od/f'{asset}_{level}.obj').write_text(obj)
    for k,v in files.items():
        p=od/k;p.write_text(v) if isinstance(v,str) else p.write_bytes(v)
    back=trimesh.load(od/f'{asset}_{level}.obj',force='scene',process=False);assert sum(len(g.faces) for g in back.geometry.values())==expected
    return {'asset':asset,'level':level,'triangles':expected,'logicalParts':len(parts),'runtimeMaterialMeshes':len(scene.geometry),'boundsMetres':scene.bounds.tolist(),'glbRoundtrip':True,'objTriangleRoundtrip':True,'componentValidation':rows,'glbSHA256':hashlib.sha256(glb.read_bytes()).hexdigest()}

def review(out,textures):
    scene=trimesh.Scene();base=compact(soffit_parts('LOD0'),textures)
    for n,g in base.geometry.items():scene.add_geometry(g.copy(),node_name='Soffit_'+n,geom_name='Soffit_'+n)
    hatch=compact(hatch_parts('LOD0'),textures)
    for n,g in hatch.geometry.items():
        q=g.copy();q.apply_translation([0,-.014,.12]);scene.add_geometry(q,node_name='Hatch_'+n,geom_name='Hatch_'+n)
    flash=compact(flashing_parts('LOD0'),textures)
    for n,g in flash.geometry.items():
        q=g.copy();q.apply_translation([0,-.010,.60]);scene.add_geometry(q,node_name='Flashing_'+n,geom_name='Flashing_'+n)
    p=out/'BalconySoffitInstalledReview_LOD0.glb';p.write_bytes(scene.export(file_type='glb'))
    return {'path':p.name,'triangles':sum(len(g.faces) for g in scene.geometry.values()),'geometryCount':len(scene.geometry),'formalBenchmarkSceneChanged':False,'unityVerified':False}

def run(out):
    out.mkdir(parents=True,exist_ok=True);textures=make_textures(out/'textures');records=[]
    for asset in ASSETS:
        counts=[]
        for level in LEVELS:
            r=export_asset(asset,level,BUILDERS[asset](level),out,textures);records.append(r);counts.append(r['triangles']);print(asset,level,r['triangles'],flush=True)
        assert all(a>b for a,b in zip(counts,counts[1:])),(asset,counts)
    rv=review(out,textures)
    report={'status':'EXTERNAL_GEOMETRY_VERIFIED_NO_UNITY_RUNTIME','records':records,'review':rv,'materials':MATERIALS,'visualFidelity':{'authority':'Assets/QA/visual_fidelity_gate.json','score':None,'pass':False,'pointsAwarded':0,'reason':'No actual Unity 3840x2160 pixels.'},'implementationReadiness':{'lastRecorded':93,'recomputed':False},'unityCompile':False,'unityImport':False,'unityRender':False,'lodTemporalVerified':False,'assumptions':['Nominal 3.6 x 1.2 m painted fiber-cement balcony soffit assembled from four panels; modern generic dimensional reference, not a specific historical product.','450 mm access hatch is a generic maintainable service opening assumption; this repair changes orientation only and does not assert a historical SKU.','Drip edge is a generic formed powder-coated-aluminum detail with hem and fasteners. No structural capacity is claimed.','Lighting, reflections, final aging, actual glass/window interaction and target-hardware performance are unverified.']}
    (out/'geometry_verification.json').write_text(json.dumps(report,indent=2)+'\n')
    meta={'schema':1,'date':'2026-09-16','units':'metres','owner':'BalconySoffitSet','manufactureInstallation':{'soffit':'Four 8 mm painted fiber-cement panels with 6 mm panel joints, near-LOD recessed sealant/backer rods, perimeter seal/reveal, concealed aluminum furring at MASTER/LOD0 and visible fastener heads.','accessHatch':'450 mm Y-up horizontal aluminum perimeter frame, separate fiber-cement leaf, EPDM gasket, recessed finger cup, near-LOD hinges/pins and corner fasteners. Geometry is authored directly in the ceiling plane; no XY-to-XZ integration rotation is required.','dripEdge':'Formed aluminum mounting flange, vertical drop, drip kick, hemmed edge, near-LOD end dams and mechanically fastened EPDM-isolated interface.','interfaces':'Soffit underside is y=0; hidden furring sits above boards; access hatch is an independent horizontal Y-up insert placed by translation only; drip flashing mounts along exterior +Z edge. No existing laundry-hardware geometry is duplicated.','orientationExposure':'Y up; exterior +Z. Exterior drip edge receives wind-driven rain/UV, wall-side seal is sheltered. Japanese midsummer lighting is not baked.','agingCausality':'No arbitrary streaks or mildew. Future wear should concentrate at exterior drip edge, fastener halos, joint tooling and hatch perimeter where condensation/dust can accumulate.','geometryVsMaterial':'Panel thickness, joints, rods, furring, fasteners, hatch frame/leaf/gasket/hinges/cavity and drip profile are geometry. Paint/cement tooth, powder-coat and rubber grain are texture proxies.'},'materials':MATERIALS,'triangles':{},'visualFidelity':{'authority':'Assets/QA/visual_fidelity_gate.json','status':'UNSCORED_UNTIL_REAL_4K_RENDER','score':None,'pass':False,'pointsAwarded':0},'unityCompile':False,'unityImport':False,'unityRender':False,'formalBenchmarkSceneChanged':False,'nextProductionTarget':'Integrate the horizontal hatch into the latest Drained hero and verify it from material-colored full and close views; then repair any remaining return-rail or picket contact issue before new prop batches.'}
    for asset in ASSETS:meta['triangles'][asset]={r['level']:r['triangles'] for r in records if r['asset']==asset}
    (out/'BalconySoffitSet.metadata.json').write_text(json.dumps(meta,indent=2)+'\n')
    return report,meta

if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);args=ap.parse_args();run(args.output)
