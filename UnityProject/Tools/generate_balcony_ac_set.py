"""Generate original late-1990s/around-2000 Japanese balcony AC service assets.
Metres, Y-up, no external files/network. These are unbranded modeling assumptions,
not replicas of any historical SKU. Exports MASTER + LOD0/1/2/3 as GLB and OBJ.
"""
from __future__ import annotations
import argparse, hashlib, json, math
from pathlib import Path
import numpy as np
import trimesh
from PIL import Image
from shapely.geometry import Point, Polygon, box
from shapely.ops import unary_union
import shapely

TAU = math.tau
LEVELS = {
    'MASTER': dict(seg=96, guard_rings=10, spokes=18, fins=72, louvers=24, screws=8, corr=84),
    'LOD0':   dict(seg=72, guard_rings=8,  spokes=14, fins=48, louvers=18, screws=8, corr=60),
    'LOD1':   dict(seg=48, guard_rings=6,  spokes=10, fins=28, louvers=12, screws=6, corr=36),
    'LOD2':   dict(seg=32, guard_rings=4,  spokes=8,  fins=14, louvers=8,  screws=4, corr=20),
    'LOD3':   dict(seg=20, guard_rings=3,  spokes=6,  fins=8,  louvers=5,  screws=4, corr=10),
}

MATERIALS = {
    'WarmWhitePowderCoat': dict(color=[0.72,0.71,0.65], roughness=.53, metallic=0, normal=.13, finish='aged warm-white baked powder coat on steel'),
    'DarkFanPP': dict(color=[0.085,0.095,0.09], roughness=.47, metallic=0, normal=.05, finish='dark molded polypropylene fan'),
    'DarkInterior': dict(color=[0.035,0.04,0.038], roughness=.62, metallic=0, normal=.03, finish='shadowed painted inner chassis; not a painted shadow'),
    'AluminumFin': dict(color=[0.52,0.55,0.55], roughness=.39, metallic=1, normal=.04, finish='bare aluminum exchanger fins'),
    'CopperTube': dict(color=[0.55,0.24,0.09], roughness=.32, metallic=1, normal=.05, finish='bare copper service tube'),
    'BlackEPDM': dict(color=[0.018,0.02,0.019], roughness=.78, metallic=0, normal=.08, finish='rubber vibration pad/seal'),
    'StainlessFastener': dict(color=[0.58,0.60,0.61], roughness=.31, metallic=1, normal=.03, finish='stainless fastener'),
    'PipeCoverPVC': dict(color=[0.79,0.77,0.69], roughness=.58, metallic=0, normal=.07, finish='warm ivory exterior PVC trunking'),
    'DrainHosePVC': dict(color=[0.45,0.48,0.43], roughness=.66, metallic=0, normal=.09, finish='aged flexible PVC condensate hose'),
    'InsulationGray': dict(color=[0.24,0.25,0.23], roughness=.72, metallic=0, normal=.10, finish='closed-cell pipe insulation')
}
_TEXTURE_CACHE = {}

def _textures(mat, size=128):
    if mat in _TEXTURE_CACHE: return _TEXTURE_CACHE[mat]
    s=MATERIALS[mat]
    yy,xx=np.mgrid[:size,:size]; u=xx/size; v=yy/size
    h=.52*np.sin(TAU*(23*u+5*v))+.31*np.sin(TAU*(37*v-11*u))+.17*np.sin(TAU*(7*u+13*v))
    base=np.clip(np.asarray(s['color'])[None,None,:]*(1+.010*h[:,:,None]),0,1)
    rough=np.clip(s['roughness']+.022*h,.04,1)
    mr=np.stack([np.ones_like(rough),rough,np.full_like(rough,s['metallic'])],axis=-1)
    gy,gx=np.gradient(h)
    scale=s['normal']
    nx=-gx*scale; ny=-gy*scale; nz=np.ones_like(nx)
    n=np.stack([nx,ny,nz],axis=-1); n/=np.linalg.norm(n,axis=-1,keepdims=True)
    normal=np.clip(n*.5+.5,0,1)
    tex=(Image.fromarray(np.uint8(base*255)),Image.fromarray(np.uint8(mr*255)),Image.fromarray(np.uint8(normal*255)))
    _TEXTURE_CACHE[mat]=tex; return tex

