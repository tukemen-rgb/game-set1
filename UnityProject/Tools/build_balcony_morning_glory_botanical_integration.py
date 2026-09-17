from __future__ import annotations
from pathlib import Path
import argparse, json, math, hashlib
import numpy as np
import trimesh
from PIL import Image

LEVELS=['MASTER','LOD0','LOD1','LOD2','LOD3']
PLACEMENT_X=-1.45; PLACEMENT_Z=0.55
FLOOR_BASE_TOP=-1.2470;FLOOR_SLOPE=-0.015;FINISH_OFFSET=.0008;POT_PLANT_LIFT=.010
FLOOR_TILT_RAD=math.atan(.015)
PLANT_MATERIALS={
 'Bamboo':dict(base=[.36,.32,.16],rough=.60,normal=.18,micro='longitudinal fibre and node-ring microvariation'),
 'JuteTwine':dict(base=[.43,.33,.20],rough=.86,normal=.28,micro='twisted bast-fibre microvariation'),
 'LivingStem':dict(base=[.16,.34,.09],rough=.56,normal=.14,micro='fine longitudinal epidermis'),
 'LivingLeaf':dict(base=[.10,.28,.07],rough=.50,normal=.22,micro='matte cuticle and fine surface variation'),
 'LeafVein':dict(base=[.13,.31,.08],rough=.56,normal=.12,micro='raised vein material; geometry retained and reseated'),
 'FlowerBlue':dict(base=[.19,.19,.56],rough=.43,normal=.10,micro='thin velvety petal microvariation'),
 'FlowerThroat':dict(base=[.82,.78,.88],rough=.46,normal=.08,micro='pale throat microvariation without baked hotspot'),
 'DrySoil':dict(base=[.12,.075,.04],rough=.93,normal=.38,micro='fine granular potting-soil microvariation')}

def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def finished_floor_y(z):return FLOOR_BASE_TOP+FLOOR_SLOPE*z+FINISH_OFFSET

def make_maps(out,n=256):
 out.mkdir(parents=True,exist_ok=True);rng=np.random.default_rng(17092026);yy,xx=np.mgrid[:n,:n];rec={}
 for name,p in PLANT_MATERIALS.items():
  noise=rng.normal(0,1,(n,n))
  if name=='Bamboo': field=.45*noise+.55*np.sin(xx*.11+.25*np.sin(yy*.021));amp=.018
  elif name=='JuteTwine': field=.55*noise+.45*np.sin((xx+yy)*.17);amp=.022
  elif name in ('LivingStem','LivingLeaf','LeafVein'): field=.72*noise+.28*np.sin(xx*.051+yy*.043);amp=.014
  elif name.startswith('Flower'): field=.82*noise+.18*np.sin(xx*.063-yy*.037);amp=.010
  else: field=noise;amp=.035
  field=(field-field.mean())/(field.std()+1e-8);base=np.clip(np.asarray(p['base'])[None,None,:]*(1+amp*field[:,:,None]),0,1);rough=np.clip(p['rough']+amp*.6*field,.04,1)
  mr=np.zeros((n,n,3),np.uint8);mr[:,:,1]=np.uint8(np.round(rough*255));gx=np.gradient(field,axis=1);gy=np.gradient(field,axis=0);strength=p['normal']*.18
  N=np.dstack([-gx*strength,-gy*strength,np.ones_like(field)]);N/=np.linalg.norm(N,axis=2)[:,:,None]
  bp=out/f'{name}_base.png';rp=out/f'{name}_mr.png';npth=out/f'{name}_normal.png'
  Image.fromarray(np.uint8(np.round(base*255)),'RGB').save(bp);Image.fromarray(mr,'RGB').save(rp);Image.fromarray(np.uint8(np.clip((N*.5+.5)*255,0,255)),'RGB').save(npth)
  rec[name]={'meanAlbedoSRGB':base.mean((0,1)).tolist(),'meanRoughness':float(rough.mean()),'metallic':0.0,'encodedNormalAmplitude':strength,'microstructure':p['micro'],'wetness':0.0,'uvAging':'none baked','fresnel':'dielectric F0 approximately 0.04'}
 return rec

def maps(folder):
 return {n:(Image.open(folder/f'{n}_base.png').convert('RGBA'),Image.open(folder/f'{n}_mr.png').convert('RGB'),Image.open(folder/f'{n}_normal.png').convert('RGB')) for n in PLANT_MATERIALS}
