"""Authored small household assets. Metres, right-handed Y-up export, no scenes modified.
Requires numpy, shapely>=2.1, trimesh, Pillow. Native Unity and performance unverified.
Every aperture is a geometric hole, not a black decal. No external service or download.
"""
from __future__ import annotations
import argparse, json, math, hashlib
from pathlib import Path
import numpy as np
import shapely
from shapely.geometry import Polygon, Point, box
from shapely.ops import unary_union
import trimesh
from PIL import Image
TAU=2*math.pi
MATERIALS={
 'MintPE':dict(color=[.25,.43,.37],roughness=.57,metallic=0,F0=.04,finish='matte pigmented polyethylene; unbranded intact dry vessel'),
 'WarmWhitePP':dict(color=[.72,.70,.61],roughness=.53,metallic=0,F0=.04,finish='warm-white polypropylene; no invented macro dirt'),
 'DarkPP':dict(color=[.18,.23,.20],roughness=.55,metallic=0,F0=.04,finish='injection molded polypropylene handle and detachable rose'),
 'BrushedStainless':dict(color=[.63,.65,.66],roughness=.34,metallic=1,F0=None,finish='unpainted stainless perforated sheet'),
 'DryBeech':dict(color=[.53,.36,.20],roughness=.64,metallic=0,F0=.04,finish='unvarnished wood; longitudinal material grain'),
 'SpringSteel':dict(color=[.44,.46,.48],roughness=.37,metallic=1,F0=None,finish='intact bright zinc-plated spring steel'),
}
def unit(a):
 a=np.asarray(a,dtype=float);n=np.linalg.norm(a)
 if n<1e-12:raise ValueError('zero direction')
 return a/n
def mesh(v,f):
 m=trimesh.Trimesh(vertices=np.asarray(v),faces=np.asarray(f),process=False)
 m.merge_vertices(digits_vertex=9);m.remove_unreferenced_vertices()
 m.update_faces(m.nondegenerate_faces(height=1e-11));m.remove_unreferenced_vertices()
 m.fix_normals(multibody=True)
 return m
class Writer:
 def __init__(self):self.v=[];self.f=[]
 def tri(self,*p):
  i=len(self.v);self.v.extend(p);self.f.append([i,i+1,i+2])
 def quad(self,a,b,c,d):self.tri(a,b,c);self.tri(a,c,d)
 def finish(self):return mesh(self.v,self.f)
def lathe(profile,n,scalez=1.):
 w=Writer()
 for j in range(len(profile)-1):
  for i in range(n):
   points=[]
   for k,ii in [(j,i),(j,i+1),(j+1,i+1),(j+1,i)]:
    r,y=profile[k];a=-math.pi+TAU*ii/n
    points.append([r*math.cos(a),y,scalez*r*math.sin(a)])
   w.quad(*points)
 return w.finish()
def tube(path,radius,sides,cap=True):
 path=np.asarray(path);rr=np.broadcast_to(radius,(len(path),));frames=[];previous=None
 for i in range(len(path)):
  t=unit(path[min(len(path)-1,i+1)]-path[max(0,i-1)])
  n=np.cross(t,[0,1,0]) if previous is None else previous-t*np.dot(previous,t)
  if np.linalg.norm(n)<1e-7:n=np.cross(t,[1,0,0])
  n=unit(n);bn=unit(np.cross(t,n));frames.append((n,bn));previous=n
 rings=[]
 for p,r,(a,b) in zip(path,rr,frames):rings.append([p+r*(a*math.cos(TAU*j/sides)+b*math.sin(TAU*j/sides)) for j in range(sides)])
 w=Writer()
 for i in range(len(path)-1):
  for j in range(sides):k=(j+1)%sides;w.quad(rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j])
 if cap:
  for j in range(sides):
   k=(j+1)%sides;w.tri(path[0],rings[0][k],rings[0][j]);w.tri(path[-1],rings[-1][j],rings[-1][k])
 return w.finish()
def sections(profile,centre,axis,n):
 m=lathe(profile,n);axis=unit(axis);a=np.cross([0,0,1],axis)
 if np.linalg.norm(a)<1e-8:a=np.cross([1,0,0],axis)
 a=unit(a);b=np.cross(a,axis)
 # Local Y follows axis; determinant +1.
 basis=np.stack([a,axis,b],axis=1)
 m.vertices=m.vertices@basis.T+np.asarray(centre)
 return m
def triangulate(poly):
 if poly.is_empty:return []
 if poly.geom_type=='MultiPolygon':return [a for p in poly.geoms for a in triangulate(p)]
 return [np.asarray(t.exterior.coords)[:3] for t in shapely.constrained_delaunay_triangles(poly).geoms]
