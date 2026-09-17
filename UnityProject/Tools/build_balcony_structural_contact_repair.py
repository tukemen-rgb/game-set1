"""Refine existing external Drained3600 meshes, preserving drain and hose parts.

This continues the existing BalconyStructuralContactRepair integration and its
generate_balcony_guardrail_set_v2 / generate_balcony_soffit_set_v2 owners. This assembler does not mutate Unity scenes or
replace QualityBlock's separate, existing runtime owners. Input files stay intact.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import platform
from pathlib import Path

import numpy as np
import trimesh
from shapely.geometry import Point, Polygon
from shapely.ops import unary_union

import generate_balcony_guardrail_set_v2 as guard
import generate_balcony_soffit_set_v2 as soffit
import build_balcony_drainage_integration as drainage
import render_material_preview as preview

LEVELS=('MASTER','LOD0','LOD1','LOD2','LOD3')
EXPECTED_TRIANGLES=(181180,101684,48720,25284,15584)
REPLACE_PREFIXES=('ExistingHero_Rail_', 'ExistingHero_Return_',
                  'ExistingHero_Soffit_', 'ExistingHero_Hatch_')
DRAIN_PREFIXES=('DrainIntegratedFloor_', 'FloorDrain100_', 'RoutedCondensate_')
REBUILD_FLOOR=('DrainIntegratedFloor_SlopedMortarWithRealDrainCutout',
               'DrainIntegratedFloor_WaterproofSkinWithRealDrainCutout')
RAIL_Y=-1.17
RAIL_Z=1.03
RETURN_X=-1.72
RETURN_LENGTH=.970
SOFFIT_ORIGIN=np.array([0.,1.37,.60])
OPENING=(.24,-.21,.66,.21)


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def finished_floor_y(z):
    # Existing BalconyDrainageIntegration.authoring.json floor and skin surface.
    return -1.2470-.015*z+.0008


def geometry_record(scene, node):
    transform,name=scene.graph[node]
    g=scene.geometry[name]
    h=hashlib.sha256()
    for a,dtype in ((transform,'<f8'),(g.vertices,'<f8'),(g.faces,'<i8')):
        h.update(np.asarray(a,dtype=dtype).tobytes())
    uv=getattr(g.visual,'uv',None)
    if uv is not None:h.update(np.asarray(uv,dtype='<f8').tobytes())
    return h.hexdigest()


def map_record(material):
    h=hashlib.sha256()
    for name in ('metallicRoughnessTexture','normalTexture'):
        tex=getattr(material,name,None)
        if tex is not None:h.update(np.asarray(tex).tobytes())
    h.update(repr((material.metallicFactor,material.roughnessFactor)).encode())
    return h.hexdigest()


def materials_for(scene, prefix):
    return {g.visual.material.name:g.visual.material for n,g in scene.geometry.items()
            if n.startswith(prefix)}


def append_parts(scene, prefix, parts, materials, transform):
    """Preserve existing per-owner PBR maps while rebuilding only owned parts."""
    grouped={}
    for part in parts:
        grouped.setdefault(part.metadata['material'],[]).append(part)
    for material,group in grouped.items():
        vs=[];fs=[];ns=[];uvs=[];offset=0
        for part in group:
            vs.append(part.vertices);fs.append(part.faces+offset)
            ns.append(part.vertex_normals)
            uvs.append(guard.uv(part,.45 if material in ('WeatheredConcrete','CementMortar') else .12))
            offset+=len(part.vertices)
        mesh=trimesh.Trimesh(vertices=np.vstack(vs),faces=np.vstack(fs),
                            vertex_normals=np.vstack(ns),process=False)
        mesh.visual=trimesh.visual.TextureVisuals(uv=np.vstack(uvs),
                                                material=copy.deepcopy(materials[material]))
        mesh.apply_transform(transform)
        name=prefix+material
        scene.add_geometry(mesh,node_name=name,geom_name=name)


def support_pad(x,z,top_y):
    """Small modeled mortar seat: sloped underside, level EPDM bearing surface."""
    mesh=trimesh.creation.box(extents=[.105,1.,.120])
    vertices=np.asarray(mesh.vertices).copy()
    upper=vertices[:,1]>0
    vertices[:,0]+=x;vertices[:,2]+=z
    vertices[:,1]=np.where(upper,top_y,finished_floor_y(vertices[:,2]))
    mesh.vertices=vertices
    mesh.metadata.update(name=f'ReturnMortarSeat_{z:.3f}',material='CementMortar')
    return mesh


def check_parts(parts):
    for part in parts:
        assert np.isfinite(part.vertices).all()
        assert np.isfinite(part.vertex_normals).all()
        assert np.all(part.area_faces>1e-14),part.metadata['name']
        assert part.is_watertight and part.is_winding_consistent,part.metadata['name']
        assert part.volume>0,part.metadata['name']


def floor_coverage(mesh):
    """Measure the actual upward-facing triangles, not their authoring metadata."""
    triangles=mesh.triangles[mesh.face_normals[:,1]>.99]
    return unary_union([Polygon(t[:,[0,2]]) for t in triangles])


def check_floor(floor, original, level):
    coverage=floor_coverage(floor[1])
    old=floor_coverage(original)
    # These locations exposed the bad rectangle ordering in the old mesh.
    bearing=[Point(.45+.049*np.cos(a),.82+.049*np.sin(a))
             for a in np.linspace(0,2*np.pi,16,endpoint=False)]
    interior=[Point(0.,.5),Point(1.0,.5),Point(1.7,.5),*bearing]
    assert all(coverage.covers(p) for p in interior)
    assert coverage.disjoint(Point(.45,.82).buffer(.0425))
    assert not coverage.covers(Point(1.69,.09))
    assert coverage.covers(Point(1.79,.09))
    assert 3.453<coverage.area<3.455
    assert coverage.symmetric_difference(drainage.floor_polygon(level)).area<1e-6
    mortar=floor[0];skin=floor[1]
    upper=mortar.vertices[np.isclose(mortar.vertices[:,1],
                                   finished_floor_y(mortar.vertices[:,2])-.004)]
    lower=skin.vertices[np.isclose(skin.vertices[:,1],
                                  finished_floor_y(skin.vertices[:,2])-.004)]
    assert len(upper)==len(lower)>0
    assert np.allclose(upper[np.lexsort(upper.T)],lower[np.lexsort(lower.T)],atol=1e-9)
    return {'originalTopPlanAreaM2':old.area,'repairedTopPlanAreaM2':coverage.area,
            'originalFlangeBearingSamplesSupported':sum(old.covers(p) for p in bearing),
            'repairedFlangeBearingSamplesSupported':len(bearing),
            'actualTopTriangleCoverageVerified':True,'clearApertureRadiusMm':42.5,
            'mortarMembraneJoinGapMm':0.0}


def refine(source, level):
    scene=trimesh.Scene()
    unchanged={}
    for node in source.graph.nodes_geometry:
        transform,name=source.graph[node]
        if name.startswith(REPLACE_PREFIXES) or name in REBUILD_FLOOR:continue
        scene.add_geometry(source.geometry[name].copy(),node_name=node,
                           geom_name=name,transform=transform)
        unchanged[node]=geometry_record(source,node)

    rail=guard.main_parts(level)
    return_positions={1:RETURN_LENGTH/2,2:RETURN_LENGTH-.08}
    base_offsets={i:finished_floor_y(RAIL_Z-z)+.006-RAIL_Y
                  for i,z in return_positions.items()}
    returned=guard.corner_parts(level,length=RETURN_LENGTH,base_offsets=base_offsets,
                                omit_first_post=True,butt_join=True)
    ceiling=soffit.soffit_parts(level,opening=OPENING)
    hatch=soffit.ceiling_hatch_parts(level)
    seats=[support_pad(RETURN_X,RAIL_Z-z,RAIL_Y+base_offsets[i]-.003)
           for i,z in return_positions.items()]
    floor=drainage.floor_parts(level)
    for parts in (rail,returned,ceiling,hatch,seats,floor):check_parts(parts)
    floor_checks=check_floor(floor,source.geometry[REBUILD_FLOOR[1]],level)

    rail_transform=np.eye(4);rail_transform[:3,3]=[0,RAIL_Y,RAIL_Z]
    return_transform=trimesh.transformations.rotation_matrix(np.pi,[0,1,0])
    return_transform[:3,3]=[RETURN_X,RAIL_Y,RAIL_Z]
    ceiling_transform=np.eye(4);ceiling_transform[:3,3]=SOFFIT_ORIGIN
    append_parts(scene,'ExistingHero_Rail_',rail,materials_for(source,'ExistingHero_Rail_'),rail_transform)
    append_parts(scene,'ExistingHero_Return_',returned,materials_for(source,'ExistingHero_Return_'),return_transform)
    append_parts(scene,'ExistingHero_Soffit_',ceiling,materials_for(source,'ExistingHero_Soffit_'),ceiling_transform)
    append_parts(scene,'ExistingHero_Hatch_',hatch,materials_for(source,'ExistingHero_Hatch_'),ceiling_transform)
    mortar=materials_for(source,'DrainIntegratedFloor_')
    append_parts(scene,'ExistingHero_ReturnSupport_',seats,mortar,np.eye(4))
    for mesh in floor:
        name='DrainIntegratedFloor_'+mesh.metadata['name']
        original=source.geometry[name].visual.material
        mesh.visual=trimesh.visual.TextureVisuals(uv=mesh.vertices[:,[0,2]]/.45,
                                                material=copy.deepcopy(original))
        scene.add_geometry(mesh,node_name=name,geom_name=name)

    pickets=[p for p in rail+returned if 'Picket' in p.metadata['name']]
    top_gap=max(abs(p.bounds[1,1]-guard.PICKET_TOP) for p in pickets)
    assert top_gap<1e-8
    # All open-board/furring volumes clear the complete specified hatch aperture.
    x0,z0,x1,z1=OPENING
    for p in ceiling:
        if not p.metadata['name'].startswith(('Panel_','Furring_')):continue
        lo,hi=p.bounds
        assert hi[0]<=x0+1e-8 or lo[0]>=x1-1e-8 or hi[2]<=z0+1e-8 or lo[2]>=z1-1e-8
    leaf_vertices=np.vstack([p.vertices for p in hatch if p.metadata['name'].startswith('HatchLeaf_')])
    assert np.ptp(leaf_vertices[:,1])<.009
    bearing=[]
    for i,z in return_positions.items():
        pad=next(p for p in returned if p.metadata['name']==f'CPad{i}')
        plate=next(p for p in returned if p.metadata['name']==f'CBase{i}')
        post=next(p for p in returned if p.metadata['name']==f'CPost{i}')
        world=trimesh.transform_points(pad.vertices,return_transform)
        assert world[:,0].min()>-1.8 and world[:,0].max()<1.8
        assert world[:,2].min()>0 and world[:,2].max()<.97
        gap=abs(pad.bounds[1,1]-plate.bounds[0,1])
        post_gap=abs(post.bounds[0,1]-plate.bounds[1,1])
        assert gap<1e-8 and post_gap<1e-8
        bearing.append({'post':i,'floorZ':RAIL_Z-z,'padPlateGapMm':gap*1000,
                        'postPlateGapMm':post_gap*1000,'mortarSeat':True})
    for node,digest in unchanged.items():assert geometry_record(scene,node)==digest,node
    return scene,unchanged,{
        'topPicketGapMm':top_gap*1000,
        'bottomPicketGapMm':max(abs(p.bounds[0,1]-(.16+.035/2)) for p in pickets)*1000,
        'frontPicketCount':sum('Picket' in p.metadata['name'] for p in rail),
        'returnPicketCount':sum('Picket' in p.metadata['name'] for p in returned),
        'hatchLeafWorldYThicknessMm':np.ptp(leaf_vertices[:,1])*1000,
        'hatchOpeningXZLocal':list(OPENING),'returnBearings':bearing,
        'retainedNodeCount':len(unchanged),
        'drainageNodesPreserved':sum(n.startswith(DRAIN_PREFIXES) for n in unchanged),
        'floorRepair':{'replacedNodes':list(REBUILD_FLOOR),
                      **floor_checks,
                      'realFloorDrainAperture':True,'rainwaterNotchXZ':[1.6,0.,1.78,.18],
                      'drainAndHoseVerticesFacesTransformsUVUnchanged':True},
        'componentFiniteWatertightWindingPositiveVolume':True}


def run(input_root,output,render=False):
    output.mkdir(parents=True,exist_ok=False)
    rows=[];before=None;after=None
    for level,expected in zip(LEVELS,EXPECTED_TRIANGLES):
        path=input_root/f'BalconyExteriorHeroDrained3600_{level}.glb'
        source=trimesh.load(path,force='scene',process=False)
        assert sum(len(g.faces) for g in source.geometry.values())==expected
        assert any(n.startswith('RoutedCondensate_') for n in source.geometry)
        repaired,unchanged,checks=refine(source,level)
        colored,changes=preview.colorize(repaired)
        for n in repaired.geometry:
            assert map_record(repaired.geometry[n].visual.material)==map_record(colored.geometry[n].visual.material),n
        stem=f'BalconyExteriorHeroDrained3600_InterfacesColored_{level}'
        glb=output/(stem+'.glb');glb.write_bytes(colored.export(file_type='glb'))
        reload=trimesh.load(glb,force='scene',process=False)
        for node,digest in unchanged.items():assert geometry_record(reload,node)==digest,node
        for name in colored.geometry:
            assert map_record(colored.geometry[name].visual.material)==map_record(reload.geometry[name].visual.material),name
        check_floor([reload.geometry[n] for n in REBUILD_FLOOR],
                    source.geometry[REBUILD_FLOOR[1]],level)
        assert np.allclose(reload.bounds,colored.bounds,atol=2e-6)
        # OBJ/MTL companion comes from exactly the same colored scene.
        od=output/'obj'/level;od.mkdir(parents=True)
        obj,files=trimesh.exchange.obj.export_obj(colored,include_normals=True,include_texture=True,return_texture=True)
        objpath=od/(stem+'.obj');objpath.write_text(obj,encoding='utf-8')
        for name,data in files.items():
            dst=od/name
            dst.write_text(data,encoding='utf-8') if isinstance(data,str) else dst.write_bytes(data)
        obj_reload=trimesh.load(objpath,force='scene',process=False)
        count=sum(len(g.faces) for g in reload.geometry.values())
        assert sum(len(g.faces) for g in obj_reload.geometry.values())==count
        rows.append({'level':level,'input':path.name,'inputSHA256':sha(path),
                     'output':glb.name,'outputSHA256':sha(glb),'triangles':count,
                     'checks':checks,'paletteChanges':changes,'objRoundtrip':True})
        print(json.dumps({'level':level,'triangles':count,'checks':checks}),flush=True)
        if level=='LOD0':before=preview.colorize(source)[0];after=reload
    assert len({r['checks']['frontPicketCount'] for r in rows})==1
    assert len({r['checks']['returnPicketCount'] for r in rows})==1
    report={'schema':1,'worldSetting':'1990s Osaka, Senri-Chuo; exact year undecided',
            'integrationOwner':'BalconyStructuralContactRepair',
            'continuedCommit':'e567715cc389d8c91c256a9b74e27c2da624bebc',
            'componentOwners':['BalconyGuardrailSet','BalconySoffitSet'],
            'inputs':'Original Drained3600 meshes; Grounded3600 was not substituted',
            'records':rows,'python':platform.python_version(),'numpy':np.__version__,
            'trimesh':trimesh.__version__,'sourceSHA256':{
                Path(m.__file__).name:sha(m.__file__) for m in (guard,soffit,drainage,preview)},
            'assemblerSHA256':sha(__file__),'unityCompile':False,'unityImport':False,
            'unityRender':False,'temporalVerified':False,'performanceVerified':False,
            'visualFidelity':{'score':None,'pass':False},
            'limits':['External geometry and material preview only; not Unity evidence.',
                      'All dimensions and connection details are modeling assumptions.',
                      'Unchanged source glazing/UV/normal and other construction defects remain.',
                      'Current assets are not proof of historical product authenticity.']}
    (output/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
    if render:
        both=np.vstack((before.bounds,after.bounds));lo=both.min(0);hi=both.max(0)
        shared=trimesh.bounds.corners(np.stack((lo,hi)))
        settings=[
            ('overall_comparison',22,10,shared,1800,1260),
            ('ceiling_detail',18,-16,[[-.02,1.20,.25],[.90,1.45,.95]],1500,1000),
            ('rail_detail',26,8,[[-.40,-.32,.97],[.42,.02,1.1]],1500,1000),
            ('return_support',-35,20,[[-1.86,-1.39,.02],[-1.50,-.83,1.16]],1400,1100)]
        for label,az,el,bounds,width,height in settings:
            for suffix,scene in [('before',before),('after',after)]:
                preview.render(scene,output/'previews'/f'{label}_{suffix}.png',az,el,width,height,
                               title=f'Drained3600 | {label.replace("_"," ")} | {suffix}',crop_bounds=bounds)
        preview.render(after,output/'previews'/'overall_after.png',22,10,1800,1260,
                       title='Drained3600 | repaired ceiling, rail connections and return supports')
        preview.render(after,output/'previews'/'drain_detail_after.png',25,60,1500,1000,
                       title='Drained3600 | floor repaired, existing drain and hose retained',
                       crop_bounds=trimesh.bounds.corners(np.array([[.30,-1.37,.62],[.75,-1.13,.94]])))
    return report


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input-root','--drained-dir',dest='input_root',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--render',action='store_true')
    parser.add_argument('--guardrail-dir',type=Path,help='Deprecated: components now rebuild from the versioned owner source.')
    parser.add_argument('--soffit-dir',type=Path,help='Deprecated: components now rebuild from the versioned owner source.')
    args=parser.parse_args()
    if args.guardrail_dir or args.soffit_dir:
        print('Component directories are superseded by the versioned v2 owner source; original Drained GLBs supply the retained parts and PBR maps.',flush=True)
    run(args.input_root,args.output,args.render)
