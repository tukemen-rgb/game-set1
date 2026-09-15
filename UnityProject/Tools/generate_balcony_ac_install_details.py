"""Generate balcony AC installation-detail assets for game-set1.
Metres, Y-up. Original unbranded geometry; dimensions are modeling assumptions,
not a claim about a specific historical product. Produces MASTER + LOD0..3 GLB/OBJ.
Requires numpy, trimesh, Pillow. No network calls and no Unity scene mutation.
"""
from __future__ import annotations
import argparse, hashlib, json, math
from pathlib import Path
import numpy as np
import trimesh
from PIL import Image

TAU=math.tau
LEVELS={
 'MASTER':dict(path=128,sides=18,cross=32,ring=72,nut=12),
 'LOD0':dict(path=88,sides=14,cross=24,ring=52,nut=10),
 'LOD1':dict(path=56,sides=10,cross=20,ring=36,nut=8),
 'LOD2':dict(path=34,sides=8,cross=16,ring=26,nut=6),
 'LOD3':dict(path=22,sides=6,cross=12,ring=18,nut=6),
}
MATERIALS={
 'CopperTube':dict(color=[0.55,0.24,0.09],roughness=.32,metallic=1,normal=.05),
 'InsulationGray':dict(color=[0.24,0.25,0.23],roughness=.72,metallic=0,normal=.10),
 'BrassFlareNut':dict(color=[0.46,0.30,0.10],roughness=.34,metallic=1,normal=.035),
 'PipeCoverPVC':dict(color=[0.79,0.77,0.69],roughness=.58,metallic=0,normal=.07),
 'SealingPutty':dict(color=[0.64,0.62,0.56],roughness=.82,metallic=0,normal=.16),
 'SleevePVC':dict(color=[0.70,0.70,0.66],roughness=.64,metallic=0,normal=.06),
 'BlackEPDM':dict(color=[0.018,0.020,0.019],roughness=.80,metallic=0,normal=.09),
}
_TEX={}

def unit(v):
 v=np.asarray(v,float); n=np.linalg.norm(v)
 if n<1e-12: raise ValueError('zero vector')
 return v/n

def finalize(v,f):
 m=trimesh.Trimesh(vertices=np.asarray(v,float),faces=np.asarray(f,int),process=False)
 m.merge_vertices(digits_vertex=10)
 m.update_faces(m.nondegenerate_faces(height=1e-12)); m.remove_unreferenced_vertices(); m.fix_normals(multibody=True)
 return m

def textures(mat,size=128):
 if mat in _TEX:return _TEX[mat]
 s=MATERIALS[mat]; yy,xx=np.mgrid[:size,:size];u=xx/size;v=yy/size
 h=.50*np.sin(TAU*(19*u+3*v))+.31*np.sin(TAU*(31*v-7*u))+.19*np.sin(TAU*(11*u+13*v))
 base=np.clip(np.asarray(s['color'])[None,None,:]*(1+.009*h[:,:,None]),0,1)
 rough=np.clip(s['roughness']+.018*h,.04,1); mr=np.stack([np.ones_like(rough),rough,np.full_like(rough,s['metallic'])],-1)
 gy,gx=np.gradient(h);nx=-gx*s['normal'];ny=-gy*s['normal'];nz=np.ones_like(nx);norm=np.stack([nx,ny,nz],-1);norm/=np.linalg.norm(norm,axis=-1,keepdims=True)
 out=(Image.fromarray(np.uint8(base*255)),Image.fromarray(np.uint8(mr*255)),Image.fromarray(np.uint8((norm*.5+.5)*255)))
 _TEX[mat]=out;return out

def materialize(scene,name,m,mat,uv_scale=.05):
 base,mr,norm=textures(mat)
 p=trimesh.visual.material.PBRMaterial(name=mat,baseColorTexture=base,metallicRoughnessTexture=mr,normalTexture=norm,metallicFactor=1,roughnessFactor=1,doubleSided=False)
 uv=np.column_stack([m.vertices[:,0]+.37*m.vertices[:,2],m.vertices[:,1]+.19*m.vertices[:,2]])/uv_scale
 m.visual=trimesh.visual.TextureVisuals(uv=uv,material=p)
 scene.add_geometry(m,node_name=name,geom_name=name)

