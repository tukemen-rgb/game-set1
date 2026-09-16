from pathlib import Path
import argparse, json, math, hashlib
import numpy as np
import trimesh
from PIL import Image
from scipy.spatial import ConvexHull

LEVELS={
 'MASTER':dict(radial=24,bevel=True,anchors=4,welds=True,picket_step=1),
 'LOD0':dict(radial=16,bevel=True,anchors=4,welds=True,picket_step=1),
 'LOD1':dict(radial=10,bevel=True,anchors=2,welds=False,picket_step=1),
 'LOD2':dict(radial=8,bevel=False,anchors=0,welds=False,picket_step=1),
 'LOD3':dict(radial=6,bevel=False,anchors=0,welds=False,picket_step=2),
}
MATERIALS={
 'PowderCoatedSteel':dict(base=[.18,.19,.18],metallic=0.,roughness=.42,normalScale=.16,microstructure='polyester powder-coat orange-peel proxy',wetness=0.,uvAging='none baked; top-face chalking future',fresnel='dielectric F0~0.04'),
 'GalvanizedSteel':dict(base=[.52,.54,.55],metallic=1.,roughness=.48,normalScale=.17,microstructure='fine zinc-spangle proxy',wetness=0.,uvAging='none baked',fresnel='conductor'),
 'EPDM':dict(base=[.025,.026,.025],metallic=0.,roughness=.78,normalScale=.28,microstructure='fine rubber grain proxy',wetness=0.,uvAging='none baked',fresnel='dielectric F0~0.04'),
 'WeatheredConcrete':dict(base=[.53,.52,.48],metallic=0.,roughness=.88,normalScale=.42,microstructure='cement paste + sparse aggregate proxy',wetness=0.,uvAging='none baked',fresnel='dielectric F0~0.04'),
 'JointSealant':dict(base=[.20,.20,.19],metallic=0.,roughness=.72,normalScale=.18,microstructure='tooled elastomer skin proxy',wetness=0.,uvAging='slight chalking proxy only',fresnel='dielectric F0~0.04')
}
ASSETS=['PowderCoatedBalconyGuardrail3600x1100','BalconyGuardrailCornerReturn1200x1100','ConcreteBalconyKerb3600x180x120','ConcreteBalconyKerbReturn1200x180x120']

def box(ext,c,name,mat):
 m=trimesh.creation.box(extents=np.asarray(ext,float));m.apply_translation(c);m.metadata.update(name=name,material=mat);return m

def chamfer(ext,c,b,name,mat):
 h=np.asarray(ext,float)/2;b=min(float(b),*(h*.45));pts=[]
 for sx in (-1,1):
  for sy in (-1,1):
   for sz in (-1,1):
    pts += [[sx*(h[0]-b),sy*h[1],sz*h[2]],[sx*h[0],sy*(h[1]-b),sz*h[2]],[sx*h[0],sy*h[1],sz*(h[2]-b)]]
 pts=np.asarray(pts,float);hu=ConvexHull(pts);m=trimesh.Trimesh(vertices=pts,faces=hu.simplices,process=False);m.fix_normals();m.apply_translation(c);m.metadata.update(name=name,material=mat);return m

def beam(ext,c,d,name,mat,b=.002): return chamfer(ext,c,b,name,mat) if d['bevel'] else box(ext,c,name,mat)

def cyl(r,h,c,axis,n,name,mat):
 m=trimesh.creation.cylinder(radius=r,height=h,sections=n);axis=np.asarray(axis,float);axis/=np.linalg.norm(axis)
 if not np.allclose(axis,[0,0,1]):m.apply_transform(trimesh.geometry.align_vectors([0,0,1],axis))
 m.apply_translation(c);m.metadata.update(name=name,material=mat);return m

def hexp(af,h,c,axis,name,mat):return cyl(af/math.sqrt(3),h,c,axis,6,name,mat)

