#!/usr/bin/env python3
from __future__ import annotations
import argparse, hashlib, json, math
from pathlib import Path
import numpy as np
import trimesh
import shapely
from PIL import Image
from shapely.geometry import Polygon, Point, box
from shapely.ops import unary_union

TAU=2*math.pi
MATS={
 'HandleWood':dict(c=[.47,.31,.17],r=.62,m=0,finish='dry unvarnished hardwood handle; longitudinal grain only'),
 'BroomFibre':dict(c=[.54,.39,.16],r=.86,m=0,finish='dry sorghum/corn-broom-like fibre, matte'),
 'BindingTwine':dict(c=[.36,.27,.15],r=.9,m=0,finish='rough natural-fibre binding'),
 'WarmGrayPP':dict(c=[.56,.57,.53],r=.56,m=0,finish='pigmented injection-molded polypropylene'),
 'BrushWood':dict(c=[.50,.34,.19],r=.65,m=0,finish='dry shaped hardwood block'),
 'BrushBristle':dict(c=[.19,.16,.12],r=.8,m=0,finish='dark stiff synthetic/natural bristle proxy'),
}

def unit(v):
 v=np.asarray(v,dtype=float);n=np.linalg.norm(v)
 if n<1e-12: raise ValueError('zero vector')
 return v/n

def cyl_between(a,b,r,sections=16):
 a=np.asarray(a,float);b=np.asarray(b,float);axis=b-a;h=np.linalg.norm(axis)
 m=trimesh.creation.cylinder(radius=r,height=h,sections=sections)
 m.apply_translation([0,0,h/2])
 m.apply_transform(trimesh.geometry.align_vectors([0,0,1],axis/h))
 m.apply_translation(a)
 return m

def tube(path,radii,sides=8,cap=True):
 path=np.asarray(path,float);radii=np.broadcast_to(np.asarray(radii,float),(len(path),))
 rings=[];prev=None
 for i,p in enumerate(path):
  t=unit(path[min(i+1,len(path)-1)]-path[max(i-1,0)])
  n=np.cross(t,[0,1,0]) if prev is None else prev-t*np.dot(prev,t)
  if np.linalg.norm(n)<1e-8:n=np.cross(t,[1,0,0])
  n=unit(n);bn=unit(np.cross(t,n));prev=n
  rings.append(np.array([p+radii[i]*(n*math.cos(TAU*j/sides)+bn*math.sin(TAU*j/sides)) for j in range(sides)]))
 vs=[];fs=[]
 for i in range(len(path)-1):
  for j in range(sides):
   k=(j+1)%sides;base=len(vs);vs.extend([rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j]]);fs.extend([[base,base+1,base+2],[base,base+2,base+3]])
 if cap:
  for end,normal_sign in [(0,-1),(-1,1)]:
   c=path[end]
   ring=rings[end]
   for j in range(sides):
    k=(j+1)%sides;base=len(vs)
    if normal_sign<0:vs.extend([c,ring[k],ring[j]])
    else:vs.extend([c,ring[j],ring[k]])
    fs.append([base,base+1,base+2])
 m=trimesh.Trimesh(vertices=np.asarray(vs),faces=np.asarray(fs),process=True)
 m.fix_normals();return m

def torus(R,r,major=32,minor=8,center=(0,0,0),axis=(0,1,0)):
 vs=[];fs=[]
 for i in range(major):
  a=TAU*i/major
  for j in range(minor):
   q=TAU*j/minor
   vs.append([(R+r*math.cos(q))*math.cos(a),r*math.sin(q),(R+r*math.cos(q))*math.sin(a)])
 for i in range(major):
  for j in range(minor):
   a=i*minor+j;b=i*minor+(j+1)%minor;c=((i+1)%major)*minor+(j+1)%minor;d=((i+1)%major)*minor+j
   fs += [[a,b,c],[a,c,d]]
 m=trimesh.Trimesh(vertices=vs,faces=fs,process=True)
 m.apply_transform(trimesh.geometry.align_vectors([0,1,0],unit(axis)))
 m.apply_translation(center);m.fix_normals();return m

