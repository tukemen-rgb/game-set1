from __future__ import annotations
import argparse, json, math, hashlib
from pathlib import Path
import numpy as np, trimesh
LEVELS=("MASTER","LOD0","LOD1","LOD2","LOD3")

def tri(scene): return sum(len(g.faces) for g in scene.geometry.values())
def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--hero',type=Path,required=True);ap.add_argument('--asset',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);a=ap.parse_args();a.output.mkdir(parents=True,exist_ok=True)
 reports=[]
 for level in LEVELS:
  hero_path=a.hero/f'BalconyExteriorHeroSummerMorningGloryCanopy3600_{level}.glb'
  asset_path=a.asset/f'BalconySummerTomatoPlanter650_{level}.glb'
  hero=trimesh.load(hero_path,force='scene',process=False); asset=trimesh.load(asset_path,force='scene',process=False)
  # derive actual waterproof floor plane y=a+b*z from current hero
  floor_nodes=[n for n in hero.graph.nodes_geometry if 'WaterproofSkinWithRealDrainCutout' in n]
  if not floor_nodes: raise RuntimeError('floor owner not found')
  vals=[]
  for n in floor_nodes:
   T,gname=hero.graph[n];g=hero.geometry[gname];v=trimesh.transform_points(g.vertices,T); vals.append(v)
  v=np.vstack(vals); A=np.c_[np.ones(len(v)),v[:,2]]; coeff=np.linalg.lstsq(A,v[:,1],rcond=None)[0]; intercept,slope=map(float,coeff)
  x0,z0=-.60,.64; y0=intercept+slope*z0; theta=math.atan(-slope)
  R=np.eye(4);c=math.cos(theta);s=math.sin(theta);R[1,1]=c;R[1,2]=-s;R[2,1]=s;R[2,2]=c
  root=np.eye(4);root[:3,3]=[x0,y0,z0]; root=root@R
  out=hero.copy(); world_all=[]
  for node in asset.graph.nodes_geometry:
   Ta,gname=asset.graph[node];g=asset.geometry[gname].copy(); Tw=root@Ta; new=f'BalconySummerTomatoPlanter_{node}'
   out.add_geometry(g,node_name=new,geom_name=new,transform=Tw)
   world_all.append(trimesh.transform_points(g.vertices,Tw))
  w=np.vstack(world_all);lo=w.min(0);hi=w.max(0)
  # derive relevant obstacle AABBs conservatively
  def bounds_for(pattern):
   arr=[]
   for n in hero.graph.nodes_geometry:
    if pattern in n:
     T,gn=hero.graph[n]; arr.append(trimesh.transform_points(hero.geometry[gn].vertices,T))
   if not arr:return None
   q=np.vstack(arr);return q.min(0),q.max(0)
  rail=bounds_for('_Hero_Repair_Rail_PowderCoatedSteel')
  morning=[]
  for n in hero.graph.nodes_geometry:
   if n.startswith('BalconyMorningGloryBotanicalPlanter_Plant_'):
    T,gn=hero.graph[n];morning.append(trimesh.transform_points(hero.geometry[gn].vertices,T))
  mb=np.vstack(morning);mlo,mhi=mb.min(0),mb.max(0)
  ac=bounds_for('_Hero_ExistingHero_AC_')
  clear={
   'rearToFacadePlaneMm':float(lo[2]*1000),
   'frontToMainRailInnerMm':float((rail[0][2]-hi[2])*1000) if rail else None,
   'leftToMorningGloryCanopyMm':float((lo[0]-mhi[0])*1000),
   'rightToACMm':float((ac[0][0]-hi[0])*1000) if ac else None,
  }
  if min(x for x in clear.values() if x is not None) < 35: raise RuntimeError((level,'clearance',clear,lo,hi))
  out_path=a.output/f'BalconyExteriorHeroSummerTomatoMorningGlory3600_{level}.glb'; out_path.write_bytes(out.export(file_type='glb'))
  re=trimesh.load(out_path,force='scene',process=False); expected=tri(hero)+tri(asset);actual=tri(re)
  if actual!=expected:raise RuntimeError((level,expected,actual))
  reports.append({'level':level,'heroInputTriangles':tri(hero),'assetTriangles':tri(asset),'outputTriangles':actual,'floorPlaneY':{'intercept':intercept,'slopePerZ':slope},'rootTranslation':[x0,y0,z0],'rootSlopeRotationDeg':math.degrees(theta),'worldBounds':w.tolist()[:0] or [lo.tolist(),hi.tolist()],'clearancesMm':clear,'glbRoundtrip':True,'sha256':sha(out_path)})
  print(level,actual,clear)
 (a.output/'tomato_hero_integration_report.json').write_text(json.dumps({'schema':1,'date':'2026-09-17','assetId':'BalconyExteriorHeroSummerTomatoMorningGlory3600','method':'Add distinct tomato planter owner at slope-derived floor transform; morning-glory and all existing hero nodes are preserved unchanged.','levels':reports,'unityVerified':False},indent=2)+'\n')
if __name__=='__main__':main()
