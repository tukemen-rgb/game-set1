from pathlib import Path
import json, trimesh, numpy as np
new=Path('/mnt/data/ac_install_details_2026-09-16')
old=Path('/mnt/data/ac_prev/generated_outputs')
out=new/'BalconyACInstalledReview_LOD0.glb'
scene=trimesh.Scene()

def add(path,prefix,translation):
 s=trimesh.load(path,force='scene',process=False)
 for name,g in s.geometry.items():
  h=g.copy();h.apply_translation(translation);scene.add_geometry(h,node_name=prefix+'_'+name,geom_name=prefix+'_'+name)

add(old/'OutdoorACCondenser780_LOD0.glb','AC',[0,0,0])
add(old/'RefrigerantPipeTrunking60x55_900_LOD0.glb','TRUNK',[.46,.13,.3065])
add(old/'CondensateDrainHose16_1200_LOD0.glb','DRAIN',[-.05,0,.02])
add(new/'RefrigerantLineSetPair_6p35_9p52_LOD0.glb','LINESET',[.505,.171,.07])
add(new/'PipeCoverWallElbow60x55_LOD0.glb','TOP_ELBOW',[.46,1.028,.3065])
add(new/'ACWallPenetrationSeal75_LOD0.glb','WALL_SEAL',[.46,1.083,.4055])
# diagnostic only; 140 mm wall puts its exterior face at z=0.3415, exactly against trunking back envelope
floor=trimesh.creation.box([1.65,.012,.92]);floor.apply_translation([.11,-.006,.08]);scene.add_geometry(floor,node_name='DIAGNOSTIC_floor',geom_name='DIAGNOSTIC_floor')
wall=trimesh.creation.box([1.65,1.30,.140]);wall.apply_translation([.11,.65,.4115]);scene.add_geometry(wall,node_name='DIAGNOSTIC_wall',geom_name='DIAGNOSTIC_wall')
out.write_bytes(scene.export(file_type='glb'))
tri=sum(len(g.faces) for g in scene.geometry.values())
# design-contact checks from authored coordinates
checks={
 'largeLineEndpointToTrunkStartMm':float(np.linalg.norm(np.array([.505+.232,.171+.029,.07+.1265])-np.array([.46+.325-.0475,.13+.070,.3065-.110]))*1000),
 'smallLineEndpointToTrunkStartMm':float(np.linalg.norm(np.array([.505+.232,.171-.001,.07+.1265])-np.array([.46+.325-.0475,.13+.040,.3065-.110]))*1000),
 'trunkBackToWallExteriorMm':float(abs((.3065+.0349701)-.3415)*1000),
 'topElbowStartOverlapIntoTrunkMm':2.0,
 'wallSealPuttyStraddlesExteriorSurface':True,
}
manifest={'id':'BalconyACInstalledReview_LOD0','trianglesIncludingDiagnosticContext':tri,'geometryCount':len(scene.geometry),'formalBenchmarkSceneChanged':False,'unityVerified':False,'wallDiagnosticThicknessMm':140,'placementPurpose':'external assembly review only; condenser, flush wall trunking, actual refrigerant line pair, top wall elbow, wall sleeve/putty seal and condensate hose','designContactChecks':checks,'intentionalContacts':['line set flare-side contacts existing condenser copper-stub ends','line set opposite ends contact existing trunking insulated-line starts','top elbow overlaps trunking top 2 mm under molded snap band','wall elbow enters sleeve volume','putty collar straddles diagnostic wall exterior plane'],'aabbOverlapValidation':'Not used as a physical collision proof; contact checks above are coordinate constraints, not Unity physics.'}
(new/'BalconyACInstalledReview.manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps(manifest,indent=2))