def washer(ro,ri,h,c,axis,n,name,mat):
 th=np.arange(n)*2*np.pi/n;v=[]
 for z in (-h/2,h/2):
  for r in (ro,ri):
   for a in th:v.append([r*np.cos(a),r*np.sin(a),z])
 f=[]
 def I(zi,ri0,j):return zi*2*n+ri0*n+j%n
 for j in range(n):
  k=(j+1)%n;f += [[I(0,0,j),I(0,0,k),I(1,0,k)],[I(0,0,j),I(1,0,k),I(1,0,j)],[I(0,1,j),I(1,1,k),I(0,1,k)],[I(0,1,j),I(1,1,j),I(1,1,k)],[I(1,0,j),I(1,0,k),I(1,1,k)],[I(1,0,j),I(1,1,k),I(1,1,j)],[I(0,0,j),I(0,1,k),I(0,0,k)],[I(0,0,j),I(0,1,j),I(0,1,k)]]
 m=trimesh.Trimesh(vertices=np.asarray(v),faces=np.asarray(f),process=False);m.fix_normals();axis=np.asarray(axis,float);axis/=np.linalg.norm(axis)
 if not np.allclose(axis,[0,0,1]):m.apply_transform(trimesh.geometry.align_vectors([0,0,1],axis))
 m.apply_translation(c);m.metadata.update(name=name,material=mat);return m

def wedge(L,W,hi,ho,c,name,mat):
 y0=-max(hi,ho)/2;v=np.array([[-L/2,y0,-W/2],[L/2,y0,-W/2],[L/2,y0,W/2],[-L/2,y0,W/2],[-L/2,y0+hi,-W/2],[L/2,y0+hi,-W/2],[L/2,y0+ho,W/2],[-L/2,y0+ho,W/2]],float)
 f=np.array([[0,2,1],[0,3,2],[4,5,6],[4,6,7],[0,1,5],[0,5,4],[3,7,6],[3,6,2],[0,4,7],[0,7,3],[1,2,6],[1,6,5]])
 m=trimesh.Trimesh(vertices=v,faces=f,process=False);m.fix_normals();m.apply_translation(c);m.metadata.update(name=name,material=mat);return m

def anchor(parts,x,z,y,d,p):
 if d['anchors']<=0:return
 parts += [washer(.011,.0048,.0022,[x,y,z],[0,1,0],d['radial'],p+'_washer','GalvanizedSteel'),hexp(.014,.006,[x,y+.004,z],[0,1,0],p+'_nut','GalvanizedSteel')]
 if d['anchors']>=4:parts.append(cyl(.0042,.012,[x,y-.004,z],[0,1,0],d['radial'],p+'_stud','GalvanizedSteel'))

def main_parts(level):
 d=LEVELS[level];W=3.6;parts=[beam([W,.052,.070],[0,1.10,0],d,'TopRail','PowderCoatedSteel',.004),beam([W-.08,.035,.035],[0,.16,0],d,'BottomRail','PowderCoatedSteel',.002)]
 for pi,x in enumerate(np.linspace(-W/2+.08,W/2-.08,5)):
  parts += [beam([.050,1.045,.050],[x,.555,0],d,f'Post{pi}','PowderCoatedSteel',.003),beam([.120,.009,.105],[x,.0045,0],d,f'BasePlate{pi}','PowderCoatedSteel',.002),box([.100,.003,.085],[x,-.002,0],f'Pad{pi}','EPDM')]
  if d['welds']:
   parts += [beam([.056,.006,.006],[x,.016,.028],d,f'WeldF{pi}','PowderCoatedSteel',.001),beam([.056,.006,.006],[x,.016,-.028],d,f'WeldB{pi}','PowderCoatedSteel',.001),beam([.006,.006,.056],[x+.028,.016,0],d,f'WeldR{pi}','PowderCoatedSteel',.001),beam([.006,.006,.056],[x-.028,.016,0],d,f'WeldL{pi}','PowderCoatedSteel',.001)]
  corners=[(-.043,-.036),(.043,-.036),(.043,.036),(-.043,.036)] if d['anchors']>=4 else [(-.043,-.036),(.043,.036)]
  for ai,(dx,dz) in enumerate(corners):anchor(parts,x+dx,dz,.014,d,f'A{pi}_{ai}')
 px=np.arange(-W/2+.145,W/2-.145+1e-9,.118)[::d['picket_step']]
 for i,x in enumerate(px):parts.append(beam([.018,.8965,.018],[x,.62575,0],d,f'Picket{i:02d}','PowderCoatedSteel',.0013))
 if level in ('MASTER','LOD0','LOD1'):
  parts += [beam([.006,.046,.064],[-W/2-.003,1.10,0],d,'EndCapL','PowderCoatedSteel',.001),beam([.006,.046,.064],[W/2+.003,1.10,0],d,'EndCapR','PowderCoatedSteel',.001)]
 return parts

