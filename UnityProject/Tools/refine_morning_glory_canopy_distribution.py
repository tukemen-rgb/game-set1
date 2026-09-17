from __future__ import annotations
from pathlib import Path
import json, math, hashlib, argparse
import numpy as np
import trimesh
from scipy.spatial import cKDTree
from scipy.optimize import linear_sum_assignment

LEVELS=("MASTER","LOD0","LOD1","LOD2","LOD3")
CONFIG={
"MASTER":dict(max_yaw_deg=18.,max_radial_tilt_deg=10.,max_sun_tilt_deg=8.,max_roll_deg=6.5,scale_span=.090,anchor_hold=.11,blend_end=.44),
"LOD0":dict(max_yaw_deg=18.,max_radial_tilt_deg=10.,max_sun_tilt_deg=8.,max_roll_deg=6.5,scale_span=.090,anchor_hold=.11,blend_end=.44),
"LOD1":dict(max_yaw_deg=16.,max_radial_tilt_deg=9.,max_sun_tilt_deg=7.,max_roll_deg=5.5,scale_span=.080,anchor_hold=.12,blend_end=.45),
"LOD2":dict(max_yaw_deg=13.,max_radial_tilt_deg=7.5,max_sun_tilt_deg=5.5,max_roll_deg=4.5,scale_span=.065,anchor_hold=.14,blend_end=.47),
"LOD3":dict(max_yaw_deg=10.,max_radial_tilt_deg=6.,max_sun_tilt_deg=4.,max_roll_deg=3.5,scale_span=.050,anchor_hold=.16,blend_end=.50),
}
SUN=np.array([.42,.78,.46],float);SUN/=np.linalg.norm(SUN)
GOLDEN=math.pi*(3.-math.sqrt(5.))
COLORS={"Bamboo":[148,128,74,255],"JuteTwine":[155,117,72,255],"LivingStem":[69,122,39,255],"LivingLeaf":[49,112,35,255],"LeafVein":[67,126,42,255],"FlowerBlue":[74,72,172,255],"FlowerThroat":[210,198,224,255],"DrySoil":[67,43,29,255]}
def unit(v):
 v=np.asarray(v,float);n=np.linalg.norm(v)
 if n<1e-12:raise ValueError('zero vector')
 return v/n
def rot(axis,a):
 x,y,z=unit(axis);c=math.cos(a);s=math.sin(a);C=1-c
 return np.array([[c+x*x*C,x*y*C-z*s,x*z*C+y*s],[y*x*C+z*s,c+y*y*C,y*z*C-x*s],[z*x*C-y*s,z*y*C+x*s,c+z*z*C]],float)
def smooth(x):x=np.clip(x,0,1);return x*x*(3-2*x)
def comps(m):return list(m.split(only_watertight=False))
def merge(ms):
 q=trimesh.util.concatenate(ms);q.merge_vertices(digits_vertex=9);q.remove_unreferenced_vertices();q.fix_normals(multibody=True);return q
def leaf_normal(m):
 v=m.vertices-m.vertices.mean(0);_,_,vh=np.linalg.svd(v,full_matrices=False);n=unit(vh[-1]);return -n if n[1]<0 else n
def deform(points,pivot,radius,R,scale,cfg):
 p=np.asarray(points,float);rel=p-pivot;d=np.linalg.norm(rel,axis=1);hold=radius*cfg['anchor_hold'];end=max(radius*cfg['blend_end'],hold+1e-5);w=smooth((d-hold)/(end-hold))[:,None];target=pivot+(R@(rel*scale).T).T;return p*(1-w)+target*w
def validate_leaf(m,label):
 if not np.isfinite(m.vertices).all() or not np.isfinite(m.vertex_normals).all():raise AssertionError((label,'nonfinite'))
 if np.any(m.area_faces<1e-13):raise AssertionError((label,'degenerate'))
 if not m.is_watertight:raise AssertionError((label,'not watertight'))
 if not m.is_winding_consistent:raise AssertionError((label,'winding'))
 if m.volume<=0:raise AssertionError((label,'nonpositive volume'))
def validate_group(m,label):
 if not np.isfinite(m.vertices).all() or not np.isfinite(m.vertex_normals).all():raise AssertionError((label,'nonfinite'))
 if np.any(m.area_faces<1e-13):raise AssertionError((label,'degenerate'))
 if not m.is_winding_consistent:raise AssertionError((label,'winding'))
