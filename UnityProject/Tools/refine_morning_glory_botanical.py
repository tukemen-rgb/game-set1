from __future__ import annotations
from pathlib import Path
import argparse, json, math, hashlib
import numpy as np
import trimesh
from scipy.interpolate import splprep, splev
from shapely.geometry import Polygon

LEVEL_CONFIG={
 'MASTER': {'leaf_boundary':48,'leaf_camber_mm':0.65,'flower_lobe':0.055},
 'LOD0': {'leaf_boundary':32,'leaf_camber_mm':0.60,'flower_lobe':0.050},
 'LOD1': {'leaf_boundary':24,'leaf_camber_mm':0.50,'flower_lobe':0.045},
 'LOD2': {'leaf_boundary':18,'leaf_camber_mm':0.35,'flower_lobe':0.035},
 'LOD3': {'leaf_boundary':12,'leaf_camber_mm':0.22,'flower_lobe':0.025},
}
LEVELS=list(LEVEL_CONFIG)

def sha(p:Path): return hashlib.sha256(p.read_bytes()).hexdigest()
def unit(v):
 v=np.asarray(v,float); n=np.linalg.norm(v)
 if n<1e-12: raise ValueError('zero vector')
 return v/n

def comps(g): return list(g.split(only_watertight=False))

def leaf_frame(leaf, vein_group=None):
 c=leaf.vertices.mean(0)
 centered=leaf.vertices-c
 _,_,vh=np.linalg.svd(centered,full_matrices=False)
 normal=vh[-1]; e1=vh[0]; e2=unit(np.cross(normal,e1))
 if vein_group is not None:
  dv=vein_group.centroid-leaf.centroid
  if np.dot(normal,dv)<0: normal=-normal; e2=-e2
 else:
  if normal[1] < 0: normal=-normal; e2=-e2
 return c,unit(e1),unit(e2),unit(normal)

def cluster_outline(leaf,c,e1,e2,tol=0.0015):
 xy=np.column_stack([(leaf.vertices-c)@e1,(leaf.vertices-c)@e2])
 n=len(xy);parent=list(range(n))
 def find(a):
  while parent[a]!=a:
   parent[a]=parent[parent[a]];a=parent[a]
  return a
 def union(a,b):
  a,b=find(a),find(b)
  if a!=b: parent[b]=a
 for i in range(n):
  d=np.linalg.norm(xy[i+1:]-xy[i],axis=1)
  for off in np.where(d<tol)[0]: union(i,i+1+int(off))
 groups={}
 for i in range(n):groups.setdefault(find(i),[]).append(i)
 pts=np.array([xy[idx].mean(0) for idx in groups.values()])
 if not (10<=len(pts)<=24): raise AssertionError(('unexpected outline clusters',len(pts)))
 ctr=pts.mean(0);ang=np.arctan2(pts[:,1]-ctr[1],pts[:,0]-ctr[0]);pts=pts[np.argsort(ang)]
 poly=Polygon(pts)
 if not poly.is_valid or poly.area<=1e-6: raise AssertionError(('bad source outline',poly.is_valid,poly.area))
 return pts

def smooth_closed_outline(pts,nout):
 out=np.asarray(pts,float)
 for _ in range(2):
  nxt=[]
  for i in range(len(out)):
   a=out[i];b=out[(i+1)%len(out)]
   nxt.extend([.75*a+.25*b,.25*a+.75*b])
  out=np.asarray(nxt)
 dense=np.vstack([out,out[0]]);cum=np.concatenate([[0],np.cumsum(np.linalg.norm(np.diff(dense,axis=0),axis=1))])
 tar=np.linspace(0,cum[-1],nout,endpoint=False);res=[]
 for sv in tar:
  i=min(np.searchsorted(cum,sv,side='right')-1,len(out)-1);d=cum[i+1]-cum[i];a=0 if d==0 else (sv-cum[i])/d
  res.append(dense[i]*(1-a)+dense[i+1]*a)
 res=np.asarray(res)
 a0=abs(Polygon(pts).area);a1=abs(Polygon(res).area)
 ctr0=pts.mean(0);ctr1=res.mean(0);scale=math.sqrt(a0/max(a1,1e-12));res=(res-ctr1)*scale+ctr0
 if not Polygon(res).is_valid:
  dense=np.vstack([pts,pts[0]]);cum=np.concatenate([[0],np.cumsum(np.linalg.norm(np.diff(dense,axis=0),axis=1))]);res=[]
  for sv in np.linspace(0,cum[-1],nout,endpoint=False):
   i=min(np.searchsorted(cum,sv,side='right')-1,len(pts)-1);d=cum[i+1]-cum[i];a=0 if d==0 else (sv-cum[i])/d
   res.append(dense[i]*(1-a)+dense[i+1]*a)
  res=np.asarray(res)
 if not Polygon(res).is_valid: raise AssertionError('outline resample self-intersection')
 return res