def triangulate(poly):
 if poly.is_empty:return []
 if poly.geom_type=="MultiPolygon":return [a for p in poly.geoms for a in triangulate(p)]
 return [np.asarray(t.exterior.coords)[:3] for t in shapely.constrained_delaunay_triangles(poly).geoms]

def extrude_poly(poly,height):
 wv=[];wf=[]
 def tri(a,b,c):
  i=len(wv);wv.extend([a,b,c]);wf.append([i,i+1,i+2])
 def quad(a,b,c,d):tri(a,b,c);tri(a,c,d)
 for t in triangulate(poly):
  tri([t[0,0],t[0,1],0],[t[2,0],t[2,1],0],[t[1,0],t[1,1],0])
  tri([t[0,0],t[0,1],height],[t[1,0],t[1,1],height],[t[2,0],t[2,1],height])
 for ring in [poly.exterior,*poly.interiors]:
  pts=list(ring.coords)
  for a,b in zip(pts,pts[1:]):quad([a[0],a[1],0],[b[0],b[1],0],[b[0],b[1],height],[a[0],a[1],height])
 m=trimesh.Trimesh(vertices=np.asarray(wv),faces=np.asarray(wf),process=True);m.fix_normals();return m

def rounded_box(extents,radius,sections=4):
 x,y,z=extents
 poly=box(-x/2+radius,-z/2+radius,x/2-radius,z/2-radius).buffer(radius,quad_segs=sections)
 m=extrude_poly(poly,y)
 m.apply_transform(trimesh.geometry.align_vectors([0,0,1],[0,1,0]));m.apply_translation([0,-y/2,0]);m.fix_normals();return m

def material_image(spec,size=128,grain=False):
 yy,xx=np.mgrid[:size,:size];u=xx/size;v=yy/size
 phase=np.sin(TAU*(11*u+7*v))*np.sin(TAU*(5*u-13*v))
 if grain:phase=.7*np.sin(TAU*(17*v+.12*np.sin(TAU*u)))+.3*phase
 base=np.clip(np.array(spec['c'])[None,None,:]*(1+.018*phase[:,:,None]),0,1)
 rough=np.clip(spec['r']+.025*phase,.03,1)
 orm=np.stack([np.ones_like(rough),rough,np.full_like(rough,spec['m'])],axis=-1)
 return Image.fromarray(np.uint8(base*255)),Image.fromarray(np.uint8(orm*255))

def assign_mat(m,key):
 spec=MATS[key];base,orm=material_image(spec,grain='Wood' in key or key=='BroomFibre')
 uv=np.column_stack([m.vertices[:,0]+.35*m.vertices[:,2],m.vertices[:,1]])/.08
 pbr=trimesh.visual.material.PBRMaterial(name=key,baseColorTexture=base,metallicRoughnessTexture=orm,metallicFactor=1,roughnessFactor=1)
 m.visual=trimesh.visual.TextureVisuals(uv=uv,material=pbr)
 return m

def add(scene,name,m,mat):
 m=assign_mat(m,mat);scene.add_geometry(m,node_name=name,geom_name=name)