def sweep(path,radius,sides,inner_radius=None,closed=False):
 pts=np.asarray(path,float)
 if closed:tang=np.roll(pts,-1,axis=0)-np.roll(pts,1,axis=0)
 else:tang=np.vstack([pts[1]-pts[0],pts[2:]-pts[:-2],pts[-1]-pts[-2]])
 tang/=np.linalg.norm(tang,axis=1)[:,None]
 frames=[];prev=None
 for t in tang:
  n=np.cross(t,[0,1,0]) if prev is None else prev-t*np.dot(prev,t)
  if np.linalg.norm(n)<1e-8:n=np.cross(t,[1,0,0])
  n=unit(n);b=np.cross(t,n);frames.append([n,b]);prev=n
 frames=np.asarray(frames); ang=np.arange(sides)*TAU/sides
 rr=np.broadcast_to(np.asarray(radius,float),(len(pts),))
 outer=pts[:,None,:]+rr[:,None,None]*(frames[:,0,None,:]*np.cos(ang)[None,:,None]+frames[:,1,None,:]*np.sin(ang)[None,:,None])
 v=list(outer.reshape(-1,3));f=[]
 for i in range(len(pts) if closed else len(pts)-1):
  k=(i+1)%len(pts)
  for j in range(sides):
   q=(j+1)%sides;a=i*sides+j;b=i*sides+q;c=k*sides+q;d=k*sides+j;f.extend([[a,b,c],[a,c,d]])
 if inner_radius is None:
  if not closed:
   for ri,rev in [(0,True),(len(pts)-1,False)]:
    c=len(v);v.append(pts[ri].tolist())
    for j in range(sides):
     a=ri*sides+j;b=ri*sides+(j+1)%sides;f.append([c,b,a] if rev else [c,a,b])
 else:
  ir=np.broadcast_to(np.asarray(inner_radius,float),(len(pts),))
  inner=pts[:,None,:]+ir[:,None,None]*(frames[:,0,None,:]*np.cos(ang)[None,:,None]+frames[:,1,None,:]*np.sin(ang)[None,:,None])
  off=len(v);v.extend(inner.reshape(-1,3).tolist())
  for i in range(len(pts)-1 if not closed else len(pts)):
   k=(i+1)%len(pts)
   for j in range(sides):
    q=(j+1)%sides;a=off+i*sides+j;b=off+k*sides+j;c=off+k*sides+q;d=off+i*sides+q;f.extend([[a,b,c],[a,c,d]])
  if not closed:
   for ri in [0,len(pts)-1]:
    for j in range(sides):
     q=(j+1)%sides;ao=ri*sides+j;bo=ri*sides+q;ai=off+ri*sides+j;bi=off+ri*sides+q
     if ri==0:f.extend([[ao,ai,bi],[ao,bi,bo]])
     else:f.extend([[ao,bo,bi],[ao,bi,ai]])
 return finalize(v,f)

def path_interp(ctrl,n):
 ctrl=np.asarray(ctrl,float); d=np.linalg.norm(np.diff(ctrl,axis=0),axis=1); t=np.r_[0,np.cumsum(d)]; t/=t[-1]
 q=np.linspace(0,1,n); out=np.column_stack([np.interp(q,t,ctrl[:,i]) for i in range(3)])
 # two Chaikin-like smoothing passes while keeping endpoints exact
 for _ in range(3):
  sm=out.copy(); sm[1:-1]=(out[:-2]+2*out[1:-1]+out[2:])/4; out=sm
 out[0]=ctrl[0];out[-1]=ctrl[-1];return out

