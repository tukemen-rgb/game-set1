"""Reference-v2 blue soap net accessory; metres, Y-up, root at host inlet-neck axis.
User photo informs construction only. No photo pixels or watermark removal.
Requires numpy, scipy, trimesh, Pillow. No network calls or benchmark-scene mutation.
"""
from __future__ import annotations
import argparse, hashlib, json, math
from pathlib import Path
import numpy as np
from scipy.interpolate import PchipInterpolator
import trimesh
from PIL import Image

TAU=math.tau
LEVELS={'MASTER':(112,10,56),'LOD0':(84,8,40),'LOD1':(56,6,28),'LOD2':(36,5,20),'LOD3':(24,4,14)}
Y0,Y1=-.226,-.111
SOAP_C=np.array([-.003,-.188,.014])
SOAP_HALF=np.array([.028,.031,.013])
EXP=.72
MOUNT=np.array([0.,.55,.024])
STRAND_R=.00034
MATERIALS={
 'BlueNylon':{'color':[.020,.38,.60],'roughness':.58,'metallic':0},
 'BlueBoundCord':{'color':[.018,.31,.48],'roughness':.64,'metallic':0},
 'UsedIvorySoap':{'color':[.82,.80,.70],'roughness':.70,'metallic':0},
 'DarkStainlessWire':{'color':[.34,.35,.36],'roughness':.42,'metallic':1},
}

def unit(v):
    v=np.asarray(v,float);n=np.linalg.norm(v)
    if n<1e-12:raise ValueError('zero direction')
    return v/n

def finalize(v,f):
    m=trimesh.Trimesh(vertices=np.asarray(v,float),faces=np.asarray(f,int),process=False);m.merge_vertices(digits_vertex=10);m.update_faces(m.nondegenerate_faces(height=1e-12));m.remove_unreferenced_vertices();m.fix_normals(multibody=True);return m

def sweep(path,radius,sides,closed=False):
    pts=np.asarray(path,float)
    if closed:tang=np.roll(pts,-1,axis=0)-np.roll(pts,1,axis=0)
    else:tang=np.vstack([pts[1]-pts[0],pts[2:]-pts[:-2],pts[-1]-pts[-2]])
    tang/=np.linalg.norm(tang,axis=1)[:,None];frames=[];prev=None
    for t in tang:
        n=np.cross(t,[0,1,0]) if prev is None else prev-t*np.dot(prev,t)
        if np.linalg.norm(n)<1e-8:n=np.cross(t,[1,0,0])
        n=unit(n);frames.append([n,np.cross(t,n)]);prev=n
    frames=np.asarray(frames);ang=np.arange(sides)*TAU/sides
    rings=pts[:,None,:]+radius*(frames[:,0,None,:]*np.cos(ang)[None,:,None]+frames[:,1,None,:]*np.sin(ang)[None,:,None])
    v=list(rings.reshape(-1,3));f=[]
    for i in range(len(pts) if closed else len(pts)-1):
        k=(i+1)%len(pts)
        for j in range(sides):
            z=(j+1)%sides;a=i*sides+j;b=i*sides+z;c=k*sides+z;d=k*sides+j;f += [[a,b,c],[a,c,d]]
    if not closed:
        for ring,rev in [(0,True),(len(pts)-1,False)]:
            c=len(v);v.append(pts[ring])
            for j in range(sides):
                a=ring*sides+j;b=ring*sides+(j+1)%sides;f.append([c,b,a] if rev else [c,a,b])
    return finalize(v,f)