def corner_parts(level):
 d=LEVELS[level];parts=[];L=1.2
 def zb(ext,c,n,m,b):
  q=beam(ext,[0,0,0],d,n,m,b);q.apply_transform(trimesh.transformations.rotation_matrix(np.pi/2,[0,1,0]));q.apply_translation(c);return q
 parts += [zb([L,.052,.070],[0,1.10,.60],'CornerTop','PowderCoatedSteel',.004),zb([L-.06,.035,.035],[0,.16,.60],'CornerBottom','PowderCoatedSteel',.002)]
 for pi,z in enumerate([.08,.60,1.12]):
  parts += [beam([.050,1.045,.050],[0,.555,z],d,f'CPost{pi}','PowderCoatedSteel',.003),beam([.105,.009,.120],[0,.0045,z],d,f'CBase{pi}','PowderCoatedSteel',.002),box([.085,.003,.100],[0,-.002,z],f'CPad{pi}','EPDM')]
  if d['welds']:parts += [beam([.056,.006,.006],[0,.016,z+.028],d,f'CWeldF{pi}','PowderCoatedSteel',.001),beam([.056,.006,.006],[0,.016,z-.028],d,f'CWeldB{pi}','PowderCoatedSteel',.001)]
  pts=[(-.036,-.043),(.036,-.043),(.036,.043),(-.036,.043)] if d['anchors']>=4 else [(-.036,-.043),(.036,.043)]
  for ai,(dx,dz) in enumerate(pts):anchor(parts,dx,z+dz,.014,d,f'CA{pi}_{ai}')
 pz=np.arange(.145,1.055+1e-9,.118)[::d['picket_step']]
 for i,z in enumerate(pz):parts.append(beam([.018,.8965,.018],[0,.62575,float(z)],d,f'CPicket{i:02d}','PowderCoatedSteel',.0013))
 if level in ('MASTER','LOD0','LOD1'):parts.append(beam([.074,.058,.074],[0,1.10,.018],d,'MiterCover','PowderCoatedSteel',.004))
 return parts

def kerb_parts(level):
 d=LEVELS[level];W=3.6;gap=.010;seg=(W-3*gap)/4;parts=[]
 for i in range(4):
  x0=-W/2+i*(seg+gap);cx=x0+seg/2;parts += [wedge(seg,.120,.180,.176,[cx,.090,0],f'Concrete{i}','WeatheredConcrete'),box([seg,.016,.010],[cx,.020,.065],f'DripNib{i}','WeatheredConcrete')]
  if i<3:
   jx=x0+seg+gap/2;parts += [box([gap*.75,.155,.085],[jx,.0775,-.014],f'Joint{i}','JointSealant'),cyl(.006,.078,[jx,.078,.020],[0,1,0],max(6,d['radial']),f'Backer{i}','EPDM')]
 if level in ('MASTER','LOD0','LOD1'):parts.append(beam([W,.018,.012],[0,.185,-.054],d,'InnerWaterStop','WeatheredConcrete',.0015))
 return parts

