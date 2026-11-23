#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ShiftCampaignTests
{
    private const string Gameplay = "Assets/TheElevatorLobby/Scenes/Elevator_Lobby_Main.unity";
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private Scene scene;
    private float previousScale;
    private int previousBest;
    private bool hadBest, previousBackground;

    [SetUp]
    public void SaveSettings()
    {
        previousScale = Time.timeScale;
        hadBest = PlayerPrefs.HasKey("ElevatorShiftBest");
        previousBest = PlayerPrefs.GetInt("ElevatorShiftBest");
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = previousScale;
        if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        if (hadBest) PlayerPrefs.SetInt("ElevatorShiftBest", previousBest);
        else PlayerPrefs.DeleteKey("ElevatorShiftBest");
        PlayerPrefs.Save();
        Application.runInBackground = previousBackground;
    }

    [Test]
    public void AllSevenQuotasWorkWithWorstCaseArrivals()
    {
        Type missionType = Type.GetType("FloorMission, Assembly-CSharp", true);
        var missions = (Array)missionType.GetMethod("DefaultCampaign").Invoke(null, null);
        Assert.AreEqual(7, missions.Length);
        int lowestArrival = 0;
        foreach (var mission in missions)
        {
            Assert.IsTrue((bool)Call(mission, "IsFeasibleFrom", lowestArrival));
            lowestArrival = Field<int>(mission, "capacityTarget");
        }
        var first = missions.GetValue(0);
        var second = missions.GetValue(1);
        Assert.AreEqual(17, Field<int>(first, "capacityTarget"));
        Assert.AreEqual(20, Field<int>(first, "spawnMin"));
        Assert.AreEqual(25, Field<int>(first, "spawnMax"));
        Assert.AreEqual(25, Field<int>(second, "capacityTarget"));
        Assert.AreEqual(18, Field<int>(second, "spawnMin"));
        Assert.AreEqual(22, Field<int>(second, "spawnMax"));
        Assert.AreEqual(5, Field<int>(second, "unloadMin"));
        Assert.AreEqual(7, Field<int>(second, "unloadMax"));
    }

    [UnityTest]
    public IEnumerator TimedWaveSpawnsExactBudgetAndLosingShowsRetry()
    {
        yield return SceneManager.LoadSceneAsync(Gameplay, LoadSceneMode.Additive);
        scene = SceneManager.GetSceneByPath(Gameplay);
        var mission = Find("FloorProgressionManager");
        var queue = Find("NPCQueueManager");
        UseLightweightTestPrefab(queue);
        Time.timeScale = 5;
        yield return WaitFor(() => Phase(mission) == "Finished", "25-second round did not finish.");
        Assert.IsFalse(Property<bool>(mission, "Won"));
        Assert.AreEqual(Property<int>(mission, "SpawnBudget"), Property<int>(queue, "MissionSpawned"));
        Assert.That(Property<int>(queue, "MissionSpawned"), Is.InRange(20, 25));
        Assert.AreEqual(1, Property<int>(mission, "CurrentFloor"));
        Assert.IsTrue(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Button>()).Any(b => b.name == "RETRY SHIFT"));
    }

    [UnityTest]
    public IEnumerator HandoffAndPassengerQuotasCanCompleteAllSevenFloors()
    {
        yield return SceneManager.LoadSceneAsync(Gameplay, LoadSceneMode.Additive);
        scene = SceneManager.GetSceneByPath(Gameplay);
        var mission = Find("FloorProgressionManager");
        var queue = Find("NPCQueueManager");
        var grab = Find("NPCGrabController");
        var clock = Find("LobbyCountdown");
        var cameras = Find("CameraSwitcher");
        UseLightweightTestPrefab(queue);
        Set(mission, "travelDuration", .6f);
        Time.timeScale = 2;
        yield return WaitFor(() => Phase(mission) == "Load" && Agents(queue).Length >= 5, "The first queue did not start.");
        var hud = Find("ShiftHud");
        Assert.IsNotNull(Property<Component>(mission, "ActivePriority"));
        Call(hud, "OpenPause");
        Assert.IsTrue(Property<bool>(mission, "IsPaused"));
        yield return null;
        float remaining = Property<float>(clock, "RemainingTime");
        yield return null;
        Assert.AreEqual(remaining, Property<float>(clock, "RemainingTime"));
        Call(hud, "Resume");
        Assert.AreEqual(2f, Time.timeScale);
        var zone = Field<BoxCollider>(mission, "elevatorZone");
        var passenger = Agents(queue)[0];
        SimulateHandoff(grab, queue, passenger);
        Camera lobbyView = Property<Camera>(cameras, "ActiveCamera");
        Call(cameras, "TryToggle");
        Assert.AreSame(lobbyView, Property<Camera>(cameras, "ActiveCamera"), "The view changed while a passenger was held.");
        yield return WaitFor(() => Property<int>(mission, "OnboardCount") == 1, "Handoff did not reach the cabin.");
        Call(cameras, "TryToggle");
        Assert.IsTrue(Property<bool>(cameras, "IsElevatorView"));
        Call(cameras, "TryToggle");
        Assert.AreSame(lobbyView, Property<Camera>(cameras, "ActiveCamera"));
        Assert.IsTrue(Object.FindObjectsByType<ConfigurableJoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(joint => joint.transform.root.name == "Passenger limb physics"), "Landing did not initialize ragdoll joints.");
        FillQueue(queue);
        BoardUntil(mission, queue, zone, 17);
        yield return WaitFor(() => Property<int>(mission, "OnboardCount") >= 17, "First floor quota did not tally.");
        Assert.IsNull(Property<Component>(mission, "ActivePriority"));
        Assert.AreEqual(1, Field<int>(mission, "priorityFares"));
        Call(clock, "StopCountdown");
        yield return WaitFor(() => Property<int>(mission, "CurrentFloor") == 2 && Phase(mission) == "Unload", "Floor 2 did not open.");
        Assert.AreEqual(17, Agents(queue).Length);
        int requiredOut = Property<int>(mission, "UnloadTarget");
        Assert.That(requiredOut, Is.InRange(5, 7));
        Assert.That(Property<float>(clock, "RemainingTime"), Is.InRange(10f, 15f));
        int scoreBefore = Property<int>(mission, "Score");
        var firstOut = Agents(queue)[0];
        SimulateHandoff(grab, queue, firstOut);
        yield return WaitFor(() => Property<int>(mission, "Unloaded") == 1, "Handoff did not reach the lobby.");
        Assert.IsTrue(Property<bool>(firstOut, "IsDelivered"));
        int offloaded = 1;
        foreach (var agent in Agents(queue).Where(a => a != firstOut).Take(requiredOut - 1))
        {
            Vector3 lobby = zone.bounds.center - Vector3.forward * (zone.bounds.extents.z + 1.4f);
            lobby.x += offloaded++ * .95f;
            Place(mission, queue, agent, lobby);
        }
        yield return WaitFor(() => Phase(mission) == "Load", "Successful unload did not open the loading wave.");
        Assert.AreEqual(requiredOut, Property<int>(mission, "Unloaded"));
        Assert.Greater(Property<int>(mission, "Score"), scoreBefore);
        Assert.That(Property<int>(mission, "SpawnBudget"), Is.InRange(18, 22));
        Assert.That(Property<float>(clock, "RemainingTime"), Is.InRange(23f, 25f));
        FillQueue(queue);
        Assert.AreEqual(Property<int>(mission, "SpawnBudget"), Property<int>(queue, "MissionSpawned"));
        BoardUntil(mission, queue, zone, 25);
        yield return WaitFor(() => Property<int>(mission, "OnboardCount") >= 25, "Second floor should count continuing passengers.");
        Call(clock, "StopCountdown");
        for (int floor = 3; floor <= 7; floor++)
        {
            int currentFloor = floor;
            int goal = 23 + floor;
            yield return WaitFor(() => Property<int>(mission, "CurrentFloor") == currentFloor && Phase(mission) == "Unload",
                "Elevator did not open at floor " + floor + ".");
            int toDeliver = Property<int>(mission, "UnloadTarget");
            int index = 0;
            foreach (var agent in Agents(queue).Take(toDeliver))
            {
                Vector3 lobby = zone.bounds.center - Vector3.forward * (zone.bounds.extents.z + 1.5f);
                lobby.x += (index % 7 - 3) * .85f;
                lobby.z -= index++ / 7 * .85f;
                Place(mission, queue, agent, lobby);
            }
            yield return WaitFor(() => Phase(mission) == "Load", "Unload failed at floor " + floor + ".");
            FillQueue(queue);
            BoardUntil(mission, queue, zone, goal);
            yield return WaitFor(() => Property<int>(mission, "OnboardCount") >= goal, "Load quota failed on floor " + floor + ".");
            Call(clock, "StopCountdown");
        }
        yield return WaitFor(() => Phase(mission) == "Finished", "Final results did not open.");
        Assert.IsTrue(Property<bool>(mission, "Won"));
        Assert.AreEqual(7, Property<int>(mission, "CurrentFloor"));
        Assert.Greater(Property<int>(mission, "Score"), 3500);
    }

    private Component Find(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp", true);
        return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren(type, true)).Single();
    }

    private static void UseLightweightTestPrefab(Component queue)
    {
        var prefabs = Field<GameObject[]>(queue, "npcPrefabs");
        Set(queue, "npcPrefabs", new[] { prefabs.First(p => p.name.StartsWith("Male_04")) });
    }

    private static IEnumerator WaitFor(Func<bool> check, string message)
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (!check() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(check(), message);
    }

    private static void SimulateHandoff(Component grab, Component queue, Component agent)
    {
        Call(agent, "SetGrabbed", true);
        Call(queue, "RemoveFromQueue", agent);
        Set(grab, "grabbedAgent", agent);
        Set(grab, "grabbedTransform", agent.transform);
        Set(grab, "grabbedCapsule", agent.GetComponent<CapsuleCollider>());
        Set(grab, "originalY", agent.transform.position.y);
        Call(grab, "BeginHandoff");
    }

    private static void FillQueue(Component queue)
    {
        for (int i = 0; i < 25 && (bool)Call(queue, "TrySpawnNPC"); i++) { }
    }

    private static void BoardUntil(Component mission, Component queue, BoxCollider zone, int target)
    {
        int onboard = Property<int>(mission, "OnboardCount");
        var candidates = Agents(queue).Where(a => Property<bool>(a, "IsInQueue")).Take(target - onboard).ToArray();
        Assert.AreEqual(target - onboard, candidates.Length);
        int number = 0;
        foreach (var agent in Agents(queue).Where(a => (bool)Call(mission, "IsInsideCabin", a)))
        {
            Vector3 position = TestSeat(zone.bounds.center, number++);
            position.y = agent.transform.position.y;
            agent.transform.position = position;
        }
        foreach (var agent in candidates)
        {
            Place(mission, queue, agent, TestSeat(zone.bounds.center, number++));
        }
    }

    private static Vector3 TestSeat(Vector3 cabin, int number) =>
        cabin + new Vector3((number % 6 - 2.5f) * .4f, 0, -.94f + number / 6 * .45f);

    private static void Place(Component mission, Component queue, Component agent, Vector3 position)
    {
        Call(agent, "SetGrabbed", true);
        Call(queue, "RemoveFromQueue", agent);
        position.y = Property<float>(agent, "GroundY");
        agent.transform.position = position;
        Call(mission, "TrackRelease", agent);
        Call(agent, "SetGrabbed", false);
    }

    private static Component[] Agents(Component queue) => ((IEnumerable)Property<object>(queue, "SpawnedAgents")).Cast<Component>().Where(a => a != null).ToArray();
    private static string Phase(Component mission) => Property<object>(mission, "Phase").ToString();
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Instance).Invoke(target, args);
    private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name, Instance).GetValue(target);
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Instance).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Instance).SetValue(target, value);
}
#endif
