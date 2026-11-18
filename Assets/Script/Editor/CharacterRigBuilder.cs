using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Non-destructive, Y-up generic rigs for the nine office characters.</summary>
public static class CharacterRigBuilder
{
    public const string Output = "Assets/Models_Characters/Animated";
    private sealed class Spec
    {
        public string id;
        public float height, hip, knee, shoulder, neck, armX, elbowX, wristX, wristY, legX, hem;
        public Spec(string id, float height, float hip, float knee, float shoulder, float neck,
            float armX, float elbowX, float wristX, float wristY, float legX, float hem = 0)
        { this.id=id; this.height=height; this.hip=hip; this.knee=knee; this.shoulder=shoulder; this.neck=neck;
          this.armX=armX; this.elbowX=elbowX; this.wristX=wristX; this.wristY=wristY; this.legX=legX; this.hem=hem; }
    }
    private static readonly Spec[] Specs = {
        new Spec("Female_01",1.72f,.51f,.26f,.74f,.81f,.105f,.235f,.34f,.74f,.055f),
        new Spec("Female_02",1.55f,.33f,.105f,.51f,.62f,.135f,.235f,.29f,.49f,.055f,.155f),
        new Spec("Female_03",1.52f,.26f,.075f,.46f,.59f,.15f,.27f,.34f,.425f,.055f,.10f),
        new Spec("Male_01",1.76f,.45f,.225f,.70f,.79f,.14f,.29f,.40f,.70f,.085f),
        new Spec("Male_03",1.60f,.29f,.14f,.50f,.59f,.17f,.31f,.40f,.50f,.075f),
        new Spec("Male_04",1.73f,.45f,.22f,.66f,.77f,.125f,.26f,.39f,.64f,.075f),
        new Spec("Male_05",1.68f,.41f,.22f,.63f,.72f,.15f,.29f,.40f,.59f,.13f),
        new Spec("Male_06",1.80f,.50f,.26f,.74f,.83f,.11f,.25f,.35f,.74f,.065f),
        new Spec("Male_07",1.72f,.31f,.15f,.62f,.72f,.24f,.36f,.45f,.56f,.10f)
    };
    private static readonly string[] BoneNames = { "Hips","Spine","Chest","Neck","Head",
        "LeftShoulder","LeftUpperArm","LeftForearm","LeftHand","RightShoulder","RightUpperArm","RightForearm","RightHand",
        "LeftThigh","LeftShin","LeftFoot","RightThigh","RightShin","RightFoot" };
    private static readonly int[] Parents = { -1,0,1,2,3,2,5,6,7,2,9,10,11,0,13,14,0,16,17 };
    private static readonly string[] Motions = Enum.GetNames(typeof(NPCCharacterAnimation.Motion));

    [MenuItem("Tools/The Elevator/Characters/Build All Rigs and Animations")]
    public static void BuildAll()
    {
        foreach (var s in Specs) BuildOne(s.id);
        AssetDatabase.SaveAssets();
        Debug.Log("Built all 9 character rigs, 99 clips and 9 controllers.");
    }

