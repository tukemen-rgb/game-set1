#!/usr/bin/env python3
"""Generate generic unbranded balcony laundry hardware reference meshes.

Outputs OBJ + GLB for MASTER/LOD0/1/2/3 of BalconyLaundryArm450,
LaundryPole2560 and a two-arm reference assembly. This is offline authoring
and provenance tooling, not Unity render evidence. Requires numpy, trimesh,
shapely. Units are metres, Y up.
"""
from __future__ import annotations
import argparse, json, math
from pathlib import Path
import numpy as np
import trimesh
from shapely.geometry import Polygon, Point, box as sbox
from shapely.ops import triangulate

LEVELS=[("MASTER",96),("LOD0",64),("LOD1",40),("LOD2",24),("LOD3",12)]
MATERIALS={
 "galvanized_steel":dict(base=[0.56,0.59,0.61,1],metallic=1.0,roughness=0.48),
 "stainless_steel":dict(base=[0.62,0.64,0.65,1],metallic=1.0,roughness=0.32),
 "gray_epdm":dict(base=[0.08,0.085,0.09,1],metallic=0.0,roughness=0.72),
}

def unit(v):
    v=np.asarray(v,float); n=np.linalg.norm(v)
    if n<1e-12: raise ValueError("zero vector")
    return v/n

def rot(axis,angle):
    x,y,z=unit(axis); c=math.cos(angle); s=math.sin(angle); C=1-c
    return np.array([[x*x*C+c,x*y*C-z*s,x*z*C+y*s,0],
                     [y*x*C+z*s,y*y*C+c,y*z*C-x*s,0],
                     [z*x*C-y*s,z*y*C+x*s,z*z*C+c,0],[0,0,0,1]],float)

def rounded_rect(x0,y0,x1,y1,r,res):
    return sbox(x0+r,y0+r,x1-r,y1-r).buffer(r,resolution=res,join_style=1)

def extrude(poly,thickness):
    vertices=[]; faces=[]; z0=-thickness/2; z1=thickness/2
    for t in [q for q in triangulate(poly) if poly.covers(q.representative_point())]:
        pts=list(t.exterior.coords)[:3]
        a=len(vertices); vertices += [[x,y,z1] for x,y in pts]; faces.append([a,a+1,a+2])
        a=len(vertices); vertices += [[x,y,z0] for x,y in pts[::-1]]; faces.append([a,a+1,a+2])
    for ring in [poly.exterior]+list(poly.interiors):
        pts=list(ring.coords)
        for i in range(len(pts)-1):
            x0,y0=pts[i]; x1,y1=pts[i+1]; a=len(vertices)
            vertices += [[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]]
            faces += [[a,a+1,a+2],[a,a+2,a+3]]
    mesh=trimesh.Trimesh(np.asarray(vertices),np.asarray(faces),process=True)
    trimesh.repair.fix_normals(mesh,multibody=True)
    return mesh

def material(name):
    s=MATERIALS[name]
    return trimesh.visual.material.PBRMaterial(name=name,baseColorFactor=s["base"],metallicFactor=s["metallic"],roughnessFactor=s["roughness"])

def tag(mesh,name): mesh.visual.material=material(name); return mesh