def return_kerb_parts(level):
 d=LEVELS[level];L=1.2;gap=.010;seg=(L-gap)/2;parts=[]
 for i in range(2):
  z0=i*(seg+gap);cz=z0+seg/2
  q=wedge(seg,.120,.180,.176,[0,.090,0],f'ReturnConcrete{i}','WeatheredConcrete')
  q.apply_transform(trimesh.transformations.rotation_matrix(np.pi/2,[0,1,0]));q.apply_translation([0,0,cz]);parts.append(q)
  parts.append(box([.010,.016,seg],[.065,.020,cz],f'ReturnDripNib{i}','WeatheredConcrete'))
 if level!='LOD3':
  jz=seg+gap/2
  parts += [box([.085,.155,gap*.75],[-.014,.0775,jz],'ReturnJoint','JointSealant'),cyl(.006,.078,[.020,.078,jz],[0,1,0],max(6,d['radial']),'ReturnBacker','EPDM')]
 if level in ('MASTER','LOD0','LOD1'):parts.append(beam([.012,.018,L],[-.054,.185,L/2],d,'ReturnInnerWaterStop','WeatheredConcrete',.0015))
 return parts

BUILD={'PowderCoatedBalconyGuardrail3600x1100':main_parts,'BalconyGuardrailCornerReturn1200x1100':corner_parts,'ConcreteBalconyKerb3600x180x120':kerb_parts,'ConcreteBalconyKerbReturn1200x180x120':return_kerb_parts}

def textures(out):
 out.mkdir(parents=True,exist_ok=True);rng=np.random.default_rng(16092637);res={};n=128;yy,xx=np.mgrid[:n,:n]
 for name,p in MATERIALS.items():
  noise=rng.normal(0,1,(n,n));field=noise; amp=.015
  if name=='WeatheredConcrete':field=.7*noise+.3*np.sin(xx*.08)*np.sin(yy*.06);amp=.03
  if name=='GalvanizedSteel':field=.5*noise+.25*np.sin(xx*.11)+.25*np.sin(yy*.13);amp=.02
  base=np.clip(np.array(p['base'])[None,None,:]*(1+amp*field[:,:,None]),0,1);mr=np.zeros((n,n,3),dtype=np.uint8);mr[:,:,1]=np.uint8(np.clip(p['roughness']+amp*field,0,1)*255);mr[:,:,2]=np.uint8(p['metallic']*255)
  gx=np.gradient(field,axis=1);gy=np.gradient(field,axis=0);N=np.dstack([-gx*p['normalScale'],-gy*p['normalScale'],np.ones_like(gx)]);N/=np.linalg.norm(N,axis=2)[:,:,None];N=np.uint8(np.clip(N*.5+.5,0,1)*255)
  bc=Image.fromarray(np.uint8(base*255));mri=Image.fromarray(mr);ni=Image.fromarray(N);bc.save(out/(name+'_base.png'));mri.save(out/(name+'_mr.png'));ni.save(out/(name+'_normal.png'));res[name]=(bc,mri,ni)
 return res

def uv(g,scale):
 v=g.vertices;span=np.ptp(v,axis=0);drop=int(np.argmin(span));axes=[i for i in range(3) if i!=drop];return v[:,axes]/scale

def compact(parts,tex):
 groups={}
 for p in parts:groups.setdefault(p.metadata['material'],[]).append(p)
 scene=trimesh.Scene()
 for mat,arr in groups.items():
  vs=[];fs=[];ns=[];us=[];o=0
  for g in arr:vs.append(g.vertices);fs.append(g.faces+o);ns.append(g.vertex_normals);us.append(uv(g,.45 if mat=='WeatheredConcrete' else .12));o+=len(g.vertices)
  m=trimesh.Trimesh(vertices=np.vstack(vs),faces=np.vstack(fs),vertex_normals=np.vstack(ns),process=False);bc,mr,nm=tex[mat];m.visual=trimesh.visual.TextureVisuals(uv=np.vstack(us),material=trimesh.visual.material.PBRMaterial(name=mat,baseColorTexture=bc,metallicRoughnessTexture=mr,normalTexture=nm,metallicFactor=1,roughnessFactor=1));scene.add_geometry(m,node_name=mat,geom_name=mat)
 return scene