def stat(a):
 a=np.asarray(a,float);return {'min':float(a.min()),'mean':float(a.mean()),'max':float(a.max())}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def export_obj(s,p):p.write_text(trimesh.exchange.obj.export_obj(s,include_normals=True,include_color=True))
def transform_for(i,c,n,canopy,height,cfg):
 phase=i*GOLDEN+c[0]*4.73+c[2]*3.19
 yaw=cfg['max_yaw_deg']*(.72*math.sin(phase)+.28*math.sin(phase*.43+1.1));roll=cfg['max_roll_deg']*math.sin(phase*1.37+.4)
 radial=np.array([c[0]-canopy[0],0,c[2]-canopy[1]],float)
 if np.linalg.norm(radial)<1e-8:radial=np.array([math.cos(phase),0,math.sin(phase)])
 radial=unit(radial);tan=unit(np.cross([0,1,0],radial));droop=.30+.70*(1-height)
 rtilt=cfg['max_radial_tilt_deg']*droop*(.82+.18*math.sin(phase*.79+.6))
 scale=1+cfg['scale_span']*(.72*(.5-height)+.28*math.sin(phase*1.61-.2));scale=float(np.clip(scale,1-cfg['scale_span'],1+cfg['scale_span']))
 base=rot([0,1,0],math.radians(yaw))@rot(tan,math.radians(rtilt));n1=unit(base@n);cr=np.cross(n1,SUN)
 if np.linalg.norm(cr)<1e-9:rs=np.eye(3);st=0.
 else:
  full=math.degrees(math.acos(float(np.clip(np.dot(n1,SUN),-1,1))));st=min(cfg['max_sun_tilt_deg'],full*(.18+.12*height));rs=rot(cr,math.radians(st))
 n2=unit(rs@n1);R=rot(n2,math.radians(roll))@rs@base
 return R,scale,{'yawDeg':yaw,'radialTiltDeg':rtilt,'sunTiltDeg':st,'rollDeg':roll,'scale':scale}
