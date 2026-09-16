"""Existing BalconyDrainageIntegration floor owner, recovered as an importable module.

The archived exporter passed (x0,x1,z0,z1) to shapely.box(minx,miny,maxx,maxy),
removing a large rectangle rather than the small rainwater notch. This module
uses the existing authoring contract's explicit XZ ordering. Drain/hose meshes
are reused from the verified original GLBs by build_balcony_structural_contact_repair.
"""
import numpy as np
import shapely
from shapely.geometry import Point, box
from shapely.ops import unary_union
import trimesh

QUADS={'MASTER':24,'LOD0':18,'LOD1':12,'LOD2':8,'LOD3':5}


def finished_floor_y(z):
    return -1.2470-.015*z+.0008


def floor_polygon(level):
    # Bounds are (min X, min Z, max X, max Z), never (X0, X1, Z0, Z1).
    outer=box(-1.8,0.,1.8,.970)
    rainwater_notch=box(1.600,0.000,1.780,.180)
    drain=Point(.450,.820).buffer(.0434,quad_segs=QUADS[level])
    return outer.difference(unary_union([rainwater_notch,drain]))


def sloped_solid(poly,bottom,top,name,material):
    vertices=[];faces=[];lookup={}
    def index(x,z,y):
        key=(round(float(x),10),round(float(y),10),round(float(z),10))
        if key not in lookup:lookup[key]=len(vertices);vertices.append([x,y,z])
        return lookup[key]
    for triangle in shapely.constrained_delaunay_triangles(poly).geoms:
        if not poly.covers(triangle.representative_point()):continue
        points=list(triangle.exterior.coords)[:3]
        a=[index(x,z,top(z)) for x,z in points]
        b=[index(x,z,bottom(z)) for x,z in points]
        faces.extend(([a[0],a[2],a[1]],b))
    for ring in (poly.exterior,*poly.interiors):
        for (x0,z0),(x1,z1) in zip(ring.coords,list(ring.coords)[1:]):
            a=index(x0,z0,bottom(z0));b=index(x1,z1,bottom(z1))
            c=index(x1,z1,top(z1));d=index(x0,z0,top(z0))
            faces.extend(([a,b,c],[a,c,d]))
    mesh=trimesh.Trimesh(vertices=np.asarray(vertices),faces=np.asarray(faces),process=False)
    mesh.fix_normals(multibody=True)
    mesh.metadata.update(name=name,material=material)
    return mesh


def floor_parts(level):
    polygon=floor_polygon(level)
    # Keep the measured finished surface and 4 mm membrane, and make the mortar
    # meet its underside rather than overlap the membrane by 3.2 mm.
    join=lambda z:finished_floor_y(z)-.004
    return [sloped_solid(polygon,lambda z:-1.335,join,
                         'SlopedMortarWithRealDrainCutout','CementMortar'),
            sloped_solid(polygon,join,finished_floor_y,
                         'WaterproofSkinWithRealDrainCutout','WaterproofCoating')]


# Material refinement of this existing external floor owner. The formal Unity
# QualityBlockBalconySurfaceMicrostructureUpgrade remains the engine-side owner.
SURFACE_TARGETS={
    'DrainIntegratedFloor_WaterproofSkinWithRealDrainCutout':'WaterproofCoating',
    'DrainIntegratedFloor_SlopedMortarWithRealDrainCutout':'CementMortar',
    'DrainIntegratedFloor_WallBaseWaterproofTurnUp':'WaterproofCoating',
    'ExistingHero_Kerb_WeatheredConcrete':'WeatheredConcrete',
}
SURFACE_COVERAGE=(3.62,.99)
SURFACE_PROFILES={
    'WaterproofCoating':{'color':(.47,.49,.47),'roughness':.72,'heightRmsM':.000040,'pigmentVariation':0.0},
    'CementMortar':{'color':(.54,.52,.47),'roughness':.85,'heightRmsM':.000065,'pigmentVariation':.008},
    'WeatheredConcrete':{'color':(.58,.56,.52),'roughness':.85,'heightRmsM':.000070,'pigmentVariation':.010},
}


