from __future__ import annotations
import argparse,json,math,hashlib
from pathlib import Path
import numpy as np,trimesh,shapely
from PIL import Image
from shapely.geometry import Polygon,Point
from shapely.affinity import scale as shscale
TAU=2*math.pi
MATS={'WhiteCottonJersey':([.78,.79,.76],.79,.035),'BluePP':([.12,.30,.48],.50,.07)}
def unit(v):
 v=np.asarray(v,float);return v/np.linalg.norm(v)
def mat(m,n):
 c,r,t=MATS[n];y,x=np.mgrid[:128,:128];u=x/128;v=y/128;p=np.sin(TAU*(17*u+13*v))*np.sin(TAU*(19*v-11*u));base=np.clip(np.array(c)[None,None,:]*(1+.02*p[:,:,None]),0,1);orm=np.stack([np.ones_like(p),np.clip(r+.035*p,.04,1),np.zeros_like(p)],-1);uv=np.c_[m.vertices[:,0]+.31*m.vertices[:,2],m.vertices[:,1]+.17*m.vertices[:,2]]/t;pm=trimesh.visual.material.PBRMaterial(name=n,baseColorTexture=Image.fromarray(np.uint8(base*255)),metallicRoughnessTexture=Image.fromarray(np.uint8(orm*255)),metallicFactor=1,roughnessFactor=1);m.visual=trimesh.visual.TextureVisuals(uv=uv,material=pm);return m
def tube(path,r,sides,cap=True):
 p=np.asarray(path,float);vs=[];fs=[];rings=[];prev=None
 for i,c in enumerate(p):
  t=unit(p[min(i+1,len(p)-1)]-p[max(0,i-1)]);n=np.cross(t,[0,1,0]) if prev is None else prev-t*np.dot(prev,t)
  if np.linalg.norm(n)<1e-7:n=np.cross(t,[1,0,0])
  n=unit(n);b=unit(np.cross(t,n));prev=n;ring=[]
  for j in range(sides):a=TAU*j/sides;ring.append(len(vs));vs.append(c+r*(n*math.cos(a)+b*math.sin(a)))
  rings.append(ring)
 for i in range(len(rings)-1):
  for j in range(sides):k=(j+1)%sides;a,b=rings[i][j],rings[i][k];c,d=rings[i+1][k],rings[i+1][j];fs += [[a,b,c],[a,c,d]]
 if cap:
  q0=len(vs);vs.append(p[0]);q1=len(vs);vs.append(p[-1])
  for j in range(sides):k=(j+1)%sides;fs += [[q0,rings[0][k],rings[0][j]],[q1,rings[-1][j],rings[-1][k]]]
 m=trimesh.Trimesh(vs,fs,process=False);m.fix_normals(multibody=True);return m
def extrude(poly,depth,steps,deform):
 tris=[np.asarray(t.exterior.coords)[:3] for t in shapely.constrained_delaunay_triangles(poly).geoms];idx={}
 def K(x,y):return(round(float(x),12),round(float(y),12))
 for t in tris:
  for x,y in t:idx.setdefault(K(x,y),len(idx))
 for ring in [poly.exterior,*poly.interiors]:
  for x,y in list(ring.coords)[:-1]:idx.setdefault(K(x,y),len(idx))
 pts=np.zeros((len(idx),2));
 for k,i in idx.items():pts[i]=k
 n=len(pts);v=np.zeros((2*n,3));v[:n,:2]=pts;v[:n,2]=-depth/2;v[n:,:2]=pts;v[n:,2]=depth/2;f=[]
 for t in tris:
  q=[idx[K(x,y)] for x,y in t];a,b,c=[pts[i] for i in q]
  if np.cross(np.r_[b-a,0],np.r_[c-a,0])[2]<0:q=[q[0],q[2],q[1]]
  f += [[n+q[0],n+q[1],n+q[2]],[q[0],q[2],q[1]]]
 for ring in [poly.exterior,*poly.interiors]:
  q=list(ring.coords)
  for (x0,y0),(x1,y1) in zip(q,q[1:]):a=idx[K(x0,y0)];b=idx[K(x1,y1)];f += [[a,b,n+b],[a,n+b,n+a]]
 m=trimesh.Trimesh(v,f,process=False);m.merge_vertices(digits_vertex=9);m.fix_normals(multibody=True)
 for _ in range(steps):v,f=trimesh.remesh.subdivide(m.vertices,m.faces);m=trimesh.Trimesh(v,f,process=False)
 x,y=m.vertices[:,0],m.vertices[:,1];m.vertices[:,2]+=deform(x,y);m.merge_vertices(digits_vertex=9);m.remove_unreferenced_vertices();m.fix_normals(multibody=True);return m
