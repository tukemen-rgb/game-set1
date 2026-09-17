
from __future__ import annotations
import math, json, hashlib, argparse
from pathlib import Path
import numpy as np
import trimesh
import shapely
from shapely.geometry import Point, Polygon
from shapely.ops import unary_union

TAU=math.tau

MATERIALS = {
    "ChromeBrass": {"base":[0.62,0.64,0.66,1.0],"metallic":1.0,"roughness":0.18},
    "WarmBrass": {"base":[0.62,0.43,0.16,1.0],"metallic":1.0,"roughness":0.28},
    "BlueIndexCap": {"base":[0.04,0.18,0.46,1.0],"metallic":0.0,"roughness":0.42},
    "GreenPVC": {"base":[0.08,0.31,0.17,1.0],"metallic":0.0,"roughness":0.55},
    "DarkPP": {"base":[0.08,0.09,0.09,1.0],"metallic":0.0,"roughness":0.50},
    "GrayPP": {"base":[0.30,0.32,0.33,1.0],"metallic":0.0,"roughness":0.47},
    "BlackTPE": {"base":[0.025,0.028,0.03,1.0],"metallic":0.0,"roughness":0.68},
    "Stainless": {"base":[0.55,0.57,0.59,1.0],"metallic":1.0,"roughness":0.30},
}

def mat(name):
    s=MATERIALS[name]
    return trimesh.visual.material.PBRMaterial(name=name, baseColorFactor=s["base"], metallicFactor=s["metallic"], roughnessFactor=s["roughness"])

def apply_mat(m,name):
    m.visual.material=mat(name)
    return m

def align_z_to(vec):
    v=np.array(vec,dtype=float); v/=np.linalg.norm(v)
    z=np.array([0.,0.,1.])
    c=np.dot(z,v)
    if c>0.999999: return np.eye(4)
    if c<-0.999999:
        T=np.eye(4); T[:3,:3]=np.array([[1,0,0],[0,-1,0],[0,0,-1.]])
        return T
    axis=np.cross(z,v); axis/=np.linalg.norm(axis)
    angle=math.acos(np.clip(c,-1,1))
    return trimesh.transformations.rotation_matrix(angle,axis)

def cylinder_between(a,b,r,sections,name,material):
    a=np.asarray(a,float); b=np.asarray(b,float)
    d=b-a; L=np.linalg.norm(d)
    m=trimesh.creation.cylinder(radius=r,height=L,sections=sections)
    T=align_z_to(d)
    m.apply_transform(T); m.apply_translation((a+b)/2)
    m.metadata["name"]=name
    return apply_mat(m,material)

def capsule_tube(path,r,sides,name,material,closed=False):
    pts=np.asarray(path,float)
    if closed:
        tang=np.roll(pts,-1,axis=0)-np.roll(pts,1,axis=0)
    else:
        tang=np.empty_like(pts)
        tang[0]=pts[1]-pts[0]; tang[-1]=pts[-1]-pts[-2]
        tang[1:-1]=pts[2:]-pts[:-2]
    tang=tang/np.linalg.norm(tang,axis=1)[:,None]
    normals=np.zeros_like(pts)
    prev=None
    for i,t in enumerate(tang):
        if prev is None:
            n=np.cross(t,[0,1,0])
            if np.linalg.norm(n)<1e-6: n=np.cross(t,[1,0,0])
        else:
            n=prev-t*np.dot(prev,t)
            if np.linalg.norm(n)<1e-6:
                n=np.cross(t,[0,1,0])
                if np.linalg.norm(n)<1e-6: n=np.cross(t,[1,0,0])
        n=n/np.linalg.norm(n)
        normals[i]=n; prev=n
    bits=np.cross(tang,normals)
    verts=[]; faces=[]
    for i,p in enumerate(pts):
        for j in range(sides):
            a=TAU*j/sides
            verts.append(p+r*(normals[i]*math.cos(a)+bits[i]*math.sin(a)))
    rings=len(pts)
    span=rings if closed else rings-1
    for i in range(span):
        ni=(i+1)%rings
        for j in range(sides):
            nj=(j+1)%sides
            a=i*sides+j;b=i*sides+nj;c=ni*sides+nj;d=ni*sides+j
            faces += [[a,b,c],[a,c,d]]
    if not closed:
        for end,flip in [(0,True),(rings-1,False)]:
            center=len(verts); verts.append(pts[end])
            for j in range(sides):
                nj=(j+1)%sides
                a=end*sides+j;b=end*sides+nj
                faces.append([center,b,a] if flip else [center,a,b])
    m=trimesh.Trimesh(vertices=np.array(verts),faces=np.array(faces),process=False)
    m.merge_vertices(digits_vertex=9); m.remove_unreferenced_vertices(); m.fix_normals(multibody=True)
    m.metadata["name"]=name
    return apply_mat(m,material)

