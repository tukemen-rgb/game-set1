from pathlib import Path
import argparse, json, math, hashlib
import numpy as np, trimesh
from PIL import Image

LEVELS={
'MASTER':(48,16,8,0.010,1,1),'LOD0':(32,10,7,0.014,1,1),'LOD1':(20,6,6,0.024,1,0),'LOD2':(12,2,4,0.045,0,0),'LOD3':(8,0,2,0.085,0,0)}
MATS={
'PaintedFiberCement':([.58,.59,.56],0,.68,.30),'JointSealant':([.23,.23,.22],0,.73,.16),'BackerRod':([.08,.08,.075],0,.86,.22),
'PaintedMetal':([.37,.39,.38],0,.44,.15),'GalvanizedSteel':([.53,.55,.56],1,.47,.18),'BlackEPDM':([.026,.027,.026],0,.79,.27),
'BlackPolyesterMesh':([.035,.038,.036],0,.84,.18),'DarkCavity':([.012,.012,.012],0,.93,0)}
ASSETS=['PaintedFiberCementFacade3600x2700','ExteriorVentHood150','FacadeBaseStarterFlashing3600']

def box(e,c,n,m):
 q=trimesh.creation.box(extents=e);q.apply_translation(c);q.metadata.update(name=n,material=m);return q

def cyl(r,h,c,axis,sec,n,m):
 q=trimesh.creation.cylinder(radius=r,height=h,sections=sec);a=np.array(axis,float);a/=np.linalg.norm(a)
 if not np.allclose(a,[0,0,1]):q.apply_transform(trimesh.geometry.align_vectors([0,0,1],a))
 q.apply_translation(c);q.metadata.update(name=n,material=m);return q

def ann(ro,ri,d,c,axis,sec,n,m):
 th=np.arange(sec)*2*np.pi/sec;v=[]
 for z in (-d/2,d/2):
  for r in (ro,ri):
   for a in th:v.append([r*np.cos(a),r*np.sin(a),z])
 f=[]
 def I(z,r,j):return z*2*sec+r*sec+(j%sec)
 for j in range(sec):
  k=j+1;f += [[I(0,0,j),I(0,0,k),I(1,0,k)],[I(0,0,j),I(1,0,k),I(1,0,j)],[I(0,1,j),I(1,1,k),I(0,1,k)],[I(0,1,j),I(1,1,j),I(1,1,k)],[I(1,0,j),I(1,0,k),I(1,1,k)],[I(1,0,j),I(1,1,k),I(1,1,j)],[I(0,0,j),I(0,1,k),I(0,0,k)],[I(0,0,j),I(0,1,j),I(0,1,k)]]
 q=trimesh.Trimesh(vertices=v,faces=f,process=False);q.fix_normals();a=np.array(axis,float);a/=np.linalg.norm(a)
 if not np.allclose(a,[0,0,1]):q.apply_transform(trimesh.geometry.align_vectors([0,0,1],a))
 q.apply_translation(c);q.metadata.update(name=n,material=m);return q

def textures(td):
 td.mkdir(parents=True,exist_ok=True);rng=np.random.default_rng(16092610);out={}
 for n,(base,met,rough,ns) in MATS.items():
  s=128;noise=rng.normal(0,1,(s,s));field=noise
  if n=='PaintedFiberCement':field=.6*noise+.2*np.sin(np.arange(s)[:,None]*.15)+.2*np.sin(np.arange(s)[None,:]*.11)
  amp=.025 if n=='PaintedFiberCement' else .015
  b=np.clip(np.array(base)[None,None,:]*(1+amp*field[:,:,None]),0,1);mr=np.zeros((s,s,3),np.uint8);mr[:,:,1]=np.uint8(np.clip(rough+amp*field,0,1)*255);mr[:,:,2]=np.uint8(met*255)
  gx=np.gradient(field,axis=1);gy=np.gradient(field,axis=0);N=np.dstack([-gx*ns,-gy*ns,np.ones_like(gx)]);N/=np.linalg.norm(N,axis=2)[:,:,None];N=np.uint8(np.clip(N*.5+.5,0,1)*255)
  bc=Image.fromarray(np.uint8(b*255));mi=Image.fromarray(mr);ni=Image.fromarray(N);bc.save(td/f'{n}_base.png');mi.save(td/f'{n}_mr.png');ni.save(td/f'{n}_normal.png');out[n]=(bc,mi,ni)
 return out