def superellipsoid(n):
    v=[];f=[];rings=max(6,n//2)
    def pw(a):return np.sign(a)*np.abs(a)**EXP
    for phi in np.linspace(-math.pi/2,math.pi/2,rings+1)[1:-1]:
        for th in np.arange(n)*TAU/n:
            # Slightly consumed upper corners, still symmetric enough not to fake a logo/product.
            p=SOAP_C+SOAP_HALF*np.array([pw(math.cos(phi))*pw(math.cos(th)),pw(math.sin(phi)),pw(math.cos(phi))*pw(math.sin(th))]);v.append(p)
    nr=rings-1
    for i in range(nr-1):
        for j in range(n):
            k=(j+1)%n;a=i*n+j;b=i*n+k;c=(i+1)*n+k;d=(i+1)*n+j;f += [[a,b,c],[a,c,d]]
    lo=len(v);v.append(SOAP_C+[0,-SOAP_HALF[1],0]);hi=len(v);v.append(SOAP_C+[0,SOAP_HALF[1],0])
    for j in range(n):
        k=(j+1)%n;f += [[lo,k,j],[hi,(nr-1)*n+j,(nr-1)*n+k]]
    return finalize(v,f)

_y=np.array([Y0,-.220,-.207,-.188,-.160,-.134,Y1])
_ax=PchipInterpolator(_y,[.004,.014,.027,.030,.033,.038,.041])
_az=PchipInterpolator(_y,[.003,.009,.014,.016,.019,.023,.026])

def envelope(theta,y):
    theta=np.asarray(theta);y=np.asarray(y);ca=np.cos(theta);sa=np.sin(theta)
    radial=1/np.sqrt((ca/_ax(y))**2+(sa/_az(y))**2)
    expanded=SOAP_HALF+.0012;dy=np.abs((y-SOAP_C[1])/expanded[1]);p=2/EXP;sf=np.maximum(0,1-np.minimum(dy,1)**p)**(1/p)
    soaprad=sf/((np.abs(ca)/expanded[0])**p+(np.abs(sa)/expanded[2])**p)**(1/p)
    return np.maximum(radial,np.where(dy<1,soaprad+.00135,0))

def bag_point(theta,t,phase=0.,offset=0.):
    t=np.asarray(t);theta=np.asarray(theta);y=Y0+(Y1-Y0)*t
    # Reference-v2: cell paths carry restrained slack instead of perfect analytic helices.
    theta2=theta+.030*np.sin(TAU*t+phase)+.012*np.sin(3*theta+phase)*(t**1.7)
    r=envelope(theta2,y)+offset
    r*=1+.025*np.sin(2*theta+phase)*(t**1.5)+.010*np.sin(5*theta-phase)
    # Mouth is irregular and sags between side attachment points; bottom is slightly twisted.
    y=y-.0038*(np.sin(theta)**2)*(t**5)-.0014*np.sin(3*theta+phase)*(t**3)+.001*np.sin(theta+phase)*(1-t)**2
    return np.stack([SOAP_C[0]+r*np.cos(theta2),y,SOAP_C[2]+r*np.sin(theta2)],axis=-1)

_TEXTURE={}
def assign(scene,name,m,mat):
    s=MATERIALS[mat]
    if mat not in _TEXTURE:
        yy,xx=np.mgrid[:128,:128];u=xx/128;v=yy/128;h=.55*np.sin(TAU*(19*u+5*v))+.45*np.sin(TAU*(31*v-7*u))
        base=np.clip(np.array(s['color'])[None,None,:]*(1+.010*h[:,:,None]),0,1);rough=np.clip(s['roughness']+.023*h,.05,1);mr=np.stack([np.ones_like(h),rough,np.ones_like(h)*s['metallic']],axis=-1)
        _TEXTURE[mat]=(Image.fromarray(np.uint8(base*255)),Image.fromarray(np.uint8(mr*255)))
    base,mr=_TEXTURE[mat];pbr=trimesh.visual.material.PBRMaterial(name=mat,baseColorTexture=base,metallicRoughnessTexture=mr,metallicFactor=1,roughnessFactor=1,doubleSided=False)
    uv=np.column_stack([m.vertices[:,0]+.35*m.vertices[:,2],m.vertices[:,1]])/.04;m.visual=trimesh.visual.TextureVisuals(uv=uv,material=pbr);scene.add_geometry(m,node_name=name,geom_name=name)

def build(level):
    samples,sides,detail=LEVELS[level];scene=trimesh.Scene();assign(scene,'RoundedUsedSoap',superellipsoid(detail*2),'UsedIvorySoap')
    count=14;turn=TAU*9.2/count;ts=np.linspace(0,1,samples)
    for family in [-1,1]:
        for i in range(count):
            phase=0.47*i+family*.33;theta=TAU*i/count+family*turn*ts
            # Alternating crossing offset creates an over/under reading without opaque net surfaces.
            delta=family*.00038*np.cos(count*turn*ts+.6*i)
            assign(scene,f'NetYarn_{family}_{i:02d}',sweep(bag_point(theta,ts,phase,delta),STRAND_R,sides),'BlueNylon')
    angles=np.arange(max(40,detail*2))*TAU/max(40,detail*2)
    rim=bag_point(angles,np.ones_like(angles),.3);assign(scene,'SoftBoundMouth',sweep(rim,.00105,sides,True),'BlueBoundCord')
    heel=bag_point(angles,np.full_like(angles,.015),.7);assign(scene,'GatheredHeel',sweep(heel,.0009,sides,True),'BlueBoundCord')
    # Thin hanger wire encircles the known 33 mm inlet neck, then drops to an arched wire handle over the mouth.
    neck_r=.0179;loop_ang=np.linspace(0,TAU,max(48,detail*2),endpoint=False);loop=np.column_stack([neck_r*np.cos(loop_ang),neck_r*np.sin(loop_ang),np.zeros_like(loop_ang)])
    assign(scene,'NeckWireLoop',sweep(loop,.00072,max(6,sides),True),'DarkStainlessWire')
    apex=np.array([0.,-.070,.010]);start=np.array([0.,-neck_r,.0]);t=np.linspace(0,1,max(18,detail))
    c1=start+[.005,-.020,.003];c2=apex+[.006,.016,.002];u=t[:,None];drop=(1-u)**3*start+3*(1-u)**2*u*c1+3*(1-u)*u**2*c2+u**3*apex
    assign(scene,'WireDrop',sweep(drop,.00072,max(6,sides)),'DarkStainlessWire')
    left=bag_point(np.array([0.]),np.array([1.]),.3)[0];right=bag_point(np.array([math.pi]),np.array([1.]),1.1)[0]
    # Two half arches meet at apex; deliberate overlap at apex and bound mouth is installation contact, not floating geometry.
    for j,end in enumerate([left,right]):
        t=np.linspace(0,1,max(14,detail//2));mid=(apex+end)/2+np.array([0,.012,.004 if j==0 else -.004]);u=t[:,None]
        path=(1-u)**2*apex+2*(1-u)*u*mid+u**2*end;assign(scene,f'WireMouthHandle_{j}',sweep(path,.00068,max(6,sides)),'DarkStainlessWire')
        tie=end+np.column_stack([.0016*np.cos(angles),.0016*np.sin(angles),np.zeros_like(angles)]);assign(scene,f'MouthTie_{j}',sweep(tie,.00058,max(5,sides),True),'BlueBoundCord')
    return scene

def check(scene):
    rows=[]
    for name,g in scene.geometry.items():
        assert np.isfinite(g.vertices).all() and np.isfinite(g.vertex_normals).all(),name;assert np.all(g.area_faces>1e-14),name
        h=g.copy();h.merge_vertices(digits_vertex=9,merge_tex=True,merge_norm=True);h.remove_unreferenced_vertices();assert h.is_watertight and h.is_winding_consistent and h.volume>0,name
        _,cnt=np.unique(h.edges_sorted,axis=0,return_counts=True);assert np.all(cnt==2),name;rows.append({'part':name,'triangles':len(g.faces),'boundaryEdges':0,'volumeM3':float(h.volume)})
    yarn=np.vstack([np.vstack([g.vertices,g.triangles_center]) for n,g in scene.geometry.items() if n.startswith('NetYarn')]);implicit=np.sum(np.abs((yarn-SOAP_C)/SOAP_HALF)**(2/EXP),axis=1)-1;assert implicit.min()>0,'net enters soap'
    return {'triangles':sum(x['triangles'] for x in rows),'parts':rows,'boundsMetres':scene.bounds.tolist(),'minimumSoapImplicitClearance':float(implicit.min())}

def compact(scene):
    out=trimesh.Scene();groups={}
    for g in scene.geometry.values():groups.setdefault(g.visual.material.name,[]).append(g)
    for name,parts in groups.items():
        vv=[];ff=[];nn=[];uv=[];off=0
        for g in parts:vv.append(g.vertices);ff.append(g.faces+off);nn.append(g.vertex_normals);uv.append(g.visual.uv);off+=len(g.vertices)
        m=trimesh.Trimesh(vertices=np.vstack(vv),faces=np.vstack(ff),vertex_normals=np.vstack(nn),process=False);m.visual=trimesh.visual.TextureVisuals(uv=np.vstack(uv),material=parts[0].visual.material);out.add_geometry(m,node_name=name,geom_name=name)
    return out

def run(out,faucet=None):
    out.mkdir(parents=True,exist_ok=True);levels=[]
    for level in LEVELS:
        source=build(level);stat=check(source);scene=compact(source);stem='HangingBlueSoapNet_'+level;glb=out/(stem+'.glb');glb.write_bytes(scene.export(file_type='glb'))
        rd=trimesh.load(glb,force='scene',process=False);assert sum(len(g.faces) for g in rd.geometry.values())==stat['triangles'];assert np.allclose(rd.bounds,scene.bounds,atol=1e-6)
        folder=out/stem;folder.mkdir(exist_ok=True);obj,files=trimesh.exchange.obj.export_obj(scene,includ_normals=True,include_texture=True,return_texure=True);(folder/(stem+'.obj')).write_text(obj)
        for fn,data in files.items():p=folder/fn;p.write_text(data) if isinstance(data,str) else p.write_bytes(data)
        od=trimesh.load(folder/(stem+'.obj'),force='scene',process=False);assert sum(len(g.faces) for g in od.geometry.values())==stat['triangles']
        levels.append({'level':level,'sha256':hashlib.sha256(glb.read_bytes()).hexdigest(),'glbRoundtrip':True,'objTriangleRoundtrip':True,'runtimeMaterialMeshCount':len(scene.geometry),**stat});print(level,stat['triangles'])
    assert all(a['triangles']>b['triangles'] for a,b in zip(levels,levels[1:]))
    if faucet:
        combined=trimesh.load(faucet,force='scene',process=False);soap=compact(build('LOD0'))
        for name,g in soap.geometry.items():g=g.copy();g.apply_translation(MOUNT);combined.add_geometry(g,node_name='SoapNet_'+name,geom_name='SoapNet_'+name)
        p=out/'FaucetWithHangingSoapNetReferenceV2_LOD0.glb';p.write_bytes(combined.export(file_type='glb'))
    report={'status':'EXTERNAL_GEOMETRY_TESTS_ONLY','variant':'reference_v2_soft_mesh_and_wire_hanger','levels':levels,'materials':MATERIALS,'units':'metres','mountTranslation':MOUNT.tolist(),'unityCompile':False,'unityImport':False,'unityRender':False,'visualFidelityScore':None,'visualPass':False,'lodTemporalVerified':False,'fullYarnIntersectionVerified':False,'deformationSimulationVerified':False,'sourceSHA256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()};(out/'geometry_verification.json').write_text(json.dumps(report,indent=2)+'\n');return report

if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);ap.add_argument('--faucet',type=Path);args=ap.parse_args();run(args.output,args.faucet)
