from __future__ import annotations
import argparse, json, math, hashlib
from pathlib import Path
import numpy as np
import trimesh
from trimesh.visual.material import PBRMaterial

LEVELS=("MASTER","LOD0","LOD1","LOD2","LOD3")
CFG={
 "MASTER":dict(stake_sections=18, stem_sections=12, leaf_per_plant=13, leaflet_pairs=3, leaflet_pts=22, fruits=14, sphere_sub=2, flowers=8),
 "LOD0":dict(stake_sections=14, stem_sections=10, leaf_per_plant=11, leaflet_pairs=3, leaflet_pts=18, fruits=12, sphere_sub=2, flowers=6),
 "LOD1":dict(stake_sections=10, stem_sections=8, leaf_per_plant=8, leaflet_pairs=2, leaflet_pts=14, fruits=8, sphere_sub=1, flowers=4),
 "LOD2":dict(stake_sections=8, stem_sections=6, leaf_per_plant=5, leaflet_pairs=2, leaflet_pts=10, fruits=5, sphere_sub=1, flowers=2),
 "LOD3":dict(stake_sections=6, stem_sections=5, leaf_per_plant=3, leaflet_pairs=1, leaflet_pts=8, fruits=3, sphere_sub=1, flowers=0),
}
COLORS={
 "PlanterPP":[46,82,39,255], "PlanterEdge":[38,67,33,255], "PottingSoil":[48,31,21,255],
 "Bamboo":[151,125,70,255], "BambooNode":[122,98,55,255], "JuteTwine":[137,101,58,255],
 "TomatoStem":[55,119,42,255], "TomatoLeaf":[45,105,35,255], "LeafVein":[67,125,49,255],
 "TomatoRipe":[177,38,28,255], "TomatoTurning":[203,92,34,255], "TomatoGreen":[94,136,40,255],
 "Calyx":[52,101,34,255], "FlowerYellow":[222,188,54,255]
}
ROUGH={"PlanterPP":.56,"PlanterEdge":.60,"PottingSoil":.96,"Bamboo":.70,"BambooNode":.78,"JuteTwine":.92,
       "TomatoStem":.72,"TomatoLeaf":.68,"LeafVein":.74,"TomatoRipe":.40,"TomatoTurning":.43,"TomatoGreen":.50,"Calyx":.74,"FlowerYellow":.66}

def mat(name, double=False):
    return PBRMaterial(name=name, baseColorFactor=COLORS[name], metallicFactor=0.0, roughnessFactor=ROUGH[name], doubleSided=double)

def add(scene,name,mesh,material):
    mesh=mesh.copy(); mesh.visual.material=material; scene.add_geometry(mesh,node_name=name,geom_name=name)

def box(extents, center):
    m=trimesh.creation.box(extents=extents); m.apply_translation(center); return m

def cylinder_between(a,b,radius,sections=12):
    a=np.asarray(a,float);b=np.asarray(b,float);v=b-a;L=np.linalg.norm(v)
    if L<1e-8: raise ValueError('zero cylinder')
    T=trimesh.geometry.align_vectors([0,0,1],v/L)
    if T is None: T=np.eye(4)
    T[:3,3]=(a+b)/2
    return trimesh.creation.cylinder(radius=radius,height=L,sections=max(5,int(sections)),transform=T)

def frustum_shell(width=.65,depth=.26,height=.22,wall=.003,bottom_scale=.88):
    # closed thin-walled tapered polypropylene shell with an open visual cavity at top,
    # represented as a closed wall/bottom ring; soil covers the inner bottom.
    wt,dt=width/2,depth/2; wb,db=wt*bottom_scale,dt*bottom_scale
    yi=.026
    loops=[]
    for y,w,d in [(0,wb,db),(height,wt,dt),(yi,wb-wall,db-wall),(height-wall,wt-wall,dt-wall)]:
        loops.append(np.array([[-w,y,-d],[w,y,-d],[w,y,d],[-w,y,d]],float))
    ob,ot,ib,it=loops
    V=np.vstack(loops); F=[]
    # outer walls ob->ot
    for i in range(4):
        j=(i+1)%4; F += [[i,j,4+j],[i,4+j,4+i]]
    # inner walls it->ib, reverse winding inward but closed mesh volume positive after fix
    for i in range(4):
        j=(i+1)%4; F += [[12+i,12+j,8+j],[12+i,8+j,8+i]]
    # top rim ot->it
    for i in range(4):
        j=(i+1)%4; F += [[4+i,4+j,12+j],[4+i,12+j,12+i]]
    # solid molded bottom: outer bottom closes the underside, inner bottom closes the cavity floor.
    # Do not connect both with a ring as that would create triple-use non-manifold edges at the inner loop.
    F += [[0,2,1],[0,3,2]]
    F += [[8,9,10],[8,10,11]]
    m=trimesh.Trimesh(vertices=V,faces=np.asarray(F),process=True)
    if m.volume<0: m.invert()
    return m