def process(level,indir,outdir):
 cfg=CONFIG[level];srcp=indir/f'MorningGloryTrellis_{level}_botanical.glb';src=trimesh.load(srcp,force='scene',process=False)
 leaves=sorted(comps(src.geometry['LivingLeaf']),key=lambda g:(float(g.centroid[1]),float(g.centroid[0]),float(g.centroid[2])))
 veins=comps(src.geometry['LeafVein']) if 'LeafVein' in src.geometry else [];cent=np.array([g.centroid for g in leaves]);vg=[[] for _ in leaves]
 if veins:
  if len(veins)!=len(leaves)*5:raise AssertionError(('unexpected total veins',len(veins),len(leaves)))
  vc=np.array([g.centroid for g in veins]);slots=np.repeat(np.arange(len(leaves),dtype=int),5);cost=np.linalg.norm(vc[:,None,:]-cent[slots][None,:,:],axis=2)
  rr,cc=linear_sum_assignment(cost)
  for vi,si in zip(rr,cc):vg[int(slots[si])].append(veins[int(vi)])
  counts=[len(x) for x in vg]
  if any(x!=5 for x in counts):raise AssertionError(('balanced vein ownership',counts))
 stems=np.asarray(src.geometry['LivingStem'].vertices);st=cKDTree(stems);ymin,ymax=cent[:,1].min(),cent[:,1].max();yr=max(ymax-ymin,1e-6);can=np.median(cent[:,[0,2]],axis=0)
 newl=[];newv=[];rows=[]
 for i,l in enumerate(leaves):
  validate_leaf(l,f'source {level} {i}');c=np.asarray(l.centroid);h=float((c[1]-ymin)/yr);dist,ix=st.query(l.vertices,k=1);j=int(np.argmin(dist));pivot=np.asarray(l.vertices[j]);gap=float(dist[j]);radius=max(float(np.linalg.norm(l.vertices-pivot,axis=1).max()),1e-5);R,scale,a=transform_for(i,c,leaf_normal(l),can,h,cfg)
  q=l.copy();q.vertices=deform(q.vertices,pivot,radius,R,scale,cfg);q.fix_normals(multibody=True);validate_leaf(q,f'new {level} {i}');newl.append(q)
  for k,v in enumerate(vg[i]):vv=v.copy();vv.vertices=deform(vv.vertices,pivot,radius,R,scale,cfg);vv.fix_normals(multibody=True);validate_group(vv,f'vein {level} {i}:{k}');newv.append(vv)
  rows.append({'index':i,'height01':h,'attachmentGapMm':gap*1000,'attachmentHoldRadiusMm':radius*cfg['anchor_hold']*1000,**a})
 groups={n:g.copy() for n,g in src.geometry.items()};groups['LivingLeaf']=merge(newl)
 if veins:groups['LeafVein']=merge(newv)
 if len(groups['LivingLeaf'].faces)!=len(src.geometry['LivingLeaf'].faces):raise AssertionError('leaf topology count changed')
 if veins and len(groups['LeafVein'].faces)!=len(src.geometry['LeafVein'].faces):raise AssertionError('vein topology count changed')
 for n,g in groups.items():validate_group(g,n)
 scene=trimesh.Scene()
 for n,g in groups.items():q=g.copy();q.visual=trimesh.visual.ColorVisuals(mesh=q,face_colors=np.tile(COLORS.get(n,[180,180,180,255]),(len(q.faces),1)));scene.add_geometry(q,node_name=n,geom_name=n)
 tri=sum(len(g.faces) for g in scene.geometry.values());src_tri=sum(len(g.faces) for g in src.geometry.values());assert tri==src_tri
 outdir.mkdir(parents=True,exist_ok=True);gp=outdir/f'MorningGloryTrellis_{level}_botanical.glb';op=outdir/f'MorningGloryTrellis_{level}_botanical.obj';gp.write_bytes(scene.export(file_type='glb'));export_obj(scene,op)
 rg=trimesh.load(gp,force='scene',process=False);ro=trimesh.load(op,force='scene',process=False);assert sum(len(g.faces) for g in rg.geometry.values())==tri;assert sum(len(g.faces) for g in ro.geometry.values())==tri
 ratio=np.ptp(scene.bounds,axis=0)/np.maximum(np.ptp(src.bounds,axis=0),1e-8)
 if np.any(ratio<.75) or np.any(ratio>1.30):raise AssertionError(('span',level,ratio))
 return {'level':level,'sourceTriangles':src_tri,'refinedTriangles':tri,'leafCount':len(leaves),'veinsPerLeaf':5 if veins else 0,'attachmentGapMm':stat([r['attachmentGapMm'] for r in rows]),'yawDeg':stat([r['yawDeg'] for r in rows]),'radialTiltDeg':stat([r['radialTiltDeg'] for r in rows]),'sunTiltDeg':stat([r['sunTiltDeg'] for r in rows]),'rollDeg':stat([r['rollDeg'] for r in rows]),'scale':stat([r['scale'] for r in rows]),'sourceBoundsMetres':src.bounds.tolist(),'refinedBoundsMetres':scene.bounds.tolist(),'spanRatio':ratio.tolist(),'glbSHA256':sha(gp),'objSHA256':sha(op),'leafShellsWatertight':all(g.is_watertight for g in newl),'leafShellsPositiveVolume':all(g.volume>0 for g in newl),'leafAuthoring':rows}
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--input',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);a=ap.parse_args();rows=[process(L,a.input,a.output) for L in LEVELS];counts=[r['refinedTriangles'] for r in rows];assert all(x>y for x,y in zip(counts,counts[1:]));rep={'schema':1,'date':'2026-09-17','owner':'MorningGloryTrellisPlant_BotanicalRefinement','status':'EXTERNAL_CANOPY_DISTRIBUTION_REFINEMENT_NOT_UNITY_VISUAL_EVIDENCE','sunDirectionLocal':SUN.tolist(),'levels':rows,'strictLODReduction':True,'visualFidelity':{'score':None,'pass':False,'pointsAwarded':0,'expectedImpactOnly':['geometry_construction','vegetation_natural_complexity'],'criticalDefectRiskAddressedOnly':'obvious_repetition'},'unityCompile':False,'unityImport':False,'unityRender':False};(a.output/'canopy_distribution_report.json').write_text(json.dumps(rep,indent=2)+'\n');print(json.dumps({L:c for L,c in zip(LEVELS,counts)},indent=2))
if __name__=='__main__':main()