def extrude(poly,z0,z1,transform=None):
 if not poly.is_valid:raise ValueError('invalid section')
 w=Writer()
 for t in triangulate(poly):w.tri(*[[x,y,z0] for x,y in t[::-1]]);w.tri(*[[x,y,z1] for x,y in t])
 for ring in [poly.exterior,*poly.interiors]:
  points=list(ring.coords)
  for a,b in zip(points,points[1:]):w.quad([*a,z0],[*b,z0],[*b,z1],[*a,z1])
 m=w.finish()
 if transform is not None:m.apply_transform(transform)
 return m
def band(R,y0,y1,n,holes,scalez=1.,taper=0.):
 """Thin wall parameter surface. Returns surface and actual hole boundary points."""
 raw=Writer();H=unary_union(holes)
 for i in range(n):
  s0=-math.pi*R+TAU*R*i/n;s1=-math.pi*R+TAU*R*(i+1)/n
  poly=box(s0,y0,s1,y1).difference(H)
  for t in triangulate(poly):raw.tri(*[[x,y,0] for x,y in t])
 planar=raw.finish()
 # Use boundary edge graph, excluding rectangular strip perimeter.
 edges=planar.edges_sorted;unique,count=np.unique(edges,axis=0,return_counts=True)
 boundary=unique[count==1];rings=[]
 inner=[]
 for a,b in boundary:
  p,q=planar.vertices[[a,b],:2]
  outer=(abs(p[0]+math.pi*R)<1e-8 and abs(q[0]+math.pi*R)<1e-8) or (abs(p[0]-math.pi*R)<1e-8 and abs(q[0]-math.pi*R)<1e-8) or (abs(p[1]-y0)<1e-8 and abs(q[1]-y0)<1e-8) or (abs(p[1]-y1)<1e-8 and abs(q[1]-y1)<1e-8)
  if not outer:inner.append((a,b))
 graph={}
 for a,b in inner:graph.setdefault(a,[]).append(b);graph.setdefault(b,[]).append(a)
 seen=set()
 for first in graph:
  if first in seen:continue
  order=[first];prev=None;current=first
  while True:
   if len(graph[current])!=2:raise ValueError('nonmanifold hole boundary')
   nxt=[x for x in graph[current] if x!=prev][0]
   if nxt==first:break
   order.append(nxt);prev,current=current,nxt
   if len(order)>len(graph):raise ValueError('boundary cycle')
  seen.update(order);rings.append(planar.vertices[order,:2])
 def map_uv(uv,rdelta=0.):
  uv=np.asarray(uv);a=uv[:,0]/R;yy=uv[:,1];r=R-taper*(y1-yy)+rdelta
  return np.stack([r*np.cos(a),yy,r*scalez*np.sin(a)],axis=1)
 out=mesh(map_uv(planar.vertices[:,:2]),planar.faces)
 return out,planar,map_uv,rings
def texture(spec,kind,size=128):
 # Subtle deterministic microstructure, never directional lighting.
 yy,xx=np.mgrid[:size,:size];u=xx/size;v=yy/size
 phase=(np.sin(TAU*(13*u+5*v))*np.sin(TAU*(17*v-7*u)))
 if kind=='DryBeech':phase=.65*np.sin(TAU*(11*u+.12*np.sin(TAU*v)))+.35*phase
 base=np.clip(np.array(spec['color'])[None,None,:]*(1+.014*phase[:,:,None]),0,1)
 rough=np.clip(spec['roughness']+.025*phase,.04,1)
 orm=np.stack([np.ones_like(rough),rough,np.ones_like(rough)*spec['metallic']],axis=-1)
 return Image.fromarray(np.uint8(base*255)),Image.fromarray(np.uint8(orm*255))
def add(scene,name,m,mat):
 assert m.is_watertight,(name,'not closed')
 assert m.is_winding_consistent and m.volume>0,(name,'orientation')
 m=trimesh.graph.smooth_shade(m,angle=math.radians(48),facet_minarea=None)
 spec=MATERIALS[mat];base,orm=texture(spec,mat)
 # Local metric projection is a starting layout, seam response still needs Unity lookdev.
 uv=np.column_stack([m.vertices[:,0]+m.vertices[:,2]*.73,m.vertices[:,1]])/.08
 if mat=='DryBeech':uv=np.column_stack([m.vertices[:,2]+.73*m.vertices[:,1],m.vertices[:,0]])/.08
 pbr=trimesh.visual.material.PBRMaterial(name=mat,baseColorTexture=base,metallicRoughnessTexture=orm,metallicFactor=1,roughnessFactor=1)
 m.visual=trimesh.visual.TextureVisuals(uv=uv,material=pbr)
 scene.add_geometry(m,node_name=name,geom_name=name)