def soil_slab(width=.60,depth=.215,y=.203,nx=14,nz=7):
    xs=np.linspace(-width/2,width/2,nx+1); zs=np.linspace(-depth/2,depth/2,nz+1)
    V=[]
    for z in zs:
        for x in xs:
            edge=(x/(width/2))**4+(z/(depth/2))**4
            und=.0025*math.sin(13*x+1.3)*math.cos(17*z-.4)-.0012*min(1,edge)
            V.append([x,y+und,z])
    topn=len(V); V += [[x,y-.012,z] for z in zs for x in xs]
    F=[]
    stride=nx+1
    for iz in range(nz):
        for ix in range(nx):
            a=iz*stride+ix;b=a+1;c=a+stride+1;d=a+stride
            F += [[a,b,c],[a,c,d]]
            aa=a+topn;bb=b+topn;cc=c+topn;dd=d+topn
            F += [[aa,cc,bb],[aa,dd,cc]]
    # close boundaries
    boundary=[]
    boundary += [i for i in range(stride)]
    boundary += [iz*stride+nx for iz in range(1,nz+1)]
    boundary += [nz*stride+ix for ix in range(nx-1,-1,-1)]
    boundary += [iz*stride for iz in range(nz-1,0,-1)]
    for k,a in enumerate(boundary):
        b=boundary[(k+1)%len(boundary)]; F += [[a,a+topn,b+topn],[a,b+topn,b]]
    m=trimesh.Trimesh(vertices=np.asarray(V),faces=np.asarray(F),process=True)
    if m.volume<0:m.invert()
    return m

def leaflet(length,width,pts=18,thick=.0009,seed=0):
    # serrated lanceolate leaflet, watertight shell, local x=length, z=width, y=normal
    n=max(8,int(pts)); per=[]
    for i in range(n):
        t=2*math.pi*i/n
        x=.5*length*math.cos(t)
        basew=width*math.sin(t)
        serr=1.0 + .075*math.sin((5+seed%3)*t+seed*.71)
        z=.5*basew*serr*(.84+.16*math.cos(t))
        # slight asymmetry and tip emphasis
        x += .035*length*math.sin(t)*(0.5-seed%2)
        per.append([x,thick/2,z])
    V=[[0,thick/2,0]]+per+[[0,-thick/2,0]]+[[p[0],-thick/2,p[2]] for p in per]
    topc=0; botc=n+1; F=[]
    for i in range(n):
        j=(i+1)%n
        F.append([topc,1+i,1+j]); F.append([botc,botc+1+j,botc+1+i])
        F.append([1+i,botc+1+i,botc+1+j]); F.append([1+i,botc+1+j,1+j])
    m=trimesh.Trimesh(vertices=np.asarray(V),faces=np.asarray(F),process=True)
    if m.volume<0:m.invert()
    return m

def basis_transform(origin, xdir, normal_hint=np.array([0,1,0.],float)):
    x=np.asarray(xdir,float); x/=np.linalg.norm(x)
    z=np.cross(x,normal_hint)
    if np.linalg.norm(z)<1e-6: z=np.cross(x,[0,0,1.])
    z/=np.linalg.norm(z); y=np.cross(z,x); y/=np.linalg.norm(y)
    T=np.eye(4); T[:3,0]=x;T[:3,1]=y;T[:3,2]=z;T[:3,3]=origin
    return T

