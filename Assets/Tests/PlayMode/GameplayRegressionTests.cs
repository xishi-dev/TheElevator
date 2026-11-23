using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class GameplayRegressionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private bool originalBackground;
    private Mouse mouse;
    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj,args);
    private static void Set(object obj,string name,object value) => obj.GetType().GetField(name,Flags).SetValue(obj,value);
    private static T Get<T>(object obj,string name) => (T)obj.GetType().GetProperty(name,Flags).GetValue(obj,null);
    private static T Field<T>(object obj,string name) => (T)obj.GetType().GetField(name,Flags).GetValue(obj);
    private static GameObject New(string name) => new GameObject("__Regression_"+name);
    private static Component Add(GameObject go,string name) => go.AddComponent(GameType(name));

    [OneTimeSetUp] public void Begin() { originalBackground=Application.runInBackground;Application.runInBackground=true; }
    [OneTimeTearDown] public void End() { Application.runInBackground=originalBackground; }

    [UnityTearDown]
    public IEnumerator Clean()
    {
        if(mouse!=null) { InputSystem.RemoveDevice(mouse);mouse=null; }
        foreach(var go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(t=>t.name.StartsWith("__Regression_")).Select(t=>t.gameObject).ToArray()) Object.Destroy(go);
        yield return null;
    }

    private static Component Agent(float ground=0)
    {
        var go=New("Agent");go.transform.position=new Vector3(1000,ground,1000);
        return Add(go,"NPCQueueAgent");
    }

    private static void Raise(Component agent)
    {
        Call(agent,"SetGrabbed",true);Call(agent,"LeaveQueue");
        agent.transform.position+=Vector3.up*.7f;
    }

    [UnityTest]
    public IEnumerator RegrabWhileFallingPreservesOriginalGround()
    {
        var agent=Agent(10.4f);Raise(agent);Call(agent,"ReleaseToGround",2.5f);
        yield return new WaitForSeconds(.05f);
        Assert.IsTrue(Get<bool>(agent,"IsLowering"));
        Call(agent,"SetGrabbed",true);
        Assert.AreEqual(10.4f,Get<float>(agent,"GroundY"),.0001f);
        Assert.IsFalse(Get<bool>(agent,"IsLowering"));
        agent.transform.position=new Vector3(1000,Get<float>(agent,"GroundY")+.7f,1000);
        Call(agent,"ReleaseToGround",10f);
        yield return new WaitForSeconds(.3f);
        Assert.AreEqual(10.4f,agent.transform.position.y,.001f);
        Assert.IsFalse(Get<bool>(agent,"IsGrabbed"));
    }

    [UnityTest]
    public IEnumerator ImmediateRegrabAndReleaseCannotRaceOlderDescent()
    {
        var agent=Agent();Raise(agent);
        Call(agent,"ReleaseToGround",1f);Call(agent,"SetGrabbed",true);Call(agent,"ReleaseToGround",10f);
        yield return new WaitForSeconds(.2f);
        Assert.AreEqual(0,agent.transform.position.y,.001f);
        Assert.IsFalse(Get<bool>(agent,"IsLowering"));
    }

    [UnityTest]
    public IEnumerator LegacyAgentWithoutAnimationStillLands()
    {
        var agent=Agent(5.2f);Raise(agent);Call(agent,"ReleaseToGround",10f);
        yield return new WaitForSeconds(.2f);
        Assert.AreEqual(5.2f,agent.transform.position.y,.001f);
        Assert.IsFalse(Get<bool>(agent,"IsGrabbed"));
    }

    private static Component HoldWithController(Component agent)
    {
        var controller=Add(New("GrabController"),"NPCGrabController");
        Set(controller,"grabbedAgent",agent);Set(controller,"grabbedTransform",agent.transform);
        Set(controller,"lowerSpeed",10f);
        return controller;
    }

    [UnityTest]
    public IEnumerator DisabledGrabControllerReleasesNPC()
    {
        var agent=Agent();Raise(agent);var controller=HoldWithController(agent);
        controller.gameObject.SetActive(false);
        yield return new WaitForSeconds(.2f);
        Assert.AreEqual(0,agent.transform.position.y,.001f);
        Assert.IsFalse(Get<bool>(agent,"IsGrabbed"));
    }

    [UnityTest]
    public IEnumerator LostApplicationFocusReleasesNPC()
    {
        var agent=Agent();Raise(agent);var controller=HoldWithController(agent);
        Call(controller,"OnApplicationFocus",false);
        yield return new WaitForSeconds(.2f);
        Assert.AreEqual(0,agent.transform.position.y,.001f);
        Assert.IsFalse(Get<bool>(agent,"IsGrabbed"));
    }

    [UnityTest]
    public IEnumerator QueueMovementCannotChangeFloorHeight()
    {
        var agent=Agent(15.6f);
        Call(agent,"SetTarget",new Vector3(1001,30,1000));
        yield return new WaitForSeconds(.2f);
        Assert.Greater(agent.transform.position.x,1000);
        Assert.AreEqual(15.6f,agent.transform.position.y,.001f);
    }

    private static Component Manager(int queueLimit=3,int totalLimit=3)
    {
        var root=New("Manager");root.SetActive(false);
        var manager=Add(root,"NPCQueueManager");((Behaviour)manager).enabled=false;
        var template=New("Template");template.transform.position=new Vector3(10000,0,10000);Add(template,"NPCQueueAgent");
        var spawn=New("Spawn").transform;spawn.position=new Vector3(1000,0,1000);
        var queue=New("Queue").transform;queue.position=new Vector3(1005,0,1000);
        Set(manager,"npcPrefabs",new GameObject[]{null,template});Set(manager,"spawnPoint",spawn);Set(manager,"queueStart",queue);
        Set(manager,"maxNPCs",queueLimit);Set(manager,"maxSpawnedNPCs",totalLimit);Set(manager,"spawnInterval",.1f);
        root.SetActive(true);
        return manager;
    }

    [UnityTest]
    public IEnumerator TotalSpawnCapIncludesNPCsRemovedFromQueue()
    {
        var manager=Manager(1,2);
        Assert.IsTrue((bool)Call(manager,"TrySpawnNPC"));
        var list=Field<IList>(manager,"npcList");var first=(Component)list[0];Call(manager,"RemoveFromQueue",first);
        Assert.IsTrue((bool)Call(manager,"TrySpawnNPC"));
        Call(manager,"RemoveFromQueue",list[0]);
        Assert.IsFalse((bool)Call(manager,"TrySpawnNPC"));
        Assert.AreEqual(2,Get<int>(manager,"SpawnedNPCCount"));
        Object.Destroy(first.gameObject);yield return null;
        Assert.IsTrue((bool)Call(manager,"TrySpawnNPC"));
        Assert.AreEqual(2,Get<int>(manager,"SpawnedNPCCount"));
    }

    [UnityTest]
    public IEnumerator DestroyedQueueMembersDoNotLeaveEmptySlots()
    {
        var manager=Manager();Call(manager,"TrySpawnNPC");Call(manager,"TrySpawnNPC");
        var list=Field<IList>(manager,"npcList");Object.Destroy(((Component)list[0]).gameObject);
        yield return null;Call(manager,"UpdateQueuePositions");
        Assert.AreEqual(1,Get<int>(manager,"QueuedNPCCount"));
        var remaining=(Component)list[0];
        Assert.AreEqual(Field<Transform>(manager,"queueStart").position,Field<Vector3>(remaining,"targetPosition"));
    }

    [UnityTest]
    public IEnumerator DisableAndReenableSpawnerStartsOnlyOneLoop()
    {
        var manager=Manager(20,20);((Behaviour)manager).enabled=true;
        yield return null;
        ((Behaviour)manager).enabled=false;int stopped=Get<int>(manager,"SpawnedNPCCount");
        yield return new WaitForSeconds(.25f);
        Assert.AreEqual(stopped,Get<int>(manager,"SpawnedNPCCount"));
        ((Behaviour)manager).enabled=true;yield return new WaitForSeconds(.25f);
        int added=Get<int>(manager,"SpawnedNPCCount")-stopped;
        Assert.That(added,Is.InRange(2,4));
    }

    [Test]
    public void QueueDirectionIgnoresVerticalComponent()
    {
        var manager=Manager();Set(manager,"queueDirection",new Vector3(1,20,0));
        Call(manager,"TrySpawnNPC");Call(manager,"TrySpawnNPC");Call(manager,"UpdateQueuePositions");
        var list=Field<IList>(manager,"npcList");
        Assert.AreEqual(Field<Transform>(manager,"queueStart").position.y,Field<Vector3>(list[1],"targetPosition").y,.001f);
    }

    private static Component CollisionController()
    {
        var agent=Agent(1000);var capsule=agent.gameObject.AddComponent<CapsuleCollider>();
        capsule.height=1.7f;capsule.radius=.25f;capsule.center=new Vector3(0,.85f,0);
        var controller=HoldWithController(agent);
        Set(controller,"grabbedCapsule",capsule);Set(controller,"obstacleLayers",(LayerMask)(1<<LayerMask.NameToLayer("Wall")));
        return controller;
    }

    [Test]
    public void DraggingStopsBeforeWall()
    {
        var controller=CollisionController();
        var current=Field<Transform>(controller,"grabbedTransform").position;
        var wall=New("Wall");wall.layer=LayerMask.NameToLayer("Wall");wall.transform.position=current+new Vector3(2,1,0);
        wall.AddComponent<BoxCollider>().size=new Vector3(.2f,4,4);
        Physics.SyncTransforms();
        var safe=(Vector3)Call(controller,"GetSafePosition",current,current+Vector3.right*5);
        Assert.That(safe.x-current.x,Is.InRange(1.4f,1.66f));
    }

    [Test]
    public void GroundContactDoesNotBlockHorizontalDragging()
    {
        var controller=CollisionController();
        var current=Field<Transform>(controller,"grabbedTransform").position;
        var floor=New("Floor");floor.layer=LayerMask.NameToLayer("Wall");floor.transform.position=current+new Vector3(0,-.1f,0);
        floor.AddComponent<BoxCollider>().size=new Vector3(20,.2f,20);
        Physics.SyncTransforms();
        var desired=current+Vector3.right*3;
        var safe=(Vector3)Call(controller,"GetSafePosition",current,desired);
        Assert.AreEqual(desired.x,safe.x,.001f);
    }

    [Test]
    public void NPCBehindWallCannotBeSelected()
    {
        var agent=Agent(1000);agent.gameObject.layer=LayerMask.NameToLayer("NPC");
        var capsule=agent.gameObject.AddComponent<CapsuleCollider>();capsule.height=1.7f;capsule.center=new Vector3(0,.85f,0);
        var camera=New("Camera").AddComponent<Camera>();camera.transform.position=agent.transform.position+new Vector3(0,.85f,-5);
        camera.enabled=false;
        var controller=Add(New("GrabController"),"NPCGrabController");
        Set(controller,"playerCamera",camera);Set(controller,"npcLayer",(LayerMask)(1<<LayerMask.NameToLayer("NPC")));
        Set(controller,"obstacleLayers",(LayerMask)(1<<LayerMask.NameToLayer("Wall")));
        var wall=New("Occluder");wall.layer=LayerMask.NameToLayer("Wall");wall.transform.position=camera.transform.position+Vector3.forward*2;
        wall.AddComponent<BoxCollider>().size=new Vector3(4,4,.2f);
        mouse=InputSystem.AddDevice<Mouse>();
        InputSystem.QueueDeltaStateEvent(mouse.position,(Vector2)camera.WorldToScreenPoint(agent.transform.position+Vector3.up*.85f));InputSystem.Update();
        Physics.SyncTransforms();Call(controller,"TryGrabNPC");
        Assert.IsFalse(Get<bool>(agent,"IsGrabbed"));
        wall.SetActive(false);Physics.SyncTransforms();Call(controller,"TryGrabNPC");
        Assert.IsTrue(Get<bool>(agent,"IsGrabbed"),"Control case: the same NPC must be selectable after removing the wall.");
    }

