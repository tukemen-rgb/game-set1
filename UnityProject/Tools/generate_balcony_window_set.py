from __future__ import annotations
from pathlib import Path
import argparse, json, math, hashlib
import numpy as np
import trimesh
from PIL import Image

LEVELS = {
    "MASTER": {"radial":24, "screen_pitch":0.004},
    "LOD0": {"radial":18, "screen_pitch":0.006},
    "LOD1": {"radial":12, "screen_pitch":0.012},
    "LOD2": {"radial":8, "screen_pitch":0.024},
    "LOD3": {"radial":6, "screen_pitch":0.060},
}
MATERIALS = {
    "AnodizedAluminum":{"base":[0.46,0.47,0.46,1],"metallic":1.0,"roughness":0.34,"normalScale":0.22,"microstructure":"fine longitudinal extrusion brush"},
    "DarkAluminum":{"base":[0.15,0.16,0.16,1],"metallic":1.0,"roughness":0.39,"normalScale":0.16,"microstructure":"aged anodized extrusion"},
    "ClearGlass":{"base":[0.72,0.82,0.86,0.34],"metallic":0.0,"roughness":0.08,"normalScale":0.0,"microstructure":"float glass; transmission awaits Unity"},
    "BlackEPDM":{"base":[0.025,0.026,0.025,1],"metallic":0.0,"roughness":0.72,"normalScale":0.35,"microstructure":"fine rubber grain"},
    "BlackPolyesterMesh":{"base":[0.035,0.038,0.036,1],"metallic":0.0,"roughness":0.83,"normalScale":0.22,"microstructure":"insect screen geometric proxy"},
    "GalvanizedSteel":{"base":[0.52,0.54,0.55,1],"metallic":1.0,"roughness":0.48,"normalScale":0.18,"microstructure":"fine zinc-spangle proxy"},
    "DarkCavity":{"base":[0.012,0.012,0.012,1],"metallic":0.0,"roughness":0.92,"normalScale":0.0,"microstructure":"recessed cavity geometry"},
}

TEXTURES = None
def create_textures(out):
    global TEXTURES
    out.mkdir(parents=True,exist_ok=True)
    rng=np.random.default_rng(20260916)
    TEXTURES={}
    for name,prm in MATERIALS.items():
        n=128
        yy,xx=np.mgrid[:n,:n]
        noise=rng.normal(0,1,(n,n))
        if "Aluminum" in name:
            h=.70*np.sin(xx*2*np.pi/19.0)+.30*noise
            amp=.012
        elif name=="BlackEPDM":
            h=noise; amp=.018
        elif name=="GalvanizedSteel":
            h=(np.sin(xx*2*np.pi/29)+np.sin(yy*2*np.pi/23)+noise)*.33; amp=.015
        else:
            h=noise; amp=.006
        base=np.clip(np.array(prm["base"][:3])[None,None,:]*(1+amp*h[:,:,None]),0,1)
        alpha=np.full((n,n,1),prm["base"][3])
        rgba=np.uint8(np.clip(np.dstack([base,alpha]),0,1)*255)
        mr=np.zeros((n,n,3),dtype=np.uint8)
        mr[:,:,1]=np.uint8(np.clip(prm["roughness"]+amp*h,0,1)*255)
        mr[:,:,2]=np.uint8(np.clip(prm["metallic"],0,1)*255)
        gx=np.gradient(h,axis=1); gy=np.gradient(h,axis=0)
        ns=prm["normalScale"]; normal=np.dstack([-gx*ns,-gy*ns,np.ones_like(h)])
        normal/=np.linalg.norm(normal,axis=2)[:,:,None]
        normal=np.uint8(np.clip(normal*.5+.5,0,1)*255)
        bi=Image.fromarray(rgba,'RGBA'); mi=Image.fromarray(mr,'RGB'); ni=Image.fromarray(normal,'RGB')
        bi.save(out/f"{name}_base.png"); mi.save(out/f"{name}_mr.png"); ni.save(out/f"{name}_normal.png")
        TEXTURES[name]=(bi,mi,ni)
    return TEXTURES

ASSETS=["AluminumSlidingSashWindow1800x1800","MosquitoScreenPanel870x1760","ExteriorSillDripFlashing1800"]

def box(ext, ctr, name, mat):
    m=trimesh.creation.box(extents=np.asarray(ext,float))
    m.apply_translation(np.asarray(ctr,float))
    m.metadata.update(name=name,material=mat)
    return m

