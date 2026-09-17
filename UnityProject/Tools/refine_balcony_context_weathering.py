"""Weather the existing Drained building surfaces without replacing the hero.

Input may contain later plants/laundry. Exactly the supplied scene is preserved,
with material/UV changes restricted to named floor, curb, facade and steel nodes.
This pass does not run Unity or imply formal benchmark fidelity approval.
"""
from pathlib import Path
import argparse, hashlib, json, copy, platform, importlib.metadata
import numpy as np
import trimesh
import balcony_context_materials as weather
import build_balcony_drainage_integration as drainage
import refine_window_service_facade_surface as facade
import render_material_preview as preview
from refine_balcony_floor_surface import record, sha

LEVELS=('MASTER','LOD0','LOD1','LOD2','LOD3')
STEEL=('ExistingHero_Rail_PowderCoatedSteel','ExistingHero_Return_PowderCoatedSteel')
SURFACES=('DrainIntegratedFloor_WaterproofSkinWithRealDrainCutout','ExistingHero_Kerb_WeatheredConcrete')


def plate_bounds(g, front):
    parts=g.split(only_watertight=False)
    result=[]
    for p in parts:
        b=p.bounds;size=b[1]-b[0];center=b.mean(0)
        if .006<size[1]<.013 and .080<size[0]<.14 and .080<size[2]<.14:
            result.append({'x':float(center[0]),'y':float(center[1]),'z':float(center[2]),'half_x':float(size[0]/2),'half_z':float(size[2]/2),'bounds':b.tolist()})
    result.sort(key=lambda p:p['x'] if front else p['z'])
    if len(result)!=(5 if front else 2):raise ValueError('Unexpected physical base plate layout')
    return result


def measured_context(scene):
    # The maps are authored in this hero's local frame. Transformed scene nodes
    # fail clearly rather than receiving stains from an incorrect coordinate set.
    required=list(STEEL)+list(SURFACES)+['FloorDrain100_PVCFloorFlange','ExistingHero_Vent_PaintedMetal']
    for name in required:
        if name not in scene.geometry:raise ValueError('Missing contextual source: '+name)
    for node in scene.graph.nodes_geometry:
        transform,name=scene.graph[node]
        if (name in required or facade.is_panel(name) or name.startswith('ExistingHero_RainClamp')) and not np.allclose(transform,np.eye(4),atol=1e-8):
            raise ValueError('Context expects existing identity-frame geometry: '+node)
    front=plate_bounds(scene.geometry[STEEL[0]],True)
    returns=plate_bounds(scene.geometry[STEEL[1]],False)
    d=scene.geometry['FloorDrain100_PVCFloorFlange'].bounds.mean(0)
    vent=scene.geometry['ExistingHero_Vent_PaintedMetal'].bounds
    sources=[]
    for side,length,width,amount,seed in [(0,.30,.031,.40,3),(1,.20,.022,.29,11)]:
        sources.append({'mesh':'ExistingHero_Vent_PaintedMetal','cause':'dry dust washed from lower mounting flange',
            'x':float(vent[side,0])+(.016 if side==0 else -.016),'y':float(vent[0,1]),'length':length,'width':width,'amount':amount,'seed':seed})
    for i in (0,1,2):
        name=f'ExistingHero_RainClamp{i}_GalvanizedSteel'
        if name not in scene.geometry:continue
        b=scene.geometry[name].bounds
        sources.append({'mesh':name,'cause':'dry dust runoff at exposed clamp wall fixture, no assumed pipe leak',
            'x':float(b[1,0]-.008),'y':float(b[0,1]),'length':.12+i*.021,'width':.018,'amount':.23-i*.025,'seed':31+i})
    return {'units':'metres','frame':'existing hero local frame','surfaceState':'dry','recentRain':False,
        'front_plates':front,'return_plates':returns,'drain_center':[float(d[0]),float(d[2])],
        'oxide_fixing':{'mesh':STEEL[0],'plate_index':1,'x':front[1]['x']+.034,'z':front[1]['z']+.046,
            'cause':'authored sparse coating damage at one measured steel base-plate corner'},
        'wall_runoff_sources':sources,'rustAffectedMainPlates':1,'mainPlateCount':5,
        'limits':['Stylized dry ageing hypothesis; no claim of measured historical maintenance condition.',
            'Projected chart positions approximate small bevels; error is reported for every changed mesh.',
            'No standing water, active leakage, thick corrosion or added structural damage.']}