def torus_component(center,major,minor,major_sections,minor_sections,name,material,axis="y",ellipticity=1.0):
    c=np.asarray(center,float)
    verts=[]; faces=[]
    for i in range(major_sections):
        u=TAU*i/major_sections
        cu,su=math.cos(u),math.sin(u)
        for j in range(minor_sections):
            v=TAU*j/minor_sections; cv,sv=math.cos(v),math.sin(v)
            if axis=="y":
                x=(major+minor*cv)*cu; z=(major+minor*cv)*su*ellipticity; y=minor*sv
            else:
                x=(major+minor*cv)*cu; y=(major+minor*cv)*su*ellipticity; z=minor*sv
            verts.append(c+[x,y,z])
    for i in range(major_sections):
        ni=(i+1)%major_sections
        for j in range(minor_sections):
            nj=(j+1)%minor_sections
            a=i*minor_sections+j;b=i*minor_sections+nj;c=ni*minor_sections+nj;d=ni*minor_sections+j
            faces += [[a,b,c],[a,c,d]]
    m=trimesh.Trimesh(vertices=np.array(verts),faces=np.array(faces),process=False)
    m.merge_vertices(digits_vertex=9);m.fix_normals(multibody=True);m.metadata["name"]=name
    return apply_mat(m,material)

def box_component(extents,center,name,material,rot=None):
    m=trimesh.creation.box(extents=extents)
    if rot is not None:m.apply_transform(rot)
    m.apply_translation(center);m.metadata["name"]=name
    return apply_mat(m,material)

