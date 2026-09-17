"""Dry, source-bound material fields for the existing external balcony owners.

This module bakes material properties, never a lit image. All coordinates are
metres in the supplied hero's local frame. The style is a maintained, occupied
1990s Senri-Chuo balcony; deposit intensity and coating age are authored choices,
not a claim to identify a historical product or simulate rainfall hydraulics.
"""
from pathlib import Path
import copy, hashlib, math
import numpy as np
import trimesh
from PIL import Image


def smooth(a, b, v):
    t = np.clip((v-a)/(b-a), 0, 1)
    return t*t*(3-2*t)


def bump(v, radius):
    return (1-np.minimum(np.abs(v)/radius, 1)**2)**2


def value_noise(p, scale, seed):
    """Continuous deterministic 3D noise, shared across atlas seams and LODs."""
    q = p/scale
    cell = np.floor(q)
    f = q-cell
    f = f*f*(3-2*f)
    result = np.zeros(p.shape[:-1], dtype=np.float32)
    for x in (0, 1):
        for y in (0, 1):
            for z in (0, 1):
                c = cell + [x, y, z]
                h = np.sin(c[..., 0]*127.1+c[..., 1]*311.7+c[..., 2]*74.7+seed*19.19)*43758.5453
                h = h-np.floor(h)
                result += h*(f[..., 0] if x else 1-f[..., 0])*(f[..., 1] if y else 1-f[..., 1])*(f[..., 2] if z else 1-f[..., 2])
    return result


def streak(p, x0, y0, length, width, seed):
    """Strictly below a measured origin; taper and wander inside its catchment."""
    x, y = p[..., 0], p[..., 1]
    d = y0-y
    t = np.clip(d/length, 0, 1)
    drift = width*.22*np.sin(t*8.1+seed)*t
    spread = width*(1-.68*t)
    halo = bump(x-x0-drift, spread)
    thread = bump(x-x0-drift, spread*.16)
    branch = bump(x-x0-drift-spread*.32, spread*.10)*(1-smooth(.25,.78,t))
    gate = smooth(0, .013, d)*(1-smooth(length*.65, length, d))
    breakup = .48+.52*value_noise(p, .007, seed)
    return np.clip(.14*halo+.76*thread+.22*branch,0,1)*gate*breakup


def floor_masks(p, n, context):
    x, y, z = np.moveaxis(p, -1, 0)
    floor_y = -1.2462-.015*z
    on_top = smooth(.78, .99, n[..., 1])*(1-smooth(.001, .010, np.abs(y-floor_y)))
    # Deposition stays next to the real wall/curb and the two return base plates.
    edge = np.maximum(bump(z-.016, .065), bump(z-.965, .070))
    contacts = np.zeros_like(x)
    for c in context['return_plates']:
        d = np.maximum(np.abs(x-c['x'])-c['half_x'], np.abs(z-c['z'])-c['half_z'])
        contacts = np.maximum(contacts, (1-smooth(.006, .065, np.maximum(d, 0))))
    irregular = .32+.68*value_noise(p, .028, 14)
    dust = np.maximum(edge*.52, contacts*.62)*irregular*on_top
    cx, cz = context['drain_center']
    dx, dz = x-cx, z-cz
    r = np.sqrt((dx/.120)**2+(dz/.091)**2)
    # A dry, broken deposit around the actual flange, denser on its low side.
    rim = bump(r-.70, .32)*(1-smooth(.98, 1.20, r))
    rim *= .25+.75*smooth(-.02, .035, dz)
    mineral = rim*(.25+.75*value_noise(p, .009, 28))*on_top
    sediment = bump(dx, .18)*bump(dz-.055, .095)*(.30+.70*value_noise(p, .016, 19))*on_top
    return {'dust': dust, 'mineral': mineral*.58, 'sediment': sediment*.30}


