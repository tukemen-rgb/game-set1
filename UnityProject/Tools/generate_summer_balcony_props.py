from __future__ import annotations
import argparse, hashlib, json, math
from pathlib import Path
import numpy as np
import shapely
from shapely.geometry import Polygon, Point
from shapely.affinity import scale as shp_scale
import trimesh
from PIL import Image

TAU = 2 * math.pi

MATERIALS = {
    "SkyBlueEVA": dict(color=[0.18,0.39,0.56], roughness=0.62, metallic=0, F0=0.04,
                       finish="matte molded EVA/PVC-like balcony slipper compound"),
    "OffWhiteEnamel": dict(color=[0.78,0.77,0.70], roughness=0.31, metallic=0, F0=0.04,
                           finish="opaque vitreous-enamel-like coating over formed steel; coating response is dielectric"),
    "BlueEnamel": dict(color=[0.08,0.19,0.36], roughness=0.28, metallic=0, F0=0.04,
                       finish="dark blue enamel rim accent, no exposed metal response"),
    "DarkSteel": dict(color=[0.20,0.21,0.22], roughness=0.45, metallic=1, F0=None,
                      finish="darkened stamped steel with real conductor response"),
    "CoilGreen": dict(color=[0.20,0.34,0.12], roughness=0.88, metallic=0, F0=0.04,
                      finish="dry compressed plant-fibre mosquito coil; no glow or smoke baked into material"),
}

def unit(v):
    v=np.asarray(v,dtype=float); n=np.linalg.norm(v)
    if n < 1e-12: raise ValueError("zero vector")
    return v/n

class Writer:
    def __init__(self): self.v=[]; self.f=[]
    def tri(self,a,b,c):
        i=len(self.v); self.v.extend([a,b,c]); self.f.append([i,i+1,i+2])
    def quad(self,a,b,c,d):
        self.tri(a,b,c); self.tri(a,c,d)
    def finish(self):
        m=trimesh.Trimesh(vertices=np.asarray(self.v,dtype=float), faces=np.asarray(self.f,dtype=np.int64), process=False)
        m.merge_vertices(digits_vertex=9)
        m.remove_unreferenced_vertices()
        m.update_faces(m.nondegenerate_faces(height=1e-11))
        m.remove_unreferenced_vertices()
        m.fix_normals(multibody=True)
        return m

def lathe_closed(profile, n, squash_z=1.0):
    p=np.asarray(profile,dtype=float)
    w=Writer()
    for j in range(len(p)):
        k=(j+1)%len(p)
        for i in range(n):
            a0=TAU*i/n; a1=TAU*(i+1)/n
            r0,y0=p[j]; r1,y1=p[k]
            w.quad([r0*math.cos(a0),y0,squash_z*r0*math.sin(a0)],
                   [r0*math.cos(a1),y0,squash_z*r0*math.sin(a1)],
                   [r1*math.cos(a1),y1,squash_z*r1*math.sin(a1)],
                   [r1*math.cos(a0),y1,squash_z*r1*math.sin(a0)])
    return w.finish()

def tube(path, radius, sides, cap=True):
    path=np.asarray(path,dtype=float)
    rr=np.broadcast_to(radius,(len(path),))
    frames=[]; prev=None
    for i in range(len(path)):
        tangent=unit(path[min(i+1,len(path)-1)]-path[max(i-1,0)])
        n=np.cross(tangent,[0,1,0]) if prev is None else prev-tangent*np.dot(prev,tangent)
        if np.linalg.norm(n)<1e-8: n=np.cross(tangent,[1,0,0])
        n=unit(n); b=unit(np.cross(tangent,n)); frames.append((n,b)); prev=n
    rings=[]
    for p,r,(n,b) in zip(path,rr,frames):
        rings.append([p+r*(n*math.cos(TAU*j/sides)+b*math.sin(TAU*j/sides)) for j in range(sides)])
    w=Writer()
    for i in range(len(path)-1):
        for j in range(sides):
            k=(j+1)%sides; w.quad(rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j])
    if cap:
        for j in range(sides):
            k=(j+1)%sides
            w.tri(path[0],rings[0][k],rings[0][j])
            w.tri(path[-1],rings[-1][j],rings[-1][k])
    return w.finish()

