"""Physical-relief refinement for the existing ExteriorFacadeSet owner.

This is a reversible v2 authoring path, not a second facade system.  It keeps
ExteriorFacadeSet's 3.6 x 2.7 m layout, 16 mm panel assumption, 10 mm joints,
vent position/aperture, existing vent hood and starter flashing.  It improves
only the fiber-cement wall field: real perimeter bevels, sub-millimetre
installation plane tolerance, and one non-repeating whole-facade PBR texture.

No lighting, highlight or weathering is baked.  The current 16 mm board and
bevel dimensions are authoring assumptions, not a claim about a historical SKU.
"""
from __future__ import annotations

from pathlib import Path
import argparse
import hashlib
import json

import numpy as np
import trimesh
from PIL import Image, ImageFilter

import generate_exterior_facade_set as v1

LEVELS = v1.LEVELS
ASSET = 'PaintedFiberCementFacade3600x2700'
W, H, T = 3.6, 2.7, .016
VENT_X, VENT_Y, VENT_APERTURE = .92, .18, .19
JOINT = .010
XS = [-1.8, -.9, 0., .9, 1.8]
YS = [-1.35, -.45, .45, 1.35]

# Real geometry at close/mid LOD. LOD3 intentionally returns to the old box.
BEVEL = {
    'MASTER': (.0026, .0012),
    'LOD0':   (.0026, .0012),
    'LOD1':   (.0022, .0010),
    'LOD2':   (.0016, .0008),
    'LOD3':   (0., 0.),
}

# One value per nominal 900 x 900 mm board bay. Split pieces around the vent
# retain their parent bay's plane, avoiding fake steps through the opening.
# Max magnitude 0.30 mm: installation-tolerance assumption, not age damage.
PANEL_PLANE_Z = np.array([
     0.00000,  0.00018, -0.00012,
     0.00008, -0.00024,  0.00014,
    -0.00009,  0.00030, -0.00016,
     0.00012, -0.00005,  0.00020,
], dtype=float)


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def beveled_panel(width, height, thickness, center, bevel_width, bevel_depth,
                  name='Panel', material='PaintedFiberCement'):
    """Closed board with a real front perimeter chamfer.

    Back and side envelope keeps the authored module size.  The front face is
    inset by bevel_width and connected to a full-size shoulder bevel_depth
    behind it; this creates actual grazing-light edge response without a
    painted highlight or normal-only silhouette trick.
    """
    cx, cy, cz = map(float, center)
    if bevel_width <= 0 or bevel_depth <= 0:
        return v1.box([width, height, thickness], [cx, cy, cz], name, material)
    if bevel_width * 2 >= min(width, height):
        raise ValueError('bevel too large for panel')
    if bevel_depth >= thickness:
        raise ValueError('bevel depth must be less than panel thickness')

    hx, hy = width / 2, height / 2
    z_back = cz - thickness / 2
    z_shoulder = cz + thickness / 2 - bevel_depth
    z_front = cz + thickness / 2
    rings = [
        [(-hx,-hy,z_back),( hx,-hy,z_back),( hx, hy,z_back),(-hx, hy,z_back)],
        [(-hx,-hy,z_shoulder),( hx,-hy,z_shoulder),( hx, hy,z_shoulder),(-hx, hy,z_shoulder)],
        [(-hx+bevel_width,-hy+bevel_width,z_front),
         ( hx-bevel_width,-hy+bevel_width,z_front),
         ( hx-bevel_width, hy-bevel_width,z_front),
         (-hx+bevel_width, hy-bevel_width,z_front)],
    ]
    vertices = np.asarray([[x+cx, y+cy, z] for ring in rings for x,y,z in ring], dtype=float)
    faces = [[0,2,1],[0,3,2], [8,9,10],[8,10,11]]
    # Back outer ring -> shoulder outer ring; then shoulder -> inset front.
    for r0, r1 in ((0,4),(4,8)):
        for j in range(4):
            k = (j + 1) % 4
            a,b,c,d = r0+j, r0+k, r1+k, r1+j
            faces += [[a,b,c],[a,c,d]]
    mesh = trimesh.Trimesh(vertices=vertices, faces=np.asarray(faces), process=False)
    mesh.fix_normals(multibody=True)
    mesh.metadata.update(name=name, material=material,
                         bevelWidthM=float(bevel_width), bevelDepthM=float(bevel_depth))
    return mesh