def curb_masks(p, n, context):
    x, y, z = np.moveaxis(p, -1, 0)
    dust = np.zeros_like(x)
    for plate in context['front_plates']:
        r = np.maximum(np.abs(x-plate['x'])/(plate['half_x']+.025), np.abs(z-plate['z'])/(plate['half_z']+.020))
        dust = np.maximum(dust, bump(r-.85, .40))
    dust *= smooth(.65, .99, n[..., 1])*(.25+.75*value_noise(p, .018, 39))*.38
    foot = context['oxide_fixing']
    # Oxide is emitted only by the one visibly damaged steel plate. The short
    # path continues across the curb cap and down its exposed face.
    top = bump(x-foot['x'], .040)*bump(z-1.079, .033)*smooth(.5, .99, n[..., 1])
    front = streak(p, foot['x'], -1.161, .142, .033, 12)*smooth(.72, .99, n[..., 2])
    oxide = np.maximum(top*.30, front*.36)
    runoff = np.zeros_like(x)
    for plate, length, width, seed in [(context['front_plates'][0], .08, .023, 1), (context['front_plates'][-1], .11, .027, 9)]:
        cx=plate['x']+plate['half_x']*.65
        runoff = np.maximum(runoff, streak(p, cx, -1.158, length, width, seed))
    runoff *= smooth(.72, .99, n[..., 2])*.18
    return {'dust': dust, 'oxide': oxide, 'runoff': runoff}


def steel_masks(p, n, context):
    x, y, z = np.moveaxis(p, -1, 0)
    dust = smooth(.75, .99, n[..., 1])*(.20+.80*value_noise(p, .011, 20))*.16
    foot = context['oxide_fixing']
    # A few millimetres of damage at the outer corner of one base plate.
    damage = bump(x-foot['x'], .019)*bump(y+1.1645, .012)*bump(z-1.076, .014)
    damage *= smooth(.30, .62, value_noise(p, .0028, 32))
    wear = bump(x-.20, .32)*bump(y+.044, .008)*bump(z-1.031, .026)
    wear *= smooth(.75, .99, n[..., 1])*(.60+.40*value_noise(p, .005, 53))
    return {'dust': dust, 'oxide': damage*.90, 'wear': wear*.65}


def facade_masks(p, n, context):
    x, y, z = np.moveaxis(p, -1, 0)
    runoff = np.zeros_like(x)
    for source in context['wall_runoff_sources']:
        runoff = np.maximum(runoff, streak(p, source['x'], source['y'], source['length'], source['width'], source['seed'])*source['amount'])
    # Low dust/splash is relative to this balcony's floor, not ground level.
    dy = y-(-1.2462)
    basal = (1-smooth(.015, .14, dy))*smooth(-.004, .008, dy)
    basal *= (.18+.82*value_noise(p, .046, 73))*.24
    # All intended deposits are on the exposed front; no new generic wet film.
    gate = smooth(.70, .99, n[..., 2])
    return {'runoff': runoff*gate, 'dust': basal*gate}


PROFILES = {
    'floor': {'srgb': [.47,.49,.47], 'roughness': .72, 'height_m': .000065, 'pigment': .006},
    'curb': {'srgb': [.58,.56,.52], 'roughness': .84, 'height_m': .000105, 'pigment': .018},
    'steel': {'srgb': [.24,.23,.21], 'roughness': .52, 'height_m': .000015, 'pigment': .006},
    'facade': {'srgb': [.72,.70,.64], 'roughness': .72, 'height_m': .000070, 'pigment': .010},
}
MASKS = {'floor': floor_masks, 'curb': curb_masks, 'steel': steel_masks, 'facade': facade_masks}
RESIDUES = {
    'dust': ([.39,.365,.315], .94, .000035),
    'sediment': ([.33,.315,.270], .95, .000045),
    'mineral': ([.68,.675,.62], .94, .000022),
    'oxide': ([.35,.205,.115], .91, .000028),
    'runoff': ([.315,.325,.285], .92, .000009),
}


def material_values(kind, p, n, context):
    profile = PROFILES[kind]
    grain = value_noise(p, .0018 if kind!='curb' else .0030, 26)-.5
    fine = value_noise(p, .0065, 59)-.5
    macro = value_noise(p, .13, 86)-.5
    color = np.broadcast_to(profile['srgb'], p.shape).copy()
    color *= 1+profile['pigment']*(1.3*fine+.7*macro)[..., None]
    rough = np.clip(profile['roughness']+.060*grain+.026*fine, .35, .93)
    height = profile['height_m']*(1.6*grain+.35*fine)
    if kind=='curb':
        # Sparse sub-millimetre surface pores, not cracks or baked shadow spots.
        pores=smooth(.69,.88,value_noise(p,.0045,17))
        height-=pores*.00024
        color*=1-pores[...,None]*.045
        rough+=pores*.035
    masks = MASKS[kind](p, n, context)
    for key, (srgb, residue_roughness, deposit_height) in RESIDUES.items():
        mask = masks.get(key)
        if mask is None: continue
        alpha = np.clip(mask, 0, 1)
        # Mix reflectance in linear light; encode the resulting texture as sRGB.
        source = np.where(color<=.04045, color/12.92, ((color+.055)/1.055)**2.4)
        c = np.asarray(srgb)
        dest = np.where(c<=.04045, c/12.92, ((c+.055)/1.055)**2.4)
        linear = source*(1-alpha[...,None])+dest*alpha[...,None]
        color = np.where(linear<=.0031308, linear*12.92, 1.055*linear**(1/2.4)-.055)
        rough = rough*(1-alpha)+residue_roughness*alpha
        height += deposit_height*alpha
    if 'wear' in masks:
        rough -= masks['wear']*.14
        color *= 1+masks['wear'][...,None]*.045
    return np.clip(color,0,1), np.clip(rough,.30,.98), height, masks


