from __future__ import annotations
import argparse, importlib.util, json, hashlib, math
from pathlib import Path
import numpy as np
import trimesh
ROOT=Path(__file__).resolve().parent

def load_owner():
 spec=importlib.util.spec_from_file_location('hose_owner',ROOT/'generate_faucet_hose.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def refine(level):
 o=load_owner();s=o.build_hose(level)
 # Exact owner geometry is retained. The handheld group is rotated about the barrel axis so the pistol grip lies sideways,
 # instead of penetrating the floor when the hose coil is placed at its intended ground plane.
 maj,minr,pathn={"MASTER":(120,18,120),"LOD0":(84,14,84),"LOD1":(56,10,56),"LOD2":(32,8,32),"LOD3":(18,6,18)}[level]
 t=np.linspace(0,1,pathn)
 p=np.column_stack([.17+.30*t,.012+.028*np.sin(math.pi*t),-.04-.09*t+.025*np.sin(2*math.pi*t)])
 end=p[-1];root=end+[0,0,-.035]
 R=trimesh.transformations.rotation_matrix(math.radians(90),[0,0,1],point=root)
 moving=[n for n in s.geometry if n=='NozzleGrip' or n=='Trigger' or n.startswith('GripBand')]
 for n in moving:s.geometry[n].apply_transform(R)
 # Normalize the lowest actual contact to y=0; this is a placement correction, not geometry scaling.
 dy=-s.bounds[0,1]
 for g in s.geometry.values():g.apply_translation([0,dy,0])
 return s,{'rotatedComponents':moving,'groundNormalizationMetres':float(dy),'restingPose':'pistol grip rotated 90 degrees around nozzle/barrel axis; lowest actual mesh point at y=0'}

def verify(s):
 rows=[]
 for name,g in s.geometry.items():
  assert np.isfinite(g.vertices).all() and np.isfinite(g.vertex_normals).all(),name
  assert np.all(g.area_faces>1e-14),name
  h=g.copy();h.merge_vertices(digits_vertex=8,merge_tex=True,merge_norm=True);h.remove_unreferenced_vertices();h.fix_normals(multibody=True)
  assert h.is_watertight and h.is_winding_consistent and h.volume>0,(name,h.is_watertight,h.volume)
  rows.append({'component':name,'triangles':int(len(g.faces))})
 assert s.bounds[0,1]>=-1e-9,s.bounds.tolist()
 return {'triangles':sum(r['triangles'] for r in rows),'components':len(rows),'minY':float(s.bounds[0,1]),'maxY':float(s.bounds[1,1]),'rows':rows}

def run(out):
 out.mkdir(parents=True,exist_ok=True);levels=[]
 for level in ['MASTER','LOD0','LOD1','LOD2','LOD3']:
  s,pose=refine(level);rep=verify(s);stem=f'GardenHoseCoilNozzleResting_{level}'
  glb=out/(stem+'.glb');glb.write_bytes(s.export(file_type='glb'))
  re=trimesh.load(glb,force='scene',process=False);assert sum(len(g.faces) for g in re.geometry.values())==rep['triangles'];assert re.bounds[0,1]>=-1e-6
  folder=out/stem;folder.mkdir(exist_ok=True);obj,files=trimesh.exchange.obj.export_obj(s,include_normals=True,include_texture=True,return_texture=True);(folder/(stem+'.obj')).write_text(obj)
  for fn,data in files.items():p=folder/fn;p.write_text(data) if isinstance(data,str) else p.write_bytes(data)
  levels.append({'level':level,'sha256':hashlib.sha256(glb.read_bytes()).hexdigest(),**rep,**pose,'glbRoundtrip':True})
  print(level,rep['triangles'],rep['minY'],rep['maxY'])
 counts=[x['triangles'] for x in levels];assert all(a>b for a,b in zip(counts,counts[1:])),counts
 report={'asset':'GardenHoseCoilNozzle','variant':'resting_pose_refinement_reusing_existing_owner','levels':levels,'unityImport':False,'unityRender':False,'visualFidelityScore':None}
 (out/'resting_pose_verification.json').write_text(json.dumps(report,indent=2));return report
if __name__=='__main__':
 ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);a=ap.parse_args();run(a.output)
