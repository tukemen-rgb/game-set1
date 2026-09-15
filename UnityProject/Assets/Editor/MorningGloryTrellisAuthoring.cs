using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Deterministic authoring utility for an unbranded potted Ipomoea nil trellis assembly.
// This creates project assets only when invoked explicitly. It has no scene-open/runtime/pre-cull hooks.
// Unity compile/import/render parity remains unverified until a real Unity 6000.3.0f1 runner executes it.
public static class MorningGloryTrellisAuthoring
{
    private const string Root = "Assets/Art/GardenProps/MorningGloryTrellis/Generated";
    private const string PrefabPath = Root + "/MorningGloryTrellis.prefab";
    private static readonly string[] MaterialNames =
    {
        "Bamboo", "JuteTwine", "LivingStem", "LivingLeaf", "LeafVein", "FlowerBlue", "FlowerThroat", "DrySoil"
    };
    private static readonly Color[] Albedos =
    {
        new Color(.36f,.32f,.16f), new Color(.43f,.33f,.20f), new Color(.16f,.34f,.09f), new Color(.10f,.28f,.07f),
        new Color(.13f,.31f,.08f), new Color(.19f,.19f,.56f), new Color(.82f,.78f,.88f), new Color(.12f,.075f,.04f)
    };
    private static readonly float[] Roughness = { .60f,.86f,.56f,.50f,.56f,.43f,.46f,.93f };
    private static readonly float[] IntendedNormalScale = { .18f,.28f,.14f,.22f,.12f,.10f,.08f,.38f };
    private static readonly int[] Leaves = { 48,36,24,14 };
    private static readonly int[] Flowers = { 8,6,4,2 };
    private static readonly int[] Vines = { 6,6,4,3 };
    private static readonly float[] LodHeights = { .42f,.20f,.075f,.018f };

