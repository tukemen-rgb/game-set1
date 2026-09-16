"""Actual GLB material preview; never an image-generated substitute or Unity evidence.
All triangles and node transforms are submitted; no face budget or geometry skipping.
Dependencies: numpy, trimesh, Pillow, numba. CPU depth/shadow buffers, supersampling.
This is a diagnostic material renderer: analytic sky, no scene reflections/refraction,
no GI, no calibrated engine parity. Existing GLB geometry/UV/topology stay unchanged.
"""
from __future__ import annotations
import argparse, copy, hashlib, json, math
from pathlib import Path
import numpy as np
import trimesh
from PIL import Image, ImageDraw, ImageFont
from numba import njit
from numba.typed import List as TextureList

DEFAULT_PALETTE = {
 'PaintedFiberCement': [0.79,0.76,0.68],
 'AnodizedAluminum': [0.66,0.68,0.68],
 'DarkAluminum': [0.30,0.31,0.30],
 'ClearGlass': [0.46,0.59,0.62],
 'WarmWhitePowderCoat': [0.84,0.83,0.76],
 'PaintedMetal': [0.69,0.70,0.66],
 'PipeCoverPVC': [0.78,0.77,0.69],
 'DrainHosePVC': [0.73,0.73,0.66],
 'SleevePVC': [0.69,0.69,0.63],
 'InsulationGray': [0.55,0.56,0.54],
 'GrayPVC': [0.61,0.63,0.61],
 'WeatheredConcrete': [0.58,0.56,0.52],
 'WaterproofCoating': [0.47,0.49,0.47],
 'CementMortar': [0.54,0.52,0.47],
 'PowderCoatedSteel': [0.24,0.23,0.21],
 'PowderCoatedAluminum': [0.57,0.59,0.57],
}

def unit(x):
 x=np.asarray(x,float); return x/np.linalg.norm(x)

def geometry_digest(scene):
 h=hashlib.sha256()
 for node in sorted(scene.graph.nodes_geometry):
  t,name=scene.graph[node];g=scene.geometry[name]
  h.update(node.encode());h.update(np.asarray(t,dtype='<f8').tobytes())
  h.update(g.vertices.astype('<f8').tobytes());h.update(g.faces.astype('<i8').tobytes())
 return h.hexdigest()

def colorize(scene):
 """Apply an original neutral architectural palette; retain each material's maps.
 Same material name can have different ownership, e.g. soffit vs wall paint.
 Returns new scene and explicit modifications, not a silent global material rename.
 """
 out=scene.copy();changes=[]
 for name,g in out.geometry.items():
  m=getattr(g.visual,'material',None)
  if m is None: raise ValueError(f'Missing material: {name}')
  target=DEFAULT_PALETTE.get(m.name)
  if m.name=='PaintedFiberCement' and ('Soffit' in name or 'Hatch' in name):target=[.84,.83,.77]
  if target is None:continue
  m=copy.deepcopy(m);g.visual.material=m
  tex=m.baseColorTexture
  if tex is not None:
   arr=np.asarray(tex.convert('RGBA'),dtype=float)/255.
   avg=arr[...,:3].mean((0,1));variation=arr[...,:3]/np.maximum(avg,1e-4)
   arr[...,:3]=np.clip(variation*np.asarray(target),0,1)
   m.baseColorTexture=Image.fromarray(np.uint8(np.round(arr*255)),'RGBA')
   m.baseColorFactor=[255,255,255,255]
  else:
   avg=np.asarray(m.baseColorFactor[:3],float)/255.
   m.baseColorFactor=list(np.uint8(np.round(np.asarray(target)*255)))+[255]
  changes.append({'geometry':name,'material':m.name,'beforeMeanSRGB':avg.tolist(),'afterMeanSRGB':target})
 assert geometry_digest(out)==geometry_digest(scene),'Color change mutated geometry'
 return out,changes

