"""Regression for the observed striped self-shadow and loss of real contact shadows."""
from pathlib import Path
import argparse,json
import numpy as np
import trimesh
from PIL import Image
import render_material_preview as preview


def verify(output):
    output.mkdir(parents=True,exist_ok=True)
    v=np.array([[-2.,0.,-2.],[-2.,0.,2.],[2.,0.,2.],[2.,0.,-2.]])
    plane=trimesh.Trimesh(vertices=v,faces=[[0,1,2],[0,2,3]],process=False)
    plane.visual=trimesh.visual.TextureVisuals(uv=v[:,[0,2]],material=trimesh.visual.material.PBRMaterial(
        name='UniformTestPlane',baseColorFactor=[.5,.5,.5,1.],metallicFactor=0.,roughnessFactor=.8))
    scene=trimesh.Scene(plane)
    bounds=trimesh.bounds.corners(np.array([[-2.,0.,-2.],[2.,1.4,2.]]))
    args=dict(azimuth=0.,elevation=65.,width=640,height=480,ss=1,crop_bounds=bounds)
    blank=output/'uniform_plane.png';shadow=output/'occluded_plane.png'
    preview.render(scene,blank,title='Regression: uniform plane',**args)
    blocker=trimesh.creation.box(extents=[.32,.32,.32]);blocker.apply_translation([0.,.9,0.])
    blocker.visual=trimesh.visual.TextureVisuals(uv=np.zeros((len(blocker.vertices),2)),material=plane.visual.material)
    scene.add_geometry(blocker)
    preview.render(scene,shadow,title='Regression: true cast shadow',**args)
    a=np.asarray(Image.open(blank),float)/255.;b=np.asarray(Image.open(shadow),float)/255.
    def patch(im,p,r=3):
        q=preview.projected(np.asarray([p],float),preview.basis(0.,65.),640,480,bounds)[0]
        x,y=map(int,q[:2]);return im[y-r:y+r+1,x-r:x+r+1,:3]
    light=preview.unit([-.7,1.6,1.8]);point=np.array([0.,.9,0.])-.9/light[1]*light
    samples=np.vstack([patch(a,[x,0.,z]).reshape(-1,3) for x,z in ((-.8,-.8),(.8,-.8),(-.8,.8),(.8,.8))])
    spread=float(np.ptp(samples.mean(axis=1)))
    contrast=float(patch(a,point).mean()-patch(b,point).mean())
    assert spread<=2/255.,('false self-shadow on a uniform plane',spread)
    assert contrast>.08,('real cast shadow was lost',contrast)
    report={'uniformPlaneLuminanceRange':spread,'maxAllowedRange':2/255.,
            'trueCastShadowContrast':contrast,'minimumRequiredContrast':.08,'passed':True}
    (output/'shadow_regression.json').write_text(json.dumps(report,indent=2)+'\n')
    return report


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True)
    print(json.dumps(verify(p.parse_args().output)))