def hollow_hex(axis_center,length,outer_radius,inner_radius,axis='x'):
 n=6;ang=np.arange(n)*TAU/n+math.pi/6
 # build along local z then orient
 v=[];f=[]
 for z in [-length/2,length/2]:
  for r in [outer_radius,inner_radius]:
   for a in ang:v.append([r*math.cos(a),r*math.sin(a),z])
 # indices: end0 outer 0..5, inner6..11; end1 outer12..17 inner18..23
 for side in [0,1]:
  oo=side*12;ii=oo+6
  for j in range(n):
   q=(j+1)%n
   if side==0:f.extend([[oo+j,ii+j,ii+q],[oo+j,ii+q,oo+q]])
   else:f.extend([[oo+j,oo+q,ii+q],[oo+j,ii+q,ii+j]])
 for j in range(n):
  q=(j+1)%n;f.extend([[j,12+j,12+q],[j,12+q,q]]);f.extend([[6+j,6+q,18+q],[6+j,18+q,18+j]])
 m=finalize(v,f)
 if axis=='x':m.apply_transform(trimesh.transformations.rotation_matrix(math.pi/2,[0,1,0]))
 elif axis=='y':m.apply_transform(trimesh.transformations.rotation_matrix(math.pi/2,[1,0,0]))
 m.apply_translation(axis_center);return m

def cylinder_shell(center,length,outer,inner,sections,axis='z'):
 if axis=='z':path=np.array([[center[0],center[1],center[2]-length/2],[center[0],center[1],center[2]+length/2]])
 elif axis=='x':path=np.array([[center[0]-length/2,center[1],center[2]],[center[0]+length/2,center[1],center[2]]])
 else:path=np.array([[center[0],center[1]-length/2,center[2]],[center[0],center[1]+length/2,center[2]]])
 return sweep(path,outer,sections,inner)