def uv_world(g,name):
 v=np.asarray(g.vertices)
 if name in ('Bamboo','JuteTwine','LivingStem'):return np.column_stack([v[:,0]/.11,v[:,1]/.19])
 if name in ('LivingLeaf','LeafVein','DrySoil'):return np.column_stack([v[:,0]/.18,v[:,2]/.18])
 return np.column_stack([(v[:,0]+v[:,2])/.16,v[:,1]/.16])
def pbr_plant_scene(glb,mapset):
 src=trimesh.load(glb,force='scene',process=False);out=trimesh.Scene()
 for name,g in src.geometry.items():
  if name not in PLANT_MATERIALS:raise ValueError(name)
  q=g.copy();bc,mr,nm=mapset[name];mat=trimesh.visual.material.PBRMaterial(name=name,baseColorTexture=bc,metallicRoughnessTexture=mr,normalTexture=nm,baseColorFactor=[255]*4,metallicFactor=1.,roughnessFactor=1.,doubleSided=False)
  q.visual=trimesh.visual.TextureVisuals(uv=uv_world(q,name),material=mat);out.add_geometry(q,node_name=name,geom_name=name)
 return out
def add_scene(dst,src,prefix,T=None):
 T=np.eye(4) if T is None else np.asarray(T,float)
 for node in src.graph.nodes_geometry:
  local,gn=src.graph[node];dst.add_geometry(src.geometry[gn].copy(),node_name=f'{prefix}_{node}',geom_name=f'{prefix}_{gn}',transform=T@local)
def transform():
 R=trimesh.transformations.rotation_matrix(FLOOR_TILT_RAD,[1,0,0]);R[0,3]=PLACEMENT_X;R[1,3]=finished_floor_y(PLACEMENT_Z);R[2,3]=PLACEMENT_Z;return R
def validate(scene):
 tri=sum(len(g.faces) for g in scene.geometry.values());bad=[]
 for n,g in scene.geometry.items():
  if not np.isfinite(g.vertices).all() or not np.isfinite(g.vertex_normals).all():bad.append((n,'finite'))
  if np.any(g.area_faces<1e-13):bad.append((n,'degenerate'))
  if not g.is_winding_consistent:bad.append((n,'winding'))
 if bad:raise AssertionError(bad[:20])
 return tri