    public static string BuildOne(string id)
    {
        var s = Specs.First(x=>x.id==id);
        int index = Array.IndexOf(Specs,s);
        var sourcePath = Directory.GetFiles("Assets/Models_Characters/"+id,"*.fbx").Single().Replace('\\','/');
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        var filter = source.GetComponentInChildren<MeshFilter>();
        string folder = Output+"/"+id;
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var root = new GameObject(id+"_Animated");
        try
        {
            int layer = LayerMask.NameToLayer("NPC");
            root.layer = layer < 0 ? 0 : layer;
            var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform,false);
            var rig = new GameObject("Rig").transform; rig.SetParent(visual.transform,false);
            var positions = Positions(s);
            var bones = new Transform[BoneNames.Length];
            for(int b=0;b<bones.Length;b++)
            {
                bones[b] = new GameObject(BoneNames[b]).transform;
                bones[b].SetParent(Parents[b]<0 ? rig : bones[Parents[b]],false);
                bones[b].position = positions[b]*s.height;
            }
            var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            mesh.name = id+"_Skin";
            var vertices = mesh.vertices;
            var matrix = filter.transform.localToWorldMatrix;
            for(int v=0;v<vertices.Length;v++) vertices[v] = matrix.MultiplyPoint3x4(vertices[v]);
            float minY=vertices.Min(v=>v.y), h=vertices.Max(v=>v.y)-minY;
            var normals=mesh.normals; var tangents=mesh.tangents;
            var weights=new BoneWeight[vertices.Length];
            for(int v=0;v<vertices.Length;v++)
            {
                var normalized=(vertices[v]-new Vector3(0,minY,0))/h;
                vertices[v]=normalized*s.height;
                weights[v]=Weight(normalized,s);
                if(normals.Length>v) normals[v]=matrix.MultiplyVector(normals[v]).normalized;
                if(tangents.Length>v)
                {
                    var t=matrix.MultiplyVector(new Vector3(tangents[v].x,tangents[v].y,tangents[v].z)).normalized;
                    tangents[v]=new Vector4(t.x,t.y,t.z,tangents[v].w);
                }
            }
            mesh.vertices=vertices; mesh.normals=normals; mesh.tangents=tangents;
            mesh.boneWeights=weights;
            mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*visual.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds();
            SaveAsset(mesh,folder+"/"+id+"_Skin.asset");
            var renderer=visual.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/"+id+"_Skin.asset");
            // Bounds are authored in rig space; a moving Hips root offsets them
            // upward and can cull feet or the whole character near screen edges.
            renderer.bones=bones; renderer.rootBone=rig;
            renderer.quality=SkinQuality.Bone4;
            renderer.updateWhenOffscreen=false;
            renderer.localBounds=new Bounds(new Vector3(0,s.height*.5f,0),new Vector3(s.height*1.7f,s.height*1.9f,s.height*1.5f));
            renderer.sharedMaterials=new[]{MakeMaterial(s,source.GetComponentInChildren<Renderer>().sharedMaterial,folder)};
            var animator=visual.AddComponent<Animator>();
            animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
            var controllerPath=folder+"/"+id+".controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            else
            {
                var previous=controller.layers[0].stateMachine;
                foreach(var state in previous.states) previous.RemoveState(state.state);
                foreach(var transition in previous.anyStateTransitions) previous.RemoveAnyStateTransition(transition);
                controller.parameters=new AnimatorControllerParameter[0];
            }
            controller.AddParameter("Motion",AnimatorControllerParameterType.Int);
            controller.AddParameter("PlaybackSpeed",AnimatorControllerParameterType.Float);
            var controllerParameters=controller.parameters;
            controllerParameters[1].defaultFloat=1;
            controller.parameters=controllerParameters;
            var sm=controller.layers[0].stateMachine;
            for(int m=0;m<Motions.Length;m++)
            {
                var clip=MakeClip(s,index,m,bones,visual.transform);
                string clipPath=folder+"/"+Motions[m]+".anim";
                SaveAsset(clip,clipPath);
                var state=sm.AddState(Motions[m],new Vector3(260+(m%3)*220,(m/3)*70));
                state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                state.speedParameter="PlaybackSpeed"; state.speedParameterActive=true;
                if(m==0) sm.defaultState=state;
                var transition=sm.AddAnyStateTransition(state);
                transition.hasExitTime=false; transition.duration=m==4?.08f:.14f;
                transition.hasFixedDuration=true; transition.canTransitionToSelf=false;
                transition.AddCondition(AnimatorConditionMode.Equals,m,"Motion");
            }
            animator.runtimeAnimatorController=controller;
            var driver=root.AddComponent<NPCCharacterAnimation>();
            var serialized=new SerializedObject(driver);
            serialized.FindProperty("animator").objectReferenceValue=animator;
            serialized.FindProperty("favouriteGesture").intValue=index%6;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<NPCQueueAgent>();
            var capsule=root.AddComponent<CapsuleCollider>();
            capsule.height=s.height; capsule.radius=Mathf.Clamp(s.armX*s.height*1.2f,.18f,.36f);
            capsule.center=new Vector3(0,s.height*.5f,0);
            visual.layer=root.layer;
            foreach(var b in bones) b.gameObject.layer=root.layer;
            string prefabPath=folder+"/"+id+"_Animated.prefab";
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            AssetDatabase.SaveAssets();
            return prefabPath;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void SaveAsset(UnityEngine.Object asset,string path)
    {
        var previous=AssetDatabase.LoadMainAssetAtPath(path);
        if(previous==null) AssetDatabase.CreateAsset(asset,path);
        else { EditorUtility.CopySerialized(asset,previous); EditorUtility.SetDirty(previous); UnityEngine.Object.DestroyImmediate(asset); }
    }

    private static Material MakeMaterial(Spec s,Material original,string folder)
    {
        var mat=new Material(original.shader) { name=s.id };
        string textureFolder=null;
        if(s.id=="Male_05") textureFolder="Assets/TheElevatorLobby/Prefabs/model/man5";
        if(s.id=="Male_07") textureFolder="Assets/TheElevatorLobby/Prefabs/model/man2";
        var texture=original.mainTexture;
        if(textureFolder!=null)
        {
            string path=Directory.GetFiles(textureFolder,"*basecolor.jpg",SearchOption.AllDirectories).Single().Replace('\\','/');
            texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        mat.SetTexture("_BaseMap",texture); mat.SetTexture("_MainTex",texture);
        mat.SetColor("_BaseColor",Color.white); mat.SetFloat("_Metallic",0); mat.SetFloat("_Smoothness",.22f);
        mat.enableInstancing=true;
        SaveAsset(mat,folder+"/"+s.id+".mat");
        return AssetDatabase.LoadAssetAtPath<Material>(folder+"/"+s.id+".mat");
    }

    private static Vector3[] Positions(Spec s)
    {
        float spine=Mathf.Lerp(s.hip,s.shoulder,.42f),chest=s.shoulder-.045f;
        var p=new Vector3[19];
        p[0]=new Vector3(0,s.hip,0); p[1]=new Vector3(0,spine,0); p[2]=new Vector3(0,chest,0);
        p[3]=new Vector3(0,s.neck,0); p[4]=new Vector3(0,s.neck+.035f,0);
        for(int side=0;side<2;side++)
        {
            float sign=side==0?1:-1; int b=5+side*4;
            p[b]=new Vector3(sign*s.armX*.6f,s.shoulder,0);
            p[b+1]=new Vector3(sign*s.armX,s.shoulder,0);
            p[b+2]=new Vector3(sign*s.elbowX,Mathf.Lerp(s.shoulder,s.wristY,.55f),0);
            p[b+3]=new Vector3(sign*s.wristX,s.wristY,0);
            int leg=13+side*3;
            p[leg]=new Vector3(sign*s.legX,s.hip,0);
            p[leg+1]=new Vector3(sign*s.legX,s.knee,.005f);
            p[leg+2]=new Vector3(sign*s.legX,.045f,.015f);
        }
        return p;
    }

    private static float Smooth(float a,float b,float value) { return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,value)); }
    private static BoneWeight Weight(Vector3 p,Spec s)
    {
        // T-pose regions isolate limbs from the large stylized heads and skirts.
        var w=new float[19];
        float ax=Mathf.Abs(p.x);
        float distal=Mathf.InverseLerp(s.armX+.04f,s.elbowX,ax);
        float armY=Mathf.Lerp(s.shoulder,s.wristY,Mathf.InverseLerp(s.armX,s.wristX,ax));
        float top=armY+Mathf.Lerp(.05f,.14f,distal);
        float bottom=armY-Mathf.Lerp(s.id=="Male_07"?.11f:.08f,.14f,distal);
        float arm = Smooth(bottom-.025f,bottom,p.y)*(1-Smooth(top,top+.025f,p.y))*Smooth(s.armX*.80f,s.armX+.045f,ax);
        if(ax<s.elbowX*.85f) arm*=1-Smooth(s.neck-.035f,s.neck-.005f,p.y);
        int upper=p.x>=0?6:10;
        float elbow=Smooth(s.elbowX-.035f,s.elbowX+.035f,ax);
        float hand=Smooth(s.wristX-.025f,s.wristX+.025f,ax);
        w[upper]+=arm*(1-elbow);w[upper+1]+=arm*elbow*(1-hand);w[upper+2]+=arm*elbow*hand;
        float body=1-arm;
        float leg=1-Smooth(s.hip-.07f,s.hip+.025f,p.y);
        if(s.hem>0 && p.y>s.hem) leg=0; // skirt remains attached to pelvis, legs animate below hem
        float left=Smooth(-.018f,.018f,p.x);
        for(int side=0;side<2;side++)
        {
            int thigh=13+side*3;
            float part=body*leg*(side==0?left:1-left);
            float shin=1-Smooth(s.knee-.035f,s.knee+.035f,p.y);
            float foot=1-Smooth(.04f,.085f,p.y);
            w[thigh]+=part*(1-shin);w[thigh+1]+=part*shin*(1-foot);w[thigh+2]+=part*shin*foot;
        }
        float torso=body*(1-leg);
        var ys=new[]{s.hip,Mathf.Lerp(s.hip,s.shoulder,.42f),s.shoulder-.045f,s.neck-.018f,s.neck+.018f};
        if(p.y<=ys[0]) w[0]+=torso;
        else if(p.y>=ys[4]) w[4]+=torso;
        else for(int b=0;b<4;b++) if(p.y>=ys[b] && p.y<ys[b+1])
        { float blend=Smooth(ys[b],ys[b+1],p.y);w[b]+=torso*(1-blend);w[b+1]+=torso*blend;break; }
        // Retain four strongest influences, normalized for stable GPU skinning.
        int[] ids={0,0,0,0};float[] values={0,0,0,0};
        for(int b=0;b<w.Length;b++) for(int slot=0;slot<4;slot++) if(w[b]>values[slot])
        { for(int j=3;j>slot;j--){values[j]=values[j-1];ids[j]=ids[j-1];}values[slot]=w[b];ids[slot]=b;break; }
        float total=values.Sum();
        if(total<.0001f) return new BoneWeight{boneIndex0=0,weight0=1};
        return new BoneWeight{boneIndex0=ids[0],boneIndex1=ids[1],boneIndex2=ids[2],boneIndex3=ids[3],
            weight0=values[0]/total,weight1=values[1]/total,weight2=values[2]/total,weight3=values[3]/total};
    }