def broom(seg,bristles):
 s=trimesh.Scene()
 add(s,'WoodHandle',cyl_between([0,.18,0],[0,.91,0],.0115,max(12,seg)),'HandleWood')
 add(s,'WoodPommel',tube([[0,.895,0],[0,.913,0],[0,.925,0]],[.0115,.014,.008],max(8,seg//2)),'HandleWood')
 add(s,'BindingCollar',tube([[0,.16,0],[0,.28,0]],[.027,.019],max(8,seg//2)),'BindingTwine')
 for y in [.205,.225,.247]:add(s,f'BindingWrap_{y:.3f}',torus(.023-(y-.205)*.1,.0018,max(16,seg),max(6,seg//4),[0,y,0]),'BindingTwine')
 for i in range(bristles):
  phi=TAU*(i+.37)/bristles;layer=(i%7)/6
  top=np.array([.012*math.cos(phi),.19+.03*layer,.012*math.sin(phi)])
  fan=.10*(.25+.75*((i*37)%101)/100);side=np.array([math.cos(phi),0,.45*math.sin(phi)])
  end=top+side*fan+np.array([.004*math.sin(i*1.7),-(.19+.03*layer),.003*math.cos(i*2.3)])
  mid=(top+end)/2+np.array([.004*math.sin(phi*3),.012,.002*math.cos(phi*2)])
  add(s,f'Bristle_{i:03d}',tube([top,mid,end],[.00095,.00072,.00028],max(5,seg//4)),'BroomFibre')
 return s,dict(overallHeight=.924,handleDiameter=.023,bristleCount=bristles,fanWidthApprox=.22)

def dustpan(seg):
 s=trimesh.Scene();W=.265;D=.225;t=.0026
 floor=np.array([[-W/2,.004,0],[W/2,.004,0],[W/2,.036,D],[-W/2,.036,D],[-W/2,.004+t,0],[W/2,.004+t,0],[W/2,.036+t,D],[-W/2,.036+t,D]])
 faces=[[0,1,2],[0,2,3],[4,6,5],[4,7,6],[0,4,5],[0,5,1],[1,5,6],[1,6,2],[2,6,7],[2,7,3],[3,7,4],[3,4,0]]
 add(s,'SlopedScoopFloor',trimesh.Trimesh(floor,faces,process=True),'WarmGrayPP')
 add(s,'FrontLip',cyl_between([-W/2,.006,0],[W/2,.006,0],.0045,max(12,seg)),'WarmGrayPP')
 sidepoly=Polygon([(0,.004),(.225,.036),(.225,.088),(.045,.055),(0,.018)])
 for sign in [-1,1]:
  m=extrude_poly(sidepoly,t);v=m.vertices.copy();m.vertices=np.column_stack([np.full(len(v),sign*(W/2-t/2))+sign*(v[:,2]-t/2),v[:,1],v[:,0]])
  m.fix_normals();add(s,f'SideWall_{sign}',m,'WarmGrayPP')
 rear=rounded_box([W,.065,t],.008,max(2,seg//16));rear.apply_translation([0,.067,D-t/2]);add(s,'RearWall',rear,'WarmGrayPP')
 outer=box(-.028,0,.028,.235).buffer(.008,quad_segs=max(2,seg//16));hole=Point(0,.19).buffer(.010,quad_segs=max(3,seg//12));shape=outer.difference(hole)
 hand=extrude_poly(shape,.010);v=hand.vertices.copy();hand.vertices=np.column_stack([v[:,0],v[:,1]+.07,v[:,2]+D-.005]);hand.fix_normals();add(s,'HandleWithRealHangHole',hand,'WarmGrayPP')
 for x in [-.045,.045]:
  rib=rounded_box([.018,.055,.010],.004,max(2,seg//20));rib.apply_translation([x,.067,D-.008]);add(s,f'HandleRib_{x}',rib,'WarmGrayPP')
 return s,dict(width=W,depth=D,shellThickness=t,hangHoleDiameter=.020)

def scrubbrush(seg,tufts):
 s=trimesh.Scene();L=.185;W=.058;H=.022
 block=rounded_box([L,H,W],.012,max(3,seg//16));block.apply_translation([0,.061,0]);add(s,'ShapedWoodBlock',block,'BrushWood')
 grip=rounded_box([.11,.018,.032],.009,max(3,seg//16));grip.apply_translation([0,.081,0]);add(s,'PalmGrip',grip,'BrushWood')
 cols=max(4,int(math.sqrt(tufts*3)));rows=max(3,tufts//cols);count=0
 for iz in range(rows):
  for ix in range(cols):
   if count>=tufts:break
   x=-L*.42+(ix+.5)*L*.84/cols;z=-W*.38+(iz+.5)*W*.76/rows;sub=5 if tufts>40 else (3 if tufts>20 else 2)
   for j in range(sub):
    a=TAU*j/sub+count*.37;off=np.array([.0016*math.cos(a),0,.0016*math.sin(a)])
    top=np.array([x,.051,z])+off;end=np.array([x+.004*math.sin(count*.8+j),0,z+.003*math.cos(count*.6-j)])+off
    add(s,f'Tuft_{count:03d}_{j}',tube([top,(top+end)/2+np.array([0,.002,0]),end],[.0006,.00048,.00028],max(5,seg//5)),'BrushBristle')
   count+=1
 return s,dict(length=L,width=W,blockHeight=H,tuftCount=tufts)

def normalize(scene):
 dy=-scene.bounds[0,1]
 for g in scene.geometry.values():g.apply_translation([0,dy,0])

def verify(scene):
 parts=[]
 for name,g in scene.geometry.items():
  assert np.isfinite(g.vertices).all() and np.isfinite(g.vertex_normals).all(),name
  assert np.all(g.area_faces>1e-14),name
  h=g.copy();h.merge_vertices(digits_vertex=8,merge_tex=True,merge_norm=True);h.remove_unreferenced_vertices();h.fix_normals()
  assert h.is_watertight and h.is_winding_consistent and h.volume>0,(name,h.is_watertight,h.volume)
  edges=np.unique(h.edges_sorted,axis=0,return_counts=True)[1];assert np.all(edges==2),name
  parts.append(dict(name=name,triangles=len(g.faces),vertices=len(g.vertices),volumeM3=float(h.volume)))
 return dict(triangles=sum(p['triangles'] for p in parts),parts=parts,bounds=scene.bounds.tolist())

def export_scene(scene,path):
 path.write_bytes(scene.export(file_type='glb'));loaded=trimesh.load(path,force='scene',process=False)
 return sum(len(g.faces) for g in loaded.geometry.values()),loaded.bounds

def export_obj(scene,folder,stem):
 folder.mkdir(parents=True,exist_ok=True);obj,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True);(folder/(stem+'.obj')).write_text(obj)
 for name,data in files.items():
  p=folder/name;p.write_text(data) if isinstance(data,str) else p.write_bytes(data)
 reload=trimesh.load(folder/(stem+'.obj'),force='scene',process=False);return sum(len(g.faces) for g in reload.geometry.values())

def run(out):
 out.mkdir(parents=True,exist_ok=True)
 configs={'OutdoorBroom900':[(128,240),(96,160),(64,96),(32,48),(16,24)],'PlasticDustpan265':[(128,None),(96,None),(64,None),(32,None),(16,None)],'HandScrubBrush185':[(128,84),(96,64),(64,42),(32,24),(16,12)]}
 builders={'OutdoorBroom900':lambda n,q:broom(n,q),'PlasticDustpan265':lambda n,q:dustpan(n),'HandScrubBrush185':lambda n,q:scrubbrush(n,q)}
 labels=['MASTER','LOD0','LOD1','LOD2','LOD3'];assets=[]
 for asset,levels in configs.items():
  reports=[]
  for label,(seg,q) in zip(labels,levels):
   scene,spec=builders[asset](seg,q);normalize(scene);rep=verify(scene);stem=f'{asset}_{label}';glb=out/(stem+'.glb');cnt,bounds=export_scene(scene,glb);assert cnt==rep['triangles'];assert np.allclose(bounds,scene.bounds,atol=1e-6);objcnt=export_obj(scene,out/stem,stem);assert objcnt==cnt
   reports.append(dict(level=label,segments=seg,triangles=cnt,components=len(rep['parts']),boundsMetres=rep['bounds'],glbRoundtrip=True,objRoundtrip=True,sha256=hashlib.sha256(glb.read_bytes()).hexdigest()))
   print(asset,label,cnt,len(rep['parts']),flush=True)
  counts=[r['triangles'] for r in reports];assert all(a>b for a,b in zip(counts,counts[1:])),(asset,counts);assets.append(dict(asset=asset,measurements=spec,levels=reports))
 report=dict(status='EXTERNAL_GEOMETRY_CHECKED_NOT_VISUAL_PASS',assets=assets,unityCompile=False,unityImport=False,unityRender=False,performanceVerified=False,temporalVerified=False,visualFidelityScore=None,notes=['All components numerically closed/manifold after export source generation','Assembly contact/interference and bending mechanics are not exhaustively simulated','No Visual Fidelity points awarded'])
 (out/'geometry_verification.json').write_text(json.dumps(report,indent=2)+'\n');return report

if __name__=='__main__':
 ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);args=ap.parse_args();run(args.output)