def cyl(r,h,ctr,axis,sections,name,mat):
    m=trimesh.creation.cylinder(radius=r,height=h,sections=sections)
    z=np.array([0.,0.,1.]); a=np.asarray(axis,float); a/=np.linalg.norm(a)
    if not np.allclose(a,z):
        m.apply_transform(trimesh.geometry.align_vectors(z,a))
    m.apply_translation(np.asarray(ctr,float))
    m.metadata.update(name=name,material=mat)
    return m

def validate(parts):
    out=[]
    for p in parts:
        assert np.isfinite(p.vertices).all() and np.isfinite(p.vertex_normals).all(),p.metadata["name"]
        assert np.all(p.area_faces>1e-14),p.metadata["name"]
        q=p.copy(); q.merge_vertices(digits_vertex=9)
        assert q.is_watertight,p.metadata["name"]
        assert q.is_winding_consistent,p.metadata["name"]
        assert q.volume>0,p.metadata["name"]
        _,cnt=np.unique(q.edges_sorted,axis=0,return_counts=True)
        assert np.all(cnt==2),p.metadata["name"]
        out.append({"part":p.metadata["name"],"material":p.metadata["material"],"triangles":int(len(p.faces)),"volumeM3":float(q.volume)})
    return out

def compact(parts):
    groups={}
    for p in parts: groups.setdefault(p.metadata["material"],[]).append(p)
    s=trimesh.Scene()
    for mat,arr in groups.items():
        m=trimesh.util.concatenate(arr)
        prm=MATERIALS[mat]
        uv=np.column_stack([m.vertices[:,0]+.35*m.vertices[:,2],m.vertices[:,1]])/0.12
        if TEXTURES is None:
            material=trimesh.visual.material.PBRMaterial(name=mat,baseColorFactor=prm["base"],metallicFactor=prm["metallic"],roughnessFactor=prm["roughness"],doubleSided=(mat=="ClearGlass"))
        else:
            bc,mr,nm=TEXTURES[mat]
            material=trimesh.visual.material.PBRMaterial(name=mat,baseColorTexture=bc,metallicRoughnessTexture=mr,normalTexture=nm,metallicFactor=1.0,roughnessFactor=1.0,doubleSided=(mat=="ClearGlass"))
        m.visual=trimesh.visual.TextureVisuals(uv=uv,material=material)
        s.add_geometry(m,node_name=mat,geom_name=mat)
    return s