def mask_checks(context):
    # Negative witnesses: deposits must not appear above their source, in the
    # clear walking area, or on the other four undamaged main base plates.
    clear=np.array([[0.,drainage.finished_floor_y(.4),.4],[.9,drainage.finished_floor_y(.6),.6]])
    f=weather.floor_masks(clear,np.tile([0,1,0],(len(clear),1)),context)
    assert max(float(v.max()) for v in f.values())==0.,f
    for s in context['wall_runoff_sources']:
        p=np.array([[s['x'],s['y']+.02,0.]])
        assert weather.streak(p,s['x'],s['y'],s['length'],s['width'],s['seed'])[0]==0
    for i,plate in enumerate(context['front_plates']):
        if i==context['oxide_fixing']['plate_index']:continue
        p=np.array([[plate['x']+.034,-1.1645,plate['z']+.046]])
        assert weather.steel_masks(p,np.array([[0,1,0]]),context)['oxide'][0]==0
    return {'clearFloorNoDeposits':True,'aboveEverySourceNoRunoff':True,'fourOtherMainPlatesNoRust':True,
        'authoredDryContext':True,'stainsAreMaterialPropertiesNotShadowMaps':True}


def scene_metrics(scene):
    return {'triangles':sum(len(g.faces) for g in scene.geometry.values()),'geometryCount':len(scene.geometry),'nodeCount':len(scene.graph.nodes_geometry)}