def flower_star(radius=.018, thick=.0015):
    # 5-petal closed star shell in local x-z plane
    pts=[]
    for i in range(10):
        a=math.pi/2+i*math.pi/5; r=radius if i%2==0 else radius*.43
        pts.append([r*math.cos(a),thick/2,r*math.sin(a)])
    n=len(pts);V=[[0,thick/2,0]]+pts+[[0,-thick/2,0]]+[[p[0],-thick/2,p[2]] for p in pts];F=[];bc=n+1
    for i in range(n):
        j=(i+1)%n;F += [[0,1+i,1+j],[bc,bc+1+j,bc+1+i],[1+i,bc+1+i,bc+1+j],[1+i,bc+1+j,1+j]]
    return trimesh.Trimesh(vertices=np.asarray(V),faces=np.asarray(F),process=True)

def plant_points(x0,z0,plant_idx):
    # deterministic, plausible main-stem curve; not camera tuned
    pts=[]
    for i in range(8):
        y=.205+i*.145
        x=x0+.018*math.sin(i*.91+plant_idx*.7)
        z=z0+.015*math.sin(i*.67+plant_idx*1.3)
        pts.append(np.array([x,y,z]))
    return pts

def make(level):
    cfg=CFG[level]; scene=trimesh.Scene(); materials={n:mat(n,n in {'TomatoLeaf','FlowerYellow'}) for n in COLORS}
    add(scene,'TomatoPlanter_PP_Shell',frustum_shell(),materials['PlanterPP'])
    # rim as four actual thick members, slightly rounded impression comes from physical thickness rather than shading
    add(scene,'TomatoPlanter_RimFront',box([.672,.026,.018],[0,.218,.139]),materials['PlanterEdge'])
    add(scene,'TomatoPlanter_RimBack',box([.672,.026,.018],[0,.218,-.139]),materials['PlanterEdge'])
    add(scene,'TomatoPlanter_RimLeft',box([.018,.026,.260],[-.336,.218,0]),materials['PlanterEdge'])
    add(scene,'TomatoPlanter_RimRight',box([.018,.026,.260],[.336,.218,0]),materials['PlanterEdge'])
    nx={'MASTER':18,'LOD0':16,'LOD1':12,'LOD2':8,'LOD3':6}[level]
    nz={'MASTER':9,'LOD0':8,'LOD1':6,'LOD2':4,'LOD3':3}[level]
    add(scene,'TomatoPlanter_PottingSoil',soil_slab(nx=nx,nz=nz),materials['PottingSoil'])
    # three bamboo stakes, including visible nodes and two horizontal jute ties
    stake_x=[-.23,0,.23]
    for si,x in enumerate(stake_x):
        add(scene,f'TomatoSupport_BambooStake_{si}',cylinder_between([x,.205,0],[x,1.42,0],.0075,cfg['stake_sections']),materials['Bamboo'])
        node_count={'MASTER':5,'LOD0':5,'LOD1':4,'LOD2':3,'LOD3':2}[level]
        for ni in range(node_count):
            y=.39+ni*(.82/max(1,node_count-1))
            add(scene,f'TomatoSupport_BambooNode_{si}_{ni}',cylinder_between([x,y-.002,0],[x,y+.002,0],.0086,max(6,cfg['stake_sections']//2)),materials['BambooNode'])
    for yi in [.76,1.12]:
        add(scene,f'TomatoSupport_JuteCrossTie_{yi:.2f}',cylinder_between([-.24,yi,.002],[.24,yi,.002],.0023,max(6,cfg['stem_sections'])),materials['JuteTwine'])
    # two tomato plants
    leaf_idx=0; fruit_sites=[]; flower_sites=[]
    for pi,(x0,z0) in enumerate([(-.145,.012),(.145,-.012)]):
        pts=plant_points(x0,z0,pi)
        for i in range(len(pts)-1):
            r0=.0042-.00022*i
            add(scene,f'TomatoStem_Main_{pi}_{i}',cylinder_between(pts[i],pts[i+1],max(.0024,r0),cfg['stem_sections']),materials['TomatoStem'])
        # vertical tie loops to nearest stake (actual short twine pieces)
        stake=min(stake_x,key=lambda x:abs(x-x0))
        for ti,y in enumerate([.55,.88,1.18]):
            # find stem center at this y approximately
            idx=min(range(len(pts)),key=lambda k:abs(pts[k][1]-y)); p=pts[idx]
            add(scene,f'TomatoSupport_StemTie_{pi}_{ti}',cylinder_between([stake,y,0],[p[0],y,p[2]],.0017,max(5,cfg['stem_sections']//2)),materials['JuteTwine'])
        # compound leaves distributed along main stem
        count=cfg['leaf_per_plant']; candidates=np.linspace(1,len(pts)-2,count)
        for li,tv in enumerate(candidates):
            seg=int(math.floor(tv)); frac=tv-seg; p=pts[seg]*(1-frac)+pts[min(seg+1,len(pts)-1)]*frac
            ang=(li*2.399963229728653 + pi*1.41)%(2*math.pi)
            # higher leaves smaller and slightly more upright; lower ones droop from weight
            h=(p[1]-.3)/1.0; h=max(0,min(1,h))
            rach_len=.18*(1-.18*h)*(1+.055*math.sin(li*1.7+pi))
            radial=np.array([math.cos(ang),0,math.sin(ang)])
            tip=p+radial*rach_len+np.array([0,.018*(h-.55),0])
            add(scene,f'TomatoLeaf_Rachis_{pi}_{li}',cylinder_between(p,tip,.0018 if level in ('MASTER','LOD0') else .0015,max(5,cfg['stem_sections']//2)),materials['LeafVein'])
            pairn=cfg['leaflet_pairs']; side=np.array([-radial[2],0,radial[0]])
            slots=[]
            for pair in range(pairn):
                s=(pair+1)/(pairn+1); base=p*(1-s)+tip*s
                for sign in (-1,1):
                    length=.075*(1-.10*pair)*(1-.15*h)
                    width=.040*(1-.08*pair)
                    ldir=radial*.58+side*(.82*sign)+np.array([0,-.13-.08*(1-h),0])
                    slots.append((base,ldir,length,width,sign,pair))
            # terminal leaflet
            slots.append((tip,radial+np.array([0,-.10,0]),.085*(1-.15*h),.045,0,pairn))
            for sj,(base,ldir,ll,ww,sign,pair) in enumerate(slots):
                lf=leaflet(ll,ww,cfg['leaflet_pts'],.0009 if level in ('MASTER','LOD0') else .00075,seed=leaf_idx*7+sj)
                # Tomato leaflets are not a horizontal card deck: deterministic petiole-side and height
                # bias produces upward, outward and drooping planes while preserving the rachis owner.
                tilt_sign = sign if sign != 0 else (1 if (li + pi) % 2 == 0 else -1)
                normal_hint = np.array([0.0, 0.72 + 0.10*h, 0.0]) + radial*(0.22 + 0.10*h) + side*(0.24*tilt_sign)
                lf.apply_transform(basis_transform(base+ldir/np.linalg.norm(ldir)*ll*.33,ldir,normal_hint))
                add(scene,f'TomatoLeaf_Leaflet_{pi}_{li}_{sj}',lf,materials['TomatoLeaf']); leaf_idx+=1
            if li in (2,4,6,8):
                fruit_sites.append(tip+np.array([0,-.035,0]))
            if li in (3,7,9): flower_sites.append(tip)
    # fruit: distribute across known branch tips, then deterministic extra cluster points
    while len(fruit_sites)<cfg['fruits']:
        k=len(fruit_sites); pi=k%2; x=(-.145,.145)[pi]+.05*math.sin(k*1.37); y=.48+.075*(k%8); z=.02+.11*math.sin(k*.79)
        fruit_sites.append(np.array([x,y,z]))
    fruit_sites=fruit_sites[:cfg['fruits']]
    for fi,p in enumerate(fruit_sites):
        # short hanging pedicel
        q=p+np.array([.012*math.sin(fi),-.045,.010*math.cos(fi*.7)])
        add(scene,f'TomatoFruit_Pedicel_{fi}',cylinder_between(p,q,.0015,max(5,cfg['stem_sections']//2)),materials['Calyx'])
        radius=.021+.003*((fi*17)%5)/4
        sph=trimesh.creation.icosphere(subdivisions=cfg['sphere_sub'],radius=radius); sph.apply_translation(q+np.array([0,-radius*.62,0]))
        stage=fi%5
        mn='TomatoRipe' if stage in (0,1) else ('TomatoTurning' if stage==2 else 'TomatoGreen')
        add(scene,f'TomatoFruit_{mn}_{fi}',sph,materials[mn])
        cap=trimesh.creation.cone(radius=radius*.52,height=.009,sections=max(5,cfg['stake_sections']//2))
        R=trimesh.geometry.align_vectors([0,0,1],[0,1,0])
        if R is not None: cap.apply_transform(R)
        cap.apply_translation(q+np.array([0,-.002,0]))
        add(scene,f'TomatoFruit_Calyx_{fi}',cap,materials['Calyx'])
    for fi,p in enumerate(flower_sites[:cfg['flowers']]):
        fl=flower_star(.016,.0012); d=np.array([math.sin(fi*1.9),-.18,math.cos(fi*1.9)]); fl.apply_transform(basis_transform(p+d*.035,d))
        add(scene,f'TomatoFlower_{fi}',fl,materials['FlowerYellow'])
    return scene

def digest_scene(scene):
    h=hashlib.sha256(); tris=0; verts=0; watertight=True; deg=0
    for node in sorted(scene.graph.nodes_geometry):
        T,name=scene.graph[node];g=scene.geometry[name];tris+=len(g.faces);verts+=len(g.vertices)
        h.update(node.encode());h.update(np.asarray(T,dtype='<f8').tobytes());h.update(g.vertices.astype('<f8').tobytes());h.update(g.faces.astype('<i8').tobytes())
        area=g.area_faces;deg+=int(np.sum(area<1e-12));watertight=watertight and bool(g.is_watertight)
    return dict(triangles=tris,vertices=verts,allMeshesWatertight=watertight,degenerateFaces=deg,sha256Geometry=h.hexdigest())

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);args=ap.parse_args();args.output.mkdir(parents=True,exist_ok=True)
    levels=[]
    for level in LEVELS:
        scene=make(level); before=digest_scene(scene)
        glb=args.output/f'BalconySummerTomatoPlanter650_{level}.glb';obj=args.output/f'BalconySummerTomatoPlanter650_{level}.obj'
        glb.write_bytes(scene.export(file_type='glb')); obj.write_text(scene.export(file_type='obj'))
        sg=trimesh.load(glb,force='scene',process=False); so=trimesh.load(obj,force='scene',process=False)
        gt=sum(len(g.faces) for g in sg.geometry.values()); ot=sum(len(g.faces) for g in so.geometry.values())
        if gt!=before['triangles'] or ot!=before['triangles']: raise RuntimeError((level,before['triangles'],gt,ot))
        before.update(level=level,glb=str(glb.name),obj=str(obj.name),glbRoundtripTriangles=gt,objRoundtripTriangles=ot,bounds=np.asarray(scene.bounds).tolist())
        levels.append(before);print(level,before['triangles'],before['vertices'])
    tris=[x['triangles'] for x in levels]
    if not all(tris[i]>tris[i+1] for i in range(4)): raise RuntimeError(('LOD not strict',tris))
    metadata={
      'schema':1,'assetId':'BalconySummerTomatoPlanter650','date':'2026-09-17','purpose':'Distinct high-coverage summer balcony vegetation/context asset; not a second morning-glory owner.',
      'dimensionsAssumptionsMetres':{'planterTopWidth':.65,'planterDepth':.26,'planterBodyHeight':.22,'ppWallThickness':.003,'stakeHeightAbovePlanterBase':1.42,'nominalFruitDiameter':.042},
      'referenceBoundary':'Dimensions are original design assumptions inspired by common modern Japanese balcony planters and home tomato cultivation; they are not a claim about a specific 2000-era product.',
      'manufactureInstallation':{'planter':'injection-molded polypropylene proxy with tapered shell and thick rim','soil':'contained potting-soil slab with non-flat top','support':'three bamboo stakes with visible nodes, two jute cross-ties, and stem ties','plant':'two tomato vines with compound serrated leaflet shells, rachises, stems, flowers and staged fruit'},
      'materials':{k:{'baseColorSRGB8':COLORS[k],'roughness':ROUGH[k],'metallic':0.0} for k in COLORS},
      'weathering':'No arbitrary dirt decals. Soil is dry-matte; bamboo/node and jute roughness differ by material. No fake baked highlight.',
      'lod':levels,'unityVerified':False,'visualFidelityScore':None
    }
    (args.output/'tomato_planter_metadata.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n')

if __name__=='__main__':main()