def sash_parts(level):
    s=LEVELS[level]; p=[]
    W,H,D=1.800,1.800,0.105
    p += [
        box([W,.055,D],[0,H/2-.0275,0],"OuterHead","AnodizedAluminum"),
        box([W,.070,D],[0,-H/2+.035,0],"OuterSill","AnodizedAluminum"),
        box([.055,H-.125,D],[-W/2+.0275,-.0075,0],"LeftJamb","AnodizedAluminum"),
        box([.055,H-.125,D],[ W/2-.0275,-.0075,0],"RightJamb","AnodizedAluminum"),
    ]
    for x in [-W/2+.064,W/2-.064]:
        p.append(box([.010,H-.16,.009],[x,0,.057],"JambShadowReveal","DarkCavity"))
    for z in [-.025,.025]:
        p.append(box([W-.10,.013,.013],[0,-H/2+.079,z],"LowerTrack","DarkAluminum"))
        p.append(box([W-.10,.012,.010],[0,H/2-.074,z],"UpperTrack","DarkAluminum"))
    sash_w=.895; sash_h=1.665
    centers=[(-.432,0,.016),(.432,0,-.016)]
    for si,(x0,y0,z0) in enumerate(centers):
        rail=.050; stile=.052
        meet_x=x0+sash_w/2-.029 if si==0 else x0-sash_w/2+.029
        p += [
            box([sash_w,rail,.041],[x0,sash_h/2-rail/2,z0],f"Sash{si}_Top","AnodizedAluminum"),
            box([sash_w,.060,.041],[x0,-sash_h/2+.030,z0],f"Sash{si}_Bottom","AnodizedAluminum"),
            box([stile,sash_h-.11,.041],[x0-sash_w/2+stile/2 if si==0 else x0+sash_w/2-stile/2,0,z0],f"Sash{si}_OuterStile","AnodizedAluminum"),
            box([.058,sash_h-.11,.043],[meet_x,0,z0],f"Sash{si}_MeetingStile","AnodizedAluminum"),
            box([sash_w-.112,sash_h-.125,.005],[x0,0,z0],f"Sash{si}_Glass","ClearGlass"),
        ]
        gw=sash_w-.100; gh=sash_h-.115
        p += [
            box([gw,.010,.008],[x0,gh/2-.005,z0+.024],f"Sash{si}_GasketTop","BlackEPDM"),
            box([gw,.010,.008],[x0,-gh/2+.005,z0+.024],f"Sash{si}_GasketBottom","BlackEPDM"),
            box([.010,gh-.020,.008],[x0-gw/2+.005,0,z0+.024],f"Sash{si}_GasketLeft","BlackEPDM"),
            box([.010,gh-.020,.008],[x0+gw/2-.005,0,z0+.024],f"Sash{si}_GasketRight","BlackEPDM"),
        ]
    p += [
        box([.032,.080,.017],[.005,.12,.052],"CrescentLockBody","DarkAluminum"),
        cyl(.014,.011,[.005,.135,.066],[0,0,1],s["radial"],"CrescentLockPivot","GalvanizedSteel"),
        box([.011,.055,.012],[-.008,.158,.069],"CrescentLockLever","DarkAluminum"),
        box([.021,.105,.007],[.430,.020,.058],"PullRecessCavity","DarkCavity"),
    ]
    for x in [-.62,.62]:
        p.append(box([.055,.008,.026],[x,-H/2+.059,.057],"WeepCavity","DarkCavity"))
    # Glazing beads, rolling hardware and weather brush seals.
    for si,(x0,y0,z0) in enumerate(centers):
        gw=sash_w-.100; gh=sash_h-.115
        p += [
            box([gw,.006,.006],[x0,gh/2-.014,z0-.024],f"Sash{si}_BeadTop","AnodizedAluminum"),
            box([gw,.006,.006],[x0,-gh/2+.014,z0-.024],f"Sash{si}_BeadBottom","AnodizedAluminum"),
            box([.006,gh-.028,.006],[x0-gw/2+.014,0,z0-.024],f"Sash{si}_BeadLeft","AnodizedAluminum"),
            box([.006,gh-.028,.006],[x0+gw/2-.014,0,z0-.024],f"Sash{si}_BeadRight","AnodizedAluminum"),
        ]
        for rx in [x0-.30,x0+.30]:
            p.append(cyl(.012,.014,[rx,-sash_h/2+.020,z0],[0,0,1],s["radial"],f"Sash{si}_Roller","DarkAluminum"))
            p.append(cyl(.004,.018,[rx,-sash_h/2+.020,z0],[0,0,1],s["radial"],f"Sash{si}_RollerAxle","GalvanizedSteel"))
    bpitch={"MASTER":.010,"LOD0":.014,"LOD1":.024,"LOD2":.045,"LOD3":.090}[level]
    ys=np.arange(-.70,.70+bpitch/2,bpitch)
    for bx,z in [(-.018,.046),(.018,-.046)]:
        br=[box([.0014,.006,.012],[bx,float(y),z],"MeetingStileBrush","BlackEPDM") for y in ys]
        q=trimesh.util.concatenate(br); q.metadata.update(name="MeetingStileBrushGroup",material="BlackEPDM"); p.append(q)
    # Frame corner fixing caps / exposed fastener heads.
    for x in [-.84,.84]:
        for y in [-.78,.78]:
            p.append(cyl(.0045,.0025,[x,y,.054],[0,0,1],s["radial"],"FrameFixingHead","GalvanizedSteel"))
    return p