def build_curved_leaf(leaf, vein_group, nout, camber_mm):
 c,e1,e2,n=leaf_frame(leaf,vein_group);src=cluster_outline(leaf,c,e1,e2);outer=smooth_closed_outline(src,nout)
 ctr=outer.mean(0);mid=ctr+(outer-ctr)*0.55
 thickness=max(0.00055,min(0.00075,float(np.ptp((leaf.vertices-c)@n))))
 camber=camber_mm/1000.0
 verts=[]
 def P(xy,off): return c+e1*xy[0]+e2*xy[1]+n*off
 for xy in outer: verts.append(P(xy,thickness/2))
 for xy in mid: verts.append(P(xy,camber*0.72+thickness/2))
 verts.append(P(ctr,camber+thickness/2)); tc=len(verts)-1
 for xy in outer: verts.append(P(xy,-thickness/2))
 for xy in mid: verts.append(P(xy,camber*0.72-thickness/2))
 verts.append(P(ctr,camber-thickness/2)); bc=len(verts)-1
 N=nout; to=0;tm=N;bo=2*N+1;bm=3*N+1
 faces=[]
 for j in range(N):
  k=(j+1)%N
  faces += [[to+j,to+k,tm+k],[to+j,tm+k,tm+j],[tm+j,tm+k,tc]]
  faces += [[bo+j,bm+k,bo+k],[bo+j,bm+j,bm+k],[bm+j,bc,bm+k]]
  faces += [[to+j,bo+k,to+k],[to+j,bo+j,bo+k]]
 m=trimesh.Trimesh(vertices=np.asarray(verts),faces=np.asarray(faces,dtype=np.int64),process=False)
 m.merge_vertices(digits_vertex=9);m.remove_unreferenced_vertices();m.fix_normals(multibody=True)
 if not m.is_watertight or not m.is_winding_consistent or m.volume<=0: raise AssertionError(('bad new leaf',m.is_watertight,m.is_winding_consistent,m.volume))
 if np.any(m.area_faces<1e-13): raise AssertionError('degenerate new leaf')
 return m,(c,e1,e2,n,outer,camber,thickness)

def seat_veins(vein_components,leaf_info):
 c,e1,e2,n,outer,camber,_=leaf_info
 ctr=outer.mean(0);maxr=max(np.linalg.norm(outer-ctr,axis=1).max(),1e-5)
 shifted=[]
 for g in vein_components:
  q=g.copy();xy=np.column_stack([(q.vertices-c)@e1,(q.vertices-c)@e2]);r=np.linalg.norm(xy-ctr,axis=1)/maxr
  dz=camber*np.clip(1-r*r,0,1)
  q.vertices=q.vertices+dz[:,None]*n[None,:]
  shifted.append(q)
 return shifted

def merge(meshes):
 out=trimesh.util.concatenate(meshes);out.merge_vertices(digits_vertex=9);out.remove_unreferenced_vertices();out.fix_normals(multibody=True);return out

def unique_throat_centres(throat_comps,tol=1e-4):
 out=[]
 for g in throat_comps:
  c=g.centroid
  if not any(np.linalg.norm(c-x)<tol for x in out):out.append(c)
 return out

def flower_lobe_refine(g,throat_centres,amp):
 if len(g.vertices)<200:return g.copy(),False
 q=g.copy();v=q.vertices.copy();c=q.centroid
 tc=min(throat_centres,key=lambda x:np.linalg.norm(x-c))
 _,_,vh=np.linalg.svd(v-v.mean(0),full_matrices=False);axis=unit(vh[0])
 if np.dot(axis,c-tc)<0:axis=-axis
 seed=np.array([0.,1.,0.])
 if abs(np.dot(seed,axis))>.9:seed=np.array([1.,0.,0.])
 e1=unit(seed-axis*np.dot(seed,axis));e2=unit(np.cross(axis,e1))
 rel=v-tc;ax=rel@axis;amin,amax=float(ax.min()),float(ax.max());span=max(amax-amin,1e-6);t=np.clip((ax-amin)/span,0,1)
 w=np.clip((t-.55)/.45,0,1);w=w*w*(3-2*w)
 x=rel@e1;y=rel@e2;theta=np.arctan2(y,x)
 scale=1+amp*np.sin(5*theta)*w
 radial=(x[:,None]*e1+y[:,None]*e2)*scale[:,None]
 axial=(ax+0.0014*np.cos(5*theta)*w)[:,None]*axis
 other=rel-(x[:,None]*e1+y[:,None]*e2)-ax[:,None]*axis
 q.vertices=tc+axial+radial+other
 if np.any(q.area_faces<1e-13) or not q.is_winding_consistent: raise AssertionError('flower deformation invalid')
 return q,True

def material_scene(groups):
 scene=trimesh.Scene()
 colors={'Bamboo':[148,128,74,255],'JuteTwine':[155,117,72,255],'LivingStem':[69,122,39,255],'LivingLeaf':[49,112,35,255],'LeafVein':[67,126,42,255],'FlowerBlue':[74,72,172,255],'FlowerThroat':[210,198,224,255],'DrySoil':[67,43,29,255]}
 for name,g in groups.items():
  q=g.copy();q.visual=trimesh.visual.ColorVisuals(mesh=q,face_colors=np.tile(colors.get(name,[180,180,180,255]),(len(q.faces),1)))
  scene.add_geometry(q,node_name=name,geom_name=name)
 return scene

