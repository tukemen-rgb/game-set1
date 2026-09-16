"""Reversible external-Art integration for the 2026-09-16 balcony structural contact repair.

Inputs are generated GLB directories, not the formal QualityBlock scene. This script does not
modify Unity scenes or Godot. It replaces only the external Drained3600 rail/return/hatch nodes,
adds the side-return kerb, and writes a new versioned hero GLB for each LOD.
"""
from pathlib import Path
import argparse, hashlib, json
import numpy as np
import trimesh

LEVELS=('MASTER','LOD0','LOD1','LOD2','LOD3')
TRANSFORMS={
 'Rail':np.array([0.0,-1.17,1.03]),
 'Return':np.array([-1.8,-1.17,1.03]),
 'ReturnKerb':np.array([-1.8,-1.35,1.03]),
 'Hatch':np.array([0.0,1.356,0.72]),
}

def triangles(scene): return sum(len(g.faces) for g in scene.geometry.values())

def add_scene(dst,src,prefix,translation):
 for node in src.graph.nodes_geometry:
  T,name=src.graph[node];g=src.geometry[name].copy()
  if not np.allclose(T,np.eye(4),atol=1e-8):g.apply_transform(T)
  g.apply_translation(translation);nn=f'{prefix}_{name}';dst.add_geometry(g,node_name=nn,geom_name=nn)

def build(drained,guardrail,soffit,out):
 out.mkdir(parents=True,exist_ok=True);records=[]
 for level in LEVELS:
  old=trimesh.load(drained/f'BalconyExteriorHeroDrained3600_{level}.glb',force='scene',process=False)
  new=trimesh.Scene();removed=[]
  for node in old.graph.nodes_geometry:
   T,name=old.graph[node]
   if node.startswith('ExistingHero_Rail_') or node.startswith('ExistingHero_Return_') or node.startswith('ExistingHero_Hatch_'):
    removed.append(node);continue
   g=old.geometry[name].copy()
   if not np.allclose(T,np.eye(4),atol=1e-8):g.apply_transform(T)
   new.add_geometry(g,node_name=node,geom_name=node)
  add_scene(new,trimesh.load(guardrail/'PowderCoatedBalconyGuardrail3600x1100'/f'PowderCoatedBalconyGuardrail3600x1100_{level}.glb',force='scene',process=False),'Repair_Rail',TRANSFORMS['Rail'])
  add_scene(new,trimesh.load(guardrail/'BalconyGuardrailCornerReturn1200x1100'/f'BalconyGuardrailCornerReturn1200x1100_{level}.glb',force='scene',process=False),'Repair_Return',TRANSFORMS['Return'])
  add_scene(new,trimesh.load(guardrail/'ConcreteBalconyKerbReturn1200x180x120'/f'ConcreteBalconyKerbReturn1200x180x120_{level}.glb',force='scene',process=False),'Repair_ReturnKerb',TRANSFORMS['ReturnKerb'])
  add_scene(new,trimesh.load(soffit/'BalconySoffitAccessHatch450'/f'BalconySoffitAccessHatch450_{level}.glb',force='scene',process=False),'Repair_Hatch',TRANSFORMS['Hatch'])
  path=out/f'BalconyExteriorHeroDrainedStructuralRepair3600_{level}.glb';path.write_bytes(new.export(file_type='glb'))
  rd=trimesh.load(path,force='scene',process=False);assert triangles(rd)==triangles(new)
  assert np.isfinite(np.vstack([g.vertices for g in rd.geometry.values()])).all()
  records.append({'level':level,'triangles':triangles(rd),'geometryCount':len(rd.geometry),'removedNodes':removed,'boundsMetres':rd.bounds.tolist(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
 assert all(a['triangles']>b['triangles'] for a,b in zip(records,records[1:])),records
 (out/'integration_verification.json').write_text(json.dumps({'records':records,'transforms':{k:v.tolist() for k,v in TRANSFORMS.items()},'formalBenchmarkSceneChanged':False,'unityCompile':False,'unityImport':False,'unityRender':False,'visualFidelity':{'score':None,'pass':False,'pointsAwarded':0}},indent=2)+'\n')
 return records

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--drained-dir',type=Path,required=True);p.add_argument('--guardrail-dir',type=Path,required=True);p.add_argument('--soffit-dir',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();print(json.dumps(build(a.drained_dir,a.guardrail_dir,a.soffit_dir,a.output),indent=2))
