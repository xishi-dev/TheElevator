using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class LobbyEnvironmentBuilder
{
    public const string R = "Assets/TheElevatorLobby";
    static Transform root, arch, floor, walls, ceiling, cols, left, right, lift, props, lights, cams;
    static Font font;
    static Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static System.Random rng = new System.Random(2707);
    static Color C(float r,float g,float b) { return new Color(r,g,b,1); }
    static Vector3 V(float x,float y,float z) { return new Vector3(x,y,z); }

    [MenuItem("Tools/The Elevator Lobby/01 Build Architecture")]
    public static void Architecture()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Elevator_Lobby_Main") throw new Exception("Open dedicated lobby scene first.");
        if (GameObject.Find("Environment_Root")) throw new Exception("Scene already contains a lobby; refusing to overwrite.");
        InitMaterials();
        root=Group("Environment_Root",null); arch=Group("Architecture",root);
        floor=Group("Floor",arch); walls=Group("Walls",arch); ceiling=Group("Ceiling",arch); cols=Group("Columns",arch);
        left=Group("Left_Corridor",arch); right=Group("Right_Corridor",arch);
        lift=Group("Elevator_Assembly",root); props=Group("Environment_Props",root); lights=Group("Lighting",root);
        cams=Group("Preview_Cameras",root);
        B("Structural_Floor_Slab",floor,0,-.17f,0,26,.3f,16,"Grout",true);
        TileField("Lobby_Stone",floor,V(0,-.025f,0),26,16,20,12,false);
        // Stone border and metal inset frame a quiet waiting zone without blocking movement.
        B("Waiting_Stone_Inlay",floor,0,.006f,1.5f,3.8f,.018f,9.4f,"InsetStone",false,.003f);
        foreach(float x in new[]{-1.92f,1.92f}) B("Waiting_Pewter_Inlay",floor,x,.018f,1.5f,.022f,.008f,9.45f,"Pewter",false,.002f);
        foreach(float z in new[]{-3.22f,6.22f}) B("Waiting_Pewter_End",floor,0,.018f,z,3.86f,.008f,.022f,"Pewter",false,.002f);
        foreach(float x in new[]{-11.9f,11.9f}) B("Perimeter_Stone_Border",floor,x,.005f,0,.55f,.015f,16,"InsetStone",false,.003f);
        // Rear structural wall leaves a precise mechanical doorway.
        foreach(float x in new[]{-7.23f,7.23f}) B("Rear_Structural_Wall",walls,x,2.25f,7.83f,11.54f,4.5f,.34f,"Plaster",true);
        B("Lift_Opening_Lintel",walls,0,3.86f,7.83f,2.92f,1.28f,.34f,"Plaster",true);
        // Side walls stop at the open cross-corridor portals.
        foreach(float s in new[]{-1f,1f}) {
            B("Side_Wall_Approach",walls,s*13.16f,2.25f,-3.25f,.32f,4.5f,9.5f,"Plaster",true);
            B("Side_Wall_Rear_Return",walls,s*13.16f,2.25f,6.91f,.32f,4.5f,2.18f,"Plaster",true);
            B("Side_Portal_Head",walls,s*13.16f,4.08f,3.6f,.32f,.84f,4.2f,"Plaster",true);
            B("Entry_Wall",walls,s*8.65f,2.25f,-8.16f,8.7f,4.5f,.32f,"Plaster",true);
        }
        B("Entry_Overdoor",walls,0,3.9f,-8.16f,8.6f,1.2f,.32f,"Plaster",true);
        // Recessed wainscoting with individual wall joints, coping and a shadowed base.
        for(int i=0;i<10;i++) {
            float x=-11.7f+i*2.6f;
            if(Mathf.Abs(x)<4) continue;
            Panel(walls,V(x,0,7.625f),0,2.56f,2.45f);
        }
        foreach(float s in new[]{-1f,1f}) for(int i=0;i<4;i++) {
            Transform p=Group("Side_Wall_Panel",walls);p.localPosition=V(s*12.97f,0,-6.8f+i*2.3f);p.localRotation=Quaternion.Euler(0,s*90,0);
            Panel(p,V(0,0,0),0,2.25f,2.38f);
        }
        // The central pier is an architectural assembly of recessed mineral panels.
        for(int i=0;i<8;i++) {
            float x=-4.375f+i*1.25f;
            if(Mathf.Abs(x)<1.5f) continue;
            B("Lift_Pier_Mineral_Panel",walls,x,2.15f,7.54f,1.23f,4.3f,.22f,"FeatureWall",true,.018f);
            B("Pier_Reveal",walls,x-.622f,2.15f,7.411f,.015f,4.3f,.014f,"Trim",false,.001f);
        }
        B("Lift_Pier_Header",walls,0,3.93f,7.50f,3.75f,1.06f,.26f,"FeatureWall",true);
        B("Lift_Pier_Coping",walls,0,4.36f,7.33f,10.05f,.12f,.20f,"Pewter");
        foreach(float s in new[]{-1f,1f}) {
            B("Pier_Vertical_End_Trim",walls,s*5.02f,2.18f,7.35f,.075f,4.36f,.16f,"Trim");
            B("Rear_Skirting",walls,s*7.3f,.085f,7.60f,11.35f,.17f,.07f,"Trim");
            B("Side_Skirting",walls,s*12.97f,.085f,-3.24f,.075f,.17f,9.5f,"Trim");
        }
        foreach(float x in new[]{-8.1f,8.1f}) foreach(float z in new[]{-5.3f,5.55f}) {
            Transform c=Group("Clad_Column",cols);c.localPosition=V(x,0,z);
            B("Column_Core",c,0,2.25f,0,.56f,4.5f,.56f,"Plaster",true,.035f);
            B("Column_Stone_Plinter",c,0,.11f,0,.64f,.22f,.64f,"InsetStone");
            B("Column_Metal_Collar",c,0,1.28f,0,.575f,.035f,.575f,"Pewter",false,.004f);
            B("Column_Capital",c,0,4.32f,0,.70f,.21f,.70f,"Ceiling");
        }
        B("Closed_Ceiling_Slab",ceiling,0,4.62f,0,26.4f,.24f,16.4f,"Ceiling",true);
        TileField("Acoustic_Ceiling",ceiling,V(0,4.485f,0),26,16,13,8,true);
        foreach(float s in new[]{-1f,1f}) {
            B("Ceiling_Perimeter_Cove",ceiling,s*12.65f,4.37f,0,.66f,.20f,16,"Plaster");
            B("Cove_Shadow_Reveal",ceiling,s*12.28f,4.43f,0,.055f,.055f,16,"Trim",false,.002f);
        }
        B("Rear_Ceiling_Cove",ceiling,0,4.37f,7.62f,26,.2f,.72f,"Plaster");
        Corridor(left,-1); Corridor(right,1);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("LOBBY_PHASE_2_COMPLETE: Architecture and connected corridors saved.");
    }

    [MenuItem("Tools/The Elevator Lobby/02 Build Details")]
    public static void Details() {
        Refs();
        if(lift.childCount>0)throw new Exception("Elevator already built.");
        Transform frame=Group("Elevator_Frame",lift);
        foreach(float s in new[]{-1f,1f}) {
            B("Structural_Jamb",frame,s*1.47f,1.58f,7.37f,.22f,3.16f,.36f,"Steel",true,.015f);
            B("Jamb_Inner_Gasket",frame,s*1.342f,1.53f,7.39f,.032f,3.04f,.26f,"Trim");
            B("Frame_Reveal",frame,s*1.607f,1.60f,7.33f,.025f,3.20f,.08f,"Trim",false,.003f);
            B("Warm_Vertical_Reveal",frame,s*1.64f,1.62f,7.37f,.021f,3.15f,.02f,"WarmGlow",false,.002f);
        }
        B("Header_Cassette",frame,0,3.145f,7.37f,3.16f,.23f,.36f,"Steel",true);
        B("Top_Door_Track",frame,0,3.035f,7.53f,2.68f,.052f,.22f,"Trim");
        B("Header_Fine_Reveal",frame,0,3.20f,7.178f,2.91f,.015f,.012f,"Trim",false,.002f);
        B("Sill_Steel",frame,0,.025f,7.39f,3.12f,.046f,.56f,"Steel",true,.006f);
        foreach(float z in new[]{7.27f,7.36f,7.45f,7.54f})B("Sill_Track_Groove",frame,0,.050f,z,2.71f,.005f,.012f,"Trim",false,.001f);
        foreach(float s in new[]{-1f,1f}) {
            Transform d=Group(s<0?"Elevator_LeftDoor":"Elevator_RightDoor",lift);d.localPosition=V(s*.654f,0,7.495f);
            B("Brushed_Door_Skin",d,0,1.54f,0,1.298f,2.98f,.084f,"Steel",true,.006f);
            B("Door_Edge_Fold",d,-s*.633f,1.54f,-.048f,.018f,2.95f,.016f,"Pewter",false,.002f);
            B("Lower_Service_Plate",d,0,.19f,-.047f,1.272f,.245f,.012f,"Pewter",false,.002f);
            B("Door_Fine_Grain_Seam",d,0,2.81f,-.045f,1.27f,.006f,.008f,"Pewter",false,.001f);
            foreach(float x in new[]{-.56f,.56f})foreach(float y in new[]{.1f,.28f}) {
                var screw=Cylinder("Flush_Service_Screw",d,V(x,y,-.057f),.006f,.004f,"Steel");screw.localRotation=Quaternion.Euler(90,0,0);
            }
            foreach(var t in d.GetComponentsInChildren<Transform>())t.gameObject.isStatic=false;
            PrefabUtility.SaveAsPrefabAssetAndConnect(d.gameObject,R+"/Prefabs/Architecture/"+d.name+".prefab",InteractionMode.AutomatedAction);
        }
        Transform cabin=Group("Elevator_Cabin",lift);
        B("Cabin_Floor",cabin,0,-.055f,9.08f,2.91f,.12f,3.14f,"InsetStone",true);
        TileField("Cabin_Tiles",cabin,V(0,.011f,9.08f),2.87f,3.10f,3,3,false);
        foreach(float s in new[]{-1f,1f}) {
            B("Cabin_Side_Shell",cabin,s*1.51f,1.55f,9.09f,.13f,3.1f,3.20f,"Steel",true);
            B("Cabin_Timber_Inset",cabin,s*1.43f,1.46f,9.14f,.025f,1.71f,2.87f,"SeatWood");
            B("Cabin_Handrail",cabin,s*1.35f,1.00f,9.14f,.055f,.052f,2.7f,"Steel",false,.02f);
            B("Cabin_Base_Rail",cabin,s*1.42f,.11f,9.14f,.04f,.18f,2.9f,"Pewter");
            B("Cabin_Corner_Trim",cabin,s*1.42f,1.55f,10.56f,.045f,3.04f,.045f,"Steel");
        }
        B("Cabin_Rear_Shell",cabin,0,1.55f,10.73f,3.14f,3.1f,.14f,"Steel",true);
        B("Cabin_Rear_Inset",cabin,0,1.52f,10.642f,2.84f,1.8f,.025f,"WallPanel");
        B("Cabin_Rear_Handrail",cabin,0,1.00f,10.53f,2.7f,.055f,.055f,"Steel",false,.02f);
        B("Cabin_Mirror_Panel",cabin,0,2.07f,10.616f,1.63f,.62f,.018f,"FrostedGlass");
        B("Cabin_Closed_Ceiling",cabin,0,3.15f,9.11f,3.15f,.12f,3.35f,"Ceiling",true);
        Fixture(cabin,V(0,3.06f,9.05f),false);
        Transform controls=Group("Cabin_Controls",cabin);controls.localPosition=V(1.413f,0,8.15f);controls.localRotation=Quaternion.Euler(0,90,0);
        B("Cabin_Control_Panel",controls,0,1.41f,0,.27f,.84f,.04f,"Pewter");
        for(int i=0;i<8;i++) {
            float x=(i%2==0?-.06f:.06f),y=1.69f-(i/2)*.145f;
            var btn=Cylinder("Floor_Button_"+(8-i),controls,V(x,y,-.03f),.032f,.014f,i==1?"WarmGlow":"Steel");btn.localRotation=Quaternion.Euler(90,0,0);
            Label((8-i).ToString(),controls,V(x,y,-.041f),.029f,"TextDark",.04f);
        }
        Label("LIFT 01",cabin,V(0,2.69f,10.62f),.10f,"TextLight",.9f);
        Transform call=Group("Elevator_CallPanel",lift);call.localPosition=V(1.99f,0,7.313f);
        B("Call_Panel_Recess",call,0,1.29f,0,.26f,.54f,.045f,"Trim");B("Call_Panel_Plate",call,0,1.29f,-.03f,.224f,.50f,.022f,"Steel");
        var ring=Cylinder("Illuminated_Call_Ring",call,V(0,1.29f,-.052f),.058f,.018f,"WarmGlow");ring.localRotation=Quaternion.Euler(90,0,0);
        var button=Cylinder("Call_Button",call,V(0,1.29f,-.064f),.042f,.018f,"Steel");button.localRotation=Quaternion.Euler(90,0,0);
        Label("^",call,V(0,1.295f,-.078f),.042f,"TextDark",.055f);
        Label("CALL",call,V(0,1.45f,-.049f),.048f,"TextDark",.17f);
        Label("LIFT 01",call,V(0,1.75f,-.01f),.064f,"TextLight",.40f);
        Transform indicator=Group("Elevator_Indicator",lift);
        B("Indicator_Bezel",indicator,0,3.39f,7.35f,.76f,.31f,.12f,"Pewter");
        B("Indicator_Screen",indicator,0,3.39f,7.276f,.66f,.23f,.018f,"Trim");
        Label("^  07",indicator,V(0,3.39f,7.26f),.159f,"TextLight",.54f);
        // Large, deliberate architectural typography establishes the focal wall.
        Label("THE ELEVATOR LOBBY",walls,V(0,4.02f,7.335f),.23f,"TextLight",6.9f);
        Label("N O R T H   T O W E R",walls,V(0,3.72f,7.333f),.083f,"TextLight",2.85f);
        Label("07",walls,V(-3.47f,2.30f,7.397f),.92f,"TextLight",1.72f);
        Label("LEVEL SEVEN",walls,V(-3.47f,1.55f,7.397f),.11f,"TextLight",1.65f);
        Label("NORTH TOWER",walls,V(3.44f,2.61f,7.397f),.145f,"TextLight",1.70f);
        Label("OFFICES  701 - 728",walls,V(3.44f,2.32f,7.397f),.094f,"TextLight",1.70f);
        B("Directory_Rule",walls,3.44f,2.12f,7.392f,1.7f,.012f,.015f,"Pewter",false,.002f);
        Label("RECEPTION   <\nMEETING ROOMS   >",walls,V(3.44f,1.83f,7.397f),.22f,"TextLight",1.70f);
        B("Lift_Information_Plaque",walls,-2.04f,1.28f,7.36f,.56f,.48f,.022f,"Pewter");
        Label("IN CASE OF FIRE\nUSE STAIRS",walls,V(-2.04f,1.30f,7.342f),.12f,"TextLight",.48f);
        Label("MAX  13 PERSONS / 1000 KG",walls,V(0,3.17f,7.17f),.038f,"TextDark",1.9f);
        foreach(float s in new[]{-1f,1f}) {
            Transform way=Group(s<0?"West_Wing_Wayfinding":"East_Wing_Wayfinding",props);way.localPosition=V(s*10.14f,3.35f,4.12f);
            B("Suspended_Sign",way,0,0,0,3.22f,.47f,.10f,"Trim");
            foreach(float x in new[]{-1.17f,1.17f})B("Sign_Hanger",way,x,.49f,0,.018f,.54f,.018f,"Pewter",false,.002f);
            Label(s<0?"<  WEST WING":"EAST WING  >",way,V(0,.067f,-.058f),.153f,"TextLight",2.86f);
            Label(s<0?"OFFICES 701 - 714":"OFFICES 715 - 728",way,V(0,-.128f,-.058f),.076f,"TextLight",2.70f);
        }
        for(int i=0;i<2;i++) {
            Transform bench=Bench(props);if(i==0)PrefabUtility.SaveAsPrefabAssetAndConnect(bench.gameObject,R+"/Prefabs/Props/Lobby_Bench.prefab",InteractionMode.AutomatedAction);
            bench.name="Waiting_Bench_"+(i==0?"West":"East");bench.localPosition=V(i==0?-6.0f:6.0f,0,1.05f);
        }
        // Ventilation, safety hardware, bins and discreet surveillance.
        for(int i=0;i<4;i++) {
            Transform vent=Group("Ceiling_Return_Grille",ceiling);
            B("Vent_Recess",vent,0,0,0,1.35f,.055f,.55f,"Trim");
            for(int j=0;j<10;j++)B("Vent_Aluminium_Slat",vent,0,-.033f,-.24f+j*.053f,1.27f,.025f,.018f,"Pewter",false,.002f);
            if(i==0)PrefabUtility.SaveAsPrefabAssetAndConnect(vent.gameObject,R+"/Prefabs/Props/Ceiling_Vent.prefab",InteractionMode.AutomatedAction);
            vent.localPosition=V(i<2?-9.8f:9.8f,4.42f,i%2==0?-3.5f:5.0f);
        }
        Transform fire=Group("Fire_Safety_Station",props);fire.localPosition=V(10.9f,0,7.55f);
        B("Safety_Mount",fire,0,1.02f,0,.42f,.92f,.035f,"Trim");
        Cylinder("Extinguisher_Cylinder",fire,V(0,.91f,-.14f),.12f,.53f,"SafetyRed");
        Cylinder("Extinguisher_Valve",fire,V(0,1.22f,-.14f),.036f,.09f,"Pewter");
        B("Extinguisher_Lever",fire,0,1.28f,-.14f,.19f,.025f,.04f,"Trim");
        B("Inspection_Label",fire,0,.94f,-.26f,.12f,.16f,.005f,"TextLight",false,.001f);
        Label("FIRE",fire,V(0,1.71f,-.025f),.12f,"TextLight",.43f);B("Safety_Header",fire,0,1.71f,.001f,.52f,.25f,.03f,"SafetyRed");
        Transform camera=Group("Security_Camera",props);camera.localPosition=V(-10.8f,3.78f,7.37f);camera.localRotation=Quaternion.Euler(16,-24,0);
        B("Wall_Bracket",camera,0,0,.10f,.12f,.24f,.18f,"Pewter");B("Camera_Housing",camera,0,-.08f,-.14f,.23f,.16f,.37f,"Ceiling");B("Camera_Face",camera,0,-.08f,-.334f,.20f,.13f,.018f,"Trim");
        var lens=Cylinder("Camera_Lens",camera,V(0,-.08f,-.349f),.046f,.025f,"FrostedGlass");lens.localRotation=Quaternion.Euler(90,0,0);
        foreach(float s in new[]{-1f,1f}) {
            Transform bin=Group("Waste_Sorting_Bin",props);bin.localPosition=V(s*11.6f,0,6.8f);
            B("Bin_Casing",bin,0,.45f,0,.38f,.9f,.38f,"Pewter",true,.055f);B("Bin_Dark_Opening",bin,0,.87f,-.02f,.27f,.07f,.26f,"Trim",false,.025f);
            Label("RECYCLE",bin,V(0,.57f,-.201f),.044f,"TextDark",.28f);
        }
        Transform entry=Group("Main_Entry_Exit_Sign",props);entry.localPosition=V(0,3.56f,-7.98f);entry.localRotation=Quaternion.Euler(0,180,0);
        B("Exit_Box",entry,0,0,0,1.1f,.26f,.10f,"ExitGlow");Label("<  EXIT",entry,V(0,0,-.06f),.15f,"TextLight",.97f);
        // Save a complete cabin for later elevator gameplay without merging it into the building.
        PrefabUtility.SaveAsPrefabAssetAndConnect(cabin.gameObject,R+"/Prefabs/Architecture/Elevator_Cabin.prefab",InteractionMode.AutomatedAction);
        PrefabUtility.SaveAsPrefabAssetAndConnect(frame.gameObject,R+"/Prefabs/Architecture/Elevator_Frame.prefab",InteractionMode.AutomatedAction);
        PrefabUtility.SaveAsPrefabAssetAndConnect(call.gameObject,R+"/Prefabs/Props/Elevator_CallPanel.prefab",InteractionMode.AutomatedAction);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        Debug.Log("LOBBY_PHASE_3_4_COMPLETE: Detailed elevator, props, wayfinding and materials saved.");
    }

    static Transform Bench(Transform p) {
        Transform b=Group("Lobby_Bench",p);
        foreach(float x in new[]{-1.04f,1.04f}) {
            B("Steel_Leg",b,x,.23f,0,.065f,.46f,.60f,"Trim",true,.018f);
            B("Floor_Foot",b,x,.035f,0,.19f,.07f,.68f,"Pewter");
            B("Back_Standard",b,x,.69f,.30f,.055f,.68f,.055f,"Trim");
        }
        B("Seat_Subframe",b,0,.39f,0,2.6f,.10f,.62f,"SeatWood",true,.025f);
        for(int i=0;i<3;i++) B("Upholstered_Seat_Cushion",b,-.85f+i*.85f,.478f,-.035f,.822f,.09f,.54f,"SeatFabric",false,.04f);
        for(int i=0;i<4;i++)B("Timber_Back_Slat",b,0,.66f+i*.095f,.29f,2.6f,.071f,.06f,"SeatWood",false,.012f);
        return b;
    }

    static Transform Cylinder(string name,Transform p,Vector3 pos,float radius,float height,string material) {
        string key="Turned_24_Round";Mesh m=AssetDatabase.LoadAssetAtPath<Mesh>(R+"/Meshes/"+key+".asset");
        if(!m) {
            var vs=new List<Vector3>();var uv=new List<Vector2>();var ts=new List<int>();int n=24;
            float[] ys={-.5f,-.47f,.47f,.5f},rs={.46f,.5f,.5f,.46f};
            for(int j=0;j<4;j++)for(int i=0;i<=n;i++){float a=i*Mathf.PI*2/n;vs.Add(V(Mathf.Cos(a)*rs[j],ys[j],Mathf.Sin(a)*rs[j]));uv.Add(new Vector2((float)i/n,(float)j/3));}
            for(int j=0;j<3;j++)for(int i=0;i<n;i++){int a=j*(n+1)+i;ts.AddRange(new[]{a,a+n+1,a+n+2,a,a+n+2,a+1});}
            int bottom=vs.Count;vs.Add(V(0,-.5f,0));uv.Add(Vector2.zero);int top=vs.Count;vs.Add(V(0,.5f,0));uv.Add(Vector2.zero);
            for(int i=0;i<n;i++){ts.AddRange(new[]{bottom,i,i+1,top,3*(n+1)+i+1,3*(n+1)+i});}
            m=new Mesh();m.name=key;m.SetVertices(vs);m.SetUVs(0,uv);m.SetTriangles(ts,0);m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();AssetDatabase.CreateAsset(m,R+"/Meshes/"+key+".asset");
        }
        Transform t=Group(name,p);t.localPosition=pos;t.localScale=V(radius*2,height,radius*2);t.gameObject.AddComponent<MeshFilter>().sharedMesh=m;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mats[material];t.gameObject.isStatic=true;return t;
    }

    [MenuItem("Tools/The Elevator Lobby/03 Lighting And Cameras")]
    public static void LightingAndCameras() {
        Refs();if(lights.childCount>0)throw new Exception("Lighting is already built.");
        // Actual diffuse emitters plus restrained, shadowed real-time light sources.
        for(int ix=0;ix<2;ix++)for(int iz=0;iz<3;iz++) {
            float x=ix==0?-5f:5f,z=-5+iz*5;
            var f=Fixture(ceiling,V(x,4.415f,z),true);
            if(ix==0&&iz==0)PrefabUtility.SaveAsPrefabAssetAndConnect(f.gameObject,R+"/Prefabs/Props/Recessed_Luminaire.prefab",InteractionMode.AutomatedAction);
            Spot("Lobby_Ceiling_Light",V(x,4.18f,z),22,9.5f,125,C(.89f,.94f,1),true);
        }
        for(int iz=0;iz<3;iz++) {
            float z=-4.8f+iz*5;
            Fixture(ceiling,V(0,4.415f,z),false);Spot("Lobby_Center_Light",V(0,4.16f,z),12,8,120,C(.95f,.94f,.86f),iz==1);
        }
        foreach(float s in new[]{-1f,1f}) {
            foreach(float x in new[]{15f,19f,23f})Spot("Corridor_Ceiling_Light",V(s*x,3.45f,3.6f),13,7.5f,115,C(.82f,.91f,.95f),x==19);
            Spot("Corridor_Return_Light",V(s*23,3.45f,8.5f),11,7,115,C(.82f,.91f,.95f),false);
            Point("Elevator_Wall_Wash",V(s*3.5f,3.5f,6.75f),2.5f,4.4f,C(1,.88f,.66f));
            Point("Portal_Indirect_Fill",V(s*11.2f,2.8f,4),2.3f,6.0f,C(.81f,.88f,.91f));
        }
        Spot("Cabin_Ceiling_Light",V(0,2.96f,9.10f),9,4.5f,125,C(1,.94f,.82f),false);
        Point("Call_Button_Glow",V(1.99f,1.29f,7.08f),.13f,.55f,C(1,.68f,.25f));
        var sun=Group("Main_Indirect_Directional",lights).gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.color=C(.79f,.86f,.94f);sun.intensity=.12f;sun.shadows=LightShadows.None;sun.transform.rotation=Quaternion.Euler(42,-28,0);RenderSettings.sun=sun;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=C(.34f,.38f,.42f);RenderSettings.ambientEquatorColor=C(.22f,.245f,.24f);RenderSettings.ambientGroundColor=C(.14f,.12f,.10f);RenderSettings.ambientIntensity=1;RenderSettings.fog=false;RenderSettings.skybox=null;RenderSettings.reflectionIntensity=.6f;
        Transform vg=Group("Lobby_Atmosphere",lights);var volume=vg.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Lobby_Atmosphere";AssetDatabase.CreateAsset(profile,R+"/Lighting/Lobby_Atmosphere.asset");volume.sharedProfile=profile;
        var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
        var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(.15f);color.contrast.Override(9);color.saturation.Override(-5);
        var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.13f);bloom.threshold.Override(1.15f);bloom.scatter.Override(.35f);
        var vig=profile.Add<Vignette>(true);vig.intensity.Override(.12f);vig.smoothness.Override(.35f);
        Camera a=Preview("Camera_A_MainEntrance",V(-2.8f,1.74f,-7.5f),V(0,1.85f,6.5f),64,true);
        Preview("Camera_B_ArchitecturalOverview",V(6.4f,4.08f,-7.2f),V(-.6f,1.5f,4),66,false);
        Preview("Camera_C_ElevatorDetail",V(4.65f,2.04f,2.25f),V(0,1.85f,7.35f),53,false);
        Probe("Lobby_Reflection",V(0,1.85f,1),V(26,4.5f,16));Probe("West_Reflection",V(-19,1.7f,3.6f),V(13,3.7f,4.4f));Probe("East_Reflection",V(19,1.7f,3.6f),V(13,3.7f,4.4f));
        var lg=Group("Interior_Light_Probes",lights).gameObject.AddComponent<LightProbeGroup>();var pp=new List<Vector3>();
        foreach(float y in new[]{.6f,1.7f,3.2f})foreach(float x in new[]{-11f,-6f,0,6f,11f})foreach(float z in new[]{-6f,0,6f})pp.Add(V(x,y,z));lg.probePositions=pp.ToArray();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(V(0,1.7f,4),Quaternion.LookRotation(V(4.3f,.09f,13.1f)),17);
        Debug.Log("LOBBY_PHASE_5_6_COMPLETE: Environment lighting and three preview cameras saved.");
    }

    static Camera Preview(string name,Vector3 pos,Vector3 target,float fov,bool main) {
        var t=Group(name,cams);t.position=pos;t.rotation=Quaternion.LookRotation(target-pos);var c=t.gameObject.AddComponent<Camera>();c.fieldOfView=fov;c.nearClipPlane=.05f;c.farClipPlane=90;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=C(.05f,.06f,.065f);c.allowHDR=true;c.enabled=main;
        var d=t.gameObject.AddComponent<UniversalAdditionalCameraData>();d.renderPostProcessing=true;d.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;d.antialiasingQuality=AntialiasingQuality.High;d.renderShadows=true;
        if(main){t.tag="MainCamera";t.gameObject.AddComponent<AudioListener>();}return c;
    }
    static Light Spot(string name,Vector3 pos,float intensity,float range,float angle,Color col,bool shadow) {
        var l=Group(name,lights).gameObject.AddComponent<Light>();l.type=LightType.Spot;l.transform.position=pos;l.transform.rotation=Quaternion.Euler(90,0,0);l.intensity=intensity;l.range=range;l.spotAngle=angle;l.innerSpotAngle=angle*.66f;l.color=col;l.shadows=shadow?LightShadows.Soft:LightShadows.None;l.shadowStrength=.72f;l.shadowBias=.035f;l.shadowNormalBias=.1f;l.renderMode=LightRenderMode.Auto;return l;
    }
    static Light Point(string name,Vector3 pos,float intensity,float range,Color col) {
        var l=Group(name,lights).gameObject.AddComponent<Light>();l.type=LightType.Point;l.transform.position=pos;l.intensity=intensity;l.range=range;l.color=col;l.shadows=LightShadows.None;return l;
    }
    static void Probe(string name,Vector3 pos,Vector3 size) {
        var p=Group(name,lights).gameObject.AddComponent<ReflectionProbe>();p.transform.position=pos;p.size=size;p.resolution=256;p.mode=ReflectionProbeMode.Baked;p.boxProjection=true;p.blendDistance=1.4f;p.clearFlags=ReflectionProbeClearFlags.SolidColor;p.backgroundColor=C(.13f,.15f,.16f);p.nearClipPlane=.1f;p.farClipPlane=50;p.intensity=.68f;
    }

    [MenuItem("Tools/The Elevator Lobby/04 Polish And Integrate")]
    public static void PolishAndIntegrate() {
        Refs();
        Texture("Honed_Limestone",0,false);Texture("Painted_Mineral",2,false);
        foreach(var m in mats.Values){if(m.HasProperty("_BumpScale"))m.SetFloat("_BumpScale",m.name=="Steel"?.12f:.065f);EditorUtility.SetDirty(m);}
        mats["InsetStone"].SetColor("_BaseColor",C(.35f,.373f,.36f));mats["InsetStone"].SetFloat("_Smoothness",.43f);mats["InsetStone"].SetFloat("_Metallic",.05f);
        mats["FeatureWall"].SetColor("_BaseColor",C(.27f,.32f,.30f));mats["WallPanel"].SetColor("_BaseColor",C(.38f,.42f,.39f));
        if(!floor.Find("Inlay_Tile_Joint")) {
            for(int i=1;i<7;i++)B("Inlay_Tile_Joint",floor,0,.017f,-3.2f+i*(9.4f/7),3.77f,.003f,.008f,"Grout",false,.0005f);
            B("Inlay_Longitudinal_Joint",floor,0,.017f,1.5f,.008f,.003f,9.38f,"Grout",false,.0005f);
        }
        // Low intensity fills stand in for local indirect bounce in this real-time setup.
        foreach(var t in lights.GetComponentsInChildren<Light>().Where(l=>l.name=="Architectural_Bounce_Fill")) {t.intensity=25;t.color=C(.94f,.95f,.92f);}
        if(!lights.Find("Architectural_Bounce_Fill"))foreach(var v in new[]{V(0,2.8f,-4.5f),V(-7,2.7f,.3f),V(7,2.7f,.3f),V(0,2.8f,5.6f)})Point("Architectural_Bounce_Fill",v,25,13,C(.94f,.95f,.92f));
        foreach(var t in lights.GetComponentsInChildren<Light>()) {if(t.name=="Lobby_Ceiling_Light")t.intensity=28;if(t.name=="Main_Indirect_Directional")t.intensity=.28f;}
        var profile=lights.Find("Lobby_Atmosphere").GetComponent<Volume>().sharedProfile;ColorAdjustments adj;profile.TryGet<ColorAdjustments>(out adj);adj.postExposure.value=.5f;EditorUtility.SetDirty(profile);
        RenderSettings.ambientSkyColor=C(.52f,.55f,.58f);RenderSettings.ambientEquatorColor=C(.37f,.39f,.38f);RenderSettings.ambientGroundColor=C(.23f,.21f,.19f);
        // Give each numbered architectural bay a reusable prefab, preserving its local geometry.
        foreach(var wing in new[]{left,right}) {
            var bays=wing.Cast<Transform>().Where(t=>t.name.StartsWith("Office_Corridor_Bay")).ToArray();
            for(int i=0;i<bays.Length;i++) {
                var bay=bays[i];if(PrefabUtility.IsPartOfPrefabInstance(bay))PrefabUtility.UnpackPrefabInstance(bay.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                bool west=wing==left;int start=(west?701:715)+i*2;
                var doors=bay.Cast<Transform>().Where(t=>t.name.StartsWith("Office_Door")).OrderBy(t=>t.localPosition.z).ToArray();
                for(int k=0;k<doors.Length;k++){doors[k].name="Office_Door_"+(start+k);foreach(var text in doors[k].GetComponentsInChildren<TextMesh>())text.text=(start+k).ToString();}
                PrefabUtility.SaveAsPrefabAssetAndConnect(bay.gameObject,R+"/Prefabs/Architecture/Office_Bay_"+(west?"West":"East")+"_"+(i+1)+".prefab",InteractionMode.AutomatedAction);
            }
        }
        // Use the same saved bench, vent, column and light-fixture assets for repeated instances.
        ReplaceRepeated(props,"Waiting_Bench_",R+"/Prefabs/Props/Lobby_Bench.prefab",false);
        ReplaceRepeated(ceiling,"Ceiling_Return_Grille",R+"/Prefabs/Props/Ceiling_Vent.prefab",false);
        var first=cols.GetChild(0);if(!AssetDatabase.LoadAssetAtPath<GameObject>(R+"/Prefabs/Architecture/Clad_Column.prefab"))PrefabUtility.SaveAsPrefabAssetAndConnect(first.gameObject,R+"/Prefabs/Architecture/Clad_Column.prefab",InteractionMode.AutomatedAction);
        ReplaceRepeated(cols,"Clad_Column",R+"/Prefabs/Architecture/Clad_Column.prefab",false);
        var bigLights=ceiling.Cast<Transform>().Where(t=>t.name.StartsWith("Recessed_Luminaire")&&t.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.x>2).ToArray();
        foreach(var t in bigLights)ReplaceOne(t,R+"/Prefabs/Props/Recessed_Luminaire.prefab");
        foreach(var c in cams.GetComponentsInChildren<Camera>())c.aspect=16f/9f;
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("LOBBY_PHASE_7_COMPLETE: Reusable environment prefabs and static scene geometry integrated.");
    }
    static void ReplaceRepeated(Transform parent,string prefix,string path,bool unused) {
        foreach(var t in parent.Cast<Transform>().Where(t=>t.name.StartsWith(prefix)).ToArray())ReplaceOne(t,path);
    }
    static void ReplaceOne(Transform t,string path) {
        var pos=t.localPosition;var rot=t.localRotation;var name=t.name;var parent=t.parent;
        var replacement=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),parent);replacement.name=name;replacement.transform.localPosition=pos;replacement.transform.localRotation=rot;UnityEngine.Object.DestroyImmediate(t.gameObject);
    }

    public static void Capture(string cameraName,string path,int width=1600,int height=900) {
        var c=GameObject.Find(cameraName).GetComponent<Camera>();var old=c.targetTexture;float oldAspect=c.aspect;
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGBHalf);var prev=RenderTexture.active;Texture2D tex=null;
        try {
            c.targetTexture=rt;c.aspect=(float)width/height;c.Render();RenderTexture.active=rt;
            tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,tex.EncodeToPNG());
        } finally {RenderTexture.active=prev;c.targetTexture=old;c.aspect=oldAspect;if(tex)UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
    public static void BakeReflections() {
        Refs();foreach(var p in lights.GetComponentsInChildren<ReflectionProbe>()) {
            string path=R+"/Lighting/"+p.name+".exr";p.mode=ReflectionProbeMode.Baked;
            if(!Lightmapping.BakeReflectionProbe(p,path))throw new Exception("Probe bake failed: "+p.name);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);p.customBakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>(path);p.mode=ReflectionProbeMode.Custom;EditorUtility.SetDirty(p);
        }
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/The Elevator Lobby/05 Final Visuals And Batching")]
    public static void FinishVisuals() {
        Refs();
        var typeMat=AssetDatabase.LoadAssetAtPath<Material>(R+"/Materials/Architecture/WorldTypography.mat");
        foreach(var text in root.GetComponentsInChildren<TextMesh>()){text.fontSize=0;text.GetComponent<MeshRenderer>().sharedMaterial=typeMat;}
        if(!lights.Find("Corridor_Indirect_Bounce")) foreach(float side in new[]{-1f,1f}) {
            foreach(float x in new[]{15.3f,20.4f})Point("Corridor_Indirect_Bounce",V(side*x,2.3f,3.6f),7.0f,7.8f,C(.86f,.91f,.92f));
            Point("Corridor_Indirect_Bounce",V(side*23,2.3f,8.8f),6.0f,7.5f,C(.86f,.91f,.92f));
            Transform turn=Group("Stair_Direction_Plaque",side<0?left:right);turn.localPosition=V(side*24.97f,2.2f,3.6f);turn.localRotation=Quaternion.Euler(0,side*90,0);
            B("Stair_Plaque",turn,0,0,0,1.35f,.37f,.035f,"Trim");Label(side<0?"STAIR W  >":"<  STAIR E",turn,V(0,0,-.024f),.17f,"TextLight",1.18f);
        }
        // Save corrected world-text assignments into the reusable prefab sources.
        var roots=root.GetComponentsInChildren<Transform>().Select(t=>PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject)).Where(g=>g!=null).Distinct().ToArray();
        foreach(var p in roots)PrefabUtility.ApplyPrefabInstance(p,InteractionMode.AutomatedAction);
        // Batches are divided by spatial sector and moving assembly for useful culling and editing.
        foreach(var scope in new[]{floor,walls,ceiling,cols,left,right})BatchSector(scope);
        foreach(Transform t in lift)BatchSector(t);
        foreach(Transform t in props)BatchSector(t);
        foreach(var r in ceiling.GetComponentsInChildren<MeshRenderer>())r.shadowCastingMode=ShadowCastingMode.Off;
        foreach(var r in floor.GetComponentsInChildren<MeshRenderer>())r.shadowCastingMode=ShadowCastingMode.Off;
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("LOBBY_PHASE_8_COMPLETE: Corridor fills, corrected signs and spatial render batches saved.");
    }

    static void BatchSector(Transform scope) {
        if(scope.Find("Optimized_Render_Batches"))return;
        var renderers=scope.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh&&r.GetComponent<TextMesh>()==null&&r.sharedMaterials.Length==1).ToArray();
        Transform target=Group("Optimized_Render_Batches",scope);
        foreach(var grouping in renderers.GroupBy(r=>r.sharedMaterial)) {
            if(grouping.Count()<2)continue;
            var m=new Mesh();m.name=scope.name+"_"+grouping.Key.name;m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            var combine=grouping.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=scope.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray();
            m.CombineMeshes(combine,true,true);AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(R+"/Meshes/Batch_"+m.name+".asset"));
            var g=Group(grouping.Key.name+"_Batch",target).gameObject;g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=grouping.Key;g.isStatic=scope.gameObject.isStatic;
            // Structure remains named and collidable; only the original draw submission is disabled.
            foreach(var original in grouping)original.enabled=false;
        }
    }

    public static void CompleteEntryAndCameras() {
        Refs();
        if(!arch.Find("Main_Entry_Vestibule")) {
            Transform entry=Group("Main_Entry_Vestibule",arch);
            B("Entry_Floor_Slab",entry,0,-.16f,-10.05f,8.6f,.28f,4.1f,"Grout",true);
            TileField("Entry_Stone",entry,V(0,-.025f,-10.05f),8.6f,4.1f,7,3,false);
            foreach(float s in new[]{-1f,1f}) {
                B("Entry_Return_Wall",entry,s*4.46f,1.7f,-10.05f,.32f,3.4f,4.4f,"Plaster",true);
                B("Entry_Skirting",entry,s*4.28f,.085f,-10.05f,.06f,.17f,4.1f,"Trim");
            }
            B("Entry_Closed_Ceiling",entry,0,3.5f,-10.05f,8.9f,.2f,4.4f,"Ceiling",true);
            B("Entry_Rear_Closure",entry,0,1.7f,-12.22f,8.9f,3.4f,.3f,"Plaster",true);
            Transform back=Group("Building_Entrance_Double_Door",entry);back.localPosition=V(0,0,-12.04f);back.localRotation=Quaternion.Euler(0,180,0);
            foreach(float s in new[]{-1f,1f}) {
                B("Entry_Door_Leaf",back,s*.68f,1.40f,0,1.34f,2.8f,.09f,"Pewter");
                B("Frosted_Door_Glass",back,s*.68f,1.64f,-.052f,1.17f,2.08f,.018f,"FrostedGlass");
                B("Entry_Push_Rail",back,s*.68f,1.10f,-.11f,1.15f,.052f,.07f,"Steel",false,.015f);
                B("Entry_Door_Jamb",back,s*1.4f,1.42f,0,.09f,2.84f,.14f,"Trim");
            }
            B("Entry_Door_Head",back,0,2.84f,0,2.87f,.08f,.14f,"Trim");
            Label("NORTH TOWER",back,V(0,3.05f,-.09f),.14f,"TextDark",2.5f);
            Fixture(entry,V(0,3.34f,-10.05f),true);Spot("Vestibule_Ceiling_Light",V(0,3.1f,-10.05f),12,7,120,C(.91f,.94f,.91f),false);Point("Vestibule_Bounce",V(0,2.1f,-10.2f),6,7,C(.91f,.94f,.91f));BatchSector(entry);
        }
        var a=cams.Find("Camera_A_MainEntrance").GetComponent<Camera>();a.transform.position=V(-2.4f,1.74f,-7.9f);a.transform.LookAt(V(-1.4f,1.85f,5));a.fieldOfView=68;
        var b=cams.Find("Camera_B_ArchitecturalOverview").GetComponent<Camera>();b.transform.position=V(5.8f,4.02f,-6.8f);b.transform.LookAt(V(-.5f,.55f,3.8f));b.fieldOfView=62;
        foreach(var t in root.GetComponentsInChildren<Transform>())if(t.parent&&t.parent.name=="Optimized_Render_Batches")t.gameObject.isStatic=true;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/The Elevator Lobby/Convert To Static Map Only")]
    public static void ConvertToMapOnly() {
        root=GameObject.Find("Environment_Root").transform;
        var oldLift=root.Find("Elevator_System");if(oldLift)oldLift.name="Elevator_Assembly";
        Refs();
        foreach(string name in new[]{"NPC_Queue","Player_Spawn"}) {var t=root.Find(name);if(t)UnityEngine.Object.DestroyImmediate(t.gameObject);}
        foreach(var c in lift.GetComponents<MonoBehaviour>())if(c&&c.GetType().Name=="LobbyElevatorDoors")UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in root.GetComponents<MonoBehaviour>())if(c&&c.GetType().Name=="NavMeshSurface") {c.GetType().GetMethod("RemoveData").Invoke(c,null);UnityEngine.Object.DestroyImmediate(c);}
        lift.Find("Elevator_LeftDoor").localPosition=V(-.654f,0,7.495f);lift.Find("Elevator_RightDoor").localPosition=V(.654f,0,7.495f);
        foreach(var t in lift.GetComponentsInChildren<Transform>())t.gameObject.isStatic=true;
        // Keep the architectural stone inlay; remove the queue instruction and line.
        var batch=floor.Find("Optimized_Render_Batches");if(batch)UnityEngine.Object.DestroyImmediate(batch.gameObject);
        foreach(var t in floor.Cast<Transform>().Where(t=>t.name=="Waiting_Line"||t.name.StartsWith("Sign_PLEASE WAIT")).ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
        foreach(var r in floor.GetComponentsInChildren<MeshRenderer>()){r.enabled=true;r.gameObject.name=r.gameObject.name.Replace("Queue_","Waiting_");}
        BatchSector(floor);foreach(var r in floor.GetComponentsInChildren<MeshRenderer>())r.shadowCastingMode=ShadowCastingMode.Off;
        foreach(var l in lights.GetComponentsInChildren<Light>())if(l.name=="Queue_Soft_Light")l.name="Lobby_Center_Light";
        if(!lights.Find("Cabin_Indirect_Bounce"))Point("Cabin_Indirect_Bounce",V(0,1.85f,9.2f),4,4.1f,C(.96f,.93f,.85f));
        var b=cams.Find("Camera_B_ArchitecturalOverview").GetComponent<Camera>();b.transform.position=V(6.2f,3.9f,-6.6f);b.transform.LookAt(V(-.4f,-1.0f,3.8f));b.fieldOfView=52;
        foreach(var c in cams.GetComponentsInChildren<Camera>())c.enabled=c.name=="Camera_A_MainEntrance";
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        foreach(string p in new[]{R+"/Models",R+"/Materials/Characters",R+"/Prefabs/Characters",R+"/Scripts/Runtime",R+"/Lighting/Lobby_Walkable_NavMesh.asset",R+"/Previews","Assets/Screenshots/Phase6_Overview.png"})if(AssetDatabase.IsValidFolder(p)||AssetDatabase.LoadMainAssetAtPath(p)) {if(!AssetDatabase.DeleteAsset(p))throw new Exception("Could not remove superseded asset "+p);}
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("STATIC_MAP_ONLY: NPCs, player controller, door behavior, NavMesh and their assets removed. Environment geometry retained.");
    }

    static void Refs() {
        root=GameObject.Find("Environment_Root").transform; arch=root.Find("Architecture");floor=arch.Find("Floor");walls=arch.Find("Walls");ceiling=arch.Find("Ceiling");cols=arch.Find("Columns");left=arch.Find("Left_Corridor");right=arch.Find("Right_Corridor");
        lift=root.Find("Elevator_Assembly");props=root.Find("Environment_Props");lights=root.Find("Lighting");cams=root.Find("Preview_Cameras");
        mats.Clear();foreach(string g in AssetDatabase.FindAssets("t:Material",new[]{R+"/Materials/Architecture"})) { var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));mats[m.name]=m; }
        font=AssetDatabase.LoadAssetAtPath<Font>(R+"/Fonts/LobbyTypography.ttf");
    }

    static void Corridor(Transform wing,int side) {
        // Two reusable four-metre office bays, followed by a genuine L-shaped continuation.
        for(int i=0;i<2;i++) {
            Transform bay=Group("Office_Corridor_Bay_"+(i+1),wing);
            B("Floor_Slab",bay,2,-.15f,0,4,.28f,4.2f,"Grout",true);
            TileField("Corridor_Stone",bay,V(2,-.018f,0),4,4.2f,4,4,false);
            foreach(float s in new[]{-1f,1f}) {
                B("Wall_Pier_A",bay,.67f,1.83f,s*2.22f,1.34f,3.66f,.26f,"Plaster",true);
                B("Wall_Pier_B",bay,3.28f,1.83f,s*2.22f,1.44f,3.66f,.26f,"Plaster",true);
                B("Door_Lintel",bay,1.95f,3.14f,s*2.22f,1.22f,1.04f,.26f,"Plaster",true);
                Transform d=Group("Office_Door_"+(side<0?701:715)+(i*2)+(s<0?0:1),bay); d.localPosition=V(1.95f,0,s*2.08f); d.localRotation=Quaternion.Euler(0,s>0?0:180,0);
                OfficeDoor(d,((side<0?701:715)+i*2+(s<0?0:1)).ToString());
                B("Wall_Base_A",bay,.67f,.08f,s*2.06f,1.34f,.16f,.05f,"Trim");
                B("Wall_Base_B",bay,3.28f,.08f,s*2.06f,1.44f,.16f,.05f,"Trim");
                B("Wall_Dado_A",bay,.67f,1.21f,s*2.067f,1.3f,.035f,.025f,"Pewter");
                B("Wall_Dado_B",bay,3.28f,1.21f,s*2.067f,1.4f,.035f,.025f,"Pewter");
            }
            B("Ceiling_Closed",bay,2,3.76f,0,4,.20f,4.55f,"Ceiling",true);
            B("Ceiling_Seam",bay,0,3.65f,0,.035f,.035f,4.15f,"Trim",false,.002f);
            Fixture(bay,V(2,3.61f,0),false);
            if(i==0) { var asset=PrefabUtility.SaveAsPrefabAssetAndConnect(bay.gameObject,R+"/Prefabs/Architecture/Office_Corridor_Bay.prefab",InteractionMode.AutomatedAction); }
            bay.localPosition=V(side*(13+4*i),0,3.6f);bay.localRotation=Quaternion.Euler(0,side<0?180:0,0);
        }
        float x=side*23;
        B("Corner_Floor",wing,x,-.15f,6.9f,4,.28f,10.8f,"Grout",true);
        TileField("Corner_Stone",wing,V(x,-.018f,6.9f),4,10.8f,4,10,false);
        B("Corner_Outer_Wall",wing,side*25.16f,1.83f,6.9f,.32f,3.66f,11.1f,"Plaster",true);
        B("Corner_Approach_Wall",wing,x,1.83f,1.34f,4,3.66f,.32f,"Plaster",true);
        B("Corner_Inner_Wall",wing,side*20.84f,1.83f,9.02f,.32f,3.66f,6.6f,"Plaster",true);
        B("Corner_Far_Wall",wing,x,1.83f,12.46f,4.3f,3.66f,.32f,"Plaster",true);
        B("Corner_Closed_Ceiling",wing,x,3.76f,6.9f,4.35f,.2f,11.1f,"Ceiling",true);
        foreach(float z in new[]{3.6f,8.5f}) Fixture(wing,V(x,3.61f,z),false);
        Transform end=Group("Fire_Exit_Assembly",wing);end.localPosition=V(x,0,12.28f);OfficeDoor(end,"STAIR "+(side<0?"W":"E"));
        B("Exit_Light_Box",end,0,2.96f,-.10f,1.08f,.27f,.14f,"ExitGlow");
        Label("EXIT  >",end,V(0,2.96f,-.18f),.16f,"TextLight",.9f);
        foreach(float z in new[]{2.1f,6.1f,10.1f}) B("Corner_Wall_Joint",wing,side*24.985f,1.82f,z,.02f,3.6f,.015f,"Trim",false,.002f);
    }

    static void Panel(Transform parent,Vector3 p,float rot,float width,float h) {
        Transform t=Group("Recessed_Wall_Module",parent);t.localPosition=p;t.localRotation=Quaternion.Euler(0,rot,0);
        B("Mineral_Lower_Panel",t,0,.64f,-.035f,width,1.22f,.075f,"WallPanel");
        B("Dado_Metal_Cap",t,0,1.27f,-.08f,width,.035f,.025f,"Pewter",false,.003f);
        B("Upper_Recessed_Panel",t,0,(h+1.32f)/2,-.012f,width,h-1.32f,.025f,"Plaster");
        B("Vertical_Shadow_Joint",t,width/2,.64f,-.076f,.014f,1.24f,.012f,"Trim",false,.001f);
    }

    static void OfficeDoor(Transform p,string number) {
        B("Door_Leaf",p,0,1.30f,.045f,1.13f,2.6f,.10f,"DoorPaint",true,.018f);
        foreach(float s in new[]{-1f,1f}) B("Door_Jamb",p,s*.605f,1.34f,-.04f,.075f,2.68f,.15f,"Pewter");
        B("Door_Head_Frame",p,0,2.66f,-.04f,1.29f,.075f,.15f,"Pewter");
        B("Door_Kickplate",p,0,.16f,-.018f,1.04f,.25f,.018f,"Steel");
        B("Glass_Inset_Gasket",p,-.21f,1.78f,-.016f,.31f,.68f,.025f,"Trim");
        B("Frosted_Vision_Panel",p,-.21f,1.78f,-.033f,.26f,.62f,.012f,"FrostedGlass");
        B("Handle_Plate",p,.414f,1.04f,-.022f,.085f,.24f,.025f,"Steel");
        B("Lever_Handle",p,.355f,1.10f,-.07f,.16f,.032f,.065f,"Steel",false,.012f);
        B("Room_Sign",p,.83f,1.62f,-.047f,.36f,.22f,.03f,"Trim");
        Label(number,p,V(.83f,1.62f,-.066f),.075f,"TextLight",.30f);
        B("Threshold",p,0,.025f,-.02f,1.2f,.035f,.18f,"Steel",false,.003f);
    }

    public static Transform Group(string name,Transform p) { var g=new GameObject(name);if(p)g.transform.SetParent(p,false);return g.transform; }
    static GameObject B(string n,Transform p,float x,float y,float z,float sx,float sy,float sz,string mat,bool col=false,float bevel=.012f) {
        var g=new GameObject(n);g.transform.SetParent(p,false);g.transform.localPosition=V(x,y,z);
        g.AddComponent<MeshFilter>().sharedMesh=Rounded(V(sx,sy,sz),bevel);
        var mr=g.AddComponent<MeshRenderer>();mr.sharedMaterial=mats[mat];mr.lightProbeUsage=LightProbeUsage.BlendProbes;mr.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
        if(col)g.AddComponent<BoxCollider>().size=V(sx,sy,sz);
        g.isStatic=true;return g;
    }

    static Mesh Rounded(Vector3 size,float radius) {
        radius=Mathf.Min(radius,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.46f);
        string key=string.Format(System.Globalization.CultureInfo.InvariantCulture,"B_{0:F3}_{1:F3}_{2:F3}_{3:F4}",size.x,size.y,size.z,radius);
        Mesh cached;if(meshes.TryGetValue(key,out cached))return cached;
        string path=R+"/Meshes/"+key+".asset";cached=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(cached){meshes[key]=cached;return cached;}
        var vs=new List<Vector3>();var ns=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();Vector3 half=size*.5f,inner=half-Vector3.one*radius;
        Vector3[] normals={Vector3.forward,Vector3.back,Vector3.right,Vector3.left,Vector3.up,Vector3.down};
        Vector3[] us={Vector3.right,Vector3.left,Vector3.back,Vector3.forward,Vector3.right,Vector3.right};
        Vector3[] vvs={Vector3.up,Vector3.up,Vector3.up,Vector3.up,Vector3.back,Vector3.forward};
        for(int f=0;f<6;f++) {
            Vector3 n=normals[f],u=us[f],v=vvs[f];float hn=Vector3.Dot(half,new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z)));float hu=Vector3.Dot(half,new Vector3(Mathf.Abs(u.x),Mathf.Abs(u.y),Mathf.Abs(u.z)));float hv=Vector3.Dot(half,new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)));
            float[] a={-hu,-hu+radius,hu-radius,hu},b={-hv,-hv+radius,hv-radius,hv};int start=vs.Count;
            for(int j=0;j<4;j++)for(int i=0;i<4;i++) {
                Vector3 p=n*hn+u*a[i]+v*b[j];Vector3 c=V(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));Vector3 d=(p-c).normalized;
                Vector3 q=c+d*radius;vs.Add(q);ns.Add(d);uv.Add(new Vector2(Vector3.Dot(q,u),Vector3.Dot(q,v)));
            }
            for(int j=0;j<3;j++)for(int i=0;i<3;i++){int a0=start+j*4+i;tri.AddRange(new[]{a0,a0+1,a0+5,a0,a0+5,a0+4});}
        }
        var m=new Mesh();m.name=key;m.SetVertices(vs);m.SetNormals(ns);m.SetUVs(0,uv);m.SetTriangles(tri,0);m.RecalculateBounds();m.RecalculateTangents();AssetDatabase.CreateAsset(m,path);meshes[key]=m;return m;
    }

    static void TileField(string name,Transform parent,Vector3 center,float width,float depth,int nx,int nz,bool ceil) {
        var instances=new List<CombineInstance>[] {new List<CombineInstance>(),new List<CombineInstance>(),new List<CombineInstance>()};
        float w=width/nx,d=depth/nz;
        Mesh tile=Rounded(V(w-.009f,ceil?.065f:.045f,d-.009f),.003f);
        for(int iz=0;iz<nz;iz++)for(int ix=0;ix<nx;ix++) {
            Vector3 pos=center+V(-width/2+w*(ix+.5f),0,-depth/2+d*(iz+.5f));
            instances[(ix*13+iz*7)%3].Add(new CombineInstance {mesh=tile,transform=Matrix4x4.TRS(pos,Quaternion.identity,Vector3.one)});
        }
        for(int i=0;i<3;i++) {
            Mesh m=new Mesh();m.name=name+"_"+i;m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;m.CombineMeshes(instances[i].ToArray(),true,true);
            string path=AssetDatabase.GenerateUniqueAssetPath(R+"/Meshes/"+m.name+".asset");AssetDatabase.CreateAsset(m,path);
            var g=Group(name+"_"+i,parent).gameObject;g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=mats[ceil?"Ceiling":("Stone"+i)];g.isStatic=true;
        }
    }

    static Material M(string name,Color color,float metallic,float smooth,Texture2D tex=null,Texture2D normal=null) {
        string path=R+"/Materials/Architecture/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.name=name;
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
        if(tex)m.SetTexture("_BaseMap",tex);
        if(normal){m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",.19f);m.EnableKeyword("_NORMALMAP");}
        m.enableInstancing=true;mats[name]=m;return m;
    }
    static void Glow(string name,Color basecol,Color emission) {var m=M(name,basecol,.05f,.3f);m.SetColor("_EmissionColor",emission);m.EnableKeyword("_EMISSION");m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;}
    static Texture2D Texture(string name,int kind,bool normal) {
        string path=R+"/Textures/"+name+(normal?"_Normal":"_Base")+".png";int n=256;var t=new Texture2D(n,n,TextureFormat.RGBA32,true,normal);var pix=new Color[n*n];
        Func<int,int,float> noise=(x,y)=>{float f=Mathf.PerlinNoise(x*.095f,y*.095f)*.5f+Mathf.PerlinNoise(x*.43f,y*.43f)*.3f+Mathf.PerlinNoise(x*1.3f,y*1.3f)*.2f;return f;};
        for(int y=0;y<n;y++)for(int x=0;x<n;x++) {
            float h=noise(x,y),v;
            if(kind==1) h=.65f+Mathf.Sin(x*3.7f)*.08f+Mathf.PerlinNoise(x*.75f,y*.012f)*.18f;
            if(normal){float dx=noise(x+1,y)-noise(x-1,y),dy=noise(x,y+1)-noise(x,y-1);if(kind==1){dx=Mathf.Sin(x*3.7f)*.06f;dy=0;}Vector3 nn=V(-dx*.55f,-dy*.55f,1).normalized;pix[y*n+x]=new Color(nn.x*.5f+.5f,nn.y*.5f+.5f,nn.z*.5f+.5f,1);}
            else {v=kind==1?(.90f+h*.10f):(.92f+h*.065f);if(kind==0&&((x*73856093 ^ y*19349663)&255)<4)v-=.022f;pix[y*n+x]=new Color(v,v*.997f,v*.985f,1);}
        }
        t.SetPixels(pix);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
        var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.wrapMode=TextureWrapMode.Repeat;imp.anisoLevel=4;imp.sRGBTexture=!normal;if(normal)imp.textureType=TextureImporterType.NormalMap;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static void InitMaterials() {
        var stone=Texture("Honed_Limestone",0,false);var stoneN=Texture("Honed_Limestone",0,true);var steel=Texture("Brushed_Steel",1,false);var steelN=Texture("Brushed_Steel",1,true);var plaster=Texture("Painted_Mineral",2,false);var plasterN=Texture("Painted_Mineral",2,true);
        for(int i=0;i<3;i++)M("Stone"+i,C(.49f+i*.016f,.475f+i*.016f,.438f+i*.016f),.08f,.36f,stone,stoneN);
        M("InsetStone",C(.235f,.255f,.252f),.12f,.36f,stone,stoneN);M("Grout",C(.16f,.17f,.165f),0,.05f);
        M("Plaster",C(.65f,.63f,.574f),0,.12f,plaster,plasterN);M("WallPanel",C(.32f,.36f,.345f),.03f,.22f,plaster,plasterN);
        M("FeatureWall",C(.19f,.237f,.228f),.06f,.27f,stone,stoneN);M("Ceiling",C(.55f,.57f,.55f),0,.06f,plaster,plasterN);
        M("Steel",C(.57f,.61f,.62f),.83f,.47f,steel,steelN);M("Pewter",C(.34f,.37f,.36f),.72f,.38f,steel,steelN);
        M("Trim",C(.035f,.047f,.046f),.28f,.28f);M("DoorPaint",C(.265f,.31f,.295f),.12f,.24f,plaster,plasterN);
        M("FrostedGlass",C(.40f,.51f,.50f),.17f,.69f,plaster,plasterN);M("SeatFabric",C(.22f,.26f,.245f),0,.06f,plaster,plasterN);
        M("SeatWood",C(.34f,.245f,.15f),.02f,.25f,steel,steelN);M("SafetyRed",C(.38f,.075f,.045f),.25f,.29f);
        M("TextLight",C(.82f,.84f,.79f),0,.2f);M("TextDark",C(.07f,.10f,.09f),0,.2f);
        Glow("PanelGlow",C(.85f,.88f,.84f),C(2.3f,2.5f,2.4f));Glow("WarmGlow",C(.78f,.66f,.45f),C(1.7f,1.3f,.77f));Glow("ScreenGlow",C(.42f,.69f,.67f),C(.8f,1.45f,1.35f));Glow("ExitGlow",C(.13f,.31f,.23f),C(.16f,.60f,.38f));
        font=AssetDatabase.LoadAssetAtPath<Font>(R+"/Fonts/LobbyTypography.ttf");
    }

    static GameObject Label(string text,Transform parent,Vector3 pos,float h,string material="TextLight",float maxwidth=0,TextAnchor anchor=TextAnchor.MiddleCenter) {
        var g=new GameObject("Sign_"+text.Replace('\n','_'));g.transform.SetParent(parent,false);g.transform.localPosition=pos;
        var tm=g.AddComponent<TextMesh>();tm.text=text;tm.font=font;tm.fontSize=font.dynamic?96:0;tm.characterSize=1;tm.anchor=anchor;tm.alignment=TextAlignment.Center;tm.color=mats[material].GetColor("_BaseColor");
        var renderer=g.GetComponent<MeshRenderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(R+"/Materials/Architecture/WorldTypography.mat")??font.material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        float bh=renderer.bounds.size.y;float scale=h/Mathf.Max(.001f,bh);if(maxwidth>0)scale=Mathf.Min(scale,maxwidth/Mathf.Max(.001f,renderer.bounds.size.x));g.transform.localScale=Vector3.one*scale;g.isStatic=true;return g;
    }

    static Transform Fixture(Transform parent,Vector3 pos,bool large) {
        var f=Group("Recessed_Luminaire",parent);
        float w=large?2.25f:1.65f,d=large?.64f:.46f;
        B("Recess_Housing",f,0,0,0,w+.13f,.10f,d+.13f,"Trim");B("Diffuser",f,0,-.052f,0,w,.018f,d,"PanelGlow",false,.008f);
        foreach(float s in new[]{-1f,1f})B("Fixture_Edge",f,0,-.058f,s*(d/2+.025f),w+.10f,.025f,.025f,"Steel",false,.004f);
        f.localPosition=pos;return f;
    }
}