def strip_old_planter(old):
 base=trimesh.Scene();removed=0;removed_tri=0
 for node in old.graph.nodes_geometry:
  T,gn=old.graph[node];g=old.geometry[gn]
  if node.startswith('BalconyMorningGloryPlanter_') or gn.startswith('BalconyMorningGloryPlanter_'):
   removed+=1;removed_tri+=len(g.faces);continue
  base.add_geometry(g.copy(),node_name=node,geom_name=gn,transform=T)
 return base,removed,removed_tri

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--refined',type=Path,required=True);ap.add_argument('--garden',type=Path,required=True);ap.add_argument('--old-integrated',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);a=ap.parse_args();a.output.mkdir(parents=True,exist_ok=True)
 matrec=make_maps(a.output/'textures');mapset=maps(a.output/'textures');T=transform();planter_rows=[];hero_rows=[]
 for L in LEVELS:
  plant=pbr_plant_scene(a.refined/f'MorningGloryTrellis_{L}_botanical.glb',mapset);pot=trimesh.load(a.garden/f'TerracottaPot240_{L}.glb',force='scene',process=False);saucer=trimesh.load(a.garden/f'TerracottaSaucer214_{L}.glb',force='scene',process=False)
  planter=trimesh.Scene();add_scene(planter,saucer,'Saucer');lift=np.eye(4);lift[1,3]=POT_PLANT_LIFT;add_scene(planter,pot,'Pot',lift);add_scene(planter,plant,'Plant',lift);pt=validate(planter)
  pd=a.output/'planter';pd.mkdir(exist_ok=True);gp=pd/f'BalconyMorningGloryBotanicalPlanter_{L}.glb';gp.write_bytes(planter.export(file_type='glb'));rr=trimesh.load(gp,force='scene',process=False);assert validate(rr)==pt
  planter_rows.append({'level':L,'triangles':pt,'boundsLocalMetres':planter.bounds.tolist(),'glbSHA256':sha(gp),'glbRoundtrip':True,'sourceBotanicalOBJAvailable':True})
  old=trimesh.load(a.old_integrated/f'BalconyExteriorHeroSummerMorningGlory3600_{L}.glb',force='scene',process=False);oldtri=validate(old);base,removed,removedtri=strip_old_planter(old);basetri=validate(base)
  if basetri+removedtri!=oldtri:raise AssertionError((L,basetri,removedtri,oldtri))
  new=trimesh.Scene();add_scene(new,base,'BaseHero');add_scene(new,planter,'BalconyMorningGloryBotanicalPlanter',T);nt=validate(new);assert nt==basetri+pt
  hd=a.output/'integrated';hd.mkdir(exist_ok=True);hp=hd/f'BalconyExteriorHeroSummerMorningGloryBotanical3600_{L}.glb';hp.write_bytes(new.export(file_type='glb'));chk=trimesh.load(hp,force='scene',process=False);assert validate(chk)==nt
  hero_rows.append({'level':L,'oldIntegratedTriangles':oldtri,'oldPlanterTrianglesRemoved':removedtri,'baseHeroTriangles':basetri,'newPlanterTriangles':pt,'integratedTriangles':nt,'oldPlanterMeshesRemoved':removed,'glbSHA256':sha(hp),'roundtrip':True})
 pc=[r['triangles'] for r in planter_rows];hc=[r['integratedTriangles'] for r in hero_rows]
 assert all(x>y for x,y in zip(pc,pc[1:]));assert all(x>y for x,y in zip(hc,hc[1:]))
 lod0=trimesh.load(a.output/'planter/BalconyMorningGloryBotanicalPlanter_LOD0.glb',force='scene',process=False);world=trimesh.Scene();add_scene(world,lod0,'P',T);wb=world.bounds
 clear={'assemblyTiltDegrees':math.degrees(FLOOR_TILT_RAD),'rearCanopyToFacadePlaneMm':float(wb[0,2])*1000,'frontCanopyToMainRailInnerMm':float(.9775-wb[1,2])*1000,'leftCanopyToReturnRailInnerMm':float(wb[0,0]-(-1.7475))*1000,'worldBoundsMetres':wb.tolist(),'placementCentre':[PLACEMENT_X,finished_floor_y(PLACEMENT_Z),PLACEMENT_Z]}
 if min(clear['rearCanopyToFacadePlaneMm'],clear['frontCanopyToMainRailInnerMm'],clear['leftCanopyToReturnRailInnerMm'])<20:raise AssertionError(clear)
 meta={'schema':1,'date':'2026-09-17','assetId':'BalconyMorningGloryBotanicalPlanter','sourceOwner':'existing MorningGloryTrellisPlant refined in place; existing pot/saucer reused','manufactureInstallation':{'support':'three existing bamboo stakes remain inserted in potting soil and tied with existing jute geometry','installation':'existing pot and saucer retained; whole assembly follows authored 1.5% balcony fall','interfaces':'old morning-glory planter is removed from the integrated hero before the refined planter is inserted, preventing duplicate assemblies','geometryVsMaterial':'leaf outline, leaf camber, veins and five-lobed trumpet lip are geometry; epidermal/velvety microstructure remains PBR texture detail','weathering':'no random weathering added; botanical refinement only'},'materials':matrec,'clearances':clear,'planter':planter_rows,'integratedHero':hero_rows,'verification':{'finiteVerticesNormals':True,'noDegenerateFaces':True,'windingConsistent':True,'glbRoundtrip':True,'strictLODReduction':True,'oldPlanterRemovedBeforeIntegration':True},'unityCompile':False,'unityImport':False,'unityRender':False,'visualFidelity':{'score':None,'pass':False,'pointsAwarded':0,'reason':'No Unity 3840x2160 render/crops/temporal evidence.'},'implementationReadiness':{'lastRecorded':93,'recomputed':False},'nextProductionTarget':'Use real Unity pixels first. Otherwise continue high-coverage summer vegetation/context after visual review, not validator expansion.'}
 (a.output/'BalconyMorningGloryBotanicalPlanter.metadata.json').write_text(json.dumps(meta,indent=2)+'\n');print(json.dumps({'planter':dict(zip(LEVELS,pc)),'hero':dict(zip(LEVELS,hc)),'clearances':clear},indent=2))
if __name__=='__main__':main()
