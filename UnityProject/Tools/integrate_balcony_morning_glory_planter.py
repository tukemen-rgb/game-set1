"""Replay the verified balcony morning-glory scene integration.

This intentionally consumes the already-authored planter GLBs rather than duplicating the
MorningGloryTrellisPlant, TerracottaPot240 or TerracottaSaucer214 owners. The exact mesh/PBR
authoring source and input assets are delivered in the conversation artifact
`balcony_morning_glory_planter_integration_2026-09-17.zip`.

External geometry replay only: this script does not prove Unity compile/import/render quality.
"""
from __future__ import annotations
from pathlib import Path
import argparse, math, numpy as np, trimesh

LEVELS=("MASTER","LOD0","LOD1","LOD2","LOD3")
EXPECTED_PLANTER={"MASTER":67032,"LOD0":39760,"LOD1":22756,"LOD2":8590,"LOD3":4204}
EXPECTED_BASE={"MASTER":181460,"LOD0":101932,"LOD1":48944,"LOD2":25456,"LOD3":15632}
X,Z=-1.45,0.55
FLOOR_Y0,FLOOR_SLOPE,FINISH=-1.2470,-0.015,0.0008
TILT=math.atan(0.015)

def tri(scene): return sum(len(g.faces) for g in scene.geometry.values())

def add_scene(dst,src,prefix,T=None):
    parent=np.eye(4) if T is None else np.asarray(T,float)
    for node in src.graph.nodes_geometry:
        local,name=src.graph[node]
        dst.add_geometry(src.geometry[name].copy(),node_name=f"{prefix}_{node}",geom_name=f"{prefix}_{name}",transform=parent@local)

def transform():
    t=trimesh.transformations.rotation_matrix(TILT,[1,0,0])
    t[0,3]=X;t[1,3]=FLOOR_Y0+FLOOR_SLOPE*Z+FINISH;t[2,3]=Z
    return t

def main():
    p=argparse.ArgumentParser();p.add_argument('--planter-root',type=Path,required=True);p.add_argument('--hero-root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    a.output.mkdir(parents=True,exist_ok=True);counts=[]
    for level in LEVELS:
        planter=trimesh.load(a.planter_root/f'BalconyMorningGloryPlanter_{level}.glb',force='scene',process=False)
        hero=trimesh.load(a.hero_root/f'BalconyExteriorHeroDrainedStructuralRepairFacadeRefined3600_{level}.glb',force='scene',process=False)
        assert tri(planter)==EXPECTED_PLANTER[level],(level,tri(planter));assert tri(hero)==EXPECTED_BASE[level],(level,tri(hero))
        scene=trimesh.Scene();add_scene(scene,hero,'Hero');add_scene(scene,planter,'BalconyMorningGloryPlanter',transform())
        out=a.output/f'BalconyExteriorHeroSummerMorningGlory3600_{level}.glb';out.write_bytes(scene.export(file_type='glb'))
        check=trimesh.load(out,force='scene',process=False);expected=EXPECTED_PLANTER[level]+EXPECTED_BASE[level]
        assert tri(check)==expected and np.isfinite(check.bounds).all();counts.append(expected)
    assert all(x>y for x,y in zip(counts,counts[1:]));print(dict(zip(LEVELS,counts)))
if __name__=='__main__':main()