def export_obj(scene,path):
 text=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_color=True)
 path.write_text(text)

def process(level,src_path,outdir):
 cfg=LEVEL_CONFIG[level];src=trimesh.load(src_path,force='scene',process=False)
 leaves=comps(src.geometry['LivingLeaf']);veins=comps(src.geometry['LeafVein']) if 'LeafVein' in src.geometry else []
 if veins and len(veins)!=len(leaves)*5: raise AssertionError((level,'vein mapping',len(leaves),len(veins)))
 if veins:
  for i,leaf in enumerate(leaves):
   vc=np.mean([g.centroid for g in veins[i*5:(i+1)*5]],axis=0)
   if np.linalg.norm(vc-leaf.centroid)>.025: raise AssertionError((level,'vein group mismatch',i,np.linalg.norm(vc-leaf.centroid)))
 newleaves=[];newveins=[]
 for i,leaf in enumerate(leaves):
  vg=merge(veins[i*5:(i+1)*5]) if veins else None
  nl,info=build_curved_leaf(leaf,vg,cfg['leaf_boundary'],cfg['leaf_camber_mm']);newleaves.append(nl)
  if veins: newveins.extend(seat_veins(veins[i*5:(i+1)*5],info))
 throat_comps=comps(src.geometry['FlowerThroat']);centres=unique_throat_centres(throat_comps)
 flower_comps=comps(src.geometry['FlowerBlue']);newflowers=[];open_count=0
 for g in flower_comps:
  q,changed=flower_lobe_refine(g,centres,cfg['flower_lobe']);newflowers.append(q);open_count+=int(changed)
 groups={name:g.copy() for name,g in src.geometry.items()}
 groups['LivingLeaf']=merge(newleaves)
 if veins: groups['LeafVein']=merge(newveins)
 groups['FlowerBlue']=merge(newflowers)
 scene=material_scene(groups);outdir.mkdir(parents=True,exist_ok=True)
 glb=outdir/f'MorningGloryTrellis_{level}_botanical.glb';glb.write_bytes(scene.export(file_type='glb'))
 obj=outdir/f'MorningGloryTrellis_{level}_botanical.obj';export_obj(scene,obj)
 reload=trimesh.load(glb,force='scene',process=False)
 tri=sum(len(g.faces) for g in scene.geometry.values());rtri=sum(len(g.faces) for g in reload.geometry.values())
 if rtri!=tri:raise AssertionError((level,'glb tri',tri,rtri))
 if not np.allclose(scene.bounds,reload.bounds,atol=1e-7):raise AssertionError((level,'bounds'))
 report={'level':level,'sourceTriangles':sum(len(g.faces) for g in src.geometry.values()),'refinedTriangles':tri,'leafCount':len(leaves),'leafTriangles':len(groups['LivingLeaf'].faces),'veinTriangles':len(groups['LeafVein'].faces) if 'LeafVein' in groups else 0,'openFlowersRefined':open_count,'flowerBlueTriangles':len(groups['FlowerBlue'].faces),'leafBoundarySamples':cfg['leaf_boundary'],'leafCamberMm':cfg['leaf_camber_mm'],'flowerLipRadialModulationFraction':cfg['flower_lobe'],'glbReloadTriangles':rtri,'boundsMetres':scene.bounds.tolist(),'glbSHA256':sha(glb),'objSHA256':sha(obj),'leafShellsWatertight':all(x.is_watertight for x in newleaves),'leafShellsPositiveVolume':all(x.volume>0 for x in newleaves),'degenerateFaces':int(sum(np.sum(g.area_faces<1e-13) for g in groups.values()))}
 if report['degenerateFaces']!=0:raise AssertionError((level,'degenerate',report['degenerateFaces']))
 return report

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--input',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);a=ap.parse_args()
 rows=[process(level,a.input/f'MorningGloryTrellis_{level}_repaired.obj',a.output) for level in LEVELS]
 counts=[r['refinedTriangles'] for r in rows]
 if not all(x>y for x,y in zip(counts,counts[1:])):raise AssertionError(('LOD',counts))
 payload={'schema':1,'date':'2026-09-17','status':'EXTERNAL_GEOMETRY_REFINEMENT_NOT_UNITY_VISUAL_EVIDENCE','design':{'leaf':'Conservative closed Chaikin silhouette smoothing with area preservation, curved watertight shell and millimetre-scale camber; existing geometric veins are seated by the same camber field.','flower':'Existing open trumpet topology retained; outer 45% receives subtle five-lobe radial modulation and millimetre-scale lip curl.','assumptions':'Botanical curvature amplitudes are authored lookdev values, not measurements of a historical cultivar.'},'levels':rows,'unityCompile':False,'unityImport':False,'unityRender':False,'visualFidelityScore':None,'pointsAwarded':0}
 (a.output/'botanical_refinement_report.json').write_text(json.dumps(payload,indent=2)+'\n')
 print(json.dumps({r['level']:r['refinedTriangles'] for r in rows},indent=2))
if __name__=='__main__':main()