def flatten(scene,texture_size=2048):
 V=[];F=[];N=[];UV=[];MI=[];TEX=[];NM=[];props=[];mats=[];offset=0;cache={}
 for node in scene.graph.nodes_geometry:
  transform,name=scene.graph[node];g=scene.geometry[name];m=getattr(g.visual,'material',None)
  if m is None:raise ValueError('Missing material '+name)
  # Deduplicate by actual texture values, not by name only.
  factor=getattr(m,'baseColorFactor',None)
  factor=np.ones(4) if factor is None else np.asarray(factor,float)/255.
  bc=m.baseColorTexture
  rm=m.metallicRoughnessTexture
  norm=m.normalTexture
  sizes=[im.size for im in (bc,rm,norm) if im is not None]
  tw,th=max(sizes,key=lambda s:s[0]*s[1]) if sizes else (8,8)
  scale=min(1.,texture_size/max(tw,th));tw=max(1,int(tw*scale));th=max(1,int(th*scale))
  if bc is None:
   arr=np.ones((th,tw,4),dtype=np.float32)*factor
  else:
   arr=np.asarray(bc.convert('RGBA').resize((tw,th),Image.Resampling.BILINEAR),dtype=np.float32)/255.*factor
  metallic=1. if m.metallicFactor is None else m.metallicFactor
  rough=1. if m.roughnessFactor is None else m.roughnessFactor
  if rm is None:mr=np.zeros((th,tw,3),dtype=np.float32);mr[:,:,1]=rough;mr[:,:,2]=metallic
  else:
   mr=np.asarray(rm.convert('RGB').resize((tw,th),Image.Resampling.BILINEAR),dtype=np.float32)/255.
   mr[:,:,1]*=rough;mr[:,:,2]*=metallic
  if norm is None:nr=np.zeros((th,tw,3),dtype=np.float32);nr[:]=[.5,.5,1.]
  else:nr=np.asarray(norm.convert('RGB').resize((tw,th),Image.Resampling.BILINEAR),dtype=np.float32)/255.
  tex=np.concatenate([arr[:,:,:3],mr[:,:,1:3]],axis=2).astype(np.float32)
  key=hashlib.sha256(str((tw,th)).encode()+tex.tobytes()+nr.tobytes()).hexdigest()
  if key not in cache:
   cache[key]=len(TEX);TEX.append(tex);NM.append(nr)
   props.append([1. if m.doubleSided else 0.,float(norm is not None),1. if m.name=='ClearGlass' else 0.])
   mats.append({'name':m.name,'meanBaseColorSRGB':arr[:,:,:3].mean((0,1)).tolist(),'metallicMean':float(mr[:,:,2].mean()),'roughnessMean':float(mr[:,:,1].mean()),'normalMap':norm is not None,'sampledTextureSize':[tw,th]})
  idx=cache[key]
  pts=trimesh.transform_points(g.vertices,transform)
  normals=g.vertex_normals@np.linalg.inv(transform[:3,:3]);normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-12)
  faces=g.faces.copy()
  if np.linalg.det(transform[:3,:3])<0:faces=faces[:,::-1]
  uv=getattr(g.visual,'uv',None)
  if uv is None:uv=np.zeros((len(pts),2))
  V.append(pts);F.append(faces+offset);N.append(normals);UV.append(uv);MI.append(np.full(len(faces),idx,np.int32));offset+=len(pts)
 # Preserve native map detail without expanding every 128 px material to 2048 px.
 return (np.vstack(V),np.vstack(F).astype(np.int32),np.vstack(N),np.vstack(UV),np.concatenate(MI),TextureList(TEX),TextureList(NM),np.asarray(props),mats)