    private static AnimationClip MakeClip(Spec s,int character,int motion,Transform[] bones,Transform root)
    {
        float duration=motion==0?3f:motion==1?.9f:motion==2?1.15f:motion==3?.8f:motion==4?.65f:1.8f;
        int frames=Mathf.RoundToInt(duration*30);
        var clip=new AnimationClip {name=Motions[motion],frameRate=30};
        var curves=new AnimationCurve[19,4];
        for(int b=0;b<19;b++)for(int a=0;a<4;a++)curves[b,a]=new AnimationCurve();
        var yCurve=new AnimationCurve(); var xCurve=new AnimationCurve();
        for(int f=0;f<=frames;f++)
        {
            float t=(float)f/frames;float wave=Mathf.Sin(t*Mathf.PI*2),cos=Mathf.Cos(t*Mathf.PI*2);
            float pulse=Mathf.Pow(Mathf.Sin(t*Mathf.PI),2);float style=.92f+character*.018f;
            var e=new Vector3[19];
            float restingArmAngle=s.id=="Male_07"?65:78;
            e[6]=new Vector3(0,0,-restingArmAngle); e[10]=new Vector3(0,0,restingArmAngle);
            e[7].y=-8; e[11].y=8;
            float y=0,x=0;
            if(motion==0)
            {
                y=.003f*wave; e[1].x=1.5f*wave; e[4].y=2.5f*wave;
                e[6].z-=2*wave;e[10].z+=2*wave;
            }
            else if(motion==1)
            {
                y=.012f*(1-cos*cos); x=.007f*wave;e[0].z=3*wave;e[2].y=-4*wave;
                e[13].x=-23*wave*style;e[16].x=23*wave*style;
                e[14].x=34*Mathf.Max(0,-wave);e[17].x=34*Mathf.Max(0,wave);
                e[15].x=-12*Mathf.Max(0,-wave);e[18].x=-12*Mathf.Max(0,wave);
                e[6].x=18*wave;e[10].x=-18*wave;e[4].y=2*wave;
            }
            else if(motion==2)
            {
                e[0].z=4*wave;e[2].x=-5;e[4].x=-12;e[4].z=6*wave;
                e[6]=new Vector3(14*wave,12,-35-10*wave);e[10]=new Vector3(-14*wave,-12,35-10*wave);
                e[7].y=-35-16*wave;e[11].y=35-16*wave;
                e[13]=new Vector3(-18+15*wave,0,8);e[16]=new Vector3(-18-15*wave,0,-8);
                e[14].x=38+15*wave;e[17].x=38-15*wave;
                e[8].x=18*wave;e[12].x=-18*wave;
            }
            else if(motion==3)
            {
                e[2].x=-8;e[4].x=-18;e[0].z=7*wave;
                e[6]=new Vector3(20*wave,-12,-12+12*wave);e[10]=new Vector3(-20*wave,12,12+12*wave);
                e[7].y=-65+20*wave;e[11].y=65+20*wave;
                e[13]=new Vector3(-30+22*wave,0,12);e[16]=new Vector3(-30-22*wave,0,-12);
                e[14].x=45+20*wave;e[17].x=45-20*wave;
            }
            else if(motion==4)
            {
                float impact=t<.22f?Mathf.Sin(t/.22f*Mathf.PI*.5f):1-Smooth(.22f,1,t);
                y=-.055f*impact;e[0].x=8*impact;e[2].x=12*impact;e[4].x=-8*impact;
                e[13].x=e[16].x=-28*impact;e[14].x=e[17].x=55*impact;e[15].x=e[18].x=-20*impact;
                e[6].z+=28*impact;e[10].z-=28*impact;
            }
            else
            {
                switch(motion)
                {
                    case 5: e[4].y=22*Mathf.Sin(t*Mathf.PI*6)*pulse;e[4].z=6*wave*pulse;break;
                    case 6: e[10].z-=35*pulse;e[11].y=8+45*pulse;e[12].x=40*Mathf.Sin(t*Mathf.PI*10)*pulse;e[2].y=-7*pulse;break;
                    case 7: e[16].x=-14*pulse;e[16].z=-8*pulse;e[17].x=22*pulse;e[18].x=22*Mathf.Sin(t*Mathf.PI*8)*pulse;x=.025f*pulse;break;
                    case 8: e[4].y=32*wave*pulse;e[2].y=10*wave*pulse;e[4].x=-5*pulse;break;
                    case 9: e[6].z+=62*pulse;e[10].z-=62*pulse;e[7].y=-8-35*pulse;e[11].y=8+35*pulse;e[2].x=-9*pulse;e[4].x=-10*pulse;break;
                    case 10: e[5].z=-10*pulse;e[9].z=10*pulse;e[6].z+=15*pulse;e[10].z-=15*pulse;e[4].z=12*pulse;y=.007f*pulse;break;
                }
            }
            for(int b=0;b<19;b++)
            {
                Quaternion q=Quaternion.Euler(e[b]);
                curves[b,0].AddKey(t*duration,q.x);curves[b,1].AddKey(t*duration,q.y);
                curves[b,2].AddKey(t*duration,q.z);curves[b,3].AddKey(t*duration,q.w);
            }
            yCurve.AddKey(t*duration,(s.hip+y)*s.height);xCurve.AddKey(t*duration,x*s.height);
        }
        for(int b=0;b<19;b++)
        {
            string path=AnimationUtility.CalculateTransformPath(bones[b],root);
            for(int a=0;a<4;a++) AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[a]),curves[b,a]);
        }
        string hipPath=AnimationUtility.CalculateTransformPath(bones[0],root);
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(hipPath,typeof(Transform),"m_LocalPosition.y"),yCurve);
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(hipPath,typeof(Transform),"m_LocalPosition.x"),xCurve);
        clip.EnsureQuaternionContinuity();
        var settings=AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime=motion<4;settings.loopBlend=motion<4;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=true;
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        return clip;
    }

    public static string[] PrefabPaths() { return Specs.Select(s=>Output+"/"+s.id+"/"+s.id+"_Animated.prefab").ToArray(); }

    [MenuItem("Tools/The Elevator/Characters/Connect Lobby Spawn Pool")]
    public static void ConnectLobby()
    {
        foreach(var manager in UnityEngine.Object.FindObjectsByType<NPCQueueManager>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            Undo.RecordObject(manager,"Use all nine animated characters");
            manager.npcPrefabs=PrefabPaths().Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    public static string ContactSheet(string motion,float time,string outputPath)
    {
        const int size=360;
        var atlas=new Texture2D(size*3,size*3,TextureFormat.RGB24,false);
        for(int i=0;i<Specs.Length;i++)
        {
            var s=Specs[i];var preview=new PreviewRenderUtility();
            try
            {
                var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths()[i]));
                var animator=go.GetComponentInChildren<Animator>();animator.enabled=false;
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Output+"/"+s.id+"/"+motion+".anim");
                clip.SampleAnimation(go.transform.Find("Visual").gameObject,time);
                preview.AddSingleGO(go);
                preview.camera.transform.position=new Vector3(0,s.height*.5f,4);
                preview.camera.transform.rotation=Quaternion.Euler(0,180,0);
                preview.camera.orthographic=true;preview.camera.orthographicSize=s.height*.63f;
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=20;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.10f,.13f,.18f);
                preview.lights[0].transform.rotation=Quaternion.Euler(30,210,0);preview.lights[0].intensity=1.4f;
                preview.lights[1].intensity=.7f;preview.ambientColor=new Color(.45f,.45f,.45f);
                preview.BeginStaticPreview(new Rect(0,0,size,size));preview.Render(true);
                var image=preview.EndStaticPreview();
                atlas.SetPixels(i%3*size,(2-i/3)*size,size,size,image.GetPixels());UnityEngine.Object.DestroyImmediate(image);
            }
            finally {preview.Cleanup();}
        }
        atlas.Apply();Directory.CreateDirectory(Path.GetDirectoryName(outputPath));File.WriteAllBytes(outputPath,atlas.EncodeToJPG(92));
        UnityEngine.Object.DestroyImmediate(atlas);return outputPath;
    }

    [MenuItem("Tools/The Elevator/Characters/Create Animation Preview Scene")]
    public static void CreateDemo()
    {
        var demo=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(demo);
        var cameraGo=new GameObject("Preview Camera");var camera=cameraGo.AddComponent<Camera>();
        camera.transform.position=new Vector3(0,9f,10);camera.transform.LookAt(new Vector3(0,.8f,0));
        camera.orthographic=true;camera.orthographicSize=3.7f;camera.backgroundColor=new Color(.08f,.11f,.16f);camera.clearFlags=CameraClearFlags.SolidColor;
        var light=new GameObject("Key Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(40,210,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
        for(int i=0;i<Specs.Length;i++)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths()[i]),demo);
            go.name=Specs[i].id;go.transform.position=new Vector3((i%3-1)*2.9f,0,(1-i/3)*3.3f);
            go.GetComponent<NPCCharacterAnimation>().enabled=false;go.GetComponent<NPCQueueAgent>().enabled=false;
            var driver=go.AddComponent<CharacterAnimationDemo>();driver.animator=go.GetComponentInChildren<Animator>();
            var caption=new GameObject("Caption").AddComponent<TextMesh>();caption.transform.position=go.transform.position+new Vector3(0,.01f,-.6f);
            caption.transform.rotation=Quaternion.Euler(90,180,0);caption.anchor=TextAnchor.MiddleCenter;caption.alignment=TextAlignment.Center;
            caption.characterSize=.07f;caption.fontSize=48;caption.text=go.name+"\nIDLE";driver.caption=caption;
            var pedestal=GameObject.CreatePrimitive(PrimitiveType.Cylinder);pedestal.name=go.name+" Platform";
            pedestal.transform.position=go.transform.position+new Vector3(0,-.07f,0);pedestal.transform.localScale=new Vector3(1.8f,.06f,1.8f);
        }
        string path=Output+"/Character_Animation_Preview.unity";
        EditorSceneManager.SaveScene(demo,path);EditorSceneManager.CloseScene(demo,true);
    }
}
