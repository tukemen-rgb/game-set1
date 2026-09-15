"""Original blue soap-net accessory; metres, Y-up, local inlet-axis origin.
Photo informs construction only. No photo pixels, watermark removal or engine-quality claim.
Dependencies: numpy, scipy, trimesh, Pillow. No network calls or scene mutation.
"""
from __future__ import annotations
import argparse, hashlib, json, math
from pathlib import Path
import numpy as np
from scipy.interpolate import PchipInterpolator
import trimesh
from PIL import Image

TAU = math.tau
LEVELS = {'MASTER':(128,10,48),'LOD0':(96,8,36),'LOD1':(64,6,28),'LOD2':(40,5,20),'LOD3':(28,4,12)}
Y0, Y1 = -.253, -.114
SOAP_C = np.array([-.006,-.211,.020])
SOAP_HALF = np.array([.026,.038,.014])
EXP = .65
MOUNT = np.array([0.,.55,.024])
STRAND_R = .00030
MATERIALS = {
 'BlueNylon':{'color':[.025,.42,.62],'roughness':.54,'metallic':0},
 'BlueBoundCord':{'color':[.025,.34,.50],'roughness':.62,'metallic':0},
 'UsedIvorySoap':{'color':[.83,.81,.72],'roughness':.68,'metallic':0},
 'DarkStainlessWire':{'color':[.36,.38,.39],'roughness':.39,'metallic':1},
}

def unit(v):
    v=np.asarray(v,float); n=np.linalg.norm(v)
    if n < 1e-12: raise ValueError('Zero-length direction')
    return v/n

def finalize(vertices,faces):
    mesh=trimesh.Trimesh(vertices=np.asarray(vertices),faces=np.asarray(faces),process=False)
    mesh.merge_vertices(digits_vertex=10)
    mesh.update_faces(mesh.nondegenerate_faces(height=1e-12))
    mesh.remove_unreferenced_vertices(); mesh.fix_normals(multibody=True)
    return mesh

def sweep(path,radius,sides,closed=False):
    pts=np.asarray(path,float)
    if closed:
        tang=np.roll(pts,-1,axis=0)-np.roll(pts,1,axis=0)
    else:
        tang=np.vstack([pts[1]-pts[0],pts[2:]-pts[:-2],pts[-1]-pts[-2]])
    tang/=np.linalg.norm(tang,axis=1)[:,None]
    frames=[];previous=None
    for t in tang:
        n=np.cross(t,[0,1,0]) if previous is None else previous-t*np.dot(previous,t)
        if np.linalg.norm(n)<1e-8:n=np.cross(t,[1,0,0])
        n=unit(n);frames.append([n,np.cross(t,n)]);previous=n
    frames=np.asarray(frames);ang=np.arange(sides)*TAU/sides
    v=pts[:,None,:]+radius*(frames[:,0,None,:]*np.cos(ang)[None,:,None]+frames[:,1,None,:]*np.sin(ang)[None,:,None])
    vertices=list(v.reshape(-1,3));faces=[]
    for i in range(len(pts) if closed else len(pts)-1):
        k=(i+1)%len(pts)
        for j in range(sides):
            z=(j+1)%sides;a=i*sides+j;b=i*sides+z;c=k*sides+z;d=k*sides+j
            faces.extend([[a,b,c],[a,c,d]])
    if not closed:
        for ring,reverse in [(0,True),(len(pts)-1,False)]:
            c=len(vertices);vertices.append(pts[ring])
            for j in range(sides):
                a=ring*sides+j;b=ring*sides+(j+1)%sides
                faces.append([c,b,a] if reverse else [c,a,b])
    return finalize(vertices,faces)