def compact(parts,tex):
 g={}
 for p in parts:g.setdefault(p.metadata['material'],[]).append(p)
 s=trimesh.Scene()
 for mat,arr in g.items():
  V=[];F=[];N=[];UV=[];o=0
  for q in arr:V.append(q.vertices);F.append(q.faces+o);N.append(q.vertex_normals);UV.append(q.vertices[:,[0,1]]/(.42 if mat=='PaintedFiberCement' else .12));o+=len(q.vertices)
  q=trimesh.Trimesh(vertices=np.vstack(V),faces=np.vstack(F),vertex_normals=np.vstack(N),process=False);bc,mr,nm=tex[mat];pm=trimesh.visual.material.PBRMaterial(name=mat,baseColorTexture=bc,metallicRoughnessTexture=mr,normalTexture=nm,metallicFactor=1,roughnessFactor=1,doubleSided=False);q.visual=trimesh.visual.TextureVisuals(uv=np.vstack(UV),material=pm);s.add_geometry(q,node_name=mat,geom_name=mat)
 return s

def facade(L):
 sec,fast,lv,pitch,bead,back=LEVELS[L];p=[];W,H,T=3.6,2.7,.016;vx,vy=.92,.18;ap=.19;gap=.01;xs=[-1.8,-.9,0,.9,1.8];ys=[-1.35,-.45,.45,1.35];pid=0
 for ix in range(4):
  for iy in range(3):
   x0,x1=xs[ix]+gap/2,xs[ix+1]-gap/2;y0,y1=ys[iy]+gap/2,ys[iy+1]-gap/2
   if x0<vx<x1 and y0<vy<y1:
    a=ap/2+.006;ps=[(x0,x1,y0,vy-a),(x0,x1,vy+a,y1),(x0,vx-a,vy-a,vy+a),(vx+a,x1,vy-a,vy+a)]
    for k,(a0,a1,b0,b1) in enumerate(ps):
     if a1>a0 and b1>b0:p.append(box([a1-a0,b1-b0,T],[(a0+a1)/2,(b0+b1)/2,0],f'Panel{pid}_{k}','PaintedFiberCement'))
    p.append(ann(.104,.083,.020,[vx,vy,-.014],[0,0,1],sec,'VentReveal','DarkCavity'))
   else:p.append(box([x1-x0,y1-y0,T],[(x0+x1)/2,(y0+y1)/2,0],f'Panel{pid}','PaintedFiberCement'))
   pid+=1
 if bead:
  for x in xs[1:-1]:p.append(box([.006,H-.02,.006],[x,0,.011],'VJoint','JointSealant'))
  for y in ys[1:-1]:p.append(box([W-.02,.006,.006],[0,y,.011],'HJoint','JointSealant'))
 if back:
  for x in xs[1:-1]:p.append(box([.005,H-.04,.005],[x,0,.004],'VBacker','BackerRod'))
  for y in ys[1:-1]:p.append(box([W-.04,.005,.005],[0,y,.004],'HBacker','BackerRod'))
 if fast:
  for i,x in enumerate(np.linspace(-1.6,1.6,max(2,fast//2))):
   p += [cyl(.0032,.003,[x,-1.31,.011],[0,0,1],sec,f'FastB{i}','GalvanizedSteel'),cyl(.0032,.003,[x,1.31,.011],[0,0,1],sec,f'FastT{i}','GalvanizedSteel')]
 return p

def vent(L):
 sec,fast,nlou,pitch,bead,back=LEVELS[L];p=[ann(.086,.075,.19,[0,0,-.078],[0,0,1],sec,'Sleeve','PaintedMetal'),ann(.102,.087,.014,[0,0,.022],[0,0,1],sec,'Trim','PaintedMetal'),ann(.105,.087,.004,[0,0,.014],[0,0,1],sec,'Gasket','BlackEPDM'),box([.265,.018,.18],[0,.119,.095],'Top','PaintedMetal'),box([.018,.23,.175],[-.124,.006,.095],'CheekL','PaintedMetal'),box([.018,.23,.175],[.124,.006,.095],'CheekR','PaintedMetal'),box([.265,.018,.035],[0,-.115,.156],'Drip','PaintedMetal'),box([.225,.010,.010],[0,-.108,.171],'Hem','PaintedMetal')]
 for i,y in enumerate(np.linspace(-.074,.074,nlou)):
  q=box([.185,.012,.018],[0,0,0],f'Louver{i}','PaintedMetal');q.apply_transform(trimesh.transformations.rotation_matrix(math.radians(-23),[1,0,0]));q.apply_translation([0,y,.092]);p.append(q)
 span=.164;vals=np.arange(-span/2,span/2+pitch/2,pitch);th=max(.00055,min(.0022,pitch*.075));warp=[box([span,th,.0012],[0,v,.061],'Warp','BlackPolyesterMesh') for v in vals];weft=[box([th,span,.0012],[v,0,.063],'Weft','BlackPolyesterMesh') for v in vals]
 if warp:q=trimesh.util.concatenate(warp);q.metadata.update(name='WarpGroup',material='BlackPolyesterMesh');p.append(q)
 if weft:q=trimesh.util.concatenate(weft);q.metadata.update(name='WeftGroup',material='BlackPolyesterMesh');p.append(q)
 if fast:
  corners=[(-.086,-.086),(.086,-.086),(.086,.086),(-.086,.086)]
  for i,(x,y) in enumerate(corners):p += [cyl(.0033,.006,[x,y,.029],[0,0,1],sec,f'Screw{i}','GalvanizedSteel'),cyl(.0065,.0015,[x,y,.026],[0,0,1],sec,f'Washer{i}','BlackEPDM')]
 return p

def flashing(L):
 sec,fast,nlou,pitch,bead,back=LEVELS[L];W=3.6;p=[box([W,.04,.0016],[0,.02,0],'WallFlange','PaintedMetal'),box([W,.0016,.07],[0,-.001,.034],'Shelf','PaintedMetal'),box([W,.025,.0016],[0,-.014,.069],'Kick','PaintedMetal'),box([W,.016,.0016],[0,-.034,.076],'Drop','PaintedMetal'),box([W,.004,.010],[0,-.043,.080],'Hem','PaintedMetal'),box([.018,.043,.082],[-W/2+.009,-.002,.040],'EndL','PaintedMetal'),box([.018,.043,.082],[W/2-.009,-.002,.040],'EndR','PaintedMetal')]
 if fast:
  for i,x in enumerate(np.linspace(-1.6,1.6,max(2,fast//2))):p.append(cyl(.0032,.004,[x,.034,-.0003],[0,0,1],sec,f'Screw{i}','GalvanizedSteel'))
 return p
BUILD={'PaintedFiberCementFacade3600x2700':facade,'ExteriorVentHood150':vent,'FacadeBaseStarterFlashing3600':flashing}

def validate(parts):
 rows=[]
 for p in parts:
  assert np.isfinite(p.vertices).all() and np.isfinite(p.vertex_normals).all() and np.all(p.area_faces>1e-14),p.metadata['name'];q=p.copy();q.merge_vertices(digits_vertex=9);assert q.is_watertight and q.is_winding_consistent and q.volume>0,p.metadata['name'];_,c=np.unique(q.edges_sorted,axis=0,return_counts=True);assert np.all(c==2),p.metadata['name'];rows.append(dict(part=p.metadata['name'],material=p.metadata['material'],triangles=len(p.faces),volumeM3=float(q.volume)))
 return rows

def export(asset,L,parts,out,tex):
 rows=validate(parts);s=compact(parts,tex);d=out/asset;d.mkdir(parents=True,exist_ok=True);glb=d/f'{asset}_{L}.glb';glb.write_bytes(s.export(file_type='glb'));tri=sum(x['triangles'] for x in rows);r=trimesh.load(glb,force='scene',process=False);assert sum(len(g.faces) for g in r.geometry.values())==tri and np.allclose(r.bounds,s.bounds,atol=1e-6);od=d/f'{asset}_{L}_OBJ';od.mkdir(exist_ok=True);obj,files=trimesh.exchange.obj.export_obj(s,include_normals=True,include_texture=True,return_texture=True);(od/f'{asset}_{L}.obj').write_text(obj)
 for k,v in files.items():q=od/k;q.write_text(v) if isinstance(v,str) else q.write_bytes(v)
 rr=trimesh.load(od/f'{asset}_{L}.obj',force='scene',process=False);assert sum(len(g.faces) for g in rr.geometry.values())==tri
 return dict(asset=asset,level=L,triangles=tri,logicalParts=len(parts),runtimeMaterialMeshes=len(s.geometry),boundsMetres=s.bounds.tolist(),glbRoundtrip=True,objTriangleRoundtrip=True,glbSHA256=hashlib.sha256(glb.read_bytes()).hexdigest(),componentValidation=rows)

def run(out):
 out.mkdir(parents=True,exist_ok=True);tex=textures(out/'textures');records=[]
 for a,b in BUILD.items():
  c=[]
  for L in LEVELS:r=export(a,L,b(L),out,tex);records.append(r);c.append(r['triangles']);print(a,L,r['triangles'],flush=True)
  assert all(x>y for x,y in zip(c,c[1:])),(a,c)
 review=trimesh.Scene()
 for n,g in compact(facade('LOD0'),tex).geometry.items():review.add_geometry(g.copy(),node_name='Facade_'+n,geom_name='Facade_'+n)
 for n,g in compact(vent('LOD0'),tex).geometry.items():q=g.copy();q.apply_translation([.92,.18,.018]);review.add_geometry(q,node_name='Vent_'+n,geom_name='Vent_'+n)
 for n,g in compact(flashing('LOD0'),tex).geometry.items():q=g.copy();q.apply_translation([0,-1.372,.010]);review.add_geometry(q,node_name='Flash_'+n,geom_name='Flash_'+n)
 rp=out/'ExteriorFacadeInstalledReview_LOD0.glb';rp.write_bytes(review.export(file_type='glb'));rv=dict(path=rp.name,triangles=sum(len(g.faces) for g in review.geometry.values()),geometryCount=len(review.geometry),formalBenchmarkSceneChanged=False,unityVerified=False)
 report=dict(status='EXTERNAL_GEOMETRY_VERIFIED_NO_UNITY_RUNTIME',records=records,review=rv,visualFidelity=dict(authority='Assets/QA/visual_fidelity_gate.json',score=None,pass_=False,pointsAwarded=0),implementationReadiness=dict(lastRecorded=93,recomputed=False),unityCompile=False,unityImport=False,unityRender=False,lodTemporalVerified=False)
 (out/'geometry_verification.json').write_text(json.dumps(report,indent=2)+'\n')
 meta=dict(schema=1,date='2026-09-16',units='metres',owner='ExteriorFacadeSet',manufactureInstallation=dict(facade='Physical 16 mm painted fiber-cement panel pieces with 10 mm joints; vent-bearing field is split around a real aperture.',vent='150 mm clear-bore sleeve, trim, EPDM gasket, painted hood, real louver geometry, coarse geometric insect screen and fasteners.',flashing='Formed painted starter flashing with wall flange, shelf, kick, drop, hem and end dams.',interfaces='Vent sleeve passes through actual panel aperture; gasket seats between trim and facade; base flashing sits below panels.',orientationExposure='Y up, exterior +Z; no lighting baked.',agingCausality='No arbitrary rust, algae, rain streaks, baked highlights or wetness. Future wear follows joint runoff, hood drip and lower splash.',geometryVsMaterial='Panels, aperture/reveal, joint beads/backers, vent bore, hood, louvers, screen, folds and fasteners are geometry; microstructure is material proxy.'),materials={k:dict(albedo=v[0],metallic=v[1],roughness=v[2],normalScale=v[3],wetness=0) for k,v in MATS.items()},triangles={a:{r['level']:r['triangles'] for r in records if r['asset']==a} for a in BUILD},visualFidelity=dict(authority='Assets/QA/visual_fidelity_gate.json',status='UNSCORED_UNTIL_REAL_4K_RENDER',score=None,passed=False,pointsAwarded=0),unityCompile=False,unityImport=False,unityRender=False,formalBenchmarkSceneChanged=False,nextProductionTarget='If no Unity render appears, integrate facade with existing window/AC/rainwater modules or add facade-mounted service hardware after owner inspection.')
 (out/'ExteriorFacadeSet.metadata.json').write_text(json.dumps(meta,indent=2)+'\n')
 inv=dict(schema=1,updated='2026-09-16',isInventoryDelta=True,ownerInspection=dict(keywords=['facade','exteriorwall','siding','vent'],dedicatedFacadeVentOwnerDetected=False),newAssets=[dict(id=a,masterTriangles=next(r['triangles'] for r in records if r['asset']==a and r['level']=='MASTER'),lodTriangles=[next(r['triangles'] for r in records if r['asset']==a and r['level']==L) for L in ('LOD0','LOD1','LOD2','LOD3')],actualOBJGLBExports=True,unityVerified=False) for a in BUILD],reviewIntegrations=[rv],productionDeltaThisRun=dict(authoredAssetSets=3,highDetailMasters=3,runtimeLodSets=3),nextProductionTarget=meta['nextProductionTarget']);(out/'asset_inventory_delta.json').write_text(json.dumps(inv,indent=2)+'\n');return report,meta,inv
if __name__=='__main__':
 ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);args=ap.parse_args();run(args.output)
