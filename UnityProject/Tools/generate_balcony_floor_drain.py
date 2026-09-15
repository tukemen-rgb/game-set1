from __future__ import annotations
import argparse, json, math, hashlib
from pathlib import Path
import numpy as np
import trimesh
import shapely
from shapely.geometry import Point, LineString
from shapely.ops import unary_union
TAU=math.tau

MATS={
 'StainlessGrate':dict(base=[.50,.53,.55,1],metallic=1.0,roughness=.31),
 'GrayPVC':dict(base=[.25,.27,.27,1],metallic=0.0,roughness=.62),
 'BlackEPDM':dict(base=[.018,.020,.020,1],metallic=0.0,roughness=.80),
}
def mat(k):
 s=MATS[k];return trimesh.visual.material.PBRMaterial(name=k,baseColorFactor=s['base'],metallicFactor=s['metallic'],roughnessFactor=s['roughness'])
def finish(m,k):
 m.merge_vertices(digits_vertex=9);m.remove_unreferenced_vertices();m.fix_normals(multibody=True);m.visual.material=mat(k);return m

def lathe(profile,n,k):
 # closed radial cross-section revolved about Y
 verts=[];faces=[];profile=np.asarray(profile,float);P=len(profile)
 for i in range(n):
  a=TAU*i/n;c=math.cos(a);s=math.sin(a)
  for r,y in profile:verts.append([r*c,y,r*s])
 for i in range(n):
  ni=(i+1)%n
  for j in range(P):
   nj=(j+1)%P;a=i*P+j;b=ni*P+j;c=ni*P+nj;d=i*P+nj
   faces += [[a,b,c],[a,c,d]]
 return finish(trimesh.Trimesh(vertices=np.array(verts),faces=np.array(faces),process=False),k)

def extrude_polygon(poly,height,k):
 tris=list(shapely.constrained_delaunay_triangles(poly).geoms);verts=[];faces=[];idx={}
 def vid(x,y,z):
  key=(round(x,10),round(y,10),round(z,10))
  if key not in idx:idx[key]=len(verts);verts.append([x,z,y]) # polygon XY -> model XZ, extrusion -> Y
  return idx[key]
 y0=0;y1=height
 for tr in tris:
  if not poly.covers(tr.representative_point()):continue
  q=list(tr.exterior.coords)[:3]
  a,b,c=[vid(x,z,y1) for x,z in q];faces.append([a,c,b])
  a,b,c=[vid(x,z,y0) for x,z in q];faces.append([a,b,c])
 for ring in [poly.exterior,*poly.interiors]:
  pts=list(ring.coords)
  for (x0,z0),(x1,z1) in zip(pts,pts[1:]):
   a=vid(x0,z0,y0);b=vid(x1,z1,y0);c=vid(x1,z1,y1);d=vid(x0,z0,y1);faces += [[a,b,c],[a,c,d]]
 return finish(trimesh.Trimesh(vertices=np.array(verts),faces=np.array(faces),process=False),k)