def validate(parts):
 rows=[]
 for p in parts:
  assert np.isfinite(p.vertices).all() and np.isfinite(p.vertex_normals).all();assert np.all(p.area_faces>1e-14);q=p.copy();q.merge_vertices(digits_vertex=9);assert q.is_watertight and q.is_winding_consistent and q.volume>0,(p.metadata['name'],q.volume);_,c=np.unique(q.edges_sorted,axis=0,return_counts=True);assert np.all(c==2),p.metadata['name'];rows.append(dict(part=p.metadata['name'],material=p.metadata['material'],triangles=int(len(p.faces)),volumeM3=float(q.volume)))
 return rows

def export_one(asset,level,out,tex):
 parts=BUILD[asset](level);rows=validate(parts);scene=compact(parts,tex);ad=out/asset;ad.mkdir(parents=True,exist_ok=True);glb=ad/f'{asset}_{level}.glb';glb.write_bytes(scene.export(file_type='glb'));expected=sum(r['triangles'] for r in rows);rd=trimesh.load(glb,force='scene',process=False);assert sum(len(g.faces) for g in rd.geometry.values())==expected and np.allclose(rd.bounds,scene.bounds,atol=1e-6)
 od=ad/f'{asset}_{level}_OBJ';od.mkdir(exist_ok=True);obj,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True);(od/f'{asset}_{level}.obj').write_text(obj)
 for k,v in files.items():p=od/k;p.write_text(v) if isinstance(v,str) else p.write_bytes(v)
 rr=trimesh.load(od/f'{asset}_{level}.obj',force='scene',process=False);assert sum(len(g.faces) for g in rr.geometry.values())==expected
 return dict(asset=asset,level=level,triangles=expected,logicalParts=len(parts),runtimeMaterialMeshes=len(scene.geometry),boundsMetres=scene.bounds.tolist(),glbRoundtrip=True,objTriangleRoundtrip=True,componentValidation=rows,glbSHA256=hashlib.sha256(glb.read_bytes()).hexdigest())

def review(out,tex):
 s=trimesh.Scene()
 for prefix,parts,t in [('Kerb',kerb_parts('LOD0'),[0,0,0]),('ReturnKerb',return_kerb_parts('LOD0'),[-1.80,0,0]),('Rail',main_parts('LOD0'),[0,.180,0]),('Return',corner_parts('LOD0'),[-1.80,.180,0])]:
  q=compact(parts,tex)
  for n,g in q.geometry.items():h=g.copy();h.apply_translation(t);s.add_geometry(h,node_name=prefix+'_'+n,geom_name=prefix+'_'+n)
 slab=compact([box([4.25,.12,1.55],[0,-.06,-.62],'Slab','WeatheredConcrete')],tex)
 for n,g in slab.geometry.items():s.add_geometry(g.copy(),node_name='DIAGNOSTIC_'+n,geom_name='DIAGNOSTIC_'+n)
 p=out/'BalconyGuardrailInstalledReview_LOD0.glb';p.write_bytes(s.export(file_type='glb'));return dict(path=p.name,triangles=sum(len(g.faces) for g in s.geometry.values()),geometryCount=len(s.geometry),formalBenchmarkSceneChanged=False,unityVerified=False)