def _normalized_field(rng, size, blur_radius):
    src = rng.normal(0, 1, (size, size))
    # Reflect-free authoring texture: the image never tiles inside the facade UV
    # domain, so blurred random structure is used instead of a repeated pattern.
    gray = np.uint8(np.clip(src * 28 + 128, 0, 255))
    img = Image.fromarray(gray, 'L').filter(ImageFilter.GaussianBlur(blur_radius))
    out = np.asarray(img, dtype=np.float32) / 255. - .5
    std = float(out.std())
    return out / max(std, 1e-6)


def textures_v2(directory: Path):
    """Build original v1 materials, replacing only fiber-cement maps.

    The fiber-cement maps cover the complete 3.6 x 2.7 m facade once.  Base
    color contains only small isotropic manufacturing/paint variation; there is
    no sun direction, shadow, leak streak or arbitrary dirt mask.
    """
    result = v1.textures(directory)
    rng = np.random.default_rng(16092642)
    size = 1024
    coarse = _normalized_field(rng, size, 42)
    medium = _normalized_field(rng, size, 13)
    fine = rng.normal(0, 1, (size, size)).astype(np.float32)
    fine /= max(float(fine.std()), 1e-6)
    field = .46 * coarse + .34 * medium + .20 * fine
    field -= float(field.mean())
    field /= max(float(field.std()), 1e-6)

    base = np.asarray([.58,.59,.56], dtype=np.float32)
    albedo = np.clip(base[None,None,:] * (1 + .010 * field[:,:,None]), 0, 1)
    rough = np.clip(.68 + .030 * (.60*medium + .40*fine), .54, .82)
    mr = np.zeros((size,size,3), dtype=np.uint8)
    mr[:,:,1] = np.uint8(np.round(rough * 255))
    # Painted fiber cement remains dielectric: metallic channel is exactly zero.
    mr[:,:,2] = 0

    # Height-proxy normal is isotropic and tiny; it contains no directional light.
    height = .60*medium + .40*fine
    gy, gx = np.gradient(height)
    normal_scale = .075
    n = np.dstack([-gx*normal_scale, -gy*normal_scale, np.ones_like(gx)])
    n /= np.maximum(np.linalg.norm(n, axis=2)[:,:,None], 1e-8)
    normal = np.uint8(np.round(np.clip(n*.5+.5, 0, 1)*255))

    bc = Image.fromarray(np.uint8(np.round(albedo*255)), 'RGB')
    mi = Image.fromarray(mr, 'RGB')
    ni = Image.fromarray(normal, 'RGB')
    bc.save(directory/'PaintedFiberCement_base_v2.png')
    mi.save(directory/'PaintedFiberCement_mr_v2.png')
    ni.save(directory/'PaintedFiberCement_normal_v2.png')
    result['PaintedFiberCement'] = (bc,mi,ni)
    return result