def sleeve(side,level):
 N={'MASTER':28,'LOD0':20,'LOD1':14,'LOD2':9,'LOD3':6}[level];A={'MASTER':32,'LOD0':24,'LOD1':18,'LOD2':12,'LOD3':8}[level];cs=[np.array([side*(.190+.230*t),.535-.050*t-.007*math.sin(math.pi*t),.0015*math.sin(math.pi*t)*(1 if side>0 else -1)]) for t in np.linspace(0,1,N)];vs=[];O=[];I=[]
 for i,c in enumerate(cs):
  t=i/(N-1);tan=unit(cs[min(i+1,N-1)]-cs[max(0,i-1)]);up=unit(np.array([0.,1.,0.])-tan*np.dot([0,1,0],tan));dep=unit(np.cross(tan,up));ry=.056+.020*math.sin(math.pi*t)-.001*t;rz=.023+.004*math.sin(math.pi*t)-.003*t;w=.00072 if t<.93 else .00146;o=[];inn=[]
  for j in range(A):a=TAU*j/A;o.append(len(vs));vs.append(c+up*ry*math.cos(a)+dep*rz*math.sin(a))
  for j in range(A):a=TAU*j/A;inn.append(len(vs));vs.append(c+up*(ry-w)*math.cos(a)+dep*(rz-w)*math.sin(a))
  O.append(o);I.append(inn)
 fs=[]
 for i in range(N-1):
  for j in range(A):k=(j+1)%A;fs += [[O[i][j],O[i][k],O[i+1][k]],[O[i][j],O[i+1][k],O[i+1][j]],[I[i][j],I[i+1][k],I[i][k]],[I[i][j],I[i+1][j],I[i+1][k]]]
 for r in [0,N-1]:
  for j in range(A):k=(j+1)%A;fs += ([[O[r][j],I[r][k],O[r][k]],[O[r][j],I[r][j],I[r][k]]] if r==0 else [[O[r][j],O[r][k],I[r][k]],[O[r][j],I[r][k],I[r][j]]])
 m=trimesh.Trimesh(vs,fs,process=False);m.merge_vertices(digits_vertex=9);m.fix_normals(multibody=True);return m