def create_surface_materials(output):
    """One physical-size field per material, with no repeated tile across the floor.

    Dry mineral/coating assumptions; no lighting, wetness or invented dirt is
    encoded. The waterproof albedo remains uniform, matching the existing formal
    owner's separation of colour from micro-normal and roughness variation.
    """
    from pathlib import Path
    from PIL import Image
    from scipy.ndimage import gaussian_filter
    output=Path(output);output.mkdir(parents=True,exist_ok=True)
    materials={};records={};width,height=2048,576
    for index,(name,profile) in enumerate(SURFACE_PROFILES.items()):
        rng=np.random.default_rng(17092640+index)
        def field(sigma):
            a=gaussian_filter(rng.standard_normal((height,width)),sigma,mode='reflect')
            return (a-a.mean())/max(a.std(),1e-12)
        grain=field(.72);medium=field(3.5);broad=field(24.)
        elevation=(.78*grain+.22*medium)*profile['heightRmsM']
        elevation-=elevation.mean()
        gy,gx=np.gradient(elevation,SURFACE_COVERAGE[1]/(height-1),SURFACE_COVERAGE[0]/(width-1))
        # Image rows run opposite the glTF +V direction.
        normal=np.dstack([-gx,gy,np.ones_like(gx)])
        normal/=np.linalg.norm(normal,axis=2)[:,:,None]
        rough=np.clip(profile['roughness']+.022*grain+.010*medium,.60,.94)
        mr=np.zeros((height,width,3),np.uint8)
        mr[:,:,1]=np.uint8(np.round(255*rough))
        nm=Image.fromarray(np.uint8(np.round(np.clip(normal*.5+.5,0,1)*255)))
        mi=Image.fromarray(mr)
        mi.save(output/(name+'_mr.png'));nm.save(output/(name+'_normal.png'))
        base=None;factor=list(profile['color'])+[1.]
        if profile['pigmentVariation']:
            color=np.asarray(profile['color'])*(1+profile['pigmentVariation']*(.65*medium+.35*broad)[:,:,None])
            base=Image.fromarray(np.uint8(np.round(np.clip(color,0,1)*255)))
            base.save(output/(name+'_base.png'));factor=[1.,1.,1.,1.]
        materials[name]=trimesh.visual.material.PBRMaterial(
            name=name,baseColorFactor=factor,baseColorTexture=base,
            metallicRoughnessTexture=mi,normalTexture=nm,
            metallicFactor=1.,roughnessFactor=1.,doubleSided=False)
        records[name]={**profile,'resolution':[width,height],'coverageMetres':list(SURFACE_COVERAGE),
            'roughnessMinMax':[float(rough.min()),float(rough.max())],
            'heightRmsMeasuredMm':float(elevation.std()*1000),
            'normalTiltMaxDegrees':float(np.degrees(np.arccos(normal[:,:,2])).max()),
            'metallic':0,'wetness':0,'bakedLighting':False,'uniqueField':True,
            'albedoTexture':base is not None}
    return materials,records


def surface_uv(vertices,face_normals):
    """Project each planar face on its own dominant plane, at metre-based scale.

    The old curb top inherited its front-face XY projection, collapsing V and
    stretching a single row of pixels into stripes across the entire top.
    """
    axes=np.repeat(np.argmax(np.abs(face_normals),axis=1),3)
    u=np.where(axes==0,vertices[:,2],vertices[:,0])
    v=np.where(axes==1,vertices[:,2]+.01,vertices[:,1]+1.36)
    return np.column_stack(((u+1.81)/SURFACE_COVERAGE[0],v/SURFACE_COVERAGE[1]))


def uv_degenerate_count(mesh):
    t=mesh.visual.uv[mesh.faces];a=t[:,1]-t[:,0];b=t[:,2]-t[:,0]
    return int(np.count_nonzero(np.abs(a[:,0]*b[:,1]-a[:,1]*b[:,0])<1e-12))


def apply_surface_materials(source,materials):
    """Update only named floor/curb surfaces; retain all other props and transforms.

    Vertex splitting supplies physical hard normals and nondegenerate UV seams.
    It changes vertex indices, but preserves every triangle corner and winding.
    """
    import copy
    target=source.copy();records=[]
    # Scene.copy clears mesh caches, including explicitly imported vertex normals.
    # Preserve those buffers on untouched meshes instead of silently recomputing.
    for name,mesh in source.geometry.items():
        _=mesh.vertex_normals
        target.geometry[name]=mesh.copy(include_cache=True)
    # The original distant LODs intentionally omit the wall turn-up detail.
    required=(set(SURFACE_TARGETS)-{'DrainIntegratedFloor_WallBaseWaterproofTurnUp'})-set(target.geometry)
    if required:raise ValueError('Missing known floor geometry: '+repr(sorted(required)))
    for name,material in SURFACE_TARGETS.items():
        if name not in source.geometry:continue
        old=source.geometry[name]
        vertices=old.triangles.reshape((-1,3)).copy()
        mesh=trimesh.Trimesh(vertices=vertices,faces=np.arange(len(vertices)).reshape((-1,3)),
            vertex_normals=np.repeat(old.face_normals,3,axis=0),process=False)
        mesh.visual=trimesh.visual.TextureVisuals(uv=surface_uv(vertices,old.face_normals),material=copy.deepcopy(materials[material]))
        assert np.array_equal(mesh.triangles,old.triangles),name
        assert uv_degenerate_count(mesh)==0,name
        welded=mesh.copy();welded.merge_vertices(merge_tex=True,merge_norm=True,digits_vertex=7)
        assert welded.is_watertight and welded.is_winding_consistent and welded.volume>0,name
        assert np.allclose(mesh.vertex_normals[mesh.faces],mesh.face_normals[:,None,:],atol=1e-7),name
        target.geometry[name]=mesh
        records.append({'name':name,'material':material,'triangles':len(mesh.faces),
            'beforeVertices':len(old.vertices),'afterVertices':len(mesh.vertices),
            'beforeDegenerateUVTriangles':uv_degenerate_count(old),'afterDegenerateUVTriangles':0,
            'triangleCornersAndWindingPreserved':True,'weldedWatertight':True,'hardFaceNormals':True})
    return target,records