def charts_for(mesh, density=850, padding=8):
    """Planar component charts keep each projection distinct and metre-scaled.

    Welding is ONLY for chart adjacency. Output triangles and their original
    corner normals are copied exactly; original mesh geometry is never welded.
    """
    welded = mesh.copy()
    welded.merge_vertices(merge_tex=True, merge_norm=True, digits_vertex=6)
    components = trimesh.graph.connected_components(welded.face_adjacency, nodes=np.arange(len(mesh.faces)), min_len=1)
    normals = mesh.face_normals
    axes = np.argmax(np.abs(normals), axis=1)
    signs = np.sign(normals[np.arange(len(normals)), axes])
    charts = []
    for component in components:
        for axis in range(3):
            for sign in (-1, 1):
                faces = np.asarray(component)[(axes[component]==axis)&(signs[component]==sign)]
                if len(faces)==0: continue
                ua, va = {0:(2,1),1:(0,2),2:(0,1)}[axis]
                def append_chart(ids, allow_split=True):
                    pts = mesh.triangles[ids].reshape((-1,3))
                    a=np.column_stack((pts[:,ua],pts[:,va],np.ones(len(pts))))
                    fit=np.linalg.lstsq(a,pts[:,axis],rcond=None)[0]
                    error=float(np.max(np.abs(a@fit-pts[:,axis])))
                    if error>.004 and allow_split:
                        # In particular, the drain-hole inner walls and distant
                        # outer slab edges must never share the same chart.
                        ns=mesh.face_normals[ids]
                        offsets=np.einsum('ij,ij->i',ns,mesh.triangles_center[ids])
                        keys=np.column_stack((np.round(ns,3),np.round(offsets,4)))
                        _,groups=np.unique(keys,axis=0,return_inverse=True)
                        for group in np.unique(groups):append_chart(ids[groups==group],False)
                        return
                    if error>.004:raise ValueError('Nonplanar chart exceeds 4 mm tolerance')
                    lo,hi=pts[:,[ua,va]].min(0),pts[:,[ua,va]].max(0)
                    span=np.maximum(hi-lo,1e-6)
                    w,h=np.maximum(np.ceil(span*density).astype(int)+1,4)+2*padding
                    charts.append(dict(faces=ids,axis=axis,sign=sign,ua=ua,va=va,lo=lo,hi=hi,span=span,w=int(w),h=int(h),fit=fit,error=error))
                append_chart(faces)
    return charts


def pack_charts(charts, width=4096):
    x=y=row_h=0
    for c in sorted(charts, key=lambda c:(-c['h'],-c['w'])):
        if c['w']>width: raise ValueError('chart exceeds texture width')
        if x+c['w']>width: x=0; y+=row_h; row_h=0
        c['at']=(x,y); x+=c['w']; row_h=max(row_h,c['h'])
    height = 2**math.ceil(math.log2(max(y+row_h,1)))
    if height>8192: raise ValueError('atlas memory budget exceeded')
    return width,height


