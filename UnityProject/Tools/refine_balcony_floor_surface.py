"""Apply the existing drainage owner's surface repair to an entire supplied hero.

No props are added, removed or repositioned. Input may include the later plants
and laundry; all nodes outside the four named floor/curb surfaces are preserved.
"""
from pathlib import Path
import argparse,hashlib,json
import numpy as np
import trimesh
import build_balcony_drainage_integration as owner
import render_material_preview as preview

LEVELS=('MASTER','LOD0','LOD1','LOD2','LOD3')


def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def record(scene,node,full=True):
    transform,name=scene.graph[node];g=scene.geometry[name];h=hashlib.sha256()
    for a in (transform,g.triangles):h.update(np.asarray(a,dtype='<f8').tobytes())
    if full:
        for a in (g.vertices,g.faces,g.vertex_normals,g.visual.uv):
            h.update(np.asarray(a,dtype='<f4').tobytes())
        m=g.visual.material
        for attr in ('baseColorFactor','metallicFactor','roughnessFactor','doubleSided'):
            h.update(str(getattr(m,attr,None)).encode())
        for attr in ('baseColorTexture','metallicRoughnessTexture','normalTexture'):
            im=getattr(m,attr,None)
            if im is not None:h.update(str(im.size).encode()+im.tobytes())
    return h.hexdigest()


def run(input_root,input_pattern,output,render=False):
    paths={level:input_root/input_pattern.format(level=level) for level in LEVELS}
    for path in paths.values():
        if not path.is_file():raise FileNotFoundError(path)
    output.mkdir(parents=True,exist_ok=False)
    materials,material_records=owner.create_surface_materials(output/'textures')
    rows=[];before=None;after=None
    for level,path in paths.items():
        source=trimesh.load(path,force='scene',process=False)
        target,changes=owner.apply_surface_materials(source,materials)
        assert set(source.graph.nodes_geometry)==set(target.graph.nodes_geometry)
        names=set(owner.SURFACE_TARGETS)&set(source.geometry)
        source_records={n:record(source,n,source.graph[n][1] not in names) for n in source.graph.nodes_geometry}
        for node,digest in source_records.items():
            assert record(target,node,target.graph[node][1] not in names)==digest,node
        out=output/(path.stem+'_SurfaceRefined.glb');out.write_bytes(target.export(file_type='glb'))
        loaded=trimesh.load(out,force='scene',process=False)
        for node,digest in source_records.items():
            assert record(loaded,node,loaded.graph[node][1] not in names)==digest,node
        for name in names:
            assert owner.uv_degenerate_count(loaded.geometry[name])==0,name
            assert np.allclose(loaded.geometry[name].visual.uv,target.geometry[name].visual.uv,atol=1e-7)
        count=sum(len(g.faces) for g in loaded.geometry.values())
        assert count==sum(len(g.faces) for g in source.geometry.values())
        od=output/'obj'/level;od.mkdir(parents=True)
        obj,files=trimesh.exchange.obj.export_obj(loaded,include_normals=True,include_texture=True,return_texture=True)
        op=od/(out.stem+'.obj');op.write_text(obj)
        for name,data in files.items():
            dst=od/name
            dst.write_text(data) if isinstance(data,str) else dst.write_bytes(data)
        re=trimesh.load(op,force='scene',process=False)
        assert sum(len(g.faces) for g in re.geometry.values())==count
        row={'level':level,'input':path.name,'inputSHA256':sha(path),'output':out.name,'outputSHA256':sha(out),
             'triangles':count,'changedSurfaces':changes,'nodeCount':len(source_records),
             'allNodeTransformsPreserved':True,'allTriangleCornersAndWindingPreserved':True,
             'allOtherGeometryNormalsUVMaterialsPreserved':True,'glbRoundtrip':True,'objRoundtrip':True}
        rows.append(row);print(json.dumps({'level':level,'triangles':count,'changedSurfaces':len(changes)}),flush=True)
        if level=='LOD0':before=source;after=loaded
    if render:
        bounds=trimesh.bounds.corners(np.array([[.25,-1.39,.58],[.85,-1.11,1.14]]))
        for suffix,scene in [('before',before),('after',after)]:
            preview.render(scene,output/'previews'/f'floor_detail_{suffix}.png',25,62,1600,1100,
                           title=f'Building component | floor and curb | {suffix}',crop_bounds=bounds)
        preview.render(after,output/'previews'/'building_overall.png',22,10,1800,1260,
                       title='Building component | current facade source + refined dry floor')
        preview.render(after,output/'previews'/'floor_detail_side_light.png',25,62,1600,1100,
                       title='Same dry floor | side lighting | unchanged material',crop_bounds=bounds,
                       light_direction=[1.2,.8,1.5])
        preview.render(after,output/'previews'/'return_foot_detail.png',-35,20,1500,1100,
                       title='Building component | floor grain and return footing',
                       crop_bounds=trimesh.bounds.corners(np.array([[-1.86,-1.39,.02],[-1.50,-.83,1.16]])))
    report={'schema':1,'owner':'BalconyDrainageIntegration; existing curb material refined in place',
            'materials':material_records,'records':rows,
            'sourceSHA256':{Path(m.__file__).name:sha(m.__file__) for m in (owner,preview)},
            'assemblerSHA256':sha(__file__),
            'unityCompile':False,'unityImport':False,'unityRender':False,'temporalVerified':False,
            'visualFidelity':{'score':None,'pass':False},
            'limits':['External CPU previews; no Unity glass/reflection/GI parity.',
                      'All material dimensions are authored assumptions, not historical product identification.',
                      'No complete-scene claim: the output contains exactly the input nodes supplied to this run.']}
    (output/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
    return report


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--input-root',type=Path,required=True)
    p.add_argument('--input-pattern',required=True,help='Filename template with {level}; accepts later plant/laundry heroes.')
    p.add_argument('--output',type=Path,required=True);p.add_argument('--render',action='store_true')
    a=p.parse_args();run(a.input_root,a.input_pattern,a.output,a.render)
