"""BalconyWindowSet installation-detail authoring source (metres, Y-up, exterior +Z).
Generates the three reversible extension assets as GLB. The automation delivery also
contains the exact executed full exporter that produced OBJ/PBR textures/reviews.
"""
from pathlib import Path
import argparse, math
import numpy as np
import trimesh

LEVELS={'MASTER':(20,96,3),'LOD0':(14,64,2),'LOD1':(10,40,1),'LOD2':(8,24,1),'LOD3':(6,12,1)}
MATS={'OffWhiteSealant':([.69,.69,.64,1],0,.73),'GrayBackerFoam':([.24,.25,.24,1],0,.93),'AnodizedAluminum':([.44,.45,.44,1],1,.36),'GalvanizedSteel':([.51,.53,.54,1],1,.46),'DarkPP':([.08,.085,.08,1],0,.58),'MineralResidue':([.61,.60,.52,1],0,.88)}

def box(e,c,n,m):
 x=trimesh.creation.box(extents=e);x.apply_translation(c);x.metadata.update(name=n,material=m);return x

def cyl(r,h,c,a,sec,n,m):
 x=trimesh.creation.cylinder(radius=r,height=h,sections=sec);T=trimesh.geometry.align_vectors([0,0,1],np.asarray(a,float)/np.linalg.norm(a));x.apply_transform(T);x.apply_translation(c);x.metadata.update(name=n,material=m);return x

def ell(c,r,sub,n,m):
 x=trimesh.creation.icosphere(subdivisions=sub);x.apply_scale(r);x.apply_translation(c);x.metadata.update(name=n,material=m);return x

def sweep(path,rx,rz,sec,n,m):
 p=np.asarray(path,float);t=np.vstack([p[1]-p[0],p[2:]-p[:-2],p[-1]-p[-2]]);t/=np.linalg.norm(t,axis=1)[:,None];F=[];prev=None
 for q in t:
  u=np.cross(q,[0,0,1]) if prev is None else prev-q*np.dot(prev,q)
  if np.linalg.norm(u)<1e-8:u=np.cross(q,[1,0,0])
  u/=np.linalg.norm(u);F.append((u,np.cross(q,u)));prev=u
 a=np.arange(sec)*2*np.pi/sec;v=np.vstack([q+np.cos(a)[:,None]*rx*u+np.sin(a)[:,None]*rz*b for q,(u,b) in zip(p,F)]);f=[]
 for i in range(len(p)-1):
  for j in range(sec):
   k=(j+1)%sec;A=i*sec+j;B=i*sec+k;C=(i+1)*sec+k;D=(i+1)*sec+j;f += [[A,B,C],[A,C,D]]
 for ring,rev in [(0,1),(len(p)-1,0)]:
  cc=len(v);v=np.vstack([v,p[ring]])
  for j in range(sec):
   A=ring*sec+j;B=ring*sec+(j+1)%sec;f.append([cc,B,A] if rev else [cc,A,B])
 x=trimesh.Trimesh(v,np.asarray(f),process=False);x.fix_normals();x.metadata.update(name=n,material=m);return x

def seal(level):
 sec,N,sub=LEVELS[level];h=.920;z=.063;p=[]
 def line(a,b,ph,n):
  t=np.linspace(0,1,N);q=(1-t[:,None])*np.array(a)+t[:,None]*np.array(b);q[:,2]+=.00032*np.sin(2*np.pi*(3*t+ph))+.00011*np.sin(2*np.pi*(11*t+ph*.7));return sweep(q,.0053,.0036,sec,n,'OffWhiteSealant')
 p += [line([-h,-h,z],[h,-h,z],.1,'SealBottom'),line([-h,h,z],[h,h,z],.37,'SealTop'),line([-h,-h,z],[-h,h,z],.55,'SealLeft'),line([h,-h,z],[h,h,z],.82,'SealRight')]
 br=z-.0062
 p += [cyl(.004,1.84,[0,-h,br],[1,0,0],sec,'BackerBottom','GrayBackerFoam'),cyl(.004,1.84,[0,h,br],[1,0,0],sec,'BackerTop','GrayBackerFoam'),cyl(.004,1.84,[-h,0,br],[0,1,0],sec,'BackerLeft','GrayBackerFoam'),cyl(.004,1.84,[h,0,br],[0,1,0],sec,'BackerRight','GrayBackerFoam')]
 for x in [-h,h]:
  for y in [-h,h]:p.append(ell([x,y,z],[.007,.007,.0045],sub,'TooledCornerPool','OffWhiteSealant'))
 return p