def bake_atlas(mesh, kind, context, directory, stem, density=850):
    """Bake one named existing mesh; no new scene nodes or collision surfaces."""
    padding=8
    charts=charts_for(mesh,density,padding)
    width,height=pack_charts(charts)
    base=np.full((height,width,3),128,np.uint8)
    mr=np.zeros_like(base);mr[:,:,1]=round(PROFILES[kind]['roughness']*255)
    normal=np.empty_like(base);normal[:]=[128,128,255]
    uv=np.zeros((len(mesh.faces)*3,2),np.float64)
    counts={}; mask_max={}; rough_range=[1.,0.]; max_tilt=0.
    for c in charts:
        w,h=c['w'],c['h']; ax,ay=c['at']
        du=c['span'][0]/(w-2*padding-1);dv=c['span'][1]/(h-2*padding-1)
        u=c['lo'][0]+(np.arange(w)-padding)*du
        v=c['hi'][1]-(np.arange(h)-padding)*dv
        uu,vv=np.meshgrid(u,v)
        p=np.zeros((h,w,3));p[:,:,c['ua']]=uu;p[:,:,c['va']]=vv
        p[:,:,c['axis']]=c['fit'][0]*uu+c['fit'][1]*vv+c['fit'][2]
        n=np.zeros_like(p);n[:,:,c['axis']]=c['sign']
        bc,r,elevation,masks=material_values(kind,p,n,context)
        gy,gx=np.gradient(elevation,dv,du)
        nm=np.dstack((-gx,gy,np.ones_like(gx)));nm/=np.linalg.norm(nm,axis=2)[:,:,None]
        base[ay:ay+h,ax:ax+w]=np.uint8(np.round(bc*255))
        mr[ay:ay+h,ax:ax+w,1]=np.uint8(np.round(r*255))
        normal[ay:ay+h,ax:ax+w]=np.uint8(np.round((nm*.5+.5)*255))
        pts=mesh.triangles[c['faces']].reshape((-1,3))
        pu=(pts[:,c['ua']]-c['lo'][0])/du+padding+ax+.5
        pv=(c['hi'][1]-pts[:,c['va']])/dv+padding+ay+.5
        indices=(c['faces'][:,None]*3+np.arange(3)).ravel()
        uv[indices]=np.column_stack((pu/width,1-pv/height))
        rough_range=[min(rough_range[0],float(r.min())),max(rough_range[1],float(r.max()))]
        max_tilt=max(max_tilt,float(np.degrees(np.arccos(nm[:,:,2])).max()))
        for key,m in masks.items():
            counts[key]=counts.get(key,0)+int(np.count_nonzero(m>.02))
            mask_max[key]=max(mask_max.get(key,0),float(m.max()))
    material=write_material(directory,stem,base,mr,normal)
    vertices=mesh.triangles.reshape((-1,3)).copy()
    out=trimesh.Trimesh(vertices=vertices,faces=np.arange(len(vertices)).reshape((-1,3)),vertex_normals=mesh.vertex_normals[mesh.faces].reshape((-1,3)).copy(),process=False)
    out.visual=trimesh.visual.TextureVisuals(uv=uv,material=material)
    out.metadata=copy.deepcopy(mesh.metadata)
    assert np.array_equal(mesh.triangles,out.triangles)
    delta=uv[out.faces][:,1:]-uv[out.faces][:,:1]
    assert np.all(np.abs(delta[:,0,0]*delta[:,1,1]-delta[:,0,1]*delta[:,1,0])>1e-14)
    report={'substrate':kind,'resolution':[width,height],'densityPixelsPerMetre':density,'chartCount':len(charts),'paddingPixels':padding,
            'projectionMaxErrorM':max(c['error'] for c in charts),'roughnessRange':rough_range,'normalTiltMaxDegrees':max_tilt,
            'maskMax':mask_max,'chartTexelsAbove002':counts,'metallic':0.,'wetness':0.,'bakedLighting':False,
            'geometryTriangleCornersPreserved':True,'originalCornerNormalsPreserved':True}
    return out,report


def write_material(directory,stem,base,mr,normal):
    directory=Path(directory);directory.mkdir(parents=True,exist_ok=True)
    imgs=[Image.fromarray(a) for a in (base,mr,normal)]
    for name,img in zip(('baseColor','metallicRoughness','normal'),imgs):img.save(directory/(stem+'_'+name+'.png'))
    return trimesh.visual.material.PBRMaterial(name=stem,baseColorFactor=[1.,1.,1.,1.],baseColorTexture=imgs[0],
        metallicRoughnessTexture=imgs[1],normalTexture=imgs[2],metallicFactor=1.,roughnessFactor=1.,doubleSided=False)


def copy_scene(source):
    target=source.copy()
    for name,g in source.geometry.items():
        _=g.vertex_normals
        target.geometry[name]=g.copy(include_cache=True)
    return target