def facade_v2(level):
    sec, fast, _, _, bead, back = LEVELS[level]
    bevel_width, bevel_depth = BEVEL[level]
    parts = []
    pid = 0
    aperture_half = VENT_APERTURE/2 + .006
    for ix in range(4):
        for iy in range(3):
            x0,x1 = XS[ix]+JOINT/2, XS[ix+1]-JOINT/2
            y0,y1 = YS[iy]+JOINT/2, YS[iy+1]-JOINT/2
            plane = float(PANEL_PLANE_Z[pid])
            def add_piece(a0,a1,b0,b1,suffix=''):
                if a1 <= a0 or b1 <= b0:
                    return
                parts.append(beveled_panel(
                    a1-a0,b1-b0,T,[(a0+a1)/2,(b0+b1)/2,plane],
                    bevel_width,bevel_depth,f'Panel{pid}{suffix}','PaintedFiberCement'))
            if x0 < VENT_X < x1 and y0 < VENT_Y < y1:
                add_piece(x0,x1,y0,VENT_Y-aperture_half,'_B')
                add_piece(x0,x1,VENT_Y+aperture_half,y1,'_T')
                add_piece(x0,VENT_X-aperture_half,VENT_Y-aperture_half,VENT_Y+aperture_half,'_L')
                add_piece(VENT_X+aperture_half,x1,VENT_Y-aperture_half,VENT_Y+aperture_half,'_R')
                parts.append(v1.ann(.104,.083,.020,[VENT_X,VENT_Y,-.014],[0,0,1],sec,
                                    'VentReveal','DarkCavity'))
            else:
                add_piece(x0,x1,y0,y1)
            pid += 1

    # Keep the existing physical joint/backer/fastener owner exactly here.
    if bead:
        for x in XS[1:-1]:
            parts.append(v1.box([.006,H-.02,.006],[x,0,.011],'VJoint','JointSealant'))
        for y in YS[1:-1]:
            parts.append(v1.box([W-.02,.006,.006],[0,y,.011],'HJoint','JointSealant'))
    if back:
        for x in XS[1:-1]:
            parts.append(v1.box([.005,H-.04,.005],[x,0,.004],'VBacker','BackerRod'))
        for y in YS[1:-1]:
            parts.append(v1.box([W-.04,.005,.005],[0,y,.004],'HBacker','BackerRod'))
    if fast:
        for i,x in enumerate(np.linspace(-1.6,1.6,max(2,fast//2))):
            parts += [
                v1.cyl(.0032,.003,[x,-1.31,.011],[0,0,1],sec,f'FastB{i}','GalvanizedSteel'),
                v1.cyl(.0032,.003,[x, 1.31,.011],[0,0,1],sec,f'FastT{i}','GalvanizedSteel'),
            ]
    return parts


def uv_for(part, material):
    if material == 'PaintedFiberCement':
        # One texture across the whole wall: no 420 mm repeated module.
        return np.column_stack(((part.vertices[:,0]+W/2)/W,
                                (part.vertices[:,1]+H/2)/H))
    return part.vertices[:,[0,1]]/.12


def compact_v2(parts, textures):
    grouped = {}
    for part in parts:
        grouped.setdefault(part.metadata['material'], []).append(part)
    scene = trimesh.Scene()
    for material, group in grouped.items():
        verts=[]; faces=[]; normals=[]; uvs=[]; offset=0
        for part in group:
            verts.append(part.vertices); faces.append(part.faces+offset)
            normals.append(part.vertex_normals); uvs.append(uv_for(part,material)); offset += len(part.vertices)
        mesh=trimesh.Trimesh(vertices=np.vstack(verts),faces=np.vstack(faces),
                            vertex_normals=np.vstack(normals),process=False)
        bc,mr,nm=textures[material]
        pbr=trimesh.visual.material.PBRMaterial(
            name=material,baseColorTexture=bc,metallicRoughnessTexture=mr,
            normalTexture=nm,metallicFactor=1,roughnessFactor=1,doubleSided=False)
        mesh.visual=trimesh.visual.TextureVisuals(uv=np.vstack(uvs),material=pbr)
        scene.add_geometry(mesh,node_name=material,geom_name=material)
    return scene


def validate_v2(parts, level):
    rows=v1.validate(parts)
    panels=[p for p in parts if p.metadata['name'].startswith('Panel')]
    assert len(panels)==15, (level,len(panels))
    assert max(abs(float(p.vertices[:,2].mean())) for p in panels) < T
    if level == 'LOD3':
        assert all(len(p.faces)==12 for p in panels)
    else:
        assert all(len(p.faces)==20 for p in panels)
        assert all(p.metadata.get('bevelWidthM',0)>0 for p in panels)
    assert float(np.max(np.abs(PANEL_PLANE_Z))) <= .00030
    return rows


def export_facade(level, parts, out, textures):
    rows=validate_v2(parts,level)
    scene=compact_v2(parts,textures)
    target=out/ASSET; target.mkdir(parents=True,exist_ok=True)
    glb=target/f'{ASSET}_{level}.glb'; glb.write_bytes(scene.export(file_type='glb'))
    triangle_count=sum(r['triangles'] for r in rows)
    reload=trimesh.load(glb,force='scene',process=False)
    assert sum(len(g.faces) for g in reload.geometry.values())==triangle_count
    assert np.allclose(reload.bounds,scene.bounds,atol=1e-6)
    objdir=target/f'{ASSET}_{level}_OBJ'; objdir.mkdir(exist_ok=True)
    obj,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True)
    objpath=objdir/f'{ASSET}_{level}.obj'; objpath.write_text(obj,encoding='utf-8')
    for name,data in files.items():
        dst=objdir/name
        dst.write_text(data,encoding='utf-8') if isinstance(data,str) else dst.write_bytes(data)
    objreload=trimesh.load(objpath,force='scene',process=False)
    assert sum(len(g.faces) for g in objreload.geometry.values())==triangle_count
    return {'asset':ASSET,'level':level,'triangles':triangle_count,'logicalParts':len(parts),
            'runtimeMaterialMeshes':len(scene.geometry),'boundsMetres':scene.bounds.tolist(),
            'glbRoundtrip':True,'objTriangleRoundtrip':True,'glbSHA256':sha(glb),
            'componentValidation':rows}


def run(output: Path):
    output.mkdir(parents=True,exist_ok=False)
    textures=textures_v2(output/'textures')
    records=[]; counts=[]
    for level in LEVELS:
        record=export_facade(level,facade_v2(level),output,textures)
        records.append(record); counts.append(record['triangles'])
        print(ASSET,level,record['triangles'],flush=True)
    assert all(a>b for a,b in zip(counts,counts[1:])),counts

    # Review remains the same owner composition: refined facade plus unchanged
    # existing vent hood and starter flashing.
    review=trimesh.Scene()
    for name,g in compact_v2(facade_v2('LOD0'),textures).geometry.items():
        review.add_geometry(g.copy(),node_name='Facade_'+name,geom_name='Facade_'+name)
    for name,g in v1.compact(v1.vent('LOD0'),textures).geometry.items():
        q=g.copy();q.apply_translation([VENT_X,VENT_Y,.018])
        review.add_geometry(q,node_name='Vent_'+name,geom_name='Vent_'+name)
    for name,g in v1.compact(v1.flashing('LOD0'),textures).geometry.items():
        q=g.copy();q.apply_translation([0,-1.372,.010])
        review.add_geometry(q,node_name='Flash_'+name,geom_name='Flash_'+name)
    review_path=output/'ExteriorFacadeInstalledReview_v2_LOD0.glb'
    review_path.write_bytes(review.export(file_type='glb'))

    report={
        'schema':2,'status':'EXTERNAL_GEOMETRY_VERIFIED_NO_UNITY_RUNTIME',
        'owner':'ExteriorFacadeSet','refines':'PaintedFiberCementFacade3600x2700',
        'records':records,
        'review':{'path':review_path.name,'triangles':sum(len(g.faces) for g in review.geometry.values()),
                  'geometryCount':len(review.geometry),'unityVerified':False},
        'construction':{
            'boardThicknessMm':16.0,'jointMm':10.0,
            'frontBevelWidthMm':{k:v[0]*1000 for k,v in BEVEL.items()},
            'frontBevelDepthMm':{k:v[1]*1000 for k,v in BEVEL.items()},
            'maxBoardPlaneToleranceMm':float(np.max(np.abs(PANEL_PLANE_Z))*1000),
            'ventAperturePreserved':True,
            'assumption':'Existing board thickness retained; bevel/tolerance are authoring assumptions, not historical SKU identification.'},
        'materials':{
            'PaintedFiberCement':{
                'albedoSRGBMean':[.58,.59,.56],'metallic':0.0,'roughnessMean':.68,
                'normalScaleProxy':.075,'textureCoverageM':[W,H],
                'microstructure':'unique facade-wide isotropic paint/cement variation; no tile inside UV domain',
                'wetness':0.0,'uvAging':'none baked','fresnel':'dielectric F0 approximately 0.04'}},
        'weathering':'No grime/streak/highlight baked; causal aging remains scene-dependent.',
        'visualFidelity':{'score':None,'pass':False,'pointsAwarded':0,
                          'reason':'Real Unity 3840x2160 evidence required.'},
        'implementationReadiness':{'lastRecorded':93,'recomputed':False},
        'unityCompile':False,'unityImport':False,'unityRender':False,'lodTemporalVerified':False,
        'nextProductionTarget':'Replace only the existing ExteriorFacadeSet facade field in the latest repaired Drained3600 integration, then render material-colored full and grazing closeup views with all faces.'
    }
    (output/'ExteriorFacadeSet.v2.execution.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    return report


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    run(args.output)