def annulus_disk_holes(radius,thickness,hole_centers,hole_radius,segments,center,normal,name,material):
    outer=Point(0,0).buffer(radius,quad_segs=max(8,segments//4))
    holes=[Point(x,y).buffer(hole_radius,quad_segs=max(2,segments//24)) for x,y in hole_centers]
    poly=outer.difference(unary_union(holes))
    tris=list(shapely.constrained_delaunay_triangles(poly).geoms)
    verts=[];faces=[]
    index={}
    def vid(p,z):
        key=(round(p[0],10),round(p[1],10),round(z,10))
        if key not in index:
            index[key]=len(verts);verts.append([p[0],p[1],z])
        return index[key]
    z0=-thickness/2;z1=thickness/2
    for tr in tris:
        coords=list(tr.exterior.coords)[:3]
        cen=tr.representative_point()
        if not poly.covers(cen):continue
        a,b,c=[vid(p,z1) for p in coords]
        faces.append([a,b,c])
        a,b,c=[vid(p,z0) for p in coords]
        faces.append([c,b,a])
    rings=[poly.exterior,*poly.interiors]
    for ring in rings:
        cc=list(ring.coords)
        for p,q in zip(cc[:-1],cc[1:]):
            a=vid(p,z0);b=vid(q,z0);c=vid(q,z1);d=vid(p,z1)
            faces += [[a,b,c],[a,c,d]]
    m=trimesh.Trimesh(vertices=np.array(verts),faces=np.array(faces),process=False)
    m.merge_vertices(digits_vertex=9);m.remove_unreferenced_vertices();m.fix_normals(multibody=True)
    T=align_z_to(normal);m.apply_transform(T);m.apply_translation(center);m.metadata["name"]=name
    return apply_mat(m,material)

def add(scene,m,name):
    scene.add_geometry(m,node_name=name,geom_name=name)

def build_faucet(level):
    vals={"MASTER":(72,18),"LOD0":(56,14),"LOD1":(40,10),"LOD2":(24,8),"LOD3":(14,6)}
    sec,small=vals[level]
    s=trimesh.Scene()
    y=.55
    add(s,cylinder_between([0,y,0],[0,y,.008],.038,sec,"WallEscutcheon","ChromeBrass"),"WallEscutcheon")
    add(s,cylinder_between([0,y,.006],[0,y,.046],.0165,sec,"InletNeck","ChromeBrass"),"InletNeck")
    add(s,cylinder_between([0,y,.032],[0,y,.092],.023,sec,"ValveBody","ChromeBrass"),"ValveBody")
    add(s,cylinder_between([0,y,.066],[0,y,.083],.027,6,"ValveHexNut","ChromeBrass"),"ValveHexNut")
    add(s,cylinder_between([0,y,.076],[0,y+.041,.076],.014,sec,"Bonnet","ChromeBrass"),"Bonnet")
    add(s,cylinder_between([0,y+.038,.076],[0,y+.066,.076],.0045,small,"Spindle","WarmBrass"),"Spindle")
    add(s,cylinder_between([0,y+.061,.076],[0,y+.067,.076],.012,sec,"HandleHub","ChromeBrass"),"HandleHub")
    for k,(a,b) in enumerate([([-.034,y+.065,.076],[.034,y+.065,.076]),([0,y+.065,.042],[0,y+.065,.110])]):
        add(s,cylinder_between(a,b,.0042,small,f"CrossHandleArm{k}","ChromeBrass"),f"CrossHandleArm{k}")
    for k,p in enumerate([[-.034,y+.065,.076],[.034,y+.065,.076],[0,y+.065,.042],[0,y+.065,.110]]):
        m=trimesh.creation.icosphere(subdivisions=2 if level in ("MASTER","LOD0") else 1,radius=.006)
        m.apply_translation(p);m.metadata["name"]=f"HandleEnd{k}";apply_mat(m,"ChromeBrass");add(s,m,f"HandleEnd{k}")
    m=trimesh.creation.cylinder(radius=.0095,height=.0028,sections=sec)
    m.apply_translation([0,y+.069,.076]);m.metadata["name"]="BlueIndexCap";apply_mat(m,"BlueIndexCap");add(s,m,"BlueIndexCap")
    n={"MASTER":40,"LOD0":32,"LOD1":22,"LOD2":14,"LOD3":9}[level]
    t=np.linspace(0,1,n)
    p=[]
    for u in t:
        z=.082 + .075*u
        yy=y-.010 - .040*(u*u)
        p.append([0,yy,z])
    add(s,capsule_tube(p,.011,small,"CurvedSpout","ChromeBrass"),"CurvedSpout")
    tip=np.array(p[-1])
    add(s,cylinder_between(tip,tip+[0,-.028,0],.0105,sec,"OutletStem","ChromeBrass"),"OutletStem")
    base=tip+[0,-.029,0]
    add(s,cylinder_between(base,base+[0,-.021,0],.00825,sec,"HoseNippleCore","WarmBrass"),"HoseNippleCore")
    for i,r in enumerate([.0102,.0108,.0102]):
        a=base+[0,-.004-i*.006,0];b=a+[0,-.0035,0]
        add(s,cylinder_between(a,b,r,sec,f"HoseNippleRidge{i}","WarmBrass"),f"HoseNippleRidge{i}")
    return s

def build_hose(level):
    vals={"MASTER":(120,18,120),"LOD0":(84,14,84),"LOD1":(56,10,56),"LOD2":(32,8,32),"LOD3":(18,6,18)}
    maj,minr,pathn=vals[level]
    s=trimesh.Scene()
    for i in range(5):
        add(s,torus_component([.0,.011+i*.0005,.0],.205-i*.012,.010,maj,minr,f"HoseLoop{i}","GreenPVC",axis="y",ellipticity=.88+0.015*i),f"HoseLoop{i}")
    n=pathn
    t=np.linspace(0,1,n)
    p=np.column_stack([.17+.30*t, .012+.028*np.sin(math.pi*t), -.04-.09*t+.025*np.sin(2*math.pi*t)])
    add(s,capsule_tube(p,.010,minr,"HoseTail","GreenPVC"),"HoseTail")
    end=p[-1]
    add(s,cylinder_between(end,end+[0,0,-.035],.014,maj//4 if maj>=32 else 12,"QuickConnectorBody","GrayPP"),"QuickConnectorBody")
    add(s,cylinder_between(end+[0,0,-.006],end+[0,0,-.012],.0158,maj//4 if maj>=32 else 12,"QuickConnectorCollar","BlackTPE"),"QuickConnectorCollar")
    root=end+[0,0,-.035]
    add(s,cylinder_between(root,root+[0,.0,-.120],.0185,maj//3 if maj>=32 else 12,"NozzleBarrel","GrayPP"),"NozzleBarrel")
    add(s,cylinder_between(root+[0,0,-.103],root+[0,0,-.145],.027,maj//3 if maj>=32 else 12,"SelectorHead","DarkPP"),"SelectorHead")
    holes=[(0,0)]
    for rr,cnt in [(.007,6),(.014,10),(.020,16)]:
        for k in range(cnt):
            a=TAU*k/cnt+(0.12 if cnt==10 else 0)
            holes.append((rr*math.cos(a),rr*math.sin(a)))
    face_center=root+[0,0,-.1455]
    add(s,annulus_disk_holes(.0258,.0010,holes,.0009,max(48,maj),face_center,[0,0,-1],"ShowerFace_33RealHoles","Stainless"),"ShowerFace_33RealHoles")
    grip_a=root+[0,-.002,-.045];grip_b=grip_a+[0,-.115,.045]
    add(s,cylinder_between(grip_a,grip_b,.018,maj//3 if maj>=32 else 12,"NozzleGrip","DarkPP"),"NozzleGrip")
    for j,u in enumerate(np.linspace(.18,.78,4)):
        c=grip_a*(1-u)+grip_b*u
        d=(grip_b-grip_a); d=d/np.linalg.norm(d)
        add(s,cylinder_between(c-d*.004,c+d*.004,.0192,maj//4 if maj>=32 else 10,f"GripBand{j}","BlackTPE"),f"GripBand{j}")
    T=trimesh.transformations.rotation_matrix(math.radians(-20),[1,0,0])
    trig=box_component([.010,.060,.008],root+[0,-.035,-.060],"Trigger","BlueIndexCap",T)
    add(s,trig,"Trigger")
    return s

def verify(scene):
    out=[]
    for name,g in scene.geometry.items():
        assert np.isfinite(g.vertices).all()
        assert np.isfinite(g.vertex_normals).all()
        assert (g.area_faces>1e-14).all(), name
        h=g.copy()
        h.merge_vertices(digits_vertex=8,merge_norm=True,merge_tex=True)
        h.remove_unreferenced_vertices()
        assert h.is_watertight, (name,"not watertight")
        assert h.is_winding_consistent, (name,"winding")
        assert abs(h.volume)>1e-12, (name,"volume")
        cnt=np.unique(h.edges_sorted,axis=0,return_counts=True)[1]
        assert np.all(cnt==2),(name,"manifold")
        out.append({"component":name,"vertices":int(len(g.vertices)),"triangles":int(len(g.faces)),"volumeM3":float(abs(h.volume))})
    return {"triangles":sum(x["triangles"] for x in out),"components":out,"bounds":scene.bounds.tolist()}

def export_scene(scene,outdir,stem):
    outdir.mkdir(parents=True,exist_ok=True)
    glb=outdir/(stem+".glb")
    glb.write_bytes(scene.export(file_type="glb"))
    reload_glb=trimesh.load(glb,force="scene",process=False)
    tri=sum(len(g.faces) for g in reload_glb.geometry.values())
    obj_text,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True)
    obj=outdir/(stem+".obj"); obj.write_text(obj_text)
    for fn,data in files.items():
        p=outdir/fn
        if isinstance(data,str):p.write_text(data)
        else:p.write_bytes(data)
    reload_obj=trimesh.load(obj,force="scene",process=False)
    return {"glbTriangles":tri,"objTriangles":sum(len(g.faces) for g in reload_obj.geometry.values()),"sha256":hashlib.sha256(glb.read_bytes()).hexdigest()}

def translate_scene(scene,delta):
    for g in scene.geometry.values(): g.apply_translation(delta)

def build_review():
    f=build_faucet("LOD0"); h=build_hose("LOD0")
    translate_scene(f,[0,0,0])
    translate_scene(h,[.22,.005,.52])
    s=trimesh.Scene()
    for prefix,sc in [("Faucet",f),("Hose",h)]:
        for name,g in sc.geometry.items():
            add(s,g.copy(),prefix+"_"+name)
    wall=box_component([1.1,1.0,.025],[0,.45,-.02],"DiagnosticWall","GrayPP")
    floor=box_component([1.1,.025,.9],[.2,-.012,.38],"DiagnosticFloor","GrayPP")
    add(s,wall,"DiagnosticWall");add(s,floor,"DiagnosticFloor")
    return s

def run(out):
    out=Path(out);out.mkdir(parents=True,exist_ok=True)
    levels=["MASTER","LOD0","LOD1","LOD2","LOD3"]
    assets=[]
    for aid,builder in [("BalconyFaucetG13",build_faucet),("GardenHoseCoilNozzle",build_hose)]:
        lev=[]
        for L in levels:
            sc=builder(L)
            rep=verify(sc); ex=export_scene(sc,out/(aid+"_"+L),aid+"_"+L)
            assert rep["triangles"]==ex["glbTriangles"]==ex["objTriangles"]
            lev.append({"level":L,**rep,**ex})
            print(aid,L,rep["triangles"])
        counts=[x["triangles"] for x in lev]
        assert all(a>b for a,b in zip(counts,counts[1:])),counts
        assets.append({"asset":aid,"levels":lev})
    review=build_review()
    review_rep=verify(review)
    review_ex=export_scene(review,out/"FaucetHoseReview_LOD0","FaucetHoseReview_LOD0")
    assert review_rep["triangles"]==review_ex["glbTriangles"]==review_ex["objTriangles"]
    report={
      "status":"EXTERNAL_GEOMETRY_CHECKED_NOT_VISUAL_PASS",
      "assets":assets,
      "review":{"id":"FaucetHoseReview_LOD0",**review_rep,**review_ex},
      "unity":{"compile":False,"import":False,"render":False,"temporal":False,"performance":False},
      "visualFidelity":{"score":None,"pass":False,"pointsAwarded":0},
      "implementationReadiness":{"lastRecorded":93,"recomputed":False},
    }
    (out/"geometry_verification.json").write_text(json.dumps(report,indent=2))
    return report

if __name__=="__main__":
    ap=argparse.ArgumentParser();ap.add_argument("--output",required=True)
    args=ap.parse_args();run(args.output)
