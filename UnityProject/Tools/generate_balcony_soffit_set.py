"""Compact deterministic authoring source for BalconySoffitSet.
Metres, Y-up. This repository version creates the three asset mesh families and LOD
geometry; the conversation artifact contains the executed exporter with OBJ/GLB
round-trip validation, PBR texture baking and diagnostic review assembly.
No Unity runtime or renderer is invoked here.
"""
from pathlib import Path
import argparse, math
import numpy as np
import trimesh

LEVELS={
 'MASTER':dict(radial=24,step=.30,joints=True),
 'LOD0':dict(radial=16,step=.45,joints=True),
 'LOD1':dict(radial=10,step=.90,joints=False),
 'LOD2':dict(radial=8,step=1.80,joints=False),
 'LOD3':dict(radial=6,step=9.99,joints=False),
}

def box(e,c,n,m):
 q=trimesh.creation.box(extents=e);q.apply_translation(c);q.metadata.update(name=n,material=m);return q

def cyl(r,h,c,a,s,n,m):
 q=trimesh.creation.cylinder(radius=r,height=h,sections=s);a=np.asarray(a,float);a/=np.linalg.norm(a)
 if not np.allclose(a,[0,0,1]):q.apply_transform(trimesh.geometry.align_vectors([0,0,1],a))
 q.apply_translation(c);q.metadata.update(name=n,material=m);return q

def ring(ro,ri,h,c,a,s,n,m):
 th=np.arange(s)*2*np.pi/s;v=[]
 for z in (-h/2,h/2):
  for r in (ro,ri):
   for t in th:v.append([r*np.cos(t),r*np.sin(t),z])
 f=[]
 def ix(zi,ri,j):return zi*2*s+ri*s+j%s
 for j in range(s):
  k=j+1;f += [[ix(0,0,j),ix(0,0,k),ix(1,0,k)],[ix(0,0,j),ix(1,0,k),ix(1,0,j)],[ix(0,1,j),ix(1,1,k),ix(0,1,k)],[ix(0,1,j),ix(1,1,j),ix(1,1,k)],[ix(1,0,j),ix(1,0,k),ix(1,1,k)],[ix(1,0,j),ix(1,1,k),ix(1,1,j)],[ix(0,0,j),ix(0,1,k),ix(0,0,k)],[ix(0,0,j),ix(0,1,j),ix(0,1,k)]]
 q=trimesh.Trimesh(vertices=v,faces=f,process=False);q.fix_normals();a=np.asarray(a,float);a/=np.linalg.norm(a)
 if not np.allclose(a,[0,0,1]):q.apply_transform(trimesh.geometry.align_vectors([0,0,1],a))
 q.apply_translation(c);q.metadata.update(name=n,material=m);return q

def soffit(level):
 d=LEVELS[level];P=[];W=3.6;D=1.2;t=.008;gap=.006;pw=(W-3*gap)/4
 for i in range(4):
  x=-W/2+pw/2+i*(pw+gap);P.append(box([pw,t,D],[x,t/2,0],f'Panel_{i}','PaintedFiberCement'))
  if d['joints'] and i<3:
   jx=-W/2+(i+1)*pw+i*gap+gap/2;P += [box([gap*.8,.003,D-.03],[jx,-.0015,0],f'JointSeal_{i}','JointSealant'),cyl(gap*.42,D-.05,[jx,.004,0],[0,0,1],d['radial'],f'BackerRod_{i}','ClosedCellFoam')]
 P += [box([W,.004,.010],[0,-.002,-D/2+.005],'WallPerimeterSeal','JointSealant'),box([W,.006,.012],[0,-.003,D/2-.006],'OuterShadowReveal','DarkCavity')]
 if level in ('MASTER','LOD0'):
  for z in (-.45,0,.45):P.append(box([W-.08,.018,.030],[0,.017,z],f'Furring_{z:+.2f}','PowderCoatedAluminum'))
 if level!='LOD3':
  for z,prefix in ((D/2-.085,'Outer'),(-D/2+.085,'Wall')):
   for i,x in enumerate(np.arange(-W/2+.12,W/2-.12+1e-9,d['step'])):
    P += [cyl(.0048,.0028,[x,-.001,z],[0,1,0],d['radial'],f'{prefix}Head_{i}','StainlessFastener'),cyl(.0021,.010,[x,.004,z],[0,1,0],d['radial'],f'{prefix}Shank_{i}','StainlessFastener')]
 return P