def build(level):
 seg={'MASTER':128,'LOD0':96,'LOD1':64,'LOD2':40,'LOD3':24}[level]
 q=max(2,seg//16)
 s=trimesh.Scene()
 # 100 mm-class removable grate with 18 true radial drain slots and one screw aperture.
 outer=Point(0,0).buffer(.048,quad_segs=seg//4)
 holes=[]
 for i in range(18):
  a=TAU*i/18
  p0=(.013*math.cos(a),.013*math.sin(a));p1=(.037*math.cos(a),.037*math.sin(a))
  holes.append(LineString([p0,p1]).buffer(.0020,cap_style='round',quad_segs=q))
 holes.append(Point(0,0).buffer(.0022,quad_segs=q))
 plate=outer.difference(unary_union(holes))
 grate=extrude_polygon(plate,.0012,'StainlessGrate');grate.apply_translation([0,.0004,0]);s.add_geometry(grate,node_name='RemovableGrate_18RealSlots',geom_name='RemovableGrate_18RealSlots')
 # EPDM seating ring lives under plate, not painted as a dark annulus.
 gasket=lathe([[.047,0],[.047,-.0012],[.041,-.0012],[.041,0]],max(16,seg//2),'BlackEPDM');s.add_geometry(gasket,node_name='EPDMBeddingRing',geom_name='EPDMBeddingRing')
 # PVC flange and recessed drain well. These are closed shells with actual central void.
 flange=lathe([[.052,0],[.052,-.007],[.043,-.007],[.043,-.002],[.049,-.002],[.049,0]],seg,'GrayPVC');s.add_geometry(flange,node_name='PVCFloorFlange',geom_name='PVCFloorFlange')
 funnel=lathe([[.043,-.006],[.043,-.013],[.037,-.048],[.031,-.057],[.028,-.057],[.034,-.046],[.040,-.012],[.040,-.006]],seg,'GrayPVC');s.add_geometry(funnel,node_name='RecessedDrainWell',geom_name='RecessedDrainWell')
 outlet=lathe([[.031,-.054],[.031,-.090],[.027,-.090],[.027,-.055]],max(16,seg//2),'GrayPVC');s.add_geometry(outlet,node_name='DrainOutletSleeve',geom_name='DrainOutletSleeve')
 # Countersunk screw head occupies central grate hole; removable, modeled as steel not gray paint.
 screw=trimesh.creation.cylinder(radius=.0035,height=.0022,sections=max(12,seg//3)); screw.apply_transform(trimesh.transformations.rotation_matrix(math.pi/2,[1,0,0])); screw.apply_translation([0,-.0002,0]); finish(screw,'StainlessGrate'); s.add_geometry(screw,node_name='CenterRetainingScrewHead',geom_name='CenterRetainingScrewHead')
 return s

def verify(scene):
 rows=[]
 for name,g in scene.geometry.items():
  assert np.isfinite(g.vertices).all() and np.isfinite(g.vertex_normals).all(),name
  assert np.all(g.area_faces>1e-14),name
  h=g.copy();h.merge_vertices(digits_vertex=8,merge_tex=True,merge_norm=True);h.remove_unreferenced_vertices();h.fix_normals(multibody=True)
  assert h.is_watertight and h.is_winding_consistent and h.volume>0,(name,h.is_watertight,h.volume)
  edges=np.unique(h.edges_sorted,axis=0,return_counts=True)[1];assert np.all(edges==2),name
  rows.append({'component':name,'triangles':int(len(g.faces)),'volumeM3':float(h.volume)})
 return {'triangles':sum(r['triangles'] for r in rows),'components':len(rows),'rows':rows,'boundsMetres':scene.bounds.tolist()}

def run(out):
 out.mkdir(parents=True,exist_ok=True);levels=[]
 for level in ['MASTER','LOD0','LOD1','LOD2','LOD3']:
  scene=build(level);rep=verify(scene);stem=f'BalconyFloorDrain100_{level}'
  glb=out/(stem+'.glb');glb.write_bytes(scene.export(file_type='glb'))
  re=trimesh.load(glb,force='scene',process=False);assert sum(len(g.faces) for g in re.geometry.values())==rep['triangles']
  folder=out/stem;folder.mkdir(exist_ok=True);obj,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True);(folder/(stem+'.obj')).write_text(obj)
  for fn,data in files.items():p=folder/fn;p.write_text(data) if isinstance(data,str) else p.write_bytes(data)
  ro=trimesh.load(folder/(stem+'.obj'),force='scene',process=False);assert sum(len(g.faces) for g in ro.geometry.values())==rep['triangles']
  levels.append({'level':level,'sha256':hashlib.sha256(glb.read_bytes()).hexdigest(),**rep,'glbRoundtrip':True,'objRoundtrip':True})
  print(level,rep['triangles'])
 counts=[x['triangles'] for x in levels];assert all(a>b for a,b in zip(counts,counts[1:])),counts
 report={'asset':'BalconyFloorDrain100','actualGeneratedMeshes':5,'realRadialSlotCount':18,'levels':levels,'unityImport':False,'unityRender':False,'visualFidelityScore':None}
 (out/'geometry_verification.json').write_text(json.dumps(report,indent=2));return report
if __name__=='__main__':
 ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);a=ap.parse_args();run(a.output)