#if UNITY_EDITOR
    [UnityTest]
    public IEnumerator AnimatedBoundsContainAllElevenPoses()
    {
        var paths=UnityEditor.AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Models_Characters/Animated"})
            .Select(UnityEditor.AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("_Animated.prefab")).ToArray();
        foreach(var path in paths)
        {
            var go=Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path));go.name="__Regression_Bounds";
            ((Behaviour)go.GetComponent(GameType("NPCCharacterAnimation"))).enabled=false;
            var animator=go.GetComponentInChildren<Animator>();animator.enabled=false;
            var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
            foreach(var clip in animator.runtimeAnimatorController.animationClips.Distinct())
                foreach(var phase in new[]{0f,.35f,.7f})
                {
                    clip.SampleAnimation(skin.gameObject,clip.length*phase);skin.BakeMesh(baked);
                    var bounds=skin.bounds;bounds.Expand(.001f);
                    Assert.IsTrue(bounds.Contains(skin.transform.TransformPoint(baked.bounds.min)),path+"/"+clip.name+" minimum");
                    Assert.IsTrue(bounds.Contains(skin.transform.TransformPoint(baked.bounds.max)),path+"/"+clip.name+" maximum");
                }
            Object.Destroy(baked);Object.Destroy(go);yield return null;
        }
    }

    [UnityTest]
    public IEnumerator AllNineAnimatorsSupportGrabFallLandAndRandomGestures()
    {
        var paths=UnityEditor.AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Models_Characters/Animated"})
            .Select(UnityEditor.AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("_Animated.prefab")).ToArray();
        Assert.AreEqual(9,paths.Length);
        foreach(var path in paths)
        {
            var go=Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path));go.name="__Regression_Animated";
            var driver=go.GetComponent(GameType("NPCCharacterAnimation"));var animator=go.GetComponentInChildren<Animator>();
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Update(.1f);
            foreach(var state in new[]{"Grabbed","Falling","Land"})
            {
                if(state=="Grabbed")Call(driver,"SetHeld",true);if(state=="Falling")Call(driver,"BeginFall");if(state=="Land")Call(driver,"Land");
                for(int i=0;i<4;i++)animator.Update(.1f);
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName(state),path+" -> "+state);
            }
            Call(driver,"SetHeld",false);Set(driver,"nextGesture",Time.time-1);Call(driver,"LateUpdate");
            Assert.GreaterOrEqual(Convert.ToInt32(Get<object>(driver,"CurrentMotion")),5);
            go.transform.position+=Vector3.right*.2f;Call(driver,"LateUpdate");
            Assert.AreEqual("Walk",Get<object>(driver,"CurrentMotion").ToString());
            var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();Assert.AreEqual(19,skin.bones.Length);
            Assert.IsTrue(skin.sharedMesh.boneWeights.All(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.001f));
            Object.Destroy(go);yield return null;
        }
    }

    [UnityTest]
    public IEnumerator SevenFloorSceneHasValidCollisionsAndUniqueGameplaySystems()
    {
        Assert.AreEqual("Assets/TheElevatorLobby/Scenes/mn.unity",UnityEditor.EditorBuildSettings.scenes.First(s=>s.enabled).path);
        Assert.IsTrue(UnityEditor.EditorBuildSettings.scenes.Any(s=>s.enabled && s.path.EndsWith("/Elevator_Lobby_Main.unity")));
        yield return SceneManager.LoadSceneAsync("Assets/TheElevatorLobby/Scenes/Elevator_Lobby_Main.unity",LoadSceneMode.Additive);
        yield return null;
        var scene=SceneManager.GetSceneByName("Elevator_Lobby_Main");
        var roots=scene.GetRootGameObjects();var building=roots.Single(g=>g.name=="Building_7_Floors");
        var floors=building.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Floor_")).ToArray();
        Assert.AreEqual(7,floors.Length);
        foreach(Transform floor in floors)
        {
            int number=int.Parse(floor.name.Substring(6));Assert.AreEqual((number-1)*5.2f,floor.position.y,.001f);
            Assert.IsTrue(floor.GetComponentsInChildren<TextMesh>(true).Any(t=>t.text==number.ToString("00")));
            Assert.IsTrue(floor.GetComponentsInChildren<Renderer>(true).All(r=>r.renderingLayerMask==(1u<<(number-1))));
        }
        Assert.IsTrue(building.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger).All(c=>c.gameObject.layer==LayerMask.NameToLayer("Wall")));
        var behaviours=roots.SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
        Assert.IsTrue(behaviours.All(c=>c!=null),"Missing script in the gameplay scene.");
        Assert.AreEqual(1,behaviours.Count(c=>c.GetType().Name=="NPCQueueManager"));
        Assert.AreEqual(1,behaviours.Count(c=>c.GetType().Name=="NPCGrabController"));
        Assert.AreEqual(1,building.GetComponentsInChildren<Camera>(true).Count(c=>c.isActiveAndEnabled));
        var manager=behaviours.Single(c=>c.GetType().Name=="NPCQueueManager");Assert.AreEqual(9,Field<GameObject[]>(manager,"npcPrefabs").Count(p=>p!=null));
        var cameras=behaviours.Single(c=>c.GetType().Name=="CameraSwitcher");
        Call(cameras,"ShowElevator");Assert.IsTrue(Get<bool>(cameras,"IsElevatorView"));
        Assert.IsTrue(Get<Camera>(cameras,"ActiveCamera").gameObject.activeInHierarchy);
        Call(cameras,"ShowLobby");Assert.IsFalse(Get<bool>(cameras,"IsElevatorView"));
        Assert.AreEqual(1,building.GetComponentsInChildren<Camera>(true).Count(c=>c.isActiveAndEnabled));
        var doorControllers=behaviours.Where(c=>c.GetType().Name=="ElevatorDoorController").ToArray();
        Assert.AreEqual(7,doorControllers.Length);
        var doors=doorControllers.Single(d=>d.GetComponentsInParent<Transform>(true).Any(t=>t.name=="Floor_01"));
        yield return new WaitForSeconds(1.2f);
        Call(doors,"CloseDoors");yield return new WaitForSeconds(1.2f);
        Assert.Less(Vector3.Distance(Field<Transform>(doors,"leftDoor").localPosition,Field<Vector3>(doors,"leftClosedPos")),.001f);
        Assert.Less(Vector3.Distance(Field<Transform>(doors,"rightDoor").localPosition,Field<Vector3>(doors,"rightClosedPos")),.001f);
        Call(doors,"OpenDoors");yield return new WaitForSeconds(1.1f);
        Assert.Less(Vector3.Distance(Field<Transform>(doors,"leftDoor").localPosition,Field<Vector3>(doors,"leftOpenPos")),.001f);
        Assert.Less(Vector3.Distance(Field<Transform>(doors,"rightDoor").localPosition,Field<Vector3>(doors,"rightOpenPos")),.001f);
        yield return SceneManager.UnloadSceneAsync(scene);
    }
#endif
}