def finalize(v,f):
    m=trimesh.Trimesh(vertices=np.asarray(v,float),faces=np.asarray(f,int),process=False)
    m.merge_vertices(digits_vertex=10); m.update_faces(m.nondegenerate_faces(height=1e-12)); m.remove_unreferenced_vertices(); m.fix_normals(multibody=True)
    return m

def triangulate(poly):
    if poly.is_empty: return []
    if poly.geom_type=='MultiPolygon': return [t for p in poly.geoms for t in triangulate(p)]
    return [np.asarray(t.exterior.coords)[:3] for t in shapely.constrained_delaunay_triangles(poly).geoms]

def extrude_xy(poly,z0,z1):
    v=[]; f=[]
    def tri(a,b,c):
        i=len(v); v.extend([a,b,c]); f.append([i,i+1,i+2])
    def quad(a,b,c,d): tri(a,b,c); tri(a,c,d)
    for t in triangulate(poly):
        tri([t[2,0],t[2,1],z0],[t[1,0],t[1,1],z0],[t[0,0],t[0,1],z0])
        tri([t[0,0],t[0,1],z1],[t[1,0],t[1,1],z1],[t[2,0],t[2,1],z1])
    rings=[poly.exterior,*poly.interiors]
    for r in rings:
        pts=list(r.coords)
        for a,b in zip(pts,pts[1:]): quad([a[0],a[1],z0],[b[0],b[1],z0],[b[0],b[1],z1],[a[0],a[1],z1])
    return finalize(v,f)

def boxmesh(extents, center=(0,0,0)):
    m=trimesh.creation.box(extents=extents); m.apply_translation(center); return m

def cylinder(radius,height,segments,axis='z',center=(0,0,0)):
    m=trimesh.creation.cylinder(radius=radius,height=height,sections=segments)
    if axis=='x': m.apply_transform(trimesh.transformations.rotation_matrix(math.pi/2,[0,1,0]))
    elif axis=='y': m.apply_transform(trimesh.transformations.rotation_matrix(math.pi/2,[1,0,0]))
    m.apply_translation(center); return m

def unit(v):
    v=np.asarray(v,float); n=np.linalg.norm(v)
    if n<1e-12: raise ValueError('zero vector')
    return v/n

def sweep(path,radius,sides,closed=False,inner_radius=None):
    pts=np.asarray(path,float)
    if closed: tang=np.roll(pts,-1,axis=0)-np.roll(pts,1,axis=0)
    else: tang=np.vstack([pts[1]-pts[0],pts[2:]-pts[:-2],pts[-1]-pts[-2]])
    tang/=np.linalg.norm(tang,axis=1)[:,None]
    frames=[]; prev=None
    for t in tang:
        n=np.cross(t,[0,1,0]) if prev is None else prev-t*np.dot(prev,t)
        if np.linalg.norm(n)<1e-8: n=np.cross(t,[1,0,0])
        n=unit(n); b=np.cross(t,n); frames.append((n,b)); prev=n
    frames=np.asarray(frames)
    ang=np.arange(sides)*TAU/sides
    rr = np.broadcast_to(np.asarray(radius,float),(len(pts),))
    outer=pts[:,None,:]+rr[:,None,None]*(frames[:,0,None,:]*np.cos(ang)[None,:,None]+frames[:,1,None,:]*np.sin(ang)[None,:,None])
    v=list(outer.reshape(-1,3)); f=[]
    for i in range(len(pts) if closed else len(pts)-1):
        k=(i+1)%len(pts)
        for j in range(sides):
            q=(j+1)%sides; a=i*sides+j; b=i*sides+q; c=k*sides+q; d=k*sides+j; f.extend([[a,b,c],[a,c,d]])
    if inner_radius is None:
        if not closed:
            for ri,rev in [(0,True),(len(pts)-1,False)]:
                c=len(v); v.append(pts[ri])
                for j in range(sides):
                    a=ri*sides+j; b=ri*sides+(j+1)%sides; f.append([c,b,a] if rev else [c,a,b])
    else:
        inn=np.broadcast_to(np.asarray(inner_radius,float),(len(pts),))
        inner=pts[:,None,:]+inn[:,None,None]*(frames[:,0,None,:]*np.cos(ang)[None,:,None]+frames[:,1,None,:]*np.sin(ang)[None,:,None])
        off=len(v); v.extend(inner.reshape(-1,3).tolist())
        for i in range(len(pts)-1):
            k=i+1
            for j in range(sides):
                q=(j+1)%sides; a=off+i*sides+j; b=off+k*sides+j; c=off+k*sides+q; d=off+i*sides+q; f.extend([[a,b,c],[a,c,d]])
        for ri,rev in [(0,True),(len(pts)-1,False)]:
            for j in range(sides):
                q=(j+1)%sides; ao=ri*sides+j; bo=ri*sides+q; ai=off+ri*sides+j; bi=off+ri*sides+q
                f.extend([[ao,ai,bi],[ao,bi,bo]] if rev else [[ao,bo,bi],[ao,bi,ai]])
    return finalize(v,f)