def build_lineset(level):
 c=LEVELS[level];sc=trimesh.Scene();n=c['path'];s=c['sides']
 specs=[
  ('Gas952',.00476,.0125,[0,.025,0],[.2320,.029,.1265]),
  ('Liquid635',.003175,.0095,[0,-.025,0],[.2320,-.001,.1265]),
 ]
 for name,copper_r,ins_r,start,end in specs:
  ctrl=[start,[.045,start[1],.006],[.095,start[1]*.85,.026],[.155,(start[1]+end[1])*.55,.075],[.205,end[1],.112],end]
  path=path_interp(ctrl,n)
  materialize(sc,name+'_Copper',sweep(path,copper_r,s),'CopperTube',.03)
  # insulation starts after flare/visible copper; inner bore touches copper OD without replacing it
  k=max(4,int(.18*n));ipath=path[k:]
  materialize(sc,name+'_Insulation',sweep(ipath,ins_r,s,max(copper_r+.00035,ins_r-.0055)),'InsulationGray',.05)
  # hollow hex flare nut seated over initial copper; open bore is real geometry
  nut_outer=.0105 if name.startswith('Gas') else .0082;nut_inner=copper_r+.00055
  nut=hollow_hex([.012,start[1],0],.018,nut_outer,nut_inner,'x');materialize(sc,name+'_FlareNut',nut,'BrassFlareNut',.025)
  # short elastomer anti-chafe boot at insulation start
  boot_path=path[k-2:k+2] if k+2<len(path) else path[k-2:k+1]
  materialize(sc,name+'_TransitionBoot',sweep(boot_path,ins_r+.0008,max(6,s//2),max(copper_r+.0002,ins_r-.0048)),'BlackEPDM',.03)
 return sc

def rounded_rect_points(w,d,r,n):
 # clockwise rounded rectangle in local u/v plane
 q=max(2,n//4);pts=[]
 centers=[(w/2-r,d/2-r,0,math.pi/2),(-w/2+r,d/2-r,math.pi/2,math.pi),(-w/2+r,-d/2+r,math.pi,3*math.pi/2),(w/2-r,-d/2+r,3*math.pi/2,TAU)]
 for cx,cy,a0,a1 in centers:
  for a in np.linspace(a0,a1,q,endpoint=False):pts.append([cx+r*math.cos(a),cy+r*math.sin(a)])
 return np.asarray(pts)

def rectangular_tube_sweep(path,w,d,wall,cross):
 pts=np.asarray(path,float);u=np.array([1.,0,0]);outer2=rounded_rect_points(w,d,.007,cross);inner2=rounded_rect_points(w-2*wall,d-2*wall,.0048,cross);N=len(outer2)
 tang=np.vstack([pts[1]-pts[0],pts[2:]-pts[:-2],pts[-1]-pts[-2]]);tang/=np.linalg.norm(tang,axis=1)[:,None]
 verts=[]
 for p,t in zip(pts,tang):
  uu=u-t*np.dot(u,t);uu=unit(uu);vv=np.cross(t,uu)
  for xy in outer2:verts.append((p+uu*xy[0]+vv*xy[1]).tolist())
  for xy in inner2:verts.append((p+uu*xy[0]+vv*xy[1]).tolist())
 f=[];stride=2*N
 for i in range(len(pts)-1):
  for j in range(N):
   q=(j+1)%N;a=i*stride+j;b=i*stride+q;c=(i+1)*stride+q;d0=(i+1)*stride+j;f.extend([[a,b,c],[a,c,d0]])
   ai=i*stride+N+j;bi=(i+1)*stride+N+j;ci=(i+1)*stride+N+q;di=i*stride+N+q;f.extend([[ai,bi,ci],[ai,ci,di]])
 for i in [0,len(pts)-1]:
  base=i*stride
  for j in range(N):
   q=(j+1)%N;ao=base+j;bo=base+q;ai=base+N+j;bi=base+N+q
   if i==0:f.extend([[ao,ai,bi],[ao,bi,bo]])
   else:f.extend([[ao,bo,bi],[ao,bi,ai]])
 return finalize(verts,f)

def build_wall_elbow(level):
 c=LEVELS[level];sc=trimesh.Scene();R=.055
 a=np.linspace(0,math.pi/2,max(8,c['path']//4));path=np.column_stack([np.zeros_like(a),R*np.sin(a),R*(1-np.cos(a))])
 body=rectangular_tube_sweep(path,.064,.055,.0022,c['cross']);materialize(sc,'HollowMoldedWallElbow',body,'PipeCoverPVC',.08)
 # joint band masks the practical snap junction and gives a readable construction seam
 ring=rectangular_tube_sweep(path[:2],.068,.059,.0018,max(12,c['cross']//2));materialize(sc,'LowerSnapJointBand',ring,'PipeCoverPVC',.06)
 return sc

def irregular_annulus(outer_r,inner_rx,inner_ry,z0,z1,n):
 ang=np.arange(n)*TAU/n
 outer=outer_r*(1+.035*np.sin(3*ang+.4)+.018*np.sin(7*ang-1.1))
 inner_scale=1+.025*np.sin(5*ang-.2)
 ox=outer*np.cos(ang);oy=outer*np.sin(ang);ix=inner_rx*inner_scale*np.cos(ang);iy=inner_ry*inner_scale*np.sin(ang)
 v=[]
 for z in [z0,z1]:
  v.extend(np.column_stack([ox,oy,np.full(n,z)]).tolist());v.extend(np.column_stack([ix,iy,np.full(n,z)]).tolist())
 f=[]
 # each z: outer then inner, stride 2n
 for j in range(n):
  q=(j+1)%n
  # front/back annular faces
  f.extend([[j,n+j,n+q],[j,n+q,q]])
  b=2*n;f.extend([[b+j,b+q,b+n+q],[b+j,b+n+q,b+n+j]])
  # outer wall
  f.extend([[j,q,b+q],[j,b+q,b+j]])
  # inner wall
  f.extend([[n+j,b+n+j,b+n+q],[n+j,b+n+q,n+q]])
 return finalize(v,f)

def build_wall_seal(level):
 c=LEVELS[level];sc=trimesh.Scene();n=c['ring'];s=c['sides']
 sleeve=cylinder_shell([0,0,0],.120,.0375,.0345,s,'z');materialize(sc,'PVCWallSleeve',sleeve,'SleevePVC',.05)
 putty=irregular_annulus(.044,.034,.029,-.067,-.055,n);materialize(sc,'IrregularExteriorSealPutty',putty,'SealingPutty',.045)
 # thin EPDM shadow/seal lip at sleeve/putty interface; real annular geometry not painted AO
 lip=cylinder_shell([0,0,-.0535],.003,.0382,.0342,s,'z');materialize(sc,'SleeveEPDMLip',lip,'BlackEPDM',.025)
 return sc

BUILDERS={'RefrigerantLineSetPair_6p35_9p52':build_lineset,'PipeCoverWallElbow60x55':build_wall_elbow,'ACWallPenetrationSeal75':build_wall_seal}

def verify(scene):
 rows=[]
 for name,g in scene.geometry.items():
  assert np.isfinite(g.vertices).all() and np.isfinite(g.vertex_normals).all(),name
  assert np.all(g.area_faces>1e-14),name
  h=g.copy();h.merge_vertices(digits_vertex=9,merge_tex=True,merge_norm=True);h.remove_unreferenced_vertices()
  assert h.is_watertight and h.is_winding_consistent and h.volume>0,(name,h.is_watertight,h.is_winding_consistent,h.volume)
  _,cnt=np.unique(h.edges_sorted,axis=0,return_counts=True);assert np.all(cnt==2),name
  rows.append({'part':name,'triangles':len(g.faces),'volumeM3':float(h.volume),'boundaryEdges':0})
 return {'triangles':sum(r['triangles'] for r in rows),'componentSurfaces':len(rows),'parts':rows,'boundsMetres':scene.bounds.tolist()}

def export_scene(scene,out,stem):
 glb=out/(stem+'.glb');glb.write_bytes(scene.export(file_type='glb'))
 rd=trimesh.load(glb,force='scene',process=False);assert sum(len(g.faces) for g in rd.geometry.values())==sum(len(g.faces) for g in scene.geometry.values());assert np.allclose(rd.bounds,scene.bounds,atol=1e-6)
 folder=out/stem;folder.mkdir(exist_ok=True);obj,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True);(folder/(stem+'.obj')).write_text(obj)
 for fn,data in files.items():
  p=folder/fn;p.write_text(data) if isinstance(data,str) else p.write_bytes(data)
 od=trimesh.load(folder/(stem+'.obj'),force='scene',process=False);assert sum(len(g.faces) for g in od.geometry.values())==sum(len(g.faces) for g in scene.geometry.values())
 return glb

def run(out):
 out.mkdir(parents=True,exist_ok=True);assets=[]
 for aid,builder in BUILDERS.items():
  levels=[]
  for level in LEVELS:
   scene=builder(level);stat=verify(scene);stem=f'{aid}_{level}';glb=export_scene(scene,out,stem)
   levels.append({'level':level,'sha256':hashlib.sha256(glb.read_bytes()).hexdigest(),'glbRoundtrip':True,'objTriangleRoundtrip':True,**stat});print(aid,level,stat['triangles'],stat['componentSurfaces'],flush=True)
  counts=[x['triangles'] for x in levels];assert all(a>b for a,b in zip(counts,counts[1:])),(aid,counts);assets.append({'asset':aid,'levels':levels})
 report={'status':'EXTERNAL_GEOMETRY_CHECKED_NOT_UNITY_VISUAL_PASS','assets':assets,'materials':MATERIALS,'unityCompile':False,'unityImport':False,'unityRender':False,'performanceVerified':False,'temporalVerified':False,'visualFidelityScore':None,'visualPass':False,'sourceSHA256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
 (out/'geometry_verification.json').write_text(json.dumps(report,indent=2)+'\n');return report

if __name__=='__main__':
 ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);a=ap.parse_args();run(a.output)
