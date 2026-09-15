from __future__ import annotations
import argparse, json, math, hashlib
from pathlib import Path
import numpy as np
import trimesh
from PIL import Image
from shapely.geometry import Polygon, Point
from shapely.ops import unary_union
import shapely

TAU = 2 * math.pi

MATERIALS = {
    'SkyBlueTerry': dict(color=[0.42,0.62,0.72], roughness=0.84, metallic=0.0, f0=0.04, tile=0.045,
                         finish='cotton terry-loop towel; soft matte dielectric'),
    'WhiteCottonJersey': dict(color=[0.78,0.79,0.76], roughness=0.79, metallic=0.0, f0=0.04, tile=0.035,
                         finish='washed cotton jersey; unbranded, dry'),
    'BluePP': dict(color=[0.12,0.30,0.48], roughness=0.50, metallic=0.0, f0=0.04, tile=0.07,
                         finish='molded polypropylene clothes hanger'),
    'PaleYellowSheet': dict(color=[0.76,0.70,0.48], roughness=0.76, metallic=0.0, f0=0.04, tile=0.06,
                         finish='thin cotton-blend summer sheet; matte, dry'),
}

def unit(v):
    v=np.asarray(v,dtype=float); n=np.linalg.norm(v)
    if n < 1e-12: raise ValueError('zero vector')
    return v/n

def make_texture(spec, name, size=128):
    yy,xx=np.mgrid[:size,:size]
    u=xx/size; v=yy/size
    # Isotropic weave/loop proxy, deliberately no directional scene lighting or highlight.
    p1=np.sin(TAU*(17*u+13*v))*np.sin(TAU*(19*v-11*u))
    p2=np.sin(TAU*37*u)*np.sin(TAU*41*v)
    amp=0.020 if 'Cotton' in name or 'Terry' in name or 'Sheet' in name else 0.010
    base=np.clip(np.array(spec['color'])[None,None,:]*(1+amp*(.72*p1+.28*p2)[:,:,None]),0,1)
    rough=np.clip(spec['roughness']+0.035*p1,0.04,1.0)
    orm=np.stack([np.ones_like(rough), rough, np.full_like(rough,spec['metallic'])],axis=-1)
    return Image.fromarray(np.uint8(base*255)), Image.fromarray(np.uint8(orm*255))

def apply_material(mesh, mat_name):
    spec=MATERIALS[mat_name]
    base,orm=make_texture(spec,mat_name)
    # Simple metric projection for portable external evidence; Unity UV/lookdev remains pending.
    v=mesh.vertices
    uv=np.column_stack([v[:,0] + .31*v[:,2], v[:,1] + .17*v[:,2]])/spec['tile']
    pbr=trimesh.visual.material.PBRMaterial(name=mat_name,baseColorTexture=base,
        metallicRoughnessTexture=orm, metallicFactor=1.0, roughnessFactor=1.0)
    mesh.visual=trimesh.visual.TextureVisuals(uv=uv, material=pbr)
    return mesh

def tube(path, radius, sides=10, cap=True):
    path=np.asarray(path,dtype=float); rr=np.broadcast_to(radius,(len(path),)).astype(float)
    frames=[]; prev=None
    for i,p in enumerate(path):
        t=unit(path[min(i+1,len(path)-1)]-path[max(i-1,0)])
        n=np.cross(t,[0,1,0]) if prev is None else prev - t*np.dot(prev,t)
        if np.linalg.norm(n)<1e-7: n=np.cross(t,[1,0,0])
        n=unit(n); b=unit(np.cross(t,n)); frames.append((n,b)); prev=n
    verts=[]; faces=[]; rings=[]
    for p,r,(n,b) in zip(path,rr,frames):
        ring=[]
        for j in range(sides):
            a=TAU*j/sides; ring.append(len(verts)); verts.append(p+r*(n*math.cos(a)+b*math.sin(a)))
        rings.append(ring)
    for i in range(len(rings)-1):
        for j in range(sides):
            k=(j+1)%sides; a,b=rings[i][j],rings[i][k]; c,d=rings[i+1][k],rings[i+1][j]
            faces += [[a,b,c],[a,c,d]]
    if cap:
        a0=len(verts); verts.append(path[0]); a1=len(verts); verts.append(path[-1])
        for j in range(sides):
            k=(j+1)%sides; faces += [[a0,rings[0][k],rings[0][j]],[a1,rings[-1][j],rings[-1][k]]]
    m=trimesh.Trimesh(vertices=np.array(verts),faces=np.array(faces),process=False); m.fix_normals(multibody=True)
    return m