def materialize(scene,name,m,mat,uv_scale=.12):
    base,mr,norm=_textures(mat)
    pbr=trimesh.visual.material.PBRMaterial(name=mat,baseColorTexture=base,metallicRoughnessTexture=mr,normalTexture=norm,metallicFactor=1,roughnessFactor=1,doubleSided=False)
    uv=np.column_stack([m.vertices[:,0]+.37*m.vertices[:,2],m.vertices[:,1]+.23*m.vertices[:,2]])/uv_scale
    m.visual=trimesh.visual.TextureVisuals(uv=uv,material=pbr)
    scene.add_geometry(m,node_name=name,geom_name=name)

def fan_blade(center,r0,r1,angle,thickness,pitch,seg):
    ts=np.linspace(0,1,max(6,seg//8))
    a=angle + .28*(ts-.5)
    r=r0+(r1-r0)*ts
    width=.032+.020*np.sin(math.pi*ts)
    p1=np.column_stack([center[0]+r*np.cos(a)-width*np.sin(a), center[1]+r*np.sin(a)+width*np.cos(a)])
    p2=np.column_stack([center[0]+r*np.cos(a)+width*np.sin(a), center[1]+r*np.sin(a)-width*np.cos(a)])[::-1]
    poly=Polygon(np.vstack([p1,p2]))
    m=extrude_xy(poly,center[2]-thickness/2,center[2]+thickness/2)
    radial=[math.cos(angle),math.sin(angle),0]
    m.apply_transform(trimesh.transformations.rotation_matrix(pitch,radial,point=center)); return m

def build_condenser(level):
    cfg=LEVELS[level]; seg=cfg['seg']; sc=trimesh.Scene()
    W,H,D=.780,.540,.289; t=.0014; front=-D/2; back=D/2
    materialize(sc,'TopPanel',boxmesh([W+.012,t*2,D+.010],[0,H/2+t,0]),'WarmWhitePowderCoat')
    materialize(sc,'BottomPan',boxmesh([W,t*2,D],[0,-H/2+t,0]),'WarmWhitePowderCoat')
    materialize(sc,'LeftPanel',boxmesh([t*2,H-.018,D],[-W/2+t,0,0]),'WarmWhitePowderCoat')
    materialize(sc,'RightServicePanel',boxmesh([t*2,H-.018,D],[W/2-t,0,0]),'WarmWhitePowderCoat')
    materialize(sc,'RearPanel',boxmesh([W-.010,H-.018,t*2],[0,0,back-t]),'WarmWhitePowderCoat')
    rr=box(-W/2+.008,-H/2+.008,W/2-.008,H/2-.008).buffer(.012,join_style=1)
    fan_c=np.array([-.100,.015,front-.001])
    opening=Point(fan_c[0],fan_c[1]).buffer(.216,quad_segs=max(8,seg//8))
    materialize(sc,'FrontPanelWithRealFanOpening',extrude_xy(rr.difference(opening),front-.002,front+.0015),'WarmWhitePowderCoat')
    zfin=front+.027; finw=.0012; x0=fan_c[0]-.205; x1=fan_c[0]+.205
    for i,x in enumerate(np.linspace(x0,x1,cfg['fins'])):
        yspan=2*math.sqrt(max(.0,.205**2-(x-fan_c[0])**2))
        if yspan>.015: materialize(sc,f'HeatExchangerFin_{i:03d}',boxmesh([finw,yspan,.010],[x,fan_c[1],zfin]),'AluminumFin',.06)
    outer=Point(fan_c[0],fan_c[1]).buffer(.207,quad_segs=max(8,seg//8)); inner=Point(fan_c[0],fan_c[1]).buffer(.191,quad_segs=max(8,seg//8))
    materialize(sc,'FanShroudRing',extrude_xy(outer.difference(inner),front+.009,front+.026),'DarkInterior')
    materialize(sc,'FanHub',cylinder(.047,.038,max(16,seg//2),'z',[fan_c[0],fan_c[1],front+.003]),'DarkFanPP')
    for j in range(3): materialize(sc,f'FanBlade_{j}',fan_blade([fan_c[0],fan_c[1],front+.003],.045,.186,TAU*j/3,.004,math.radians(18),seg),'DarkFanPP')
    zguard=front-.012
    for i,r in enumerate(np.linspace(.050,.205,cfg['guard_rings'])):
        ang=np.linspace(0,TAU,max(32,seg),endpoint=False); path=np.column_stack([fan_c[0]+r*np.cos(ang),fan_c[1]+r*np.sin(ang),np.full_like(ang,zguard-.010*(r/.205)**2)])
        materialize(sc,f'GuardRing_{i:02d}',sweep(path,.00155,max(5,seg//12),True),'WarmWhitePowderCoat',.04)
    for i,a in enumerate(np.arange(cfg['spokes'])*TAU/cfg['spokes']):
        rs=np.linspace(.045,.208,max(6,seg//12)); path=np.column_stack([fan_c[0]+rs*np.cos(a),fan_c[1]+rs*np.sin(a),zguard-.010*(rs/.205)**2])
        materialize(sc,f'GuardSpoke_{i:02d}',sweep(path,.00145,max(5,seg//12)),'WarmWhitePowderCoat',.04)
    lx=W/2-.004
    for i,y in enumerate(np.linspace(-.165,.155,cfg['louvers'])):
        blade=boxmesh([.004,.008,.120],[lx,y,.035]); blade.apply_transform(trimesh.transformations.rotation_matrix(math.radians(-18),[1,0,0],point=[lx,y,.035])); materialize(sc,f'SideLouver_{i:02d}',blade,'WarmWhitePowderCoat')
    materialize(sc,'ServicePanelVerticalSeam',boxmesh([.003,H*.72,.002],[W*.267,0,front-.003]),'DarkInterior')
    materialize(sc,'BlankNameplate',boxmesh([.125,.055,.0012],[W*.268,.145,front-.004]),'WarmWhitePowderCoat')
    screw_pts=[(-W*.34,H*.38),(W*.34,H*.38),(-W*.34,-H*.38),(W*.34,-H*.38),(W*.22,.20),(W*.22,-.18),(W*.34,.08),(W*.34,-.08)]
    for i,(x,y) in enumerate(screw_pts[:cfg['screws']]): materialize(sc,f'Fastener_{i:02d}',cylinder(.004,.003,max(10,seg//6),'z',[x,y,front-.004]),'StainlessFastener',.025)
    for i,x in enumerate([-.285,.285]):
        materialize(sc,f'FootRail_{i}',boxmesh([.165,.026,.095],[x,-H/2-.013,.035]),'WarmWhitePowderCoat')
        materialize(sc,f'VibrationPad_{i}',boxmesh([.120,.010,.070],[x,-H/2-.031,.035]),'BlackEPDM')
    vx=W/2+.020
    materialize(sc,'LargeServiceValve',cylinder(.014,.050,max(12,seg//4),'x',[vx,-.105,.070]),'WarmWhitePowderCoat')
    materialize(sc,'SmallServiceValve',cylinder(.010,.044,max(12,seg//4),'x',[vx,-.155,.070]),'WarmWhitePowderCoat')
    materialize(sc,'LargeCopperStub',cylinder(.008,.080,max(12,seg//4),'x',[vx+.055,-.105,.070]),'CopperTube',.04)
    materialize(sc,'SmallCopperStub',cylinder(.005,.080,max(12,seg//4),'x',[vx+.055,-.155,.070]),'CopperTube',.04)
    return sc

def rounded_channel_section(w,d,r): return box(-w/2+r,-d/2+r,w/2-r,d/2-r).buffer(r,join_style=1)

def build_trunking(level):
    cfg=LEVELS[level]; seg=cfg['seg']; sc=trimesh.Scene(); w=.064; d=.055; h=.900; wall=.0022
    outer=rounded_channel_section(w,d,.007); inner=rounded_channel_section(w-2*wall,d-2*wall,.005)
    m=extrude_xy(outer.difference(inner),0,h)
    vv=m.vertices.copy(); m.vertices=np.column_stack([vv[:,0],vv[:,2],vv[:,1]]); m.faces=m.faces[:,::-1]; m.fix_normals(multibody=True)
    materialize(sc,'VerticalTrunkingShell',m,'PipeCoverPVC',.08)
    materialize(sc,'SnapFrontCover',boxmesh([w-.008,h-.012,.0025],[0,h/2,-d/2-.001]),'PipeCoverPVC',.08)
    for i,y in enumerate([.18,.46,.74]): materialize(sc,f'CoverClipBand_{i}',boxmesh([w+.002,.006,.004],[0,y,-d/2-.0025]),'PipeCoverPVC')
    elbow_samples={'MASTER':26,'LOD0':20,'LOD1':14,'LOD2':10,'LOD3':8}[level]
    elbow_sides={'MASTER':18,'LOD0':14,'LOD1':10,'LOD2':8,'LOD3':6}[level]
    line_sections={'MASTER':24,'LOD0':18,'LOD1':12,'LOD2':8,'LOD3':6}[level]
    R=.075; ang=np.linspace(math.pi,math.pi/2,elbow_samples); path=np.column_stack([R*np.cos(ang)+R,np.full_like(ang,.055),R*np.sin(ang)-R])
    materialize(sc,'LowerElbowHousing',sweep(path,.035,elbow_sides),'PipeCoverPVC',.08)
    materialize(sc,'HorizontalTrunkingRun',boxmesh([.280,.060,.055],[.140,.055,-.075]),'PipeCoverPVC',.08)
    for i,(y,z,r) in enumerate([(0.040,-.110,.010),(0.070,-.110,.007)]): materialize(sc,f'InsulatedLine_{i}',cylinder(r,.095,line_sections,'x',[.325,y,z]),'InsulationGray',.04)
    return sc

def build_drain_hose(level):
    cfg=LEVELS[level]; corr=cfg['corr']; sc=trimesh.Scene()
    samples={'MASTER':336,'LOD0':240,'LOD1':144,'LOD2':80,'LOD3':48}[level]
    hose_sides={'MASTER':16,'LOD0':12,'LOD1':8,'LOD2':7,'LOD3':6}[level]
    clip_samples={'MASTER':48,'LOD0':36,'LOD1':24,'LOD2':18,'LOD3':14}[level]
    clip_sides={'MASTER':8,'LOD0':7,'LOD1':6,'LOD2':5,'LOD3':4}[level]
    t=np.linspace(0,1,samples); x=.22+.36*t; y=.34*(1-t)+.045*t-.018*np.sin(math.pi*t); z=.12-.12*t+.015*np.sin(2*math.pi*t)*(1-t)
    path=np.column_stack([x,y,z]); base=.0102; rr=base+.00125*np.sin(TAU*corr*t); inner=rr-.0013
    materialize(sc,'CorrugatedCondensateHose',sweep(path,rr,hose_sides,False,inner),'DrainHosePVC',.045)
    ang=np.linspace(0,TAU,clip_samples,endpoint=False); c=path[min(6,len(path)-1)]; ring=np.column_stack([c[0]+.012*np.cos(ang),np.full_like(ang,c[1]),c[2]+.012*np.sin(ang)])
    materialize(sc,'UpperRetentionClip',sweep(ring,.0010,clip_sides,True),'WarmWhitePowderCoat',.03)
    return sc

BUILDERS={'OutdoorACCondenser780':build_condenser,'RefrigerantPipeTrunking60x55_900':build_trunking,'CondensateDrainHose16_1200':build_drain_hose}

def verify(scene):
    rows=[]
    for name,g in scene.geometry.items():
        assert np.isfinite(g.vertices).all() and np.isfinite(g.vertex_normals).all(),name
        assert np.all(g.area_faces>1e-14),name
        h=g.copy(); h.merge_vertices(digits_vertex=9,merge_tex=True,merge_norm=True); h.remove_unreferenced_vertices()
        assert h.is_watertight and h.is_winding_consistent and h.volume>0,(name,h.is_watertight,h.is_winding_consistent,h.volume)
        _,cnt=np.unique(h.edges_sorted,axis=0,return_counts=True); assert np.all(cnt==2),name
        rows.append({'part':name,'triangles':len(g.faces),'boundaryEdges':0,'volumeM3':float(h.volume)})
    return {'triangles':sum(r['triangles'] for r in rows),'componentSurfaces':len(rows),'parts':rows,'boundsMetres':scene.bounds.tolist()}

def export_scene(scene,out,stem):
    glb=out/(stem+'.glb'); glb.write_bytes(scene.export(file_type='glb'))
    rd=trimesh.load(glb,force='scene',process=False); assert sum(len(g.faces) for g in rd.geometry.values())==sum(len(g.faces) for g in scene.geometry.values())
    folder=out/stem; folder.mkdir(exist_ok=True)
    obj,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True); (folder/(stem+'.obj')).write_text(obj)
    for fn,data in files.items():
        p=folder/fn; p.write_text(data) if isinstance(data,str) else p.write_bytes(data)
    od=trimesh.load(folder/(stem+'.obj'),force='scene',process=False); assert sum(len(g.faces) for g in od.geometry.values())==sum(len(g.faces) for g in scene.geometry.values())
    return glb

def run(out):
    out.mkdir(parents=True,exist_ok=True); assets=[]
    for aid,builder in BUILDERS.items():
        levels=[]
        for level in LEVELS:
            scene=builder(level); dy=-scene.bounds[0,1]
            for g in scene.geometry.values(): g.apply_translation([0,dy,0])
            stat=verify(scene); stem=f'{aid}_{level}'; glb=export_scene(scene,out,stem)
            levels.append({'level':level,'sha256':hashlib.sha256(glb.read_bytes()).hexdigest(),'glbRoundtrip':True,'objTriangleRoundtrip':True,**stat}); print(aid,level,stat['triangles'],stat['componentSurfaces'],flush=True)
        counts=[x['triangles'] for x in levels]; assert all(a>b for a,b in zip(counts,counts[1:])),(aid,counts); assets.append({'asset':aid,'levels':levels})
    report={'status':'EXTERNAL_GEOMETRY_CHECKED_NOT_UNITY_VISUAL_PASS','assets':assets,'materials':MATERIALS,'unityCompile':False,'unityImport':False,'unityRender':False,'performanceVerified':False,'temporalVerified':False,'visualFidelityScore':None,'visualPass':False,'sourceSHA256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
    (out/'geometry_verification.json').write_text(json.dumps(report,indent=2)+'\n'); return report

if __name__=='__main__':
    ap=argparse.ArgumentParser(); ap.add_argument('--output',type=Path,required=True); args=ap.parse_args(); run(args.output)