def bracket(sections):
    res=max(2,sections//16)
    outer=rounded_rect(.010,-.027,.460,.027,.018,res)
    holes=[Point(x,0).buffer(.019,resolution=max(8,sections//4)) for x in (.180,.310,.425)]
    arm=tag(extrude(Polygon(outer.exterior.coords,holes=[h.exterior.coords for h in holes]),.008),"galvanized_steel")
    base=extrude(rounded_rect(-.035,-.105,.035,.105,.012,res),.006)
    base.apply_transform(np.array([[0,0,1,.003],[0,1,0,0],[1,0,0,0],[0,0,0,1]],float))
    comps=[("arm_plate",arm,"galvanized_steel"),("wall_base",tag(base,"galvanized_steel"),"galvanized_steel")]
    gus=tag(extrude(Polygon([(.008,-.030),(.150,-.030),(.008,-.095)]),.006),"galvanized_steel")
    comps.append(("lower_gusset",gus,"galvanized_steel"))
    boss=tag(trimesh.creation.cylinder(.014,.074,sections=sections),"galvanized_steel"); boss.apply_translation([.035,0,0])
    pin=tag(trimesh.creation.cylinder(.006,.080,sections=sections),"stainless_steel"); pin.apply_translation([.035,0,0])
    comps += [("hinge_boss",boss,"galvanized_steel"),("pivot_pin",pin,"stainless_steel")]
    for z in (-.041,.041):
        w=tag(trimesh.creation.cylinder(.011,.0025,sections=sections),"stainless_steel"); w.apply_translation([.035,0,z])
        comps.append((f"pivot_washer_{z:+}",w,"stainless_steel"))
    for y in (-.070,.070):
        for z in (-.021,.021):
            w=trimesh.creation.cylinder(.008,.0015,sections=sections); w.apply_transform(rot([0,1,0],math.pi/2)); w.apply_translation([.0075,y,z])
            h=trimesh.creation.cylinder(.0065,.005,sections=6); h.apply_transform(rot([0,1,0],math.pi/2)); h.apply_translation([.011,y,z])
            comps += [(f"mount_washer_{y}_{z}",tag(w,"stainless_steel"),"stainless_steel"),(f"mount_hex_{y}_{z}",tag(h,"stainless_steel"),"stainless_steel")]
    return comps

def pole(sections):
    tube=tag(trimesh.creation.annulus(r_min=.0148,r_max=.0160,height=2.56,sections=sections),"stainless_steel")
    comps=[("hollow_tube",tube,"stainless_steel")]
    for sign in (-1,1):
        body=tag(trimesh.creation.cylinder(.01465,.020,sections=sections),"gray_epdm"); body.apply_translation([0,0,sign*(1.28-.006)])
        collar=tag(trimesh.creation.cylinder(.0172,.004,sections=sections),"gray_epdm"); collar.apply_translation([0,0,sign*1.282])
        cap=tag(trimesh.creation.uv_sphere(.0172,count=[max(8,sections//2),max(8,sections//2)]),"gray_epdm"); cap.apply_scale([1,1,.18]); cap.apply_translation([0,0,sign*1.285])
        comps += [(f"end_plug_body_{sign}",body,"gray_epdm"),(f"end_plug_collar_{sign}",collar,"gray_epdm"),(f"end_cap_round_{sign}",cap,"gray_epdm")]
    stop=tag(trimesh.creation.annulus(r_min=.01615,r_max=.0205,height=.010,sections=sections),"stainless_steel"); stop.apply_translation([0,0,1.05])
    screw=trimesh.creation.cylinder(.0045,.015,sections=max(8,sections//2)); screw.apply_transform(rot([0,1,0],math.pi/2)); screw.apply_translation([.022,0,1.05])
    comps += [("rod_stop_collar",stop,"stainless_steel"),("rod_stop_screw",tag(screw,"stainless_steel"),"stainless_steel")]
    return comps

def export_scene(out,name,components):
    scene=trimesh.Scene(); parts=[]
    for cname,mesh,mat in components:
        if not np.isfinite(mesh.vertices).all() or not np.isfinite(mesh.vertex_normals).all(): raise AssertionError(cname+" nonfinite")
        if (mesh.area_faces<1e-12).any(): raise AssertionError(cname+" degenerate")
        if not mesh.is_watertight or not mesh.is_winding_consistent: raise AssertionError(cname+" topology")
        scene.add_geometry(mesh,node_name=cname,geom_name=cname); parts.append(dict(name=cname,triangles=len(mesh.faces),material=mat,watertight=True,winding=True))
    tri=sum(p["triangles"] for p in parts)
    glb=out/(name+".glb"); glb.write_bytes(scene.export(file_type="glb"))
    obj=scene.export(file_type="obj"); (out/(name+".obj")).write_text(obj if isinstance(obj,str) else obj.decode())
    reload=trimesh.load(glb,force="scene")
    assert sum(len(g.faces) for g in reload.geometry.values())==tri
    assert np.allclose(scene.bounds,reload.bounds,atol=1e-6)
    return dict(triangles=tri,boundsMetres=np.asarray(scene.bounds).tolist(),parts=parts,glbRoundtrip=True)

def main():
    ap=argparse.ArgumentParser(); ap.add_argument("--output",type=Path,required=True); args=ap.parse_args(); out=args.output; out.mkdir(parents=True,exist_ok=True)
    report={"assets":{},"referenceAssembly":{}}
    for asset,builder in (("BalconyLaundryArm450",bracket),("LaundryPole2560",pole)):
        report["assets"][asset]={}
        for label,sections in LEVELS: report["assets"][asset][label]=export_scene(out,f"{asset}_{label}",builder(sections))
        counts=[report["assets"][asset][label]["triangles"] for label,_ in LEVELS]; assert all(a>b for a,b in zip(counts,counts[1:])),counts
    for label,sections in LEVELS:
        comps=[]
        for side,z in (("A",-.9),("B",.9)):
            for name,m,mat in bracket(sections): m=m.copy(); m.apply_translation([0,0,z]); comps.append((side+"_"+name,m,mat))
        for name,m,mat in pole(sections): m=m.copy(); m.apply_translation([.425,0,0]); comps.append(("Pole_"+name,m,mat))
        report["referenceAssembly"][label]=export_scene(out,f"BalconyLaundryReferenceAssembly_{label}",comps)
    report["verification"]={"status":"GEOMETRY_EXPORT_CHECKS_PASSED_NOT_VISUAL_FIDELITY_PASS","analyticalPoleHoleRadialClearanceMetres":.003,"unityCompile":False,"unityImport":False,"unityRender":False,"visualFidelityScore":None}
    (out/"geometry_verification.json").write_text(json.dumps(report,indent=2)+"\n")
    print(json.dumps({"assets":{a:{k:v["triangles"] for k,v in d.items()} for a,d in report["assets"].items()},"assembly":{k:v["triangles"] for k,v in report["referenceAssembly"].items()}},indent=2))
if __name__=="__main__": main()
