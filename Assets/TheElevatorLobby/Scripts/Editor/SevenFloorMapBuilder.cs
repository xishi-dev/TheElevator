using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Copies the existing map geometry into seven vertically aligned floors.</summary>
public static class SevenFloorMapBuilder
{
    public const int FloorCount = 7;
    public const float FloorSpacing = 5.2f;
    public const string BuildingName = "Building_7_Floors";
    private static readonly string[] LevelWords = { "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN" };
    private static readonly string[] MapGroups = { "Architecture", "Elevator_Assembly", "Environment_Props", "Lighting" };

    [MenuItem("Tools/The Elevator Lobby/Create Seven Floors")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before building the map.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Elevator_Lobby_Main")
            throw new InvalidOperationException("Open Elevator_Lobby_Main first.");
        var existing = GameObject.Find(BuildingName);
        if (existing != null)
        {
            ConfigureCollisionLayers(existing.transform);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = existing;
            return;
        }
        var source = GameObject.Find("Environment_Root");
        if (source == null) throw new InvalidOperationException("Missing Environment_Root.");
        foreach (var group in MapGroups)
            if (source.transform.Find(group) == null) throw new InvalidOperationException("Missing map group: " + group);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create seven map floors");
        var building = new GameObject(BuildingName);
        Undo.RegisterCreatedObjectUndo(building,"Create building");
        var floors = new Transform[FloorCount];
        var maps = new Transform[FloorCount];
        for (int i=0;i<FloorCount;i++)
        {
            var floor = new GameObject("Floor_"+(i+1).ToString("00"));
            Undo.RegisterCreatedObjectUndo(floor,"Create floor");
            floor.transform.SetParent(building.transform,false);
            floor.transform.localPosition = Vector3.up*(i*FloorSpacing);
            floors[i]=floor.transform;
            if (i==0)
            {
                Undo.SetTransformParent(source.transform,floor.transform,"Group original map");
                maps[i]=source.transform;
                continue;
            }
            var map = new GameObject("Map");
            map.transform.SetParent(floor.transform,false);
            maps[i]=map.transform;
            foreach (var group in MapGroups)
            {
                var original=source.transform.Find(group);
                var clone=UnityEngine.Object.Instantiate(original.gameObject,map.transform,false);
                clone.name=original.name;
            }
            // Gameplay cameras, input controllers and spawners remain in the original map.
            // Only local lights and probes are copied; global lighting stays unique.
            var lighting=map.transform.Find("Lighting");
            foreach (var volume in lighting.GetComponentsInChildren<Volume>(true))
                if (volume.isGlobal) UnityEngine.Object.DestroyImmediate(volume.gameObject);
            foreach (var light in lighting.GetComponentsInChildren<Light>(true))
                if (light.type==LightType.Directional) UnityEngine.Object.DestroyImmediate(light.gameObject);

            var arrival=new GameObject("Elevator_Arrival_"+(i+1).ToString("00"));
            arrival.transform.SetParent(floor.transform,false);
            arrival.transform.localPosition=new Vector3(0,0,6.35f);
        }
        // Rename signage after all copies exist, so every copy starts from the same source.
        for (int i=0;i<FloorCount;i++)
        {
            int number=i+1;
            SetLabels(maps[i],number);
            uint mask=1u<<i;
            foreach(var renderer in maps[i].GetComponentsInChildren<Renderer>(true))
            {
                if(i==0) Undo.RecordObject(renderer,"Assign floor lighting");
                renderer.renderingLayerMask=mask;
                RecordOverride(renderer);
            }
            foreach(var light in maps[i].GetComponentsInChildren<Light>(true))
            {
                uint lightMask=light.type==LightType.Directional?127u:mask;
                if(i==0) Undo.RecordObject(light,"Assign floor lighting");
                light.renderingLayerMask=(int)lightMask;
                var data=light.GetComponent<UniversalAdditionalLightData>();
                if(data!=null)
                {
                    if(i==0) Undo.RecordObject(data,"Assign floor lighting");
                    data.renderingLayers=lightMask;
                    data.customShadowLayers=false;
                    RecordOverride(data);
                }
                RecordOverride(light);
            }
        }
        var arrival01=new GameObject("Elevator_Arrival_01");
        Undo.RegisterCreatedObjectUndo(arrival01,"Create arrival marker");
        arrival01.transform.SetParent(floors[0],false);
        arrival01.transform.localPosition=new Vector3(0,0,6.35f);
        ConfigureCollisionLayers(building.transform);
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=building;
        FrameBuilding();
        Debug.Log("Created 7 map floors at 5.2 m spacing, with independent signage and local lighting.");
    }