def screen_parts(level):
    s=LEVELS[level]; p=[]
    W,H,D=.870,1.760,.020; rail=.030
    p += [
        box([W,rail,D],[0,H/2-rail/2,0],"ScreenTopRail","DarkAluminum"),
        box([W,rail,D],[0,-H/2+rail/2,0],"ScreenBottomRail","DarkAluminum"),
        box([rail,H-2*rail,D],[-W/2+rail/2,0,0],"ScreenLeftRail","DarkAluminum"),
        box([rail,H-2*rail,D],[ W/2-rail/2,0,0],"ScreenRightRail","DarkAluminum"),
    ]
    for x in [-W/2+.036,W/2-.036]:
        p.append(cyl(.0022,H-.078,[x,0,.008],[0,1,0],s["radial"],"ScreenSplineV","BlackEPDM"))
    for y in [-H/2+.036,H/2-.036]:
        p.append(cyl(.0022,W-.078,[0,y,.008],[1,0,0],s["radial"],"ScreenSplineH","BlackEPDM"))
    pitch=s["screen_pitch"]; thick=max(.00045,min(.0012,pitch*.08))
    xs=np.arange(-W/2+.045,W/2-.045+pitch/2,pitch)
    ys=np.arange(-H/2+.045,H/2-.045+pitch/2,pitch)
    hs=[box([W-.09,thick,.00065],[0,float(y),-.0012],"MeshWarp","BlackPolyesterMesh") for y in ys]
    vs=[box([thick,H-.09,.00065],[float(x),0,.0012],"MeshWeft","BlackPolyesterMesh") for x in xs]
    mh=trimesh.util.concatenate(hs); mh.metadata.update(name="MeshWarpGroup",material="BlackPolyesterMesh"); p.append(mh)
    mv=trimesh.util.concatenate(vs); mv.metadata.update(name="MeshWeftGroup",material="BlackPolyesterMesh"); p.append(mv)
    p.append(box([.018,.090,.009],[W/2-.026,-.08,.014],"ScreenFingerPull","DarkAluminum"))
    return p

def flashing_parts(level):
    s=LEVELS[level]; p=[]; W=1.800
    pan=box([W,.002,.130],[0,0,0],"SlopedSillPan","AnodizedAluminum")
    ang=math.atan2(.006,.125)
    pan.apply_transform(trimesh.transformations.rotation_matrix(ang,[1,0,0],point=[0,0,-.065])); p.append(pan)
    p += [
        box([W,.030,.002],[0,.015,-.064],"WallUpstand","AnodizedAluminum"),
        box([W,.018,.002],[0,-.012,.069],"FrontDrop","AnodizedAluminum"),
        box([W,.004,.010],[0,-.023,.073],"HemmedDripEdge","AnodizedAluminum"),
        box([.018,.032,.130],[-W/2+.009,0,-.002],"LeftEndDam","AnodizedAluminum"),
        box([.018,.032,.130],[ W/2-.009,0,-.002],"RightEndDam","AnodizedAluminum"),
    ]
    for x in np.linspace(-.72,.72,5):
        p.append(cyl(.0045,.003,[x,.031,-.056],[0,1,0],s["radial"],"PanFastener","GalvanizedSteel"))
        p.append(cyl(.0070,.0015,[x,.029,-.056],[0,1,0],s["radial"],"IsolationWasher","BlackEPDM"))
    return p

BUILD={"AluminumSlidingSashWindow1800x1800":sash_parts,"MosquitoScreenPanel870x1760":screen_parts,"ExteriorSillDripFlashing1800":flashing_parts}

def export_one(asset,level,parts,out):
    stats=validate(parts); scene=compact(parts); ad=out/asset; ad.mkdir(parents=True,exist_ok=True)
    glb=ad/f"{asset}_{level}.glb"; glb.write_bytes(scene.export(file_type="glb"))
    rd=trimesh.load(glb,force="scene",process=False)
    tri=sum(len(g.faces) for g in rd.geometry.values()); exp=sum(x["triangles"] for x in stats); assert tri==exp
    objd=ad/f"{asset}_{level}_OBJ"; objd.mkdir(exist_ok=True)
    obj,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True)
    (objd/f"{asset}_{level}.obj").write_text(obj)
    for k,v in files.items():
        q=objd/k; q.write_text(v) if isinstance(v,str) else q.write_bytes(v)
    od=trimesh.load(objd/f"{asset}_{level}.obj",force="scene",process=False)
    assert sum(len(g.faces) for g in od.geometry.values())==exp
    return {"asset":asset,"level":level,"triangles":exp,"logicalParts":len(parts),"runtimeMaterialMeshes":len(scene.geometry),"boundsMetres":scene.bounds.tolist(),"glbRoundtrip":True,"objTriangleRoundtrip":True,"componentValidation":stats,"glbSHA256":hashlib.sha256(glb.read_bytes()).hexdigest()}