def superellipsoid(n):
    verts=[];faces=[]
    def power(a):return np.sign(a)*np.abs(a)**EXP
    # Non-polar rings, then unique pole vertices to avoid zero-area fans.
    rings=max(6,n//2)
    for phi in np.linspace(-math.pi/2,math.pi/2,rings+1)[1:-1]:
        for theta in np.arange(n)*TAU/n:
            verts.append(SOAP_C+SOAP_HALF*np.array([power(math.cos(phi))*power(math.cos(theta)),power(math.sin(phi)),power(math.cos(phi))*power(math.sin(theta))]))
    nr=rings-1
    for i in range(nr-1):
        for j in range(n):
            k=(j+1)%n;a=i*n+j;b=i*n+k;c=(i+1)*n+k;d=(i+1)*n+j
            faces.extend([[a,b,c],[a,c,d]])
    low=len(verts);verts.append(SOAP_C+[0,-SOAP_HALF[1],0])
    high=len(verts);verts.append(SOAP_C+[0,SOAP_HALF[1],0])
    for j in range(n):
        k=(j+1)%n;faces.extend([[low,k,j],[high,(nr-1)*n+j,(nr-1)*n+k]])
    return finalize(verts,faces)

_y=np.array([Y0,-.247,-.232,-.210,-.173,-.143,Y1])
_ax=PchipInterpolator(_y,[.003,.013,.026,.028,.032,.037,.043])
_az=PchipInterpolator(_y,[.002,.008,.014,.016,.019,.022,.026])

def envelope(theta,y):
    theta=np.asarray(theta);y=np.asarray(y)
    ca,sa=np.cos(theta),np.sin(theta)
    radial=1/np.sqrt((ca/_ax(y))**2+(sa/_az(y))**2)
    expanded=SOAP_HALF+.0010
    dy=np.abs((y-SOAP_C[1])/expanded[1]);p=2/EXP
    valid=dy<1
    sf=np.maximum(0,1-np.minimum(dy,1)**p)**(1/p)
    soaprad=sf/((np.abs(ca)/expanded[0])**p+(np.abs(sa)/expanded[2])**p)**(1/p)
    radial=np.maximum(radial,np.where(valid,soaprad+.00115,0))
    return radial

def bag_point(theta,t,offset=0.):
    t=np.asarray(t);theta=np.asarray(theta);y=Y0+(Y1-Y0)*t
    r=envelope(theta,y)+offset
    # Upper rim sags between suspension points; damping preserves lower soap fit.
    y=y-.0025*np.sin(theta)**2*t**5
    return np.stack([SOAP_C[0]+r*np.cos(theta),y,SOAP_C[2]+r*np.sin(theta)],axis=-1)

_TEXTURES={}
def assign(scene,name,mesh,mat):
    s=MATERIALS[mat]
    if mat not in _TEXTURES:
        yy,xx=np.mgrid[:128,:128];u=xx/128;v=yy/128
        h=.55*np.sin(TAU*(17*u+3*v))+.45*np.sin(TAU*(29*v-7*u))
        base=np.clip(np.array(s['color'])[None,None,:]*(1+.008*h[:,:,None]),0,1)
        mr=np.stack([np.ones_like(h),np.clip(s['roughness']+.018*h,.04,1),np.full_like(h,s['metallic'])],axis=-1)
        _TEXTURES[mat]=(Image.fromarray(np.uint8(base*255)),Image.fromarray(np.uint8(mr*255)))
    base,mr=_TEXTURES[mat]
    pbr=trimesh.visual.material.PBRMaterial(name=mat,baseColorTexture=base,metallicRoughnessTexture=mr,metallicFactor=1,roughnessFactor=1,doubleSided=False)
    uv=np.column_stack([mesh.vertices[:,0]+.4*mesh.vertices[:,2],mesh.vertices[:,1]])/.04
    mesh.visual=trimesh.visual.TextureVisuals(uv=uv,material=pbr)
    scene.add_geometry(mesh,node_name=name,geom_name=name)

def build(level):
    samples,sides,detail=LEVELS[level];scene=trimesh.Scene()
    assign(scene,'RoundedSoapBar',superellipsoid(detail*2),'UsedIvorySoap')
    # 16 yarns per diagonal family; weave alternates over/under at nominal crossings.
    count=16;drift=TAU*12/count
    ts=np.linspace(0,1,samples)
    for family in [-1,1]:
        for i in range(count):
            theta=TAU*i/count+family*drift*ts
            delta=family*.00033*np.cos(count*drift*ts)
            assign(scene,f'NetYarn_{family}_{i:02d}',sweep(bag_point(theta,ts,delta),STRAND_R,sides),'BlueNylon')
    angles=np.arange(max(32,detail*2))*TAU/max(32,detail*2)
    assign(scene,'ReinforcedOpenMouth',sweep(bag_point(angles,np.ones_like(angles)),.0010,sides,True),'BlueBoundCord')
    # Gathered heel and binding; not an opaque closed bag replacing the net.
    bottom=bag_point(angles,np.full_like(angles,.012))
    assign(scene,'GatheredBottomBinding',sweep(bottom,.00085,sides,True),'BlueBoundCord')
    # Smooth dark steel hook over existing 33 mm inlet; underside contacts its crown.
    angle=np.sort(np.unique(np.r_[np.linspace(math.radians(-38),math.radians(215),max(24,detail*2)),math.pi/2]))
    radius=.0178;cy=.0165+.0008-radius
    arc=np.column_stack([radius*np.cos(angle),cy+radius*np.sin(angle),np.zeros_like(angle)])
    end=arc[-1];target=np.array([0.,-.066,.009])
    t=np.linspace(0,1,max(10,detail//2))[1:]
    c1=end+[.006,-.026,0];c2=target+[-.012,.012,0]
    tail=(1-t[:,None])**3*end+3*(1-t[:,None])**2*t[:,None]*c1+3*(1-t[:,None])*t[:,None]**2*c2+t[:,None]**3*target
    # Closed eye is a separate welded wire part; joining overlap is documented.
    assign(scene,'NeckHookAndDrop',sweep(np.vstack([arc,tail]),.0008,max(6,sides)),'DarkStainlessWire')
    eye=np.column_stack([.004*np.cos(angles),-.070+.004*np.sin(angles),np.full_like(angles,.009)])
    assign(scene,'SuspensionEye',sweep(eye,.0008,max(6,sides),True),'DarkStainlessWire')
    # Cord bears in lower inner eye and terminates around mouth bindings.
    apex=np.array([0.,-.07245,.009])
    for side,theta in enumerate([0.,math.pi]):
        foot=bag_point(theta,1.)
        t=np.linspace(0,1,max(10,detail//2))
        path=foot*(1-t[:,None])+apex*t[:,None]
        path[:,2]+=.001*np.sin(math.pi*t)
        assign(scene,f'SuspensionCord_{side}',sweep(path,.00075,max(5,sides)),'BlueBoundCord')
        tie=foot+np.column_stack([.0015*np.cos(angles),.0015*np.sin(angles),np.zeros_like(angles)])
        assign(scene,f'MouthTie_{side}',sweep(tie,.00055,max(5,sides),True),'BlueBoundCord')
    return scene

def check(scene):
    stats=[]
    for name,g in scene.geometry.items():
        assert np.isfinite(g.vertices).all() and np.isfinite(g.vertex_normals).all(),name
        assert np.all(g.area_faces>1e-14),name
        welded=g.copy();welded.merge_vertices(digits_vertex=9,merge_tex=True,merge_norm=True)
        assert welded.is_watertight and welded.is_winding_consistent and welded.volume>0,name
        _,counts=np.unique(welded.edges_sorted,axis=0,return_counts=True)
        assert np.all(counts==2),name
        stats.append({'part':name,'triangles':len(g.faces),'boundaryEdges':0,'materialVolumeM3':float(welded.volume)})
    # Analytic soap-surface containment; negative means strand geometry enters soap.
    yarn=np.vstack([np.vstack([g.vertices,g.triangles_center]) for n,g in scene.geometry.items() if n.startswith('NetYarn')])
    implicit=np.sum(np.abs((yarn-SOAP_C)/SOAP_HALF)**(2/EXP),axis=1)-1
    assert implicit.min()>0,'Net yarn penetrates modeled soap envelope'
    return {'triangles':sum(x['triangles'] for x in stats),'parts':stats,'boundsMetres':scene.bounds.tolist(),'minimumSoapImplicitClearance':float(implicit.min())}

def compact(scene):
    """Keep component reasoning, but export four material meshes instead of one per yarn."""
    out=trimesh.Scene();groups={}
    for g in scene.geometry.values():groups.setdefault(g.visual.material.name,[]).append(g)
    for name,parts in groups.items():
        vertices=[];faces=[];normals=[];uv=[];offset=0
        for g in parts:
            vertices.append(g.vertices);faces.append(g.faces+offset);normals.append(g.vertex_normals);uv.append(g.visual.uv);offset+=len(g.vertices)
        mesh=trimesh.Trimesh(vertices=np.vstack(vertices),faces=np.vstack(faces),vertex_normals=np.vstack(normals),process=False)
        mesh.visual=trimesh.visual.TextureVisuals(uv=np.vstack(uv),material=parts[0].visual.material)
        out.add_geometry(mesh,node_name=name,geom_name=name)
    return out

def run(out,faucet=None):
    out.mkdir(parents=True,exist_ok=True);records=[]
    for level in LEVELS:
        source=build(level);stat=check(source);scene=compact(source);stem='HangingBlueSoapNet_'+level
        path=out/(stem+'.glb');path.write_bytes(scene.export(file_type='glb'))
        rd=trimesh.load(path,force='scene',process=False)
        assert sum(len(g.faces) for g in rd.geometry.values())==stat['triangles']
        assert np.allclose(rd.bounds,scene.bounds,atol=1e-6)
        obj,files=trimesh.exchange.obj.export_obj(scene,include_normals=True,include_texture=True,return_texture=True)
        folder=out/stem;folder.mkdir(exist_ok=True);(folder/(stem+'.obj')).write_text(obj)
        for name,data in files.items():
            p=folder/name;p.write_text(data) if isinstance(data,str) else p.write_bytes(data)
        od=trimesh.load(folder/(stem+'.obj'),force='scene',process=False)
        assert sum(len(g.faces) for g in od.geometry.values())==stat['triangles']
        records.append(dict(level=level,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),glbRoundtrip=True,objTriangleRoundtrip=True,runtimeMaterialMeshCount=len(scene.geometry),**stat))
        print(level,stat['triangles'],flush=True)
    assert all(a['triangles']>b['triangles'] for a,b in zip(records,records[1:]))
    if faucet is not None:
        # Reuse caller-supplied authoritative faucet. No guessed/rebuilt faucet geometry.
        combined=trimesh.load(faucet,force='scene',process=False)
        soap=compact(build('LOD0'))
        for name,g in soap.geometry.items():
            g=g.copy();g.apply_translation(MOUNT);combined.add_geometry(g,node_name='SoapNet_'+name,geom_name='SoapNet_'+name)
        (out/'FaucetWithHangingSoapNetReview_LOD0.glb').write_bytes(combined.export(file_type='glb'))
    report={'status':'EXTERNAL_GEOMETRY_TESTS_ONLY','levels':records,'materials':MATERIALS,'units':'metres','root':'inlet neck axis; review translation [0,0.55,0.024]','unityCompile':False,'unityImport':False,'unityRender':False,'visualFidelityScore':None,'visualPass':False,'lodTemporalVerified':False,'fullThreadSelfIntersectionVerified':False,'deformationSimulationVerified':False,'sourceSHA256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
    (out/'geometry_verification.json').write_text(json.dumps(report,indent=2)+'\n')
    return report

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,required=True);parser.add_argument('--faucet',type=Path)
    args=parser.parse_args();run(args.output,args.faucet)