    [MenuItem("Tools/New Town/Author/Morning Glory Trellis Assets")]
    public static void BuildAssets()
    {
        EnsureFolder(Root);
        Shader shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Built-in Standard shader is required; no silent material fallback.");
        Material[] materials = BuildMaterials(shader);

        Mesh master = BuildMesh(-1);
        SaveMesh(master, Root + "/MorningGloryTrellis_MASTER.asset");

        GameObject root = new GameObject("MorningGloryTrellis");
        try
        {
            LOD[] lods = new LOD[4];
            for (int level = 0; level < 4; level++)
            {
                Mesh mesh = BuildMesh(level);
                string meshPath = Root + "/MorningGloryTrellis_LOD" + level + ".asset";
                SaveMesh(mesh, meshPath);
                Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                GameObject child = new GameObject("LOD" + level);
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = saved;
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                lods[level] = new LOD(LodHeights[level], new Renderer[] { renderer });
            }
            LODGroup group = root.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.SetLODs(lods);
            group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[GardenProps] Morning-glory assets authored. MASTER is not a runtime renderer; real Unity render/temporal verification remains pending.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Material[] BuildMaterials(Shader shader)
    {
        Material[] result = new Material[MaterialNames.Length];
        for (int i = 0; i < result.Length; i++)
        {
            string path = Root + "/" + MaterialNames[i] + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = MaterialNames[i] };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.color = Albedos[i];
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", 1f - Roughness[i]);
            m.SetFloat("_BumpScale", 0f); // no fabricated normal texture; authored target is stored below
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
            m.enableInstancing = true;
            m.SetOverrideTag("NewTownIntendedNormalScale", IntendedNormalScale[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            EditorUtility.SetDirty(m);
            result[i] = m;
        }
        return result;
    }

    private static Mesh BuildMesh(int level)
    {
        bool master = level < 0;
        int leafCount = master ? 56 : Leaves[level];
        int flowerCount = master ? 10 : Flowers[level];
        int vineCount = master ? 6 : Vines[level];
        int stakeSides = master ? 16 : new[] { 12,10,8,6 }[level];
        int vineSides = master ? 10 : new[] { 8,7,6,5 }[level];
        int soilSegments = master ? 128 : new[] { 96,64,40,24 }[level];
        MeshBuilder b = new MeshBuilder(MaterialNames.Length);
        AddSoil(b, soilSegments);

        Vector3[] bases = new Vector3[3];
        Vector3[] tops = new Vector3[3];
        for (int s = 0; s < 3; s++)
        {
            float angle = Mathf.PI * 2f * s / 3f + .2f;
            bases[s] = new Vector3(.071f * Mathf.Cos(angle), .125f, .071f * Mathf.Sin(angle));
            tops[s] = new Vector3(.032f * Mathf.Cos(angle), 1.16f, .032f * Mathf.Sin(angle));
            List<Vector3> path = new List<Vector3>();
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                Vector3 side = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                path.Add(Vector3.Lerp(bases[s], tops[s], t) + side * (.002f * Mathf.Sin(Mathf.PI * t) * (s - 1)));
            }
            AddSweep(b, path, .0046f, .0031f, stakeSides, 0, true);
            float[] nodes = level >= 0 && level >= 2 ? (level == 2 ? new[] { .46f,.86f } : new[] { .73f }) : new[] { .32f,.58f,.84f,1.08f };
            foreach (float y in nodes)
            {
                float t = Mathf.InverseLerp(bases[s].y, tops[s].y, y);
                Vector3 c = Vector3.Lerp(bases[s], tops[s], t);
                AddTorus(b, c, (tops[s]-bases[s]).normalized, .0041f, master ? .00069f : .00055f, Mathf.Max(8, stakeSides), Mathf.Max(5, stakeSides/2), 0);
            }
        }

        int tierCount = master ? 4 : new[] { 4,4,3,2 }[level];
        int twineSides = master ? 8 : new[] { 7,6,5,4 }[level];
        for (int tier = 0; tier < tierCount; tier++)
        {
            float y = tierCount == 1 ? .75f : Mathf.Lerp(.46f, 1.05f, tier / (float)(tierCount - 1));
            Vector3[] p = new Vector3[3];
            for (int s = 0; s < 3; s++) p[s] = Vector3.Lerp(bases[s], tops[s], Mathf.InverseLerp(bases[s].y, tops[s].y, y));
            for (int s = 0; s < 3; s++)
            {
                Vector3 a = p[s], c = p[(s+1)%3], mid = (a+c)*.5f + Vector3.down*.006f;
                AddSweep(b, new List<Vector3> { a,mid,c }, .0012f, .0012f, twineSides, 1, true);
            }
        }
        for (int s = 0; s < 3; s++)
            AddTorus(b, Vector3.Lerp(bases[s],tops[s],.985f), (tops[s]-bases[s]).normalized, .0042f,.00115f,Mathf.Max(8,stakeSides),twineSides,1);

        List<Vector3[]> vines = new List<Vector3[]>();
        int samples = master ? 34 : new[] { 28,24,17,12 }[level];
        for (int v = 0; v < vineCount; v++)
        {
            int s = v % 3;
            float phase = v * 1.37f + .4f;
            float topY = 1.12f - .04f * (v % 3);
            float turns = 3.2f + .35f * (v % 2);
            Vector3[] path = new Vector3[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples-1);
                float y = Mathf.Lerp(.205f, topY, t);
                Vector3 spine = Vector3.Lerp(bases[s],tops[s],Mathf.InverseLerp(bases[s].y,tops[s].y,y));
                float r = .014f * (1f - .55f*t), a = phase + Mathf.PI*2f*turns*t;
                path[i] = spine + new Vector3(r*Mathf.Cos(a),0f,r*Mathf.Sin(a));
            }
            AddSweep(b, new List<Vector3>(path), .0021f,.00115f,vineSides,2,true);
            vines.Add(path);
        }

        for (int i = 0; i < leafCount; i++)
        {
            int v = i % vineCount;
            Vector3[] path = vines[v];
            float frac = .12f + .78f * Frac(i * .61803398875f);
            int index = Mathf.Clamp(Mathf.RoundToInt(frac*(path.Length-1)),1,path.Length-2);
            Vector3 p = path[index];
            float angle = i*2.399963f + v*.4f;
            Vector3 outward = new Vector3(Mathf.Cos(angle), .16f*((i%3)-1), Mathf.Sin(angle)).normalized;
            float length = (.082f + .022f*((i*37)%7)/6f) * (.82f + .18f*(1f-frac));
            float width = length * (.95f + .08f*((i*19)%5)/4f);
            float petiole = .018f + .012f*((i*13)%5)/4f;
            Vector3 end = p + outward*petiole + Vector3.up*(.003f*Mathf.Sin(i));
            AddSweep(b,new List<Vector3>{p,end},.0008f,.00055f,Mathf.Max(4,vineSides-1),2,true);
            Vector3 dir = (outward + Vector3.up*(.25f-.35f*frac)).normalized;
            AddLeaf(b,end,dir,length,width,level <= 1 || master, master ? 1.25f : (level == 0 ? 1f : (level == 1 ? .75f : .5f)));
        }

        int flowerRings = master ? 12 : new[] { 10,8,6,5 }[level];
        int flowerSides = master ? 40 : new[] { 32,24,16,12 }[level];
        for (int i = 0; i < flowerCount; i++)
        {
            int v = (i*2+1) % vineCount;
            Vector3[] path = vines[v];
            float frac = .45f + .42f * Frac(i*.381966f);
            int index = Mathf.Clamp(Mathf.RoundToInt(frac*(path.Length-1)),1,path.Length-2);
            Vector3 p = path[index];
            float az = i*2.22f+.6f;
            Vector3 dir = new Vector3(Mathf.Cos(az),.18f+.12f*(i%2),Mathf.Sin(az)).normalized;
            Vector3 basePoint = p+dir*.018f;
            AddSweep(b,new List<Vector3>{p,basePoint},.00075f,.0005f,Mathf.Max(4,vineSides-1),2,true);
            AddFlower(b,basePoint,dir,.85f+.08f*(i%3),flowerRings,flowerSides);
        }
        return b.Finish(master ? "MorningGloryTrellis_MASTER" : "MorningGloryTrellis_LOD"+level);
    }

    private static void AddSoil(MeshBuilder b, int segments)
    {
        List<int> top = new List<int>(), bottom = new List<int>();
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.PI*2f*i/segments;
            float r = .101f*(1f-.006f*(.5f+.5f*Mathf.Sin(7f*a+1.2f)));
            float y = .203f+.0012f*Mathf.Sin(3f*a)*Mathf.Sin(5f*a+.6f);
            top.Add(b.Vertex(new Vector3(r*Mathf.Cos(a),y,r*Mathf.Sin(a)),Vector3.up));
            bottom.Add(b.Vertex(new Vector3(r*Mathf.Cos(a),.190f,r*Mathf.Sin(a)),Vector3.down));
        }
        int ct=b.Vertex(new Vector3(0,.203f,0),Vector3.up), cb=b.Vertex(new Vector3(0,.190f,0),Vector3.down);
        for (int i=0;i<segments;i++)
        {
            int k=(i+1)%segments;
            b.Tri(ct,top[i],top[k],7); b.Tri(cb,bottom[k],bottom[i],7);
            float a=Mathf.PI*2f*i/segments, z=Mathf.PI*2f*k/segments;
            Vector3 n1=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)), n2=new Vector3(Mathf.Cos(z),0,Mathf.Sin(z));
            int a0=b.Vertex(b.Position(top[i]),n1), a1=b.Vertex(b.Position(top[k]),n2), a2=b.Vertex(b.Position(bottom[k]),n2), a3=b.Vertex(b.Position(bottom[i]),n1);
            b.Quad(a0,a1,a2,a3,7);
        }
    }

    private static void AddLeaf(MeshBuilder b, Vector3 origin, Vector3 direction, float length, float width, bool heroVeins, float detail)
    {
        Vector2[] profile =
        {
            new Vector2(0,-.07f),new Vector2(-.27f,-.005f),new Vector2(-.50f,.12f),new Vector2(-.60f,.30f),new Vector2(-.53f,.43f),new Vector2(-.31f,.48f),new Vector2(-.34f,.62f),new Vector2(-.22f,.78f),new Vector2(0,1),
            new Vector2(.22f,.78f),new Vector2(.34f,.62f),new Vector2(.31f,.48f),new Vector2(.53f,.43f),new Vector2(.60f,.30f),new Vector2(.50f,.12f),new Vector2(.27f,-.005f)
        };
        Vector3 d=direction.normalized, lateral=Vector3.Cross(Vector3.up,d);
        if (lateral.sqrMagnitude<1e-8f) lateral=Vector3.right; lateral.Normalize();
        Vector3 normal=Vector3.Cross(d,lateral).normalized;
        const float thickness=.00038f;
        Func<Vector2,float,Vector3> P=(q,z)=>origin+lateral*(q.x*width)+d*(q.y*length)+normal*z;
        Vector2 centre=new Vector2(0,.40f);
        int topCentre=b.Vertex(P(centre,thickness*.5f),normal), bottomCentre=b.Vertex(P(centre,-thickness*.5f),-normal);
        int[] top=new int[profile.Length], bottom=new int[profile.Length];
        for(int i=0;i<profile.Length;i++)
        {
            float arch=.0018f*Mathf.Sin(Mathf.PI*Mathf.Clamp01(profile[i].y));
            top[i]=b.Vertex(P(profile[i],thickness*.5f+arch),normal);
            bottom[i]=b.Vertex(P(profile[i],-thickness*.5f),-normal);
        }
        for(int i=0;i<profile.Length;i++)
        {
            int k=(i+1)%profile.Length;
            b.Tri(topCentre,top[i],top[k],3); b.Tri(bottomCentre,bottom[k],bottom[i],3);
            Vector2 edge=profile[k]-profile[i]; Vector2 s=new Vector2(edge.y,-edge.x).normalized;
            Vector3 side=(lateral*s.x+d*s.y).normalized;
            int a=b.Vertex(P(profile[i],-thickness*.5f),side), c=b.Vertex(P(profile[k],-thickness*.5f),side), e=b.Vertex(P(profile[k],thickness*.5f),side), f=b.Vertex(P(profile[i],thickness*.5f),side);
            b.Quad(a,c,e,f,3);
        }
        if(heroVeins)
        {
            List<Vector3> mid=new List<Vector3>();
            for(int i=0;i<6;i++){float t=Mathf.Lerp(.02f,.86f,i/5f);mid.Add(P(new Vector2(0,t),thickness*.5f+.0013f*(1f-t)));}
            AddSweep(b,mid,.00075f,.00023f,Mathf.Max(5,Mathf.RoundToInt(6f*detail)),4,true);
        }
    }

    private static void AddFlower(MeshBuilder b, Vector3 origin, Vector3 direction, float scale, int rings, int sides)
    {
        Vector3 d=direction.normalized, lateral=Vector3.Cross(Vector3.up,d);
        if(lateral.sqrMagnitude<1e-8f) lateral=Vector3.right; lateral.Normalize();
        Vector3 normal=Vector3.Cross(d,lateral).normalized;
        int[][] ids=new int[rings+1][];
        float length=.070f*scale;
        for(int r=0;r<=rings;r++)
        {
            float t=r/(float)rings, z=t*length, baseR=(.004f*(1f-t)+.034f*Mathf.Pow(t,1.65f))*scale;
            ids[r]=new int[sides];
            for(int i=0;i<sides;i++)
            {
                float a=Mathf.PI*2f*i/sides, radius=baseR*(1f+.075f*t*t*t*Mathf.Cos(5f*a));
                Vector3 radial=lateral*Mathf.Cos(a)+normal*Mathf.Sin(a), p=origin+d*z+radial*radius;
                Vector3 n=(radial*.92f-d*(t<.7f?.28f:.12f)).normalized;
                ids[r][i]=b.Vertex(p,n);
            }
        }
        for(int r=0;r<rings;r++) for(int i=0;i<sides;i++) b.Quad(ids[r][i],ids[r][(i+1)%sides],ids[r+1][(i+1)%sides],ids[r+1][i],r<Mathf.Max(2,rings/3)?6:5);
        int centre=b.Vertex(origin,-d); for(int i=0;i<sides;i++) b.Tri(centre,ids[0][(i+1)%sides],ids[0][i],6);
    }

    private static void AddSweep(MeshBuilder b, List<Vector3> points, float r0, float r1, int sides, int material, bool cap)
    {
        int[][] rings=new int[points.Count][]; Vector3 previous=Vector3.zero;
        for(int i=0;i<points.Count;i++)
        {
            Vector3 t=(i==0?points[1]-points[0]:(i==points.Count-1?points[i]-points[i-1]:points[i+1]-points[i-1])).normalized;
            Vector3 n=previous==Vector3.zero?Vector3.Cross(t,Vector3.up):Vector3.ProjectOnPlane(previous,t);
            if(n.sqrMagnitude<1e-8f)n=Vector3.Cross(t,Vector3.right); n.Normalize(); previous=n;
            Vector3 bitangent=Vector3.Cross(t,n).normalized; float radius=Mathf.Lerp(r0,r1,i/(float)(points.Count-1)); rings[i]=new int[sides];
            for(int j=0;j<sides;j++){float a=Mathf.PI*2f*j/sides;Vector3 normal=n*Mathf.Cos(a)+bitangent*Mathf.Sin(a);rings[i][j]=b.Vertex(points[i]+normal*radius,normal);}
        }
        for(int i=0;i<points.Count-1;i++)for(int j=0;j<sides;j++)b.Quad(rings[i][j],rings[i][(j+1)%sides],rings[i+1][(j+1)%sides],rings[i+1][j],material);
        if(cap)
        {
            int a=b.Vertex(points[0],(points[0]-points[1]).normalized), z=b.Vertex(points[points.Count-1],(points[points.Count-1]-points[points.Count-2]).normalized);
            for(int j=0;j<sides;j++){b.Tri(a,rings[0][(j+1)%sides],rings[0][j],material);b.Tri(z,rings[rings.Length-1][j],rings[rings.Length-1][(j+1)%sides],material);}
        }
    }

    private static void AddTorus(MeshBuilder b, Vector3 centre, Vector3 axis, float major, float minor, int majorSteps, int minorSteps, int material)
    {
        axis.Normalize(); Vector3 n=Vector3.Cross(axis,Vector3.up); if(n.sqrMagnitude<1e-8f)n=Vector3.Cross(axis,Vector3.right); n.Normalize(); Vector3 bitangent=Vector3.Cross(axis,n).normalized;
        int[,] ids=new int[majorSteps,minorSteps];
        for(int i=0;i<majorSteps;i++)
        {
            float a=Mathf.PI*2f*i/majorSteps; Vector3 radial=n*Mathf.Cos(a)+bitangent*Mathf.Sin(a), ringCentre=centre+radial*major;
            for(int j=0;j<minorSteps;j++){float q=Mathf.PI*2f*j/minorSteps;Vector3 normal=radial*Mathf.Cos(q)+axis*Mathf.Sin(q);ids[i,j]=b.Vertex(ringCentre+normal*minor,normal);}
        }
        for(int i=0;i<majorSteps;i++)for(int j=0;j<minorSteps;j++)b.Quad(ids[i,j],ids[i,(j+1)%minorSteps],ids[(i+1)%majorSteps,(j+1)%minorSteps],ids[(i+1)%majorSteps,j],material);
    }

    private static float Frac(float v) { return v-Mathf.Floor(v); }

    private static void SaveMesh(Mesh source, string path)
    {
        Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null) AssetDatabase.CreateAsset(source,path);
        else { EditorUtility.CopySerialized(source,existing); UnityEngine.Object.DestroyImmediate(source); EditorUtility.SetDirty(existing); }
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts=folder.Split('/'); string current=parts[0];
        for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}
    }

    private sealed class MeshBuilder
    {
        private readonly List<Vector3> vertices=new List<Vector3>();
        private readonly List<Vector3> normals=new List<Vector3>();
        private readonly List<int>[] triangles;
        public MeshBuilder(int materialCount){triangles=new List<int>[materialCount];for(int i=0;i<materialCount;i++)triangles[i]=new List<int>();}
        public int Vertex(Vector3 p, Vector3 n){vertices.Add(p);normals.Add(n.normalized);return vertices.Count-1;}
        public Vector3 Position(int index){return vertices[index];}
        public void Tri(int a,int b,int c,int material)
        {
            Vector3 cross=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]); if(cross.sqrMagnitude<1e-20f)return;
            if(Vector3.Dot(cross,normals[a]+normals[b]+normals[c])<0f){int t=b;b=c;c=t;}
            triangles[material].Add(a);triangles[material].Add(b);triangles[material].Add(c);
        }
        public void Quad(int a,int b,int c,int d,int material){Tri(a,b,c,material);Tri(a,c,d,material);}
        public Mesh Finish(string name)
        {
            Mesh mesh=new Mesh { name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16 };
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.subMeshCount=triangles.Length;
            for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i,false);
            mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
    }
}