def rect_sweep(path, width, thickness):
    path=np.asarray(path,dtype=float); w=Writer(); rings=[]
    for i,p in enumerate(path):
        t=unit(path[min(i+1,len(path)-1)]-path[max(0,i-1)])
        side=np.cross([0,1,0],t)
        if np.linalg.norm(side)<1e-8: side=np.array([1,0,0],float)
        side=unit(side); up=unit(np.cross(t,side))
        rings.append([p-side*width/2-up*thickness/2,
                      p+side*width/2-up*thickness/2,
                      p+side*width/2+up*thickness/2,
                      p-side*width/2+up*thickness/2])
    for i in range(len(rings)-1):
        a,b=rings[i],rings[i+1]
        for j in range(4): w.quad(a[j],a[(j+1)%4],b[(j+1)%4],b[j])
    w.quad(rings[0][3],rings[0][2],rings[0][1],rings[0][0])
    w.quad(rings[-1][0],rings[-1][1],rings[-1][2],rings[-1][3])
    return w.finish()

def triangulate(poly):
    if poly.is_empty: return []
    if poly.geom_type=="MultiPolygon":
        return [t for g in poly.geoms for t in triangulate(g)]
    return [np.asarray(t.exterior.coords)[:3] for t in shapely.constrained_delaunay_triangles(poly).geoms]

def extrude(poly, height):
    if not poly.is_valid: raise ValueError("invalid polygon")
    w=Writer()
    for t in triangulate(poly):
        w.tri([t[0,0],0,t[0,1]],[t[2,0],0,t[2,1]],[t[1,0],0,t[1,1]])
        w.tri([t[0,0],height,t[0,1]],[t[1,0],height,t[1,1]],[t[2,0],height,t[2,1]])
    rings=[poly.exterior,*poly.interiors]
    for ring in rings:
        pts=list(ring.coords)
        for a,b in zip(pts,pts[1:]):
            w.quad([a[0],0,a[1]],[b[0],0,b[1]],[b[0],height,b[1]],[a[0],height,a[1]])
    return w.finish()

def box_mesh(extents, centre):
    m=trimesh.creation.box(extents=extents)
    m.apply_translation(centre)
    return m

def apply_material(mesh, material):
    spec=MATERIALS[material]
    size=96
    yy,xx=np.mgrid[:size,:size]; phase=np.sin(TAU*(11*xx/size+7*yy/size))*np.sin(TAU*(13*yy/size-3*xx/size))
    base=np.clip(np.asarray(spec["color"])[None,None,:]*(1+0.012*phase[:,:,None]),0,1)
    rough=np.clip(spec["roughness"]+0.018*phase,.04,1)
    orm=np.stack([np.ones_like(rough),rough,np.ones_like(rough)*spec["metallic"]],axis=-1)
    base_img=Image.fromarray(np.uint8(base*255))
    orm_img=Image.fromarray(np.uint8(orm*255))
    uv=np.column_stack([mesh.vertices[:,0]+.71*mesh.vertices[:,2], mesh.vertices[:,1]])/.08
    pbr=trimesh.visual.material.PBRMaterial(name=material, baseColorTexture=base_img,
                                            metallicRoughnessTexture=orm_img, metallicFactor=1, roughnessFactor=1)
    mesh.visual=trimesh.visual.TextureVisuals(uv=uv,material=pbr)

def add(scene,name,m,mat):
    w=m.copy(); w.merge_vertices(digits_vertex=8)
    assert w.is_watertight,(name,"not watertight")
    assert w.is_winding_consistent and w.volume>0,(name,"orientation")
    apply_material(m,mat)
    scene.add_geometry(m,node_name=name,geom_name=name)