def run(input_root,pattern,output,levels=LEVELS,render=False):
    paths={level:input_root/pattern.format(level=level) for level in levels}
    for p in paths.values():
        if not p.is_file():raise FileNotFoundError(p)
    if 'LOD0' not in paths:raise ValueError('LOD0 input required for measured source context')
    source0=trimesh.load(paths['LOD0'],force='scene',process=False)
    context=measured_context(source0)
    checks=mask_checks(context)
    output.mkdir(parents=True,exist_ok=False)
    mat,facade_report=facade.create_context_weathered_facade(output/'textures'/'shared',context)
    rows=[]
    for level,path in paths.items():
        source=trimesh.load(path,force='scene',process=False)
        target,records=drainage.apply_context_weathering(source,context,output/'textures'/level)
        changed=set(SURFACES)
        for name in STEEL:
            target.geometry[name],records[name]=weather.bake_atlas(source.geometry[name],'steel',context,output/'textures'/level,name.replace('ExistingHero_','')+'_OccupiedDry',700)
            changed.add(name)
        for name,g in target.geometry.items():
            if facade.is_panel(name):
                g.visual.material=mat;changed.add(name)
        assert len([n for n in changed if facade.is_panel(n)])>0
        assert set(source.graph.nodes_geometry)==set(target.graph.nodes_geometry)
        before={n:record(source,n,source.graph[n][1] not in changed) for n in source.graph.nodes_geometry}
        for n,h in before.items():assert record(target,n,target.graph[n][1] not in changed)==h,n
        out=output/f'BalconyOccupiedDryWeathering3600_{level}.glb'
        # Re-normalizing already imported float32 normals moves some untouched
        # buffers by one ULP. Preserve their original values through export.
        out.write_bytes(target.export(file_type='glb',unitize_normals=False))
        loaded=trimesh.load(out,force='scene',process=False)
        for n,h in before.items():assert record(loaded,n,loaded.graph[n][1] not in changed)==h,n
        for name in changed:
            g=loaded.geometry[name];old=source.geometry[name]
            assert np.array_equal(g.triangles,old.triangles),name
            assert np.allclose(g.vertex_normals[g.faces],old.vertex_normals[old.faces],atol=2e-7),name
            m=g.visual.material
            assert np.asarray(m.metallicRoughnessTexture)[:,:,2].max()==0,name
            assert np.allclose(g.visual.uv,target.geometry[name].visual.uv,atol=1e-7),name
            for key in ('baseColorTexture','metallicRoughnessTexture','normalTexture'):
                assert getattr(m,key).tobytes()==getattr(target.geometry[name].visual.material,key).tobytes(),(name,key)
        metrics=scene_metrics(loaded);assert metrics==scene_metrics(source)
        # GLB is authoritative PBR; OBJ is a compatible geometry/base-colour copy.
        # The original PBR maps are included separately, not implied by MTL support.
        od=output/'obj'/level;od.mkdir(parents=True)
        text,files=trimesh.exchange.obj.export_obj(loaded,include_normals=True,include_texture=True,return_texture=True)
        op=od/(out.stem+'.obj');op.write_text(text)
        for name,data in files.items():
            dst=od/name
            dst.write_text(data) if isinstance(data,str) else dst.write_bytes(data)
        assert scene_metrics(trimesh.load(op,force='scene',process=False))['triangles']==metrics['triangles']
        row={'level':level,'input':path.name,'inputSHA256':sha(path),'output':out.name,'outputSHA256':sha(out),**metrics,
            'changedNames':sorted(changed),'materialReports':records,'allTransformsAndTrianglesPreserved':True,
            'allUntargetedMeshesNormalsUVMaterialsPreserved':True,'glbRoundtripVerified':True,'objTrianglesVerified':True}
        rows.append(row);print(json.dumps({'level':level,**metrics,'changed':len(changed)}),flush=True)
        if level=='LOD0':
            if render:render_views(source,loaded,output/'previews')
        del loaded,target,source
    report={'schema':1,'scope':'existing building-component surface ageing pass; preserve complete input scene',
        'context':context,'negativeWitnesses':checks,'facade':facade_report,'levels':rows,
        'runtime':{'python':platform.python_version(),**{n:importlib.metadata.version(n) for n in ['trimesh','numpy','Pillow','scipy','numba','shapely']}},
        'sourceSHA256':{Path(m.__file__).name:sha(m.__file__) for m in [weather,drainage,facade,preview]},'assemblerSHA256':sha(__file__),
        'unityCompile':False,'unityImport':False,'unityRender':False,'visualFidelity':{'score':None,'pass':False},
        'limits':['External CPU preview; no Unity reflection/GI/refraction parity or temporal/performance validation.',
            'This delivered run uses the restored building component. Later plants/laundry were not supplied.',
            'Do not replace a later complete hero with these building-only GLBs.',
            'GLB carries metallic/roughness/normal; OBJ/MTL is a geometry/base-colour interchange copy.']}
    (output/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
    return report


def render_views(before,after,out):
    views=[
        ('overall',22,10,1800,1260,None,None),
        ('floor_drain',25,62,1700,1200,[[.22,-1.38,.58],[.83,-1.10,1.14]],None),
        ('floor_side_view',-74,26,1700,1200,[[.25,-1.34,.65],[.70,-1.12,.99]],None),
        ('steel_fixing',-14,28,1650,1200,[[-1.06,-1.36,.90],[-.63,-.91,1.13]],None),
        ('vent_wall',-15,12,1600,1200,[[.64,.20,-.02],[1.10,.93,.25]],None),
        ('floor_side_light',25,62,1700,1200,[[.22,-1.38,.58],[.83,-1.10,1.14]],[1.2,.8,1.5]),
    ]
    for name,az,el,w,h,bounds,light in views:
        preview.render(after,out/(name+'.png'),az,el,w,h,title='Actual mesh | dry material ageing | '+name.replace('_',' '),
            crop_bounds=None if bounds is None else trimesh.bounds.corners(np.asarray(bounds)),light_direction=light,texture_size=4096)
    preview.render(before,out/'floor_before.png',25,62,1700,1200,title='Actual mesh | previous dry floor | same lighting',
        crop_bounds=trimesh.bounds.corners(np.asarray([[.22,-1.38,.58],[.83,-1.10,1.14]])),texture_size=4096)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--input-root',type=Path,required=True);p.add_argument('--input-pattern',required=True)
    p.add_argument('--output',type=Path,required=True);p.add_argument('--levels',nargs='+',choices=LEVELS,default=list(LEVELS));p.add_argument('--render',action='store_true')
    a=p.parse_args();run(a.input_root,a.input_pattern,a.output,a.levels,a.render)