def hatch(level):
 d=LEVELS[level];S=.45;r=.030;dep=.022
 P=[box([S,r,dep],[0,S/2-r/2,0],'FrameTop','PowderCoatedAluminum'),box([S,r,dep],[0,-S/2+r/2,0],'FrameBottom','PowderCoatedAluminum'),box([r,S-2*r,dep],[-S/2+r/2,0,0],'FrameLeft','PowderCoatedAluminum'),box([r,S-2*r,dep],[S/2-r/2,0,0],'FrameRight','PowderCoatedAluminum'),box([S-.07,S-.07,.008],[0,0,.004],'Leaf','PaintedFiberCement')]
 if level=='LOD3':return P
 P += [box([S-.05,.007,.006],[0,S/2-.034,.010],'GasketTop','EPDM'),box([S-.05,.007,.006],[0,-S/2+.034,.010],'GasketBottom','EPDM'),box([.007,S-.064,.006],[-S/2+.034,0,.010],'GasketLeft','EPDM'),box([.007,S-.064,.006],[S/2-.034,0,.010],'GasketRight','EPDM'),box([.055,.018,.013],[0,-.145,-.006],'FingerCupCavity','DarkCavity')]
 if level in ('MASTER','LOD0','LOD1'):P.append(ring(.017,.011,.003,[0,-.145,-.015],[0,0,1],d['radial'],'FingerCupRim','PowderCoatedAluminum'))
 corners=[(-.195,-.195),(.195,-.195),(.195,.195),(-.195,.195)];use=corners if level in ('MASTER','LOD0') else corners[::2] if level=='LOD1' else []
 for i,(x,y) in enumerate(use):P.append(cyl(.0045,.003,[x,y,-.013],[0,0,1],d['radial'],f'Fastener_{i}','StainlessFastener'))
 if level in ('MASTER','LOD0'):
  for x in (-.12,.12):P += [cyl(.006,.075,[x,.225,.014],[1,0,0],d['radial'],'HingeKnuckle','PowderCoatedAluminum'),cyl(.0025,.082,[x,.225,.014],[1,0,0],d['radial'],'HingePin','StainlessFastener')]
 return P

def flashing(level):
 d=LEVELS[level];W=3.6;P=[box([W,.002,.055],[0,.001,-.0275],'MountingFlange','PowderCoatedAluminum'),box([W,.055,.002],[0,-.0265,-.056],'VerticalDrop','PowderCoatedAluminum'),box([W,.002,.028],[0,-.054,-.069],'DripKick','PowderCoatedAluminum')]
 if level!='LOD3':P.append(box([W,.008,.006],[0,-.050,-.084],'HemmedEdge','PowderCoatedAluminum'))
 if level in ('MASTER','LOD0','LOD1'):
  for i,x in enumerate(np.arange(-W/2+.15,W/2-.15+1e-9,d['step'])):P += [cyl(.004,.0025,[x,.004,-.025],[0,1,0],d['radial'],f'Fastener_{i}','StainlessFastener'),ring(.0065,.0042,.0012,[x,.002,-.025],[0,1,0],d['radial'],f'Washer_{i}','EPDM')]
 if level in ('MASTER','LOD0'):P += [box([.014,.060,.060],[-W/2+.007,-.025,-.050],'EndDamL','PowderCoatedAluminum'),box([.014,.060,.060],[W/2-.007,-.025,-.050],'EndDamR','PowderCoatedAluminum')]
 return P

BUILD={'PaintedFiberCementSoffit3600x1200':soffit,'BalconySoffitAccessHatch450':hatch,'SoffitDripEdgeFlashing3600':flashing}

def export(out):
 out.mkdir(parents=True,exist_ok=True)
 for name,fn in BUILD.items():
  for level in LEVELS:
   parts=fn(level);scene=trimesh.Scene()
   for i,g in enumerate(parts):scene.add_geometry(g,node_name=f'{g.metadata["name"]}_{i}')
   (out/f'{name}_{level}.glb').write_bytes(scene.export(file_type='glb'))

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);export(p.parse_args().output)