    private static void RecordOverride(UnityEngine.Object target)
    {
        EditorUtility.SetDirty(target);
        if(PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    public static void ConfigureCollisionLayers(Transform building)
    {
        int wallLayer=LayerMask.NameToLayer("Wall");
        if(wallLayer<0) throw new InvalidOperationException("Create the Wall layer before configuring map collisions.");
        foreach(var collider in building.GetComponentsInChildren<Collider>(true))
        {
            if(collider.isTrigger || collider.GetComponentInParent<NPCQueueAgent>()!=null || collider.gameObject.layer==wallLayer) continue;
            Undo.RecordObject(collider.gameObject,"Assign map collision layer");
            collider.gameObject.layer=wallLayer;
            RecordOverride(collider.gameObject);
        }
        // Seven copies can expose up to 63 spot-light shadow maps at once.
        // 256px maps fit the existing 2048px atlas without automatic downscaling.
        foreach(var light in building.GetComponentsInChildren<Light>(true))
        {
            if(light.shadows==LightShadows.None) continue;
            var data=light.GetComponent<UniversalAdditionalLightData>();
            if(data==null || data.additionalLightsShadowResolutionTier==UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow) continue;
            Undo.RecordObject(data,"Fit seven-floor shadow budget");
            var serializedLight=new SerializedObject(data);
            serializedLight.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue=UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
            serializedLight.ApplyModifiedProperties();
            RecordOverride(data);
        }
        EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
    }

    private static void SetLabels(Transform map,int number)
    {
        foreach(var label in map.GetComponentsInChildren<TextMesh>(true))
        {
            Undo.RecordObject(label,"Set floor signage");
            string value=label.text;
            if(value=="07") value=number.ToString("00");
            else if(value=="LEVEL SEVEN") value="LEVEL "+LevelWords[number-1];
            else if(value=="^  07") value="^  "+number.ToString("00");
            else if(label.transform.parent.name=="Cabin_Controls" && value=="8") value="•";
            else value=Regex.Replace(value,@"\b7(\d{2})\b",m=>number+m.Groups[1].Value);
            label.text=value;
            label.name="Sign_"+value.Replace('\n','_');
            RecordOverride(label);
            RecordOverride(label.gameObject);
        }
        foreach(var t in map.GetComponentsInChildren<Transform>(true))
        {
            if(t.name.StartsWith("Office_Door_7"))
            {
                Undo.RecordObject(t.gameObject,"Number office doors");
                t.name="Office_Door_"+number+t.name.Substring("Office_Door_7".Length);
                RecordOverride(t.gameObject);
            }
            else if(t.name=="Floor_Button_8")
            {
                Undo.RecordObject(t.gameObject,"Name service button");
                t.name="Service_Button";
                RecordOverride(t.gameObject);
            }
        }
    }

    [MenuItem("Tools/The Elevator Lobby/Frame All Seven Floors")]
    public static void FrameBuilding()
    {
        var building=GameObject.Find(BuildingName);
        if(building==null || SceneView.lastActiveSceneView==null) return;
        var bounds=new Bounds(new Vector3(0,FloorSpacing*3+2,0),new Vector3(51,FloorSpacing*6+5.1f,26));
        var view=SceneView.lastActiveSceneView;
        view.rotation=Quaternion.Euler(15,-35,0);
        view.Frame(bounds,false);
        view.Repaint();
    }
}