def solid_grid(width, s_samples, across_samples, path_func, thickness_func, mat_name, edge_uneven=0.0):
    """Thin closed fabric solid over a parameterized path surface.
    u is lateral [-.5,.5], v is longitudinal [0,1]. path_func(u,v)->position.
    Thickness is physical solid thickness/hem fold proxy, not a two-sided zero-thickness card.
    """
    nu=across_samples; nv=s_samples
    base=np.zeros((nv,nu,3),float)
    for j in range(nv):
        v=j/(nv-1)
        for i in range(nu):
            u=i/(nu-1)-.5
            p=np.asarray(path_func(u,v),float)
            if edge_uneven and (j in [0,nv-1]): p[1]+=edge_uneven*math.sin(7.1*u+1.7)*(.25+abs(u))
            base[j,i]=p
    normals=np.zeros_like(base)
    for j in range(nv):
        for i in range(nu):
            du=base[j,min(i+1,nu-1)]-base[j,max(i-1,0)]
            dv=base[min(j+1,nv-1),i]-base[max(j-1,0),i]
            n=np.cross(du,dv)
            if np.linalg.norm(n)<1e-10:n=np.array([0,0,1.])
            normalsj,i]=unit(n)
    thick=np.zeros((nv,nu),float)
    for j in range(nv):
        v=j/(nv-1)
        for i in range(nu):
            u=i/(nu-1)-.5; thick[j,i]=thickness_func(u,v)
    top=base+normals*thick[:,:,None]/2; bot=base-normals*thick[:,:,None]/2
    verts=np.vstack([top.reshape(-1,3),bot.reshape(-1,3)])
    faces=[]; off=nv*nu
    def idx(j,i):return j*nu+i
    for j in range(nv-1):
        for i in range(nu-1):
            a=idx(j,i);b=idx(j,i+1);c=idx(j+1,i+1);d=idx(j+1,i)
            faces += [[a,b,c],[a,c,d],[off+a,off+c,off+b],[off+a,off+d,off+c]]
    # edge walls around complete perimeter
    perimeter=[]
    perimeter += [idx(0,i) for i in range(nu)]
    perimeter += [idx(j,nu-1) for j in range(1,nv)]
    perimeter += [idx(nv-1,i) for i in range(nu-2,-1,-1)]
    perimeter += [idx(j,0) for j in range(nv-2,0,-1)]
    for a,b in zip(perimeter,perimeter[1:]+perimeter[:1]):
        faces += [[a,off+a,off+b],[a,off+b,b]]
    m=trimesh.Trimesh(vertices=verts,faces=np.asarray(faces),process=False);m.merge_vertices(digits_vertex=9);m.remove_unreferenced_vertices();m.fix_normals(multibody=True)
    apply_material(m,mat_name)
    return m