def head(level):
 sec,_,_=LEVELS[level];W=1.9;p=[box([W,.045,.0008],[0,.0225,-.010],'RearWallLeg','AnodizedAluminum'),box([W,.0008,.062],[0,-.0004,.020],'SlopedHeadPan','AnodizedAluminum'),box([W,.018,.0008],[0,-.009,.051],'FrontDripFace','AnodizedAluminum'),box([W,.006,.006],[0,-.021,.054],'HemmedDripEdge','AnodizedAluminum'),box([.012,.045,.064],[-.944,.002,.021],'LeftEndDam','AnodizedAluminum'),box([.012,.045,.064],[.944,.002,.021],'RightEndDam','AnodizedAluminum')]
 for x in np.linspace(-.76,.76,5):p += [cyl(.004,.003,[x,.0455,-.010],[0,1,0],sec,'HeadFastener','GalvanizedSteel'),cyl(.0065,.0012,[x,.0438,-.010],[0,1,0],sec,'FastenerWasher','DarkPP')]
 return p

def weep(level):
 sec,_,sub=LEVELS[level];p=[]
 for i,x in enumerate([-.62,.62]):
  p += [box([.070,.004,.026],[x,-.004,.078],f'Hood{i}_Top','DarkPP'),box([.004,.030,.026],[x-.033,-.019,.078],f'Hood{i}_Left','DarkPP'),box([.004,.030,.026],[x+.033,-.019,.078],f'Hood{i}_Right','DarkPP'),box([.070,.020,.004],[x,-.015,.091],f'Hood{i}_FrontLip','DarkPP'),box([.050,.018,.004],[x,-.015,.064],f'Hood{i}_OutletBack','DarkPP')]
  if level!='LOD3':p.append(ell([x,-.044,.096],[.020,.014,.0012],sub,f'Hood{i}_Residue','MineralResidue'))
  if level in ('MASTER','LOD0','LOD1'):p.append(cyl(.0018,.004,[x,-.003,.092],[0,0,1],sec,f'Hood{i}_ClipScrew','GalvanizedSteel'))
 return p
BUILD={'WindowPerimeterSealJoint1840x1840':seal,'WindowHeadDripCap1900':head,'WindowSillWeepHoodPair':weep}

def scene(parts):
 s=trimesh.Scene();G={}
 for p in parts:G.setdefault(p.metadata['material'],[]).append(p)
 for n,a in G.items():
  x=trimesh.util.concatenate(a);base,metal,rough=MATS[n];x.visual.material=trimesh.visual.material.PBRMaterial(name=n,baseColorFactor=base,metallicFactor=metal,roughnessFactor=rough);s.add_geometry(x,node_name=n,geom_name=n)
 return s

def check(parts):
 for p in parts:
  q=p.copy();q.merge_vertices(digits_vertex=9);assert np.isfinite(q.vertices).all() and np.all(q.area_faces>1e-14) and q.is_watertight and q.is_winding_consistent and q.volume>0

def run(out):
 out.mkdir(parents=True,exist_ok=True)
 for name,builder in BUILD.items():
  counts=[]
  for level in LEVELS:
   p=builder(level);check(p);s=scene(p);counts.append(sum(len(g.faces) for g in s.geometry.values()));d=out/name;d.mkdir(exist_ok=True);(d/f'{name}_{level}.glb').write_bytes(s.export(file_type='glb'))
  assert all(a>b for a,b in zip(counts,counts[1:])),(name,counts);print(name,counts)
if __name__=='__main__':
 ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);run(ap.parse_args().output)