def torus(level):
 hs={'MASTER':16,'LOD0':12,'LOD1':10,'LOD2':8,'LOD3':6}[level];M=max(20,hs*3);m=max(6,hs//2);vs=[];fs=[]
 for i in range(M):
  a=TAU*i/M;p=np.array([.0915*math.cos(a),.638+.0475*math.sin(a),0]);t=unit(np.array([-.0915*math.sin(a),.0475*math.cos(a),0]));r=unit(np.array([math.cos(a)/.0915,math.sin(a)/.0475,0]));b=unit(np.cross(t,r))
  for j in range(m):q=TAU*j/m;vs.append(p+.0021*(r*math.cos(q)+b*math.sin(q)))
 for i in range(M):
  for j in range(m):ni=(i+1)%M;nj=(j+1)%m;a=i*m+j;b=i*m+nj;c=ni*m+nj;d=ni*m+j;fs += [[a,b,c],[a,c,d]]
 q=trimesh.Trimesh(vs,fs,process=False);q.fix_normals(multibody=True);return q
def build(level):
 edge={'MASTER':3,'LOD0':2,'LOD1':2,'LOD2':1,'LOD3':0}[level];hs={'MASTER':16,'LOD0':12,'LOD1':10,'LOD2':8,'LOD3':6}[level];out=[(-.255,0),(.255,0),(.255,.455),(.246,.505),(.232,.565),(.205,.620),(.135,.665),(-.135,.665),(-.205,.620),(-.232,.565),(-.246,.505),(-.255,.455)];poly=Polygon(out);neck=shscale(Point(0,.638).buffer(1,resolution=max(8,int(36*({'MASTER':1,'LOD0':.75,'LOD1':.5,'LOD2':.34,'LOD3':.24}[level])))),xfact=.090,yfact=.046,origin=(0,.638));poly=poly.difference(neck)
 def F(x,y):b=np.clip((y-.03)/.60,0,1);return .018+.009*np.sin(7*np.pi*(x+.255)/.51)*b*(1-b*.35)+.003*np.sin(3.5*np.pi*y/.665+8*x)
 def B(x,y):b=np.clip((y-.03)/.60,0,1);return -.018+.005*np.sin(5*np.pi*(x+.255)/.51+.6)*b*(1-b*.25)-.002*np.sin(3*np.pi*y/.665+6*x)
 parts=[('JerseyFrontPanelWithOpenWaistAndNeck',mat(extrude(poly,.00078,edge,F),'WhiteCottonJersey')),('JerseyBackPanelWithOpenWaistAndNeck',mat(extrude(poly,.00078,edge,B),'WhiteCottonJersey'))]
 strip=poly.intersection(Polygon([(-.26,.004),(.26,.004),(.26,.020),(-.26,.020)]));parts += [('FoldedFrontWaistHem',mat(extrude(strip,.00155,edge,F),'WhiteCottonJersey')),('FoldedBackWaistHem',mat(extrude(strip,.00155,edge,B),'WhiteCottonJersey')),('LeftHollowSleeve_OpenCuff',mat(sleeve(-1,level),'WhiteCottonJersey')),('RightHollowSleeve_OpenCuff',mat(sleeve(1,level),'WhiteCottonJersey')),('RibCollar_OpenCentre',mat(torus(level),'WhiteCottonJersey'))]
 ss=max(5,hs//2)
 for side in (-1,1):parts += [('SideSeam_'+str(side),mat(tube([[side*.252,.025,-.017],[side*.252,.43,-.017],[side*.252,.43,.017],[side*.252,.025,.017]],.00135,ss),'WhiteCottonJersey')),('ShoulderSeam_'+str(side),mat(tube([[side*.202,.607,-.015],[side*.150,.653,-.014],[side*.150,.653,.014],[side*.202,.607,.015]],.0012,ss),'WhiteCottonJersey'))]
 count={'MASTER':48,'LOD0':32,'LOD1':18,'LOD2':0,'LOD3':0}[level]
 for ztag,z in [('F',.019),('B',-.019)]:
  for i in range(count):x=-.238+(i+.5)*.476/count;d=.476/(count*1.8);parts.append((f'WaistStitch_{ztag}_{i}',mat(tube([[x-d*.45,.014,z],[x+d*.45,.014,z]],.00023,6 if level in ('MASTER','LOD0') else 4),'WhiteCottonJersey')))
 y=.615;z=-.030;shoulder=[[-.205,y,z],[-.145,y+.040,z],[-.018,y+.020,z],[.145,y+.040,z],[.205,y,z]];c=np.array([0,y+.085,z]);hook=[c+[.035*math.cos(a),.035*math.sin(a),0] for a in np.linspace(-math.pi*.15,math.pi*1.2,max(10,hs*2))];parts += [('MoldedHangerShoulder',mat(tube(shoulder,.0042,hs),'BluePP')),('MoldedHangerCentreBridge',mat(tube([[-.018,y+.020,z],[.018,y+.020,z]],.0042,hs),'BluePP')),('MoldedHangerHook',mat(tube(hook,.0038,hs),'BluePP'))];return parts
def verify(scene):
 c=[]
 for n,g in scene.geometry.items():
  assert np.isfinite(g.vertices).all() and np.all(g.area_faces>1e-14);w=g.copy();w.merge_vertices(digits_vertex=8,merge_tex=True,merge_norm=True);w.remove_unreferenced_vertices();w.fix_normals(multibody=True);assert w.is_watertight and w.is_winding_consistent and w.volume>0;_,cnt=np.unique(w.edges_sorted,axis=0,return_counts=True);assert np.all(cnt==2);c.append({'component':n,'triangles':len(g.faces)})
 return {'triangles':sum(x['triangles'] for x in c),'components':c,'boundsMetres':scene.bounds.tolist()}
def run(out):
 out.mkdir(parents=True,exist_ok=True);levels=[]
 for level in ['MASTER','LOD0','LOD1','LOD2','LOD3']:
  s=trimesh.Scene();
  for n,g in build(level):s.add_geometry(g,node_name=n,geom_name=n)
  dy=-s.bounds[0,1]
  for g in s.geometry.values():g.apply_translation([0,dy,0])
  info=verify(s);stem='CottonTShirtM_Hanger_'+level;glb=out/(stem+'.glb');glb.write_bytes(s.export(file_type='glb'));r=trimesh.load(glb,force='scene',process=False);assert sum(len(g.faces) for g in r.geometry.values())==info['triangles'];folder=out/stem;folder.mkdir(exist_ok=True);obj,files=trimesh.exchange.obj.export_obj(s,include_normals=True,include_texture=True,return_texture=True);(folder/(stem+'.obj')).write_text(obj)
  for fn,data in files.items():p=folder/fn;p.write_text(data) if isinstance(data,str) else p.write_bytes(data)
  r2=trimesh.load(folder/(stem+'.obj'),force='scene',process=False);assert sum(len(g.faces) for g in r2.geometry.values())==info['triangles'];levels.append({'level':level,'sha256':hashlib.sha256(glb.read_bytes()).hexdigest(),**info});print(level,info['triangles'])
 counts=[x['triangles'] for x in levels];assert all(a>b for a,b in zip(counts,counts[1:]));report={'asset':'CottonTShirtM_Hanger','status':'EXTERNAL_GEOMETRY_CHECKED_NOT_VISUAL_PASS','levels':levels,'unityCompile':False,'unityRender':False,'visualFidelityScore':None};(out/'tshirt_refinement_verification.json').write_text(json.dumps(report,indent=2)+'\n')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--output',required=True,type=Path);run(a.parse_args().output)
