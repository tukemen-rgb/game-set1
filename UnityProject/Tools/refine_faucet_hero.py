from __future__ import annotations
import argparse, importlib.util, json, math, hashlib
from pathlib import Path
import numpy as np
import trimesh
from PIL import Image

ROOT = Path(__file__).resolve().parent
OWNER = ROOT / 'generate_faucet_hose.py'
LEVELS = ['MASTER','LOD0','LOD1','LOD2','LOD3']

def load_owner(path=OWNER):
    spec=importlib.util.spec_from_file_location('faucet_owner',path)
    mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod);return mod

MATS={
 'ChromeBodyAged':dict(base=(0.47,0.49,0.50),metallic=1.0,rough=.34,normal=.14,kind='aged chrome body; broad water-side roughness, no painted highlight'),
 'ChromeSpout':dict(base=(0.67,0.69,0.70),metallic=1.0,rough=.22,normal=.10,kind='polished curved chrome spout with fine wipe scratches'),
 'ChromeLever':dict(base=(0.72,0.74,0.75),metallic=1.0,rough=.18,normal=.08,kind='frequently handled polished lever'),
 'WarmBrass':dict(base=(0.57,0.39,0.14),metallic=1.0,rough=.31,normal=.09,kind='exposed brass at packing/fastener reveal'),
 'BlueIndexCap':dict(base=(0.02,0.23,0.55),metallic=0.0,rough=.43,normal=.06,kind='blue thermoset/plastic index cap'),
 'BlackEPDM':dict(base=(0.015,0.017,0.018),metallic=0.0,rough=.81,normal=.09,kind='compressed rubber gasket/packing'),
}

def texture_set(name,size=256):
    s=MATS[name];yy,xx=np.mgrid[:size,:size];u=xx/size;v=yy/size
    rng=np.random.default_rng(2669+sum(map(ord,name)));noise=rng.normal(0,1,(size,size))
    if s['metallic']:
        scratch=np.sin(2*math.pi*(37*v+.55*np.sin(2*math.pi*2*u)))+.35*np.sin(2*math.pi*83*v)
        low=np.sin(2*math.pi*(2.3*u+1.7*v))
        h=.50*noise+.34*scratch+.16*low
    else:
        h=.68*noise+.32*np.sin(2*math.pi*(29*u+19*v))
    h=(h-h.min())/(h.max()-h.min()+1e-9)*2-1
    # Albedo variation is deliberately tiny. Weathering is primarily roughness, not fake lighting.
    base=np.clip(np.array(s['base'])[None,None,:]*(1+.007*h[...,None]),0,1)
    rough=np.clip(s['rough']+.050*h,0.04,.98)
    mr=np.zeros((size,size,3),dtype=np.uint8);mr[...,1]=np.uint8(rough*255);mr[...,2]=np.uint8(s['metallic']*255)
    gy,gx=np.gradient(h);nx=-gx*s['normal'];ny=-gy*s['normal'];nz=np.ones_like(nx)
    nrm=np.stack([nx,ny,nz],axis=-1);nrm/=np.linalg.norm(nrm,axis=-1,keepdims=True)
    return Image.fromarray(np.uint8(base*255)),Image.fromarray(mr),Image.fromarray(np.uint8(np.clip(nrm*.5+.5,0,1)*255))

def uv_metric(mesh):
    v=np.asarray(mesh.vertices);ext=np.ptp(v,axis=0);major=int(np.argmax(ext));others=[i for i in range(3) if i!=major]
    c=(v.min(axis=0)+v.max(axis=0))/2;a=v[:,others[0]]-c[others[0]];b=v[:,others[1]]-c[others[1]]
    u=(np.arctan2(b,a)/math.tau+.5)%1.;vv=(v[:,major]-v[:,major].min())/max(ext[major],1e-9)
    return np.column_stack([u*3.0,vv*4.0])