def watering(n):
 scene=trimesh.Scene();R=.09;th=.0028;y0=.027;y1=.158
 hole=Point(0,.068).buffer(.014,quad_segs=max(4,n//8))
 outer,flat,mapper,rings=band(R,y0,y1,n,[hole])
 inner=mesh(mapper(flat.vertices[:,:2],-th),flat.faces[:,::-1])
 top=lathe([(R,y1),(.089,.168),(.081,.179),(.06,.192),(.039,.201),(.039,.211),(.038,.213),(.0358,.213),(.035,.211),(.035,.201),(.057,.189),(.078,.176),(R-th,.158)],n)
 bottom=lathe([(R-th,y0),(.0855,.011),(.080,.006),(0,.006),(0,.001),(.080,.001),(.086,.004),(.089,.010),(R,y0)],n)
 assert len(rings)==1
 uv=rings[0];angles=np.arctan2(uv[:,1]-.068,uv[:,0]);idx=np.argsort(angles);uv=uv[idx];angles=angles[idx]
 # Real communicating port: body wall removed; tube starts exactly at its boundary.
 startout=mapper(uv);startin=mapper(uv,-th)
 centres=np.array([[.106,.069,0],[.126,.079,0],[.155,.100,0],[.19,.13,0],[.22,.163,0],[.253,.197,0],[.276,.221,0]])
 axis=unit(centres[-1]-centres[-2]);along=unit([0,0,1]);up=np.cross(along,axis)
 OR=[startout];IR=[startin]
 for i,p in enumerate(centres):
  tangent=unit(centres[min(len(centres)-1,i+1)]-centres[max(i-1,0)])
  vv=unit(np.cross([0,0,1],tangent));radius=.014-(.004*i/(len(centres)-1))
  direction=np.cos(angles)[:,None]*[0,0,1]+np.sin(angles)[:,None]*vv
  OR.append(p+direction*radius);IR.append(p+direction*(radius-th))
 w=Writer();N=len(uv)
 for ss in [OR,IR]:
  for k in range(len(ss)-1):
   for j in range(N):q=(j+1)%N;w.quad(ss[k][j],ss[k][q],ss[k+1][q],ss[k+1][j])
 for j in range(N):q=(j+1)%N;w.quad(OR[-1][j],OR[-1][q],IR[-1][q],IR[-1][j])
 body=trimesh.util.concatenate([outer,inner,top,bottom,w.finish()]);body=mesh(body.vertices,body.faces)
 add(scene,'HollowVesselAndCommunicatingSpout',body,'MintPE')
 # Detachable perforated rose, body kept separate with a documented insertion fit.
 end=centres[-1];rose=sections([(.0101,-.014),(.013,-.014),(.013,0),(.023,.012),(.037,.026),(.04,.033),(.04,.038),(.0375,.038),(.0375,.033),(.034,.028),(.020,.014),(.0101,.003),(.0101,-.014)],end,axis,n)
 add(scene,'RoseSocketAndHollowHousing',rose,'DarkPP')
 perforations=[Point(0,0).buffer(.001,quad_segs=max(2,n//32))]
 for r,k in [(.009,8),(.018,14),(.027,20),(.033,24)]:
  for i in range(k):a=TAU*i/k+.1;perforations.append(Point(r*math.cos(a),r*math.sin(a)).buffer(.001,quad_segs=max(2,n//32)))
 face=Point(0,0).buffer(.0376,quad_segs=n//4).difference(unary_union(perforations))
 disk=extrude(face,0,.0006)
 a=unit(np.cross([0,0,1],axis));b=np.cross(axis,a)
 disk.vertices=disk.vertices@np.stack([a,b,axis],axis=1).T+end+axis*.037
 add(scene,'RoseStainlessFace_67RealHoles',disk,'BrushedStainless')
 # Detachable solid PP handle, two terminal bosses meet vessel as attachment interfaces.
 theta=np.linspace(-math.pi/2,math.pi/2,max(12,n//2))
 path=np.stack([-.088-.058*np.cos(theta),.104+.075*np.sin(theta),np.zeros(len(theta))],axis=1)
 add(scene,'RearPouringHandle',tube(path,.0072,max(6,n//8)),'DarkPP')
 for i,y in enumerate([.029,.179]):
  p=[-.087,y,0];add(scene,'HandleMountBoss'+str(i),sections([(0,-.009),(.011,-.009),(.012,-.006),(.012,.006),(.009,.009),(0,.009)],p,[1,0,0],max(12,n//2)),'MintPE')
 # Integral raised standing rib, no stain or baked-contact shadow.
 add(scene,'StandingHeel',lathe([(.078,0),(.082,0),(.084,.001),(.084,.005),(.081,.006),(.078,.006),(.078,0)],n),'MintPE')
 return scene,{'roseHoleCount':len(perforations),'bodyPortHoles':1,'shellThicknessMetres':th,'fillMouthDiameterMetres':.07}
def basket(n):
 scene=trimesh.Scene();R=.11;scale=.072/.11;th=.0028;y0=.012;y1=.107;taper=.15
 holes=[]
 for i in range(32):
  s=-math.pi*R+TAU*R*(i+.5)/32
  # One full capsule per molding slot; rounded corners are real topology.
  holes.append(box(s-.005,.034,s+.005,.084).buffer(.002,quad_segs=max(2,n//32)))
 outer,flat,mapper,rings=band(R,y0,y1,n,holes,scale,taper)
 inner=mesh(mapper(flat.vertices[:,:2],-th),flat.faces[:,::-1]);w=Writer()
 for uv in rings:
  a=mapper(uv);b=mapper(uv,-th)
  for i in range(len(uv)):k=(i+1)%len(uv);w.quad(a[i],a[k],b[k],b[i])
 top=lathe([(R,y1),(R+.002,.109),(R+.002,.113),(R,.115),(R-.0028,.115),(R-.0045,.113),(R-.0045,.109),(R-th,y1)],n,scale)
 bottomR=R-taper*(y1-y0)
 bottom=lathe([(bottomR-th,y0),(bottomR-th-.002,.008),(0,.008),(0,.004),(bottomR-.002,.004),(bottomR,.006),(bottomR,y0)],n,scale)
 body=trimesh.util.concatenate([outer,inner,w.finish(),top,bottom]);body=mesh(body.vertices,body.faces)
 add(scene,'MoldedBasketShell_32OpenSlots',body,'WarmWhitePP')
 # Open handle hoop: bent PP rod, captured on fixed pivot bosses.
 t=np.linspace(0,math.pi,max(16,n//2));path=np.stack([np.zeros(len(t)),.112+.101*np.sin(t),.079*np.cos(t)],axis=1)
 add(scene,'SwingCarryHandle',tube(path,.0045,max(6,n//8)),'MintPE')
 for sign in [-1,1]:
  # Washer and shaft intended to insert through handle terminal sphere/eye.
  c=[0,.108,sign*.074]
  add(scene,'PivotBoss_'+str(sign),sections([(0,-.006),(.005,-.006),(.006,-.002),(.006,.001),(.0022,.001),(.0022,.008),(.004,.008),(.004,.009),(0,.009)],c,[0,0,sign],max(12,n//2)),'WarmWhitePP')
  add(scene,'HandlePivotEye_'+str(sign),sections([(.0025,-.0015),(.0055,-.0015),(.006,-.0008),(.006,.0008),(.0055,.0015),(.0025,.0015),(.0025,-.0015)],[0,.108,sign*.079],[0,0,sign],max(12,n//2)),'MintPE')
 return scene,{'sideDrainSlots':32,'nominalWidthMetres':.224,'nominalDepthMetres':.1466,'shellThicknessRadialMetres':th}
def clothespin(n):
 scene=trimesh.Scene()
 # Sawn/milled wooden jaws: section in XY, extrusion along Z. Real gripping teeth.
 upper=[(-.035,.0058),(-.033,.0078),(-.011,.0078),(-.007,.008),(-.004,.0062),(.004,.0062),(.007,.008),(.031,.014),(.035,.0128),(.035,.0085),(.007,.0028),(-.011,.0015),(-.013,.00045),(-.015,.00125),(-.017,.00045),(-.019,.00125),(-.021,.00045),(-.023,.00125),(-.025,.00045),(-.027,.00125),(-.029,.00045),(-.031,.00125),(-.033,.00045),(-.035,.00045)]
 p=Polygon(upper).difference(Point(0,0).buffer(.00385,quad_segs=max(3,n//16)))
 # Real concave coil seat; do not run spring through uncut wood.
 for sign in [-1,1]:
  m=extrude(p,-.0045,.0045)
  if sign<0:m.vertices[:,1]*=-1;m.faces=m.faces[:,::-1]
  # Lift onto the lower tip so it is a tabletop asset, not below ground.
  m.vertices[:,1]+=.0155
  add(scene,'MilledWoodJaw_'+str(sign),m,'DryBeech')
 coilR=.0031;wire=.00055;turns=5.2
 t=np.linspace(0,TAU*turns,max(48,n*3));z=np.linspace(-.0048,.0048,len(t))
 path=np.column_stack([coilR*np.cos(t),.0155+coilR*np.sin(t),z])
 # Two tangent legs carry spring load onto jaw backs; bends included, not free endpoints.
 start=path[0];end=path[-1]
 before=[np.array([.012,.0253,-.0035]),np.array([.012,.0253,-.0054]),np.array([.008,.0235,-.0054]),start]
 after=[end,np.array([.008,.0075,.0054]),np.array([.012,.0057,.0054]),np.array([.012,.0057,.0035])]
 springpath=np.vstack([before[:-1],path,after[1:]])
 add(scene,'FiveTurnTorsionSpringAndLegs',tube(springpath,wire,max(6,n//12)),'SpringSteel')
 return scene,{'lengthMetres':.07,'jawPieces':2,'springWireDiameterMetres':.0011,'springTurns':turns,'mechanicalSimulationVerified':False}
def verify_scene(scene):
 results=[]
 for name,g in scene.geometry.items():
  assert np.isfinite(g.vertices).all() and np.isfinite(g.vertex_normals).all()
  assert np.all(g.area_faces>1e-14),(name,'degenerate')
  welded=g.copy();welded.merge_vertices(digits_vertex=8,merge_tex=True,merge_norm=True)
  assert welded.is_watertight and welded.is_winding_consistent and welded.volume>0,(name,'boundary')
  boundary=np.unique(welded.edges_sorted,axis=0,return_counts=True)[1]
  assert np.all(boundary==2),(name,'edge incidence')
  results.append(dict(component=name,vertices=len(g.vertices),triangles=len(g.faces),boundaryEdges=0,nonmanifoldEdges=0,materialVolumeM3=float(g.volume)))
 return dict(triangles=sum(x['triangles'] for x in results),components=results,boundsMetres=scene.bounds.tolist())
def run(out):
 out.mkdir(parents=True,exist_ok=True);entries=[]
 for name,builder in [('WateringCanCompact',watering),('ClothespinBasket224',basket),('WoodenClothespin70',clothespin)]:
  levels=[]
  for label,n in [('MASTER',128),('LOD0',96),('LOD1',64),('LOD2',32),('LOD3',16)]:
   scene,spec=builder(n)
   # Tabletop origin, no floating base in a scene at identity transform.
   dy=-float(scene.bounds[0,1])
   for g in scene.geometry.values():g.apply_translation([0,dy,0])
   report=verify_scene(scene);stem=name+'_'+label
   glb=out/(stem+'.glb');glb.write_bytes(scene.export(file_type='glb'))
   reloaded=trimesh.load(glb,force='scene',process=False)
   assert sum(len(m.faces) for m in reloaded.geometry.values())==report['triangles']
   assert np.allclose(reloaded.bounds,scene.bounds,atol=1e-6)
   folder=out/stem;folder.mkdir(exist_ok=True)
   obj,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True)
   (folder/(stem+'.obj')).write_text(obj)
   for f,data in files.items():
    p=folder/f;p.write_text(data) if isinstance(data,str) else p.write_bytes(data)
   obj_reload=trimesh.load(folder/(stem+'.obj'),force='scene',process=False)
   assert sum(len(m.faces) for m in obj_reload.geometry.values())==report['triangles']
   levels.append(dict(level=label,segments=n,sha256=hashlib.sha256(glb.read_bytes()).hexdigest(),glbRoundtrip=True,objRoundtrip=True,**report))
   print(name,label,report['triangles'],flush=True)
  counts=[x['triangles'] for x in levels];assert all(a>b for a,b in zip(counts,counts[1:])),counts
  entries.append(dict(asset=name,designMeasurements=spec,levels=levels))
 report=dict(status='EXTERNAL_GEOMETRY_CHECKED_NOT_VISUAL_PASS',assets=entries,unityCompile=False,unityImport=False,unityRender=False,performanceVerified=False,temporalVerified=False,allPartIntersectionsVerified=False,visualFidelityScore=None)
 (out/'geometry_verification.json').write_text(json.dumps(report,indent=2))
 return report
if __name__=='__main__':
 ap=argparse.ArgumentParser();ap.add_argument('--output',required=True,type=Path);args=ap.parse_args();run(args.output)