@njit(cache=True)
def fill_depth(v,faces,h,w,cull,material_ids,props):
 depth=np.full((h,w),-1e30);facebuf=np.full((h,w),-1,np.int32);bu=np.zeros((h,w),np.float32);bv=np.zeros((h,w),np.float32)
 accepted=0
 for k in range(len(faces)):
  a=v[faces[k,0]];b=v[faces[k,1]];c=v[faces[k,2]]
  ar=(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
  if abs(ar)<1e-10:continue
  if cull and ar>=0 and props[material_ids[k],0]<.5:continue
  accepted+=1
  x0=max(0,int(math.floor(min(a[0],b[0],c[0]))));x1=min(w-1,int(math.ceil(max(a[0],b[0],c[0]))))
  y0=max(0,int(math.floor(min(a[1],b[1],c[1]))));y1=min(h-1,int(math.ceil(max(a[1],b[1],c[1]))))
  for y in range(y0,y1+1):
   for x in range(x0,x1+1):
    px=x+.5;py=y+.5
    u=((b[0]-px)*(c[1]-py)-(b[1]-py)*(c[0]-px))/ar
    vv=((c[0]-px)*(a[1]-py)-(c[1]-py)*(a[0]-px))/ar
    ww=1-u-vv
    if u>=-1e-8 and vv>=-1e-8 and ww>=-1e-8:
     d=u*a[2]+vv*b[2]+ww*c[2]
     if d>depth[y,x]:depth[y,x]=d;facebuf[y,x]=k;bu[y,x]=u;bv[y,x]=vv
 return depth,facebuf,bu,bv,accepted

@njit(cache=True)
def sample(tex,u,v):
 h,w,c=tex.shape
 # Pillow GLB textures use image top-left; OBJ/glTF UV origin is lower-left.
 fx=(u-math.floor(u))*w-.5;fy=(1-(v-math.floor(v)))*h-.5
 x=int(math.floor(fx));y=int(math.floor(fy));dx=fx-x;dy=fy-y
 return tex[y%h,x%w]*(1-dx)*(1-dy)+tex[y%h,(x+1)%w]*dx*(1-dy)+tex[(y+1)%h,x%w]*(1-dx)*dy+tex[(y+1)%h,(x+1)%w]*dx*dy

@njit(cache=True)
def srgb2linear(x):
 return x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4
@njit(cache=True)
def linear2srgb(x):
 return 12.92*x if x<=.0031308 else 1.055*x**(1/2.4)-.055

@njit(cache=True)
def shade(vertices,faces,normals,uv,mi,tex,nmap,props,fb,bu,bv,view,light,sp,sd,shadow_bias,exposure):
 h,w=fb.shape;image=np.ones((h,w,3),np.float32)
 H=light+view;H/=np.linalg.norm(H)
 for y in range(h):
  for x in range(w):
   k=fb[y,x]
   if k<0:
    for ch in range(3): image[y,x,ch]=.95-.025*(y/h)
    continue
   ids=faces[k];u=bu[y,x];v=bv[y,x];z=1-u-v;mid=mi[k]
   p=vertices[ids[0]]*u+vertices[ids[1]]*v+vertices[ids[2]]*z
   N=normals[ids[0]]*u+normals[ids[1]]*v+normals[ids[2]]*z
   nl=np.linalg.norm(N)
   e1=vertices[ids[1]]-vertices[ids[0]];e2=vertices[ids[2]]-vertices[ids[0]]
   fn=np.cross(e1,e2);fn/=max(1e-10,np.linalg.norm(fn))
   if nl<1e-10:N=fn
   else:N/=nl
   # The source's uncreased 8-vertex boxes have unsuitable smooth corner normals.
   # Only use flat normals on demonstrably coplanar coarse triangles in this preview;
   # exported source normals are not changed.
   if max(abs(fn[0]),abs(fn[1]),abs(fn[2]))>.999:N=fn
   if np.dot(N,view)<0 and props[mid,0]>.5:N=-N
   uvp=uv[ids[0]]*u+uv[ids[1]]*v+uv[ids[2]]*z
   vals=sample(tex[mid],uvp[0],uvp[1]);rough=max(.06,min(1.,vals[3]));metal=max(0.,min(1.,vals[4]))
   # Tangent-space normals only if the existing UV triangle is nondegenerate.
   du=uv[ids[1]]-uv[ids[0]];dv=uv[ids[2]]-uv[ids[0]];det=du[0]*dv[1]-du[1]*dv[0]
   if props[mid,1]>.5 and abs(det)>1e-10:
    T=(e1*dv[1]-e2*du[1])/det;T=T-N*np.dot(N,T);tl=np.linalg.norm(T)
    if tl>1e-10:
     T/=tl;B=np.cross(N,T)
     Bref=(e2*du[0]-e1*dv[0])/det
     if np.dot(B,Bref)<0:B=-B
     nm=sample(nmap[mid],uvp[0],uvp[1])*2-1
     N=T*nm[0]+B*nm[1]+N*nm[2];N/=max(1e-10,np.linalg.norm(N))
   ndl=max(0.,np.dot(N,light));ndv=max(.02,np.dot(N,view));ndh=max(0.,np.dot(N,H));vdh=max(0.,np.dot(view,H))
   shpos=sp[ids[0]]*u+sp[ids[1]]*v+sp[ids[2]]*z
   se1=sp[ids[1]]-sp[ids[0]];se2=sp[ids[2]]-sp[ids[0]]
   sdet=se1[0]*se2[1]-se1[1]*se2[0]
   dzdx=0.;dzdy=0.
   if abs(sdet)>1e-10:
    dzdx=(se1[2]*se2[1]-se2[2]*se1[1])/sdet
    dzdy=(se1[0]*se2[2]-se2[0]*se1[2])/sdet
   sx=int(shpos[0]);sy=int(shpos[1]);vis=0.;cnt=0
   for iy in range(-1,2):
    for ix in range(-1,2):
     xx=sx+ix;yy=sy+iy
     if xx>=0 and xx<sd.shape[1] and yy>=0 and yy<sd.shape[0]:
      cnt+=1
      # Each PCF sample lies at a different point on the receiver plane. Comparing
      # every neighbour to the center depth creates striped false self-shadows.
      receiver=shpos[2]+dzdx*(xx+.5-shpos[0])+dzdy*(yy+.5-shpos[1])
      if receiver+shadow_bias>=sd[yy,xx]:vis+=1
   visibility=vis/cnt if cnt>0 else 1.
   alpha=rough*rough;a2=alpha*alpha;den=ndh*ndh*(a2-1)+1
   D=a2/(math.pi*den*den+.0000001);K=(rough+1)**2/8
   G=(ndl/(ndl*(1-K)+K+.000001))*(ndv/(ndv*(1-K)+K))
   env=(.37+.23*max(0.,N[1]))
   for ch in range(3):
    base=srgb2linear(max(0.,min(1.,vals[ch])));F0=.04*(1-metal)+base*metal
    F=F0+(1-F0)*(1-vdh)**5
    spec=min(4.,D*G*F/(4*max(ndl,.001)*ndv+.000001))
    direct=((1-F)*(1-metal)*base/math.pi+spec)*ndl*2.5*visibility
    # Analytic ambient reflection proxy, not scene-reflection evidence.
    ambient=base*env*(1-metal)+F0*(.38+.3*max(0.,N[1]))*metal
    col=(direct+ambient)*exposure
    col=max(0.,col)
    # Gentle highlight shoulder, no blue artistic LUT or baked lighting.
    if col>1:col=1-(.02/(col+.02))
    image[y,x,ch]=min(1.,linear2srgb(col))
 return image

def basis(azimuth,elevation):
 a,e=np.radians([azimuth,elevation]);f=np.array([math.sin(a)*math.cos(e),math.sin(e),math.cos(a)*math.cos(e)])
 r=unit(np.cross([0,1,0],f));up=unit(np.cross(f,r));return np.stack([r,up,f],axis=1)

def projected(v,cam,width,height,bounds=None,fill=.84):
 a=v@cam
 pts=a if bounds is None else np.asarray(bounds)@cam
 lo=pts.min(0);hi=pts.max(0);c=(lo+hi)/2
 scale=min(width*fill/max(hi[0]-lo[0],1e-9),height*fill/max(hi[1]-lo[1],1e-9))
 a[:,0]=(a[:,0]-c[0])*scale+width/2;a[:,1]=-(a[:,1]-c[1])*scale+height/2
 return a

def render(scene,path,azimuth=22.,elevation=10.,width=1600,height=1100,ss=2,title='Material color preview',crop_bounds=None,exposure=1.18,texture_size=2048,light_direction=None):
 vertices,faces,normals,uv,mi,tex,nmap,props,mats=flatten(scene,texture_size)
 assert np.isfinite(vertices).all() and np.isfinite(uv).all()
 W,H=width*ss,height*ss;cam=basis(azimuth,elevation)
 vp=projected(vertices,cam,W,H,crop_bounds)
 light=unit(np.array([-.7,1.6,1.8] if light_direction is None else light_direction));lr=unit(np.cross([0,1,0],light));lup=unit(np.cross(light,lr));lc=np.stack([lr,lup,light],axis=1)
 shadowres=2048;sp=projected(vertices,lc,shadowres,shadowres,fill=.95)
 # Source face selection is identical for camera and shadow maps; all triangles.
 sd,*_=fill_depth(sp,faces,shadowres,shadowres,False,mi,props)
 depth,fb,bu,bv,visible=fill_depth(vp,faces,H,W,True,mi,props)
 image=shade(vertices,faces,normals,uv,mi,tex,nmap,props,fb,bu,bv,cam[:,2],light,sp,sd,max(np.ptp(vertices,axis=0))*1e-5,exposure)
 im=Image.fromarray(np.uint8(np.clip(image,0,1)*255),'RGB').resize((width,height),Image.Resampling.LANCZOS)
 d=ImageDraw.Draw(im);f='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf';font=ImageFont.truetype(f,max(16,int(width/65)));small=ImageFont.truetype(f,max(12,int(width/100)))
 d.rectangle((0,0,width,int(height*.062)),fill=(246,245,241));d.text((24,14),title,fill=(37,40,42),font=font)
 d.rectangle((0,height-40,width,height),fill=(246,245,241));d.text((24,height-30),'ACTUAL GLB | material colors + textures | NOT UNITY | Visual Fidelity unscored',fill=(60,62,65),font=small)
 path=Path(path);path.parent.mkdir(parents=True,exist_ok=True);im.save(path)
 mask=fb>=0;visible_m=np.unique(mi[fb[mask]])
 report={'image':path.name,'trianglesSubmitted':len(faces),'trianglesCulledOrDegenerate':len(faces)-visible,'sourceMeshes':len(scene.geometry),'visibleMaterials':len(visible_m),'materials':mats,'allSourceFacesSubmitted':True,'arbitraryFaceLimit':None,'backfaceCulling':True,'depthBuffer':True,'baseColorTextureSampling':True,'normalMapSamplingOnValidUV':True,'metallicRoughnessMapSampling':True,'shadowMap':True,'geometrySHA256':geometry_digest(scene),'size':[width,height],'supersampling':ss,'azimuth':azimuth,'elevation':elevation,'unityRender':False,'visualScore':None,'limitations':['analytic sky, no scene reflection/refraction or GI','not Unity','glass remains source opaque tint','normal display for coarse planar faces is diagnostic; saved normals unchanged','model defects are not repaired by coloring']}
 report.update(textureResolutionCap=texture_size,receiverPlaneShadowDepth=True,lightDirection=light.tolist())
 path.with_suffix('.json').write_text(json.dumps(report,indent=2)+'\n')
 print(json.dumps({k:report[k] for k in ['image','trianglesSubmitted','visibleMaterials','size']}),flush=True)
 return report

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('input',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--palette',action='store_true');p.add_argument('--azimuth',type=float,default=22);p.add_argument('--elevation',type=float,default=10);p.add_argument('--width',type=int,default=1600);p.add_argument('--height',type=int,default=1100);a=p.parse_args()
 scene=trimesh.load(a.input,force='scene',process=False)
 if a.palette:
  scene,changes=colorize(scene);dest=a.output.with_suffix('.glb');dest.write_bytes(scene.export(file_type='glb'));a.output.with_suffix('.palette.json').write_text(json.dumps(changes,indent=2)+'\n')
 render(scene,a.output,azimuth=a.azimuth,elevation=a.elevation,width=a.width,height=a.height)