def apply_pbr(mesh,name):
    base,mr,nrm=texture_set(name);mat=trimesh.visual.material.PBRMaterial(name=name,baseColorFactor=[1,1,1,1],metallicFactor=1,roughnessFactor=1,baseColorTexture=base,metallicRoughnessTexture=mr,normalTexture=nrm,doubleSided=False)
    mesh.visual=trimesh.visual.TextureVisuals(uv=uv_metric(mesh),material=mat);return mesh

def finalize(v,f):
    m=trimesh.Trimesh(vertices=np.asarray(v,float),faces=np.asarray(f,int),process=False);m.merge_vertices(digits_vertex=10);m.update_faces(m.nondegenerate_faces(height=1e-12));m.remove_unreferenced_vertices();m.fix_normals(multibody=True);return m

def loft_z(sections,n):
    # sections: z, center_y, radius_x, radius_y. x center is zero.
    v=[];f=[]
    for z,cy,rx,ry in sections:
        for i in range(n):
            a=math.tau*i/n;v.append([rx*math.cos(a),cy+ry*math.sin(a),z])
    for k in range(len(sections)-1):
        for i in range(n):
            j=(i+1)%n;a=k*n+i;b=k*n+j;c=(k+1)*n+j;d=(k+1)*n+i;f += [[a,b,c],[a,c,d]]
    for ring,rev in [(0,True),(len(sections)-1,False)]:
        center=len(v);z,cy,_,_=sections[ring];v.append([0,cy,z])
        for i in range(n):
            j=(i+1)%n;a=ring*n+i;b=ring*n+j;f.append([center,b,a] if rev else [center,a,b])
    return finalize(v,f)

def gear_prism_z(z0,z1,cy,r_inner,r_outer,teeth):
    n=teeth*2;v=[];f=[]
    for z in [z0,z1]:
        for i in range(n):
            a=math.tau*i/n;r=r_outer if i%2==0 else r_inner;v.append([r*math.cos(a),cy+r*math.sin(a),z])
    for i in range(n):
        j=(i+1)%n;f += [[i,j,n+j],[i,n+j,n+i]]
    c0=len(v);v.append([0,cy,z0]);c1=len(v);v.append([0,cy,z1])
    for i in range(n):
        j=(i+1)%n;f += [[c0,j,i],[c1,n+i,n+j]]
    return finalize(v,f)

def annular_pipe_y(center_x,center_z,y0,y1,ro,ri,n):
    v=[];f=[]
    # outer rings then inner rings
    for y,r in [(y0,ro),(y1,ro),(y0,ri),(y1,ri)]:
        for i in range(n):
            a=math.tau*i/n;v.append([center_x+r*math.cos(a),y,center_z+r*math.sin(a)])
    O0=0;O1=n;I0=2*n;I1=3*n
    for i in range(n):
        j=(i+1)%n
        f += [[O0+i,O0+j,O1+j],[O0+i,O1+j,O1+i]]
        f += [[I0+i,I1+j,I0+j],[I0+i,I1+i,I1+j]]
        f += [[O0+i,I0+i,I0+j],[O0+i,I0+j,O0+j]]
        f += [[O1+i,O1+j,I1+j],[O1+i,I1+j,I1+i]]
    return finalize(v,f)