def run(out):
 out.mkdir(parents=True,exist_ok=True);tex=textures(out/'textures');records=[]
 for a in ASSETS:
  counts=[]
  for l in LEVELS:r=export_one(a,l,out,tex);records.append(r);counts.append(r['triangles']);print(a,l,r['triangles'],flush=True)
  assert all(x>y for x,y in zip(counts,counts[1:])),(a,counts)
 rv=review(out,tex)
 rep=dict(status='EXTERNAL_GEOMETRY_VERIFIED_NO_UNITY_RUNTIME',records=records,review=rv,materials=MATERIALS,visualFidelity=dict(authority='Assets/QA/visual_fidelity_gate.json',score=None,pass_=False,pointsAwarded=0,canonicalExpectedImpactOnly=['geometry_construction','material_pbr','texture_microdetail','weathering_causality','period_authenticity'],criticalDefectsScored=False),implementationReadiness=dict(lastRecorded=93,recomputed=False),unityCompile=False,unityImport=False,unityRender=False,lodTemporalVerified=False,assumptions=['3.6m x 1.1m main guardrail, 1.2m return and 180mm curb are generic modern references, not a historical SKU identification.','Powder-coated galvanized steel, base plates, anchors and EPDM isolation are authored assumptions.','LOD3 alternates pickets and therefore requires Unity pop/shimmer review before benchmark use.','No arbitrary rain streaks, rust or mildew are baked. Picket-to-rail contact and return-kerb support are geometry corrections, not visual scoring evidence.'])
 (out/'geometry_verification.json').write_text(json.dumps(rep,indent=2).replace('"pass_"','"pass"')+'\n')
 meta=dict(schema=1,date='2026-09-16',units='metres',owner='BalconyGuardrailSet',manufactureInstallation=dict(guardrail='Closed rectangular galvanized-steel profiles with dielectric powder coat. Five welded posts, top/bottom rails, vertical pickets, base plates, EPDM isolation pads and mechanical anchors. MASTER/LOD0 include raised weld fillet strips; cut ends are capped.',cornerReturn='Independent 1.2m return module with matching profiles, three posts, miter cover, pickets, base plates and anchors. Pickets meet both rails exactly instead of floating below the top rail.',kerb='Four front curb segments plus a dedicated 1.2m side-return curb so the perpendicular return posts are grounded on real mineral substrate. Both use slight outward top fall, drip detail, movement joint seal/backer and near-LOD water-stop detail.',interfaces='Front and return base plates bear on their respective curb tops in review. Main and return pickets terminate at bottom-rail top and top-rail underside with no visible air gap. EPDM separates coated steel from mineral substrate. No structural capacity is claimed.',orientationExposure='Y up, exterior +Z. Top rail/curb receive strongest summer UV and rain. Lighting is not baked.',agingCausality='No random rust or streaking. Future wear should concentrate at top rail, fasteners, curb joints and post-base splash zone.',geometryVsMaterial='Rails, pickets, posts, caps, base plates, weld strips, washers/nuts/studs, curb segments, movement joints and backer rods are geometry. Powder orange-peel, zinc spangle, rubber grain and concrete microstructure are material proxies.'),materials=MATERIALS,triangles={},visualFidelity=dict(authority='Assets/QA/visual_fidelity_gate.json',status='UNSCORED_UNTIL_REAL_4K_RENDER',score=None,pass_=False,pointsAwarded=0),unityCompile=False,unityImport=False,unityRender=False,formalBenchmarkSceneChanged=False,nextProductionTarget='Integrate the repaired guardrail/return support into the latest Drained hero, render material-colored full and close views, then verify remaining contact defects without adding validator-only work.')
 for a in ASSETS:meta['triangles'][a]={r['level']:r['triangles'] for r in records if r['asset']==a}
 (out/'BalconyGuardrailSet.metadata.json').write_text(json.dumps(meta,indent=2).replace('"pass_"','"pass"')+'\n')
 inv=dict(schema=1,updated='2026-09-16',isInventoryDelta=True,ownerInspection=dict(recursiveBranchTreeKeywords=['railing','handrail','parapet'],matchingExistingOwnerFound=False,decision='Create one BalconyGuardrailSet owner; do not duplicate window, AC, laundry, cleaning or faucet systems.'),newAssets=[dict(id=a,masterTriangles=next(r['triangles'] for r in records if r['asset']==a and r['level']=='MASTER'),lodTriangles=[next(r['triangles'] for r in records if r['asset']==a and r['level']==l) for l in ('LOD0','LOD1','LOD2','LOD3')],actualOBJGLBExports=True,unityVerified=False) for a in ASSETS],reviewIntegrations=[rv],productionDeltaThisRun=dict(authoredAssetSets=3,highDetailMasters=3,runtimeLodSets=3),nextProductionTarget=meta['nextProductionTarget'])
 (out/'asset_inventory_delta.json').write_text(json.dumps(inv,indent=2)+'\n');return rep,meta,inv

if __name__=='__main__':
 ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);args=ap.parse_args();run(args.output)