def towel(level):
    settings={'MASTER':(100,140),'LOD0':(72,104),'LOD1':(48,68),'LOD2':(28,40),'LOD3':(16,24)}
    nu,nv=settings[level]; width=.70; R=.0170; total=1.20; front=.64; arc=math.pi*R; back=total-front-arc
    # Widthwise amplitude intentionally low at contact crown; higher on hanging panels from gravity folds.
    def path(u,v):
        s=v*total
        x=u*width
        if s < front:
            t=s/front; y=-front+s; z=R + .0065*math.sin(5*math.pi*(u+.5))*(.25+.75*(1-t)) + .002*math.sin(5.3*t+11*u)
        elif s < front+arc:
            a=(s-front)/R; y=R*math.sin(a); z=R*math.cos(a); z += .0012*math.sin(5*math.pi*(u+.5))*math.sin(a)
        else:
            q=s-front-arc; t=q/back; y=-q; z=-R + .0055*math.sin(5*math.pi*(u+.5)+.45)*(.22+.78*t) + .0018*math.sin(4.7*t+13*u)
        # mild edge scallop / cloth relaxation across width
        y += -.006*(abs(u)**2.2) * (0.4+0.6*math.sin(math.pi*v)**2)
        return [x,y,z]
    def thick(u,v):
        hem = abs(u)>.47 or v<.017 or v>.983
        return .00165 if hem else .00082
    m=solid_grid(width,nv,nu,path,thick,'SkyBlueTerry',edge_uneven=.0025)
    return [('TerryTowelBodyWithFoldedHems',m,'SkyBlueTerry')], {
        'widthMetres':width,'finishedLengthMetres':total,'poleRadiusDependencyMetres':R,'frontDropMetres':front,'backDropMetres':back,'baseFabricThicknessMetres':.00082,'foldedHemThicknessMetres':.00165}

def constrained_tris(poly):
    return [np.asarray(t.exterior.coords)[:3] for t in shapely.constrained_delaunay_triangles(poly).geoms]

def extrude_polygon_deformed(poly, depth, max_edge, deform):
    # Build a shared-vertex closed prism from constrained triangulation, including hole boundaries.
    tris=constrained_tris(poly)
    coords={}
    def key(x,y): return (round(float(x),12),round(float(y),12))
    for t in tris:
        for x,y in t: coords.setdefault(key(x,y), len(coords))
    # Ensure every explicit boundary coordinate has an index even if triangulator simplification occurs.
    for ring in [poly.exterior,*poly.interiors]:
        for x,y in list(ring.coords)[:-1]: coords.setdefault(key(x,y), len(coords))
    points=np.zeros((len(coords),2),float)
    for k,i in coords.items(): points[i]=k
    n=len(points)
    verts=np.zeros((n*2,3),float)
    verts[:n,:2]=points; verts[:n,2]=-depth/2
    verts[n:,:2]=points; verts[n:,2]= depth/2
    faces=[]
    for t in tris:
        ids=[coords[key(x,y)] for x,y in t]
        a,b,c=[points[i] for i in ids]
        area=np.cross(np.r_[b-a,0],np.r_cc-a,0])[2]
        if area<0: ids=[ids[0],ids[2],ids[1]]
        faces.append([n+ids[0],n+ids[1],n+ids[2]])
        faces.append([ids[0],ids[2],ids[1]])
    # Use boundary orientation from shapely rings and let multibody fix normalize outward orientation.
    for ring in [poly.exterior,*poly.interiors]:
        pts=list(ring.coords)
        for (x0,y0),(x1,y1) in zip(pts,pts[1:]):
            i0=coords[key(x0,y0)]; i1=coords[key(x1,y1)]
            faces += [[i0,i1,n+i1],[i0,n+i1,n+i0]]
    m=trimesh.Trimesh(vertices=verts,faces=np.asarray(faces),process=False)
    m.merge_vertices(digits_vertex=9);m.remove_unreferenced_vertices();m.fix_normals(multibody=True)
    if not m.is_watertight:
        raise ValueError('base shirt prism is not watertight')
    # Uniform topological subdivision preserves shared edges; avoid per-face T-junctions.
    steps = 3 if max_edge <= .025 else (2 if max_edge <= .050 else (1 if max_edge <= .075 else 0))
    v,f=m.vertices,m.faces
    for _ in range(steps): v,f=trimesh.remesh.subdivide(v,f)
    m=trimesh.Trimesh(vertices=v,faces=f,process=False)
    x=m.vertices[:,0];y=m.vertices[:,1]
    m.vertices[:,2]+=deform(x,y)
    m.merge_vertices(digits_vertex=9);m.remove_unreferenced_vertices();m.fix_normals(multibody=True)
    return m

def tshirt(level):
    edge={'MASTER':.025,'LOD0':.035,'LOD1':.050,'LOD2':.075,'LOD3':.105}[level]
    hanger_sides={'MASTER':16,'LODMÄS‘„,