def refine(level):
    o=load_owner();scene=o.build_faucet(level)
    sec={'MASTER':88,'LOD0':64,'LOD1':44,'LOD2':28,'LOD3':18}[level]
    small={'MASTER':20,'LOD0':16,'LOD1':12,'LOD2':8,'LOD3':6}[level]
    y=.55
    # Preserve the base owner's coordinate/dimension contract but replace the full visible silhouette inside this existing refiner.
    # This avoids creating a second faucet system while letting the reference photo drive the camera-near form.
    for name in list(scene.geometry.keys()): scene.delete_geometry(name)
    esc=o.cylinder_between([0,y,0],[0,y,.006],.0315,sec,'PhotoRefWallEscutcheon','ChromeBrass');apply_pbr(esc,'ChromeBodyAged');scene.add_geometry(esc,node_name='PhotoRefWallEscutcheon',geom_name='PhotoRefWallEscutcheon')
    neck=o.cylinder_between([0,y,.0045],[0,y,.047],.0165,sec,'PhotoRefInletNeck','ChromeBrass');apply_pbr(neck,'ChromeBodyAged');scene.add_geometry(neck,node_name='PhotoRefInletNeck',geom_name='PhotoRefInletNeck')
    # Photo-reference silhouette: rounded cast body rather than the former cylindrical body + cross handle.
    body=loft_z([
        (.028,y,.0165,.0160),(.044,y-.001,.0205,.0190),(.060,y-.002,.0255,.0225),(.079,y-.001,.0275,.0240),(.096,y-.002,.0230,.0200),(.107,y-.004,.0170,.0155)
    ],sec);apply_pbr(body,'ChromeBodyAged');scene.add_geometry(body,node_name='PhotoRefRoundedValveBody',geom_name='PhotoRefRoundedValveBody')
    # Wall and neck sealing details.
    wall=o.torus_component([0,y,.0075],.0352,.00125,sec,max(6,small//2),'EscutcheonEPDMGasket','BlueIndexCap',axis='z');apply_pbr(wall,'BlackEPDM');scene.add_geometry(wall,node_name='EscutcheonEPDMGasket',geom_name='EscutcheonEPDMGasket')
    neckband=o.torus_component([0,y,.043],.0170,.00115,sec,max(6,small//2),'InletNeckWeatherBand','ChromeBrass',axis='z');apply_pbr(neckband,'ChromeBodyAged');scene.add_geometry(neckband,node_name='InletNeckWeatherBand',geom_name='InletNeckWeatherBand')
    # Upper bonnet and compact single lever seen in the supplied reference.
    bonnet=o.cylinder_between([0,y+.011,.073],[0,y+.045,.073],.0158,sec,'BonnetCylinder','ChromeBrass');apply_pbr(bonnet,'ChromeBodyAged');scene.add_geometry(bonnet,node_name='BonnetCylinder',geom_name='BonnetCylinder')
    bonnet_hex=o.cylinder_between([0,y+.036,.073],[0,y+.052,.073],.0205,6,'BonnetHex','ChromeBrass');apply_pbr(bonnet_hex,'ChromeBodyAged');scene.add_geometry(bonnet_hex,node_name='BonnetHex',geom_name='BonnetHex')
    spindle=o.cylinder_between([0,y+.050,.073],[0,y+.066,.073],.0052,small,'Spindle','WarmBrass');apply_pbr(spindle,'WarmBrass');scene.add_geometry(spindle,node_name='Spindle',geom_name='Spindle')
    hub=o.cylinder_between([0,y+.059,.073],[0,y+.069,.073],.0122,sec,'LeverHub','ChromeBrass');apply_pbr(hub,'ChromeLever');scene.add_geometry(hub,node_name='LeverHub',geom_name='LeverHub')
    # Lever sweeps left and slightly forward/down like the photo, not a symmetric cross.
    path=np.array([[.004,y+.067,.073],[-.010,y+.070,.075],[-.029,y+.070,.078],[-.047,y+.067,.080],[-.058,y+.063,.081]])
    lever=o.capsule_tube(path,.0074,small,'SingleLeverHandle','ChromeBrass');apply_pbr(lever,'ChromeLever');scene.add_geometry(lever,node_name='SingleLeverHandle',geom_name='SingleLeverHandle')
    tip=trimesh.creation.icosphere(subdivisions=2 if level in ('MASTER','LOD0') else 1,radius=.0086);tip.apply_translation(path[-1]);apply_pbr(tip,'ChromeLever');scene.add_geometry(tip,node_name='LeverPalmEnd',geom_name='LeverPalmEnd')
    cap=o.cylinder_between([0,y+.069,.073],[0,y+.072,.073],.0096,sec,'BlueIndexCap','BlueIndexCap');apply_pbr(cap,'BlueIndexCap');scene.add_geometry(cap,node_name='BlueIndexCap',geom_name='BlueIndexCap')
    # Front union: actual toothed/knurled silhouette before the curved outlet.
    union=gear_prism_z(.099,.113,y-.005,.0156,.0168,26 if level in ('MASTER','LOD0') else max(12,sec//2));apply_pbr(union,'ChromeBodyAged');scene.add_geometry(union,node_name='KnurledSpoutUnion',geom_name='KnurledSpoutUnion')
    packing=o.torus_component([0,y-.005,.114],.0107,.00105,sec,max(6,small//2),'SpoutPackingReveal','WarmBrass',axis='z');apply_pbr(packing,'BlackEPDM');scene.add_geometry(packing,¹¹½‘•}¹…µ”ôMÁ½ÕÑA…­¥¹I•Ù•…°œ±•½µ}¹…µ”ôMÁ½ÕÑA…­¥¹I•Ù•…°œ¤(€€€€ŒM¡½ÉĞ¡½½­•ÍÁ½ÕĞèÍÑ…ÉÑÌ¡½É¥é½¹Ñ…°°Ñ¡•¸‰•¹‘Ì‘½İ¸°±½Í•ÈÑ¼Ñ¡”ÍÕÁÁ±¥•Á¡½Ñ¼ÁÉ½Á½ÉÑ¥½¹Ì¸(€€€¸õì5MQHœèÔÈ°1=ÀœèĞÀ°1=ÄœèÈà°1=ÈœèÄà°1=ÌœèÄÉõm±•Ù•±tíĞõ¹À¹±¥¹ÍÁ…” À°Ä±¸¤(€€€ÀÀõ¹À¹…ÉÉ…ä¡lÀ±ä´¸ÀÀÔ°¸ÄÄÅt¤íÀÄõ¹À¹…ÉÉ…ä¡lÀ±ä´¸ÀÀØ°¸ÄĞÕt¤íÀÈõ¹À¹…ÉÉ…ä¡lÀ±ä´¸ÀÌÀ°¸ÄÜÑt¤íÀÌõ¹À¹…ÉÉ…ä¡lÀ±ä´¸ÀØÄ°¸ÄÜÙt¤(€€€ÔõÑlè±9½¹•tíÁ…Ñ ô ÄµÔ¤¨¨Ì©ÀÀ¬Ì¨ ÄµÔ¤¨¨È©Ô©ÀÄ¬Ì¨ ÄµÔ¤©Ô¨¨È©ÀÈ­Ô¨¨Ì©ÀÌ(€€€ÍÁ½ÕĞõ¼¹…ÁÍÕ±•}ÑÕ‰”¡Á…Ñ °¸ÀÄÀĞ±Íµ…±°°A¡½Ñ½I•™ÕÉÙ•‘MÁ½ÕĞœ°¡É½µ•	É…ÍÌœ¤í…ÁÁ±å}Á‰È¡ÍÁ½ÕĞ°¡É½µ•MÁ½ÕĞœ¤íÍ•¹”¹…‘‘}•½µ•ÑÉä¡ÍÁ½ÕĞ±¹½‘•}¹…µ”ôA¡½Ñ½I•™ÕÉÙ•‘MÁ½ÕĞœ±•½µ}¹…µ”ôA¡½Ñ½I•™ÕÉÙ•‘MÁ½ÕĞœ¤(€€€½ÕÑ±•Ğõ…¹¹Õ±…É}Á¥Á•}ä À°¸ÄÜØ±ä´¸ÀØÜ±ä´¸ÀàÌ°¸ÀÄÄÈ°¸ÀÀÜà±Í•Œ¤í…ÁÁ±å}Á‰È¡½ÕÑ±•Ğ°¡É½µ•MÁ½ÕĞœ¤íÍ•¹”¹…‘‘}•½µ•ÑÉä¡½ÕÑ±•Ğ±¹½‘•}¹…µ”ô=Á•¹=ÕÑ±•ÑM±••Ù”œ±•½µ}¹…µ”ô=Á•¹=ÕÑ±•ÑM±••Ù”œ¤(€€€±¥Àõ¼¹Ñ½ÉÕÍ}½µÁ½¹•¹Ğ¡lÀ±ä´¸ÀàÌ°¸ÄÜÙt°¸ÀÄÀØ°¸ÀÀÄÈÔ±Í•Œ±µ…à Ø±Íµ…±°¼¼È¤°=ÕÑ±•Ñ1¥Àœ°¡É½µ•	É…ÍÌœ±…á¥Ìôäœ¤í…ÁÁ±å}Á‰È¡±¥À°¡É½µ•MÁ½ÕĞœ¤íÍ•¹”¹…‘‘}•½µ•ÑÉä¡±¥À±¹½‘•}¹…µ”ô=ÕÑ±•Ñ1¥Àœ±•½µ}¹…µ”ô=ÕÑ±•Ñ1¥Àœ¤(€€€É•ÑÕÉ¸Í•¹”()‘•˜Ù•É¥™ä¡Í•¹”¤è(€€€É½İÌõmtí¹½Éµ…°ôÀ(€€€™½È¹…µ”±œ¥¸Í•¹”¹•½µ•ÑÉä¹¥Ñ•µÌ ¤è(€€€€€€€…ÍÍ•ÉĞ¹À¹¥Í™¥¹¥Ñ”¡œ¹Ù•ÉÑ¥•Ì¤¹…±° ¤…¹¹À¹¥Í™¥¹¥Ñ”¡œ¹Ù•ÉÑ•á}¹½Éµ…±Ì¤¹…±° ¤±¹…µ”(€€€€€€€…ÍÍ•ÉĞ¹À¹…±°¡œ¹…É•…}™…•ÌøÅ”´ÄĞ¤±¹…µ”(€€€€€€€ õœ¹½Áä ¤í ¹µ•É•}Ù•ÉÑ¥•Ì¡‘¥¥ÑÍ}Ù•ÉÑ•àôà±µ•É•}Ñ•àõQÉÕ”±µ•É•}¹½É´õQÉÕ”¤í ¹É•µ½Ù•}Õ¹É•™•É•¹•‘}Ù•ÉÑ¥•Ì ¤(€€€€€€€…ÍÍ•ÉĞ ¹¥Í}İ…Ñ•ÉÑ¥¡Ğ…¹ ¹¥Í}İ¥¹‘¥¹}½¹Í¥ÍÑ•¹Ğ…¹ ¹Ù½±Õµ”øÀ°¡¹…µ”± ¹¥Í}İ…Ñ•ÉÑ¥¡Ğ± ¹Ù½±Õµ”¤(€€€€€€€ÕØõ•Ñ…ÑÑÈ¡œ¹Ù¥ÍÕ…°°ÕØœ±9½¹”¤í…ÍÍ•ÉĞÕØ¥Ì¹½Ğ9½¹”…¹±•¸¡ÕØ¤ôõ±•¸¡œ¹Ù•ÉÑ¥•Ì¤…¹¹À¹¥Í™¥¹¥Ñ”¡ÕØ¤¹…±° ¤±¹…µ”(€€€€€€€µ…Ğõ•Ñ…ÑÑÈ¡œ¹Ù¥ÍÕ…°°µ…Ñ•É¥…°œ±9½¹”¤í¡…Í¹½É´õ•Ñ…ÑÑÈ¡µ…Ğ°¹½Éµ…±Q•áÑÕÉ”œ±9½¹”¤¥Ì¹½Ğ9½¹”í¹½Éµ…°¬õ¥¹Ğ¡¡…Í¹½É´¤(€€€€€€€É½İÌ¹…ÁÁ•¹¡ì½µÁ½¹•¹Ğœé¹…µ”°ÑÉ¥…¹±•Ìœé¥¹Ğ¡±•¸¡œ¹™…•Ì¤¤°İ…Ñ•ÉÑ¥¡ĞœéQÉÕ”°İ¥¹‘¥¹œœéQÉÕ”°ÕØœéQÉÕ”°¹½Éµ…±Q•áÑÕÉ”œé¡…Í¹½Éµô¤(€€€É•ÑÕÉ¸ìÑÉ¥…¹±•ÌœéÍÕ´¡álÑÉ¥…¹±•Ìt™½Èà¥¸É½İÌ¤°½µÁ½¹•¹ÑÌœé±•¸¡É½İÌ¤°¹½Éµ…±5…ÁÁ•‘½µÁ½¹•¹ÑÌœé¹½Éµ…°°É½İÌœéÉ½İÌ°‰½Õ¹‘Í5•ÑÉ•ÌœéÍ•¹”¹‰½Õ¹‘Ì¹Ñ½±¥ÍĞ ¥ô()‘•˜•áÁ½ÉĞ¡½ÕĞ¤è(€€€½ÕĞ¹µ­‘¥È¡Á…É•¹ÑÌõQÉÕ”±•á¥ÍÑ}½¬õQÉÕ”¤í±•Ù•±Ìõmt(€€€™½È±•Ù•°¥¸1Y1Lè(€€€€€€€Í•¹”õÉ•™¥¹”¡±•Ù•°¤íÍÑ…ĞõÙ•É¥™ä¡Í•¹”¤íÍÑ•´õ˜	…±½¹å…Õ•ÑÄÍ!•É½}í±•Ù•±ôœ(€€€€€€€±ˆõ½ÕĞ¼¡ÍÑ•´¬œ¹±ˆœ¤í±ˆ¹İÉ¥Ñ•}‰åÑ•Ì¡Í•¹”¹•áÁ½ÉĞ¡™¥±•}ÑåÁ”ô±ˆœ¤¤(€€€€€€€ÉõÑÉ¥µ•Í ¹±½…¡±ˆ±™½É”ôÍ•¹”œ±ÁÉ½•ÍÌõ…±Í”¤í…ÍÍ•ÉĞÍÕ´¡±•¸¡œ¹™…•Ì¤™½Èœ¥¸É¹•½µ•ÑÉä¹Ù…±Õ•Ì ¤¤ôõÍÑ…ÑlÑÉ¥…¹±•Ìtí…ÍÍ•ÉĞ¹À¹…±±±½Í”¡É¹‰½Õ¹‘Ì±Í•¹”¹‰½Õ¹‘Ì±…Ñ½°ôÅ”´Ø¤(€€€€€€€…ÍÍ•ÉĞÍÕ´¡•Ñ…ÑÑÈ¡•Ñ…ÑÑÈ¡œ¹Ù¥ÍÕ…°°µ…Ñ•É¥…°œ±9½¹”¤°¹½Éµ…±Q•áÑÕÉ”œ±9½¹”¤¥Ì¹½Ğ9½¹”™½Èœ¥¸É¹•½µ•ÑÉä¹Ù…±Õ•Ì ¤¤øõÍÑ…Ñl¹½Éµ…±5…ÁÁ•‘½µÁ½¹•¹ÑÌt´È(€€€€€€€™½±‘•Èõ½ÕĞ½ÍÑ•´í™½±‘•È¹µ­‘¥È¡•á¥ÍÑ}½¬õQÉÕ”¤í½‰¨±™¥±•ÌõÑÉ¥µ•Í ¹•á¡…¹”¹½‰¨¹•áÁ½ÉÑ}½‰¨¡Í•¹”±¥¹±Õ‘}¹½Éµ…±ÌõQÉÕ”±¥¹±Õ‘•}Ñ•áÑÕÉ”õQÉÕ”±É•ÑÕÉ¹}Ñ•áÑÕÉ”õQÉÕ”¤ì¡™½±‘•È¼¡ÍÑ•´¬œ¹½‰¨œ¤¤¹İÉ¥Ñ•}Ñ•áĞ¡½‰¨¤(€€€€€€€™½È™¸°Á…Ñ„¥¸™¥±•Ì¹¥Ñ•µÌ ¤è(€€€€€€€€€€€Àõ™½±‘•È½™¸íÀ¹İÉ¥Ñ•}Ñ•áĞ¡‘…Ñ„¤¥˜¥Í¥¹ÍÑ…¹”¡‘…Ñ„±ÍÑÈ¤•±Í”À¹İÉ¥Ñ•}‰åÑ•Ì¡‘…Ñ„¤(€€€€€€€½õÑÉ¥µ•Í ¹±½…¡™½±‘•È¼¡ÍÑ•´¬œ¹½‰¨œ¤±™½É”ôÍ•¹”œ±ÁÉ½•ÍÌõ…±Í”¤í…ÍÍ•ÉĞÍÕ´¡±•¸¡œ¹™…•Ì¤™½Èœ¥¸½¹•½µ•ÑÉä¹Ù…±Õ•Ì ¤¤ôõÍÑ…ÑlÑÉ¥…¹±•Ìt(€€€€€€€±•Ù•±Ì¹…ÁÁ•¹¡ì±•Ù•°œé±•Ù•°°Í¡„ÈÔØœé¡…Í¡±¥ˆ¹Í¡„ÈÔØ¡±ˆ¹É•…‘}‰åÑ•Ì ¤¤¹¡•á‘¥•ÍĞ ¤°±‰I½Õ¹‘ÑÉ¥ÀœéQÉÕ”°½‰©QÉ¥…¹±•I½Õ¹‘ÑÉ¥ÀœéQÉÕ”°°¨©ÍÑ…Ñô¤íÁÉ¥¹Ğ¡±•Ù•°±ÍÑ…ÑlÑÉ¥…¹±•Ìt±ÍÑ…Ñl½µÁ½¹•¹ÑÌt¤(€€€½Õ¹ÑÌõmálÑÉ¥…¹±•Ìt™½Èà¥¸±•Ù•±Ítí…ÍÍ•ÉĞ…±°¡„ùˆ™½È„±ˆ¥¸é¥À¡½Õ¹ÑÌ±½Õ¹ÑÍlÄét¤¤±½Õ¹ÑÌ(€€€É•ÍÕ±Ğõì…ÍÍ•Ğœè	…±½¹å…Õ•ÑÄÌœ°Ù…É¥…¹ĞœèÁ¡½Ñ½}É•™•É•¹•}¡•É½}É•™¥¹•µ•¹Ñ}É•ÕÍ¥¹}•¹•É…Ñ•}™…Õ•Ñ}¡½Í•}½İ¹•Èœ°±•Ù•±Ìœé±•Ù•±Ì°Ù¥ÍÕ…±¥‘•±¥ÑåM½É”œé9½¹”°Õ¹¥ÑåI•¹‘•Èœé…±Í”°Õ¹¥Ñå%µÁ½ÉĞœé…±Í”°Í½ÕÉ•A¡½Ñ½A¥á•±ÍI•ÕÍ•œé…±Í”°¥µÁ±•µ•¹Ñ…Ñ¥½¹±…¥µÌœè•áÑ•É¹…°•½µ•ÑÉä…¹±Qµ…Ñ•É¥…°¥µÁ±•µ•¹Ñ…Ñ¥½¸½¹±äô(€€€€¡½ÕĞ¼¡•É½}™…Õ•Ñ}Ù•É¥™¥…Ñ¥½¸¹©Í½¸œ¤¹İÉ¥Ñ•}Ñ•áĞ¡©Í½¸¹‘ÕµÁÌ¡É•ÍÕ±Ğ±¥¹‘•¹ĞôÈ¤¬q¸œ¤íÉ•ÑÕÉ¸É•ÍÕ±Ğ()¥˜}}¹…µ•}|ôô}}µ…¥¹}|œè(€€€…Àõ…ÉÁ…ÉÍ”¹ÉÕµ•¹ÑA…ÉÍ•È ¤í…À¹…‘‘}…ÉÕµ•¹Ğ œ´µ½ÕÑÁÕĞœ±ÑåÁ”õA…Ñ ±É•ÅÕ¥É•õQÉÕ”¤í…À¹…‘‘}…ÉÕµ•¹Ğ œ´µ½İ¹•Èœ±ÑåÁ”õA…Ñ ¤í…ÉÌõ…À¹Á…ÉÍ•}…ÉÌ ¤(€€€¥˜…ÉÌ¹½İ¹•Èè±½‰…±Ì ¥l=]9Htõ…ÉÌ¹½İ¹•È¹É•Í½±Ù” ¤(€€€•áÁ½ÉĞ¡…ÉÌ¹½ÕÑÁÕĞ¤