def slipper_single(n, mirror=False):
    outline=np.array([
        [-.046,-.110],[-.060,-.085],[-.064,-.045],[-.062,.005],[-.055,.065],[-.043,.102],
        [-.020,.120],[.018,.122],[.042,.110],[.057,.080],[.064,.030],[.064,-.020],
        [.058,-.068],[.047,-.105],[.020,-.122],[-.020,-.122]
    ])
    poly=Polygon(outline).buffer(.003,resolution=max(3,n//16)).buffer(-.003,resolution=max(3,n//16))
    sole=extrude(poly,.015)
    scene=trimesh.Scene(); add(scene,"MoldedSole",sole,"SkyBlueEVA")
    tread_count={128:18,96:16,64:12,32:8,16:6}[n]
    for i in range(tread_count):
        t=i/(tread_count-1)
        z=-.097+.188*t
        x=.024*math.sin(i*2.2)
        pod=box_mesh([.024,.0025,.010],[x,.00125,z])
        add(scene,f"TreadPod_{i:02d}",pod,"SkyBlueEVA")
    samples=max(7,n//12)
    xvals=np.linspace(-.053,.053,samples)
    path=[]
    for x in xvals:
        arch=.022 + .028*(1-(x/.053)**2)
        z=.032 + .008*(x/.053)
        path.append([x,.015+arch,z])
    strap=rect_sweep(path,.038,.0042)
    add(scene,"BridgeStrap",strap,"SkyBlueEVA")
    for side in [-1,1]:
        foot=box_mesh([.017,.009,.030],[side*.050,.019,.032+side*.008])
        add(scene,"StrapAnchor_"+str(side),foot,"SkyBlueEVA")
    heel_path=[[x,.016,-.103] for x in np.linspace(-.038,.038,max(5,n//20))]
    add(scene,"HeelLip",rect_sweep(heel_path,.008,.004),"SkyBlueEVA")
    return scene

def slipper_pair(n):
    out=trimesh.Scene()
    for side,offset,yaw in [("L",[-.075,0,0],math.radians(-5)),("R",[.075,0,.012],math.radians(7))]:
        sc=slipper_single(n)
        c,s=math.cos(yaw),math.sin(yaw)
        T=np.array([[c,0,s,offset[0]],[0,1,0,0],[-s,0,c,offset[2]],[0,0,0,1]],float)
        for name,g in sc.geometry.items():
            gg=g.copy();gg.apply_transform(T);out.add_geometry(gg,node_name=side+"_"+name,geom_name=side+"_"+name)
    return out, {"pairSpacingMetres":.15,"soleLengthMetres":.244,"soleThicknessMetres":.015,
                 "strapThicknessMetres":.0042,"construction":"molded one-material slide interpreted as separate watertight production surfaces"}

def wash_basin(n):
    profile=[
        (0,0.000),(0.112,0.000),(0.123,0.002),(0.132,0.008),(0.139,0.021),(0.142,0.039),
        (0.142,0.053),(0.1415,0.058),(0.140,0.061),(0.137,0.063),(0.1345,0.0618),
        (0.1335,0.059),(0.133,0.053),(0.131,0.040),(0.125,0.019),(0.116,0.005),(0,0.002)
    ]
    scene=trimesh.Scene(); shell=lathe_closed(profile,n)
    add(scene,"FormedEnamelShell",shell,"OffWhiteEnamel")
    rim_profile=[(.137,0.0585),(.1425,0.0585),(.1435,0.060),(.1435,0.063),(.1425,0.0645),(.137,0.0645),(.136,0.063),(.136,0.060)]
    add(scene,"BlueEnamelRolledRim",lathe_closed(rim_profile,n),"BlueEnamel")
    for a in [0,TAU/3,2*TAU/3]:
        p=[.065*math.cos(a),.0025,.065*math.sin(a)]
        add(scene,"BaseFoot_"+str(round(a,2)),box_mesh([.026,.005,.014],p),"OffWhiteEnamel")
    return scene,{"outerDiameterMetres":.287,"heightMetres":.0645,"nominalSheetAndEnamelThicknessMetres":.0015,
                  "finish":"opaque enamel interpreted as dielectric coating; steel substrate not exposed"}

def coil_holder(n):
    scene=trimesh.Scene()
    tray_profile=[
        (0,0),(0.067,0),(0.072,.002),(0.075,.006),(0.076,.011),(0.076,.014),
        (0.0745,.016),(0.072,.017),(0.0705,.016),(0.070,.014),(0.069,.009),(0.065,.004),(0,0.002)
    ]
    add(scene,"StampedSteelTray",lathe_closed(tray_profile,n),"DarkSteel")
    add(scene,"CentreHub",lathe_closed([(0,.004),(.008,.004),(.009,.006),(.009,.012),(.008,.014),(0,.014)],max(12,n//2)),"DarkSteel")
    for a in [0,TAU/4,TAU/2,3*TAU/4]:
        c,s=math.cos(a),math.sin(a)
        tab=box_mesh([.035,.0025,.006],[.025*c,.010,.025*s])
        T=np.eye(4);T[:3,:3]=[[c,0,s],[0,1,0],[-s,0,c]]
        centre=np.array([.025*c,.010,.025*s])
        tab.apply_translation(-centre);tab.apply_transform(T);tab.apply_translation(centre)
        add(scene,"SupportTab_"+str(round(a,2)),tab,"DarkSteel")
    turns=4.4
    samples=max(90,n*2)
    t=np.linspace(.18,turns*TAU,samples)
    r=.007 + (.056-.007)*(t-t.min())/(t.max()-t.min())
    path=np.column_stack([r*np.cos(t), np.full_like(t,.019), r*np.sin(t)])
    add(scene,"GreenMosquitoCoil",tube(path,.0022,max(6,n//10)),"CoilGreen")
    for x in [-.005,.005]:
        add(scene,"CoilFork_"+str(x),box_mesh([.0025,.013,.010],[x,.0125,0]),"DarkSteel")
    return scene,{"trayOuterDiameterMetres":.152,"coilOuterDiameterMetres":.116,"coilCrossSectionDiameterMetres":.0044,
                  "coilTurns":turns,"smokeSimulation":False,"ignitionState":"unlit dry reference"}

BUILDERS=[("BalconySlipperPair",slipper_pair),("EnamelWashBasin280",wash_basin),("MosquitoCoilTray140",coil_holder)]

def verify(scene):
    components=[]
    for name,g in scene.geometry.items():
        assert np.isfinite(g.vertices).all() and np.isfinite(g.face_normals).all()
        assert np.all(g.area_faces>1e-13),(name,"degenerate")
        w=g.copy();w.merge_vertices(digits_vertex=8)
        assert w.is_watertight,(name,"open edges")
        assert w.is_winding_consistent and w.volume>0,(name,"winding")
        edges,count=np.unique(w.edges_sorted,axis=0,return_counts=True)
        assert np.all(count==2),(name,"nonmanifold")
        components.append(dict(name=name,triangles=len(g.faces),watertight=True,boundaryEdges=0,
                               nonmanifoldEdges=0,volumeM3=float(w.volume)))
    return dict(triangles=sum(x["triangles"] for x in components), components=components, boundsMetres=scene.bounds.tolist())

def render_preview(scene,path,az=35,el=24,res=(1000,750)):
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d.art3d import Poly3DCollection
    fig=plt.figure(figsize=(10,7.5),dpi=100)
    ax=fig.add_subplot(111,projection="3d")
    allv=[]
    for name,g in scene.geometry.items():
        spec=MATERIALS.get(getattr(getattr(g.visual,"material",None),"name","SkyBlueEVA"),MATERIALS["SkyBlueEVA"])
        v=np.column_stack([g.vertices[:,0],g.vertices[:,2],g.vertices[:,1]])
        allv.append(v)
        faces=v[g.faces]
        coll=Poly3DCollection(faces,linewidths=0.05,edgecolors=(0,0,0,.04),alpha=1)
        coll.set_facecolor(tuple(spec["color"])+(.98,))
        ax.add_collection3d(coll)
    V=np.vstack(allv);lo=V.min(axis=0);hi=V.max(axis=0);centre=(lo+hi)/2;span=max(hi-lo)*.62
    ax.set_xlim(centre[0]-span,centre[0]+span)
    ax.set_ylim(centre[1]-span,centre[1]+span)
    ax.set_zlim(max(0,centre[2]-span),centre[2]+span)
    ax.view_init(elev=el,azim=az);ax.set_axis_off()
    fig.text(.01,.01,"ACTUAL MESH DIAGNOSTIC — NOT UNITY — NO VISUAL FIDELITY SCORE",fontsize=8)
    plt.tight_layout();fig.savefig(path,bbox_inches="tight",pad_inches=.05);plt.close(fig)

def run(out:Path):
    out.mkdir(parents=True,exist_ok=True)
    all_assets=[]
    for asset,builder in BUILDERS:
        levels=[]
        for label,n in [("MASTER",128),("LOD0",96),("LOD1",64),("LOD2",32),("LOD3",16)]:
            scene,spec=builder(n)
            dy=-float(scene.bounds[0,1])
            for g in scene.geometry.values(): g.apply_translation([0,dy,0])
            vr=verify(scene)
            glb=out/f"{asset}_{label}.glb";glb.write_bytes(scene.export(file_type="glb"))
            reload_glb=trimesh.load(glb,force="scene",process=False)
            assert sum(len(m.faces) for m in reload_glb.geometry.values())==vr["triangles"]
            assert np.allclose(reload_glb.bounds,scene.bounds,atol=1e-6)
            folder=out/f"{asset}_{label}";folder.mkdir(exist_ok=True)
            obj,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True)
            (folder/f"{asset}_{label}.obj").write_text(obj)
            for fn,data in files.items():
                p=folder/fn;p.write_text(data) if isinstance(data,str) else p.write_bytes(data)
            reload_obj=trimesh.load(folder/f"{asset}_{label}.obj",force="scene",process=False)
            assert sum(len(m.faces) for m in reload_obj.geometry.values())==vr["triangles"]
            levels.append(dict(level=label,segments=n,sha256=hashlib.sha256(glb.read_bytes()).hexdigest(),
                               glbRoundtrip=True,objRoundtrip=True,**vr))
            if label=="LOD0":
                render_preview(scene,out/f"{asset}_LOD0_preview.png")
        counts=[x["triangles"] for x in levels]
        assert all(a>b for a,b in zip(counts,counts[1:])),(asset,counts)
        all_assets.append(dict(asset=asset,designAssumptions=spec,levels=levels))
        print(asset,counts)
    report=dict(status="EXTERNAL_GEOMETRY_CHECKED_NOT_VISUAL_PASS",assets=all_assets,
                unityCompile=False,unityImport=False,unityRender=False,performanceVerified=False,temporalVerified=False,
                visualFidelityScore=None,notes=["Actual OBJ/GLB meshes generated and reloaded outside Unity.",
                "Component closure and manifoldness do not prove component-to-component contact clearance.",
                "Diagnostic previews are not Unity render evidence."])
    (out/"geometry_verification.json").write_text(json.dumps(report,indent=2))
    return report

if __name__=="__main__":
    ap=argparse.ArgumentParser();ap.add_argument("--output",type=Path,required=True);args=ap.parse_args();run(args.output)