def build_review(out):
    scene=trimesh.Scene()
    for prefix,parts,offset in [
        ("Window",sash_parts("LOD0"),[0,0,0]),
        ("Screen",screen_parts("LOD0"),[.445,0,.067]),
        ("Flashing",flashing_parts("LOD0"),[0,-.916,.040]),
    ]:
        s=compact(parts)
        for name,g in s.geometry.items():
            q=g.copy(); q.apply_translation(offset)
            scene.add_geometry(q,node_name=f"{prefix}_{name}",geom_name=f"{prefix}_{name}")
    for p in [
        box([2.2,.20,.16],[0,.99,-.075],"ContextLintel","DarkCavity"),
        box([.20,1.8,.16],[-1.0,0,-.075],"ContextWallL","DarkCavity"),
        box([.20,1.8,.16],[1.0,0,-.075],"ContextWallR","DarkCavity")
    ]:
        scene.add_geometry(p,node_name="DIAGNOSTIC_"+p.metadata["name"],geom_name="DIAGNOSTIC_"+p.metadata["name"])
    path=out/"BalconyWindowInstalledReview_LOD0.glb"; path.write_bytes(scene.export(file_type="glb"))
    return {"path":path.name,"triangles":sum(len(g.faces) for g in scene.geometry.values()),"geometryCount":len(scene.geometry),"formalBenchmarkSceneChanged":False,"unityVerified":False}

def run(out):
    out.mkdir(parents=True,exist_ok=True); create_textures(out/"textures"); records=[]
    for asset in ASSETS:
        tris=[]
        for level in LEVELS:
            r=export_one(asset,level,BUILD[asset](level),out); records.append(r); tris.append(r["triangles"]); print(asset,level,r["triangles"],flush=True)
        assert all(a>b for a,b in zip(tris,tris[1:])),(asset,tris)
    review=build_review(out)
    report={
        "status":"EXTERNAL_GEOMETRY_VERIFIED_NO_UNITY_RUNTIME","assets":ASSETS,"records":records,"review":review,"materials":MATERIALS,
        "visualFidelity":{"score":None,"pass":False,"pointsAwarded":0,"reason":"No actual Unity 3840x2160 pixels."},
        "implementationReadiness":{"lastRecorded":93,"recomputed":False},
        "unityCompile":False,"unityImport":False,"unityRender":False,"lodTemporalVerified":False,
        "assumptions":[
            "Nominal 1800x1800 two-track aluminum sliding window; not asserted as a specific historical Japanese SKU.",
            "Nominal 870x1760 insect screen. Geometric mesh pitch is deliberately coarser than real insect mesh to remain tractable and is a lookdev proxy.",
            "Nominal 1800 exterior sill flashing with 6 mm fall over 125 mm projection.",
            "Glass transmission/refraction is not verified outside Unity; exported material is only a physically plausible starting point."
        ]
    }
    (out/"geometry_verification.json").write_text(json.dumps(report,indent=2)+"\n")
    metadata={
        "schema":1,"date":"2026-09-16","units":"metres",
        "manufactureInstallation":{
            "window":"Extruded aluminum outer frame, twin tracks, two sliding sash leaves, recessed float glass, EPDM glazing gaskets, meeting-stile crescent lock, pull recess and sill weep cavities.",
            "screen":"Four aluminum rails, EPDM spline, crossed polyester screen geometry and finger pull. Screen is exterior and removable.",
            "flashing":"Thin formed aluminum sill pan with positive outward fall, rear upstand, front drop, hemmed drip edge, end dams, mechanical fasteners and EPDM isolation washers.",
            "interfaces":"Sliding leaves overlap at meeting stiles; glass is recessed behind gasket geometry; screen remains outside sash plane; flashing sits below frame and projects outward.",
            "orientationExposure":"Exterior is +Z; drainage is downward/outward. Japanese midsummer sun/sky is intended for future Unity lookdev, not baked.",
            "aging":"No arbitrary dirt or rust. Future weathering should follow sill corners, weep paths, track water paths, gasket compression and lower exterior aluminum.",
            "geometryVsMaterial":"Frame profiles, rails, gaskets, lock, drainage cavities, screen strands and flashing are geometry. Fine brushing, rubber grain and zinc microstructure remain material definitions."
        },
        "materials":MATERIALS,
        "visualFidelity":{"authority":"Assets/QA/visual_fidelity_gate.json","score":None,"pass":False,"pointsAwarded":0},
        "nextProductionTarget":"Window-wall perimeter sealant/backer rod and causal sill weathering; integrate with AC/service-corner only after owner review. If Unity pixels appear, score/fix them first."
    }
    (out/"BalconyWindowSet.metadata.json").write_text(json.dumps(metadata,indent=2)+"\n")
    return report

if __name__=="__main__":
    ap=argparse.ArgumentParser();ap.add_argument("--output",type=Path,required=True);args=ap.parse_args();run(args.output)
