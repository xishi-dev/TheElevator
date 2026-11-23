using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
public class FloorProgressionManager : MonoBehaviour
{
    [System.Serializable]
    public class FloorSetup
    {
        public string floorName = "Floor";
        public Transform stopPoint;
        public ElevatorDoorController doorController;
        public Transform spawnPoint;
        public Transform queueStart;
    }

    public enum ShiftPhase { Travel, Unload, Load, Finished }

    [Header("Seven Floors")]
    [SerializeField] private FloorSetup[] floors = new FloorSetup[7];
    [SerializeField] private FloorMission[] missions = FloorMission.DefaultCampaign();
    [Header("Managers")]
    [SerializeField] private LobbyCountdown countdown;
    [SerializeField] private CameraSwitcher cameraSwitcher;
    [SerializeField] private NPCGrabController grabController;
    [SerializeField] private NPCQueueManager queueManager;
    [Header("Elevator References")]
    [SerializeField] private BoxCollider elevatorZone;
    [SerializeField] private Transform elevatorMoveDirection;
    [SerializeField] private Transform[] extraMovingObjects;
    [Header("Travel")]
    [SerializeField] private float travelDuration = 2f;
    [SerializeField] private float fadeDuration = .4f;
    [SerializeField] private CanvasGroup blackFade;
    [Header("Legacy UI")]
    [SerializeField] private TMP_Text currentFloorText;
    [SerializeField] private GameObject finishPanel;
    [SerializeField] private TMP_Text finishText;

    private readonly HashSet<NPCQueueAgent> manifest = new HashSet<NPCQueueAgent>();
    private readonly HashSet<NPCQueueAgent> released = new HashSet<NPCQueueAgent>();
    private readonly HashSet<NPCQueueAgent> credited = new HashSet<NPCQueueAgent>();
    private readonly List<NPCQueueAgent> seated = new List<NPCQueueAgent>();
    private readonly List<NPCQueueAgent> deliveries = new List<NPCQueueAgent>();
    private ShiftHud hud;
    private int floorIndex, unloadTarget, unloaded, spawnBudget, combo;
    private int totalBoarded, totalDelivered, priorityFares;
    private int lastTick = -1;
    private float lastBoardedAt = -10;
    private string feedback;
    private float feedbackUntil;
    private NPCQueueAgent heldAtDeadline;

    public int CurrentFloor => floorIndex + 1;
    public int Score { get; private set; }
    public int OnboardCount { get; private set; }
    public int SpawnBudget => spawnBudget;
    public int UnloadTarget => unloadTarget;
    public int Unloaded => unloaded;
    public ShiftPhase Phase { get; private set; } = ShiftPhase.Travel;
    public bool Won { get; private set; }
    public bool IsPaused { get; private set; }
    public bool CanDispatch => !IsPaused && Phase == ShiftPhase.Load && countdown.IsRunning &&
        OnboardCount >= missions[floorIndex].capacityTarget && queueManager.MissionSpawned == spawnBudget &&
        !grabController.HasPassenger;
    public NPCQueueAgent ActivePriority => Phase == ShiftPhase.Load && queueManager.PriorityPassenger != null &&
        !credited.Contains(queueManager.PriorityPassenger) ? queueManager.PriorityPassenger : null;

    private IEnumerator Start()
    {
        if (!ValidateSetup()) { enabled = false; yield break; }
        hud = gameObject.AddComponent<ShiftHud>();
        hud.Bind(this, cameraSwitcher, grabController);
        ShiftAudio.Ready();
        countdown.StopCountdown();
        if (countdown.countdownText != null) countdown.countdownText.gameObject.SetActive(false);
        if (currentFloorText != null) currentFloorText.gameObject.SetActive(false);
        if (finishText != null) finishText.gameObject.SetActive(false);
        if (finishPanel != null) finishPanel.SetActive(false);
        if (blackFade != null) blackFade.gameObject.SetActive(false);
        grabController.PassengerReleased += TrackRelease;
        grabController.CanGrabPassenger = MayGrab;
        grabController.HandoffPath = FindHandoffPath;
        cameraSwitcher.CanChangeView = () => !grabController.HasPassenger && !IsPaused;

        var wander = FindFirstObjectByType<NPCWalkAwayManager>();
        if (wander != null) wander.enabled = false;
        var bodyCollision = FindFirstObjectByType<NPCBodyCollisionManager>();
        if (bodyCollision != null) bodyCollision.ConfigureCabin(elevatorZone);
        PlaceCameras();
        foreach (var floor in floors)
        {
            floor.doorController.SetManualInputEnabled(false);
            floor.doorController.CloseDoors();
        }
        SetInteraction(false, false);
        queueManager.ConfigureForFloor(floors[0].spawnPoint, floors[0].queueStart, manifest);
        ShowHud();
        yield return Arrive();
    }

    private bool ValidateSetup()
    {
        if (countdown == null || cameraSwitcher == null || grabController == null || queueManager == null ||
            elevatorZone == null || floors == null || floors.Length != 7)
        {
            Debug.LogError("Elevator shift requires seven stops, a timer, cameras, NPC systems and a cabin zone.", this);
            return false;
        }
        if (missions == null || missions.Length != floors.Length) missions = FloorMission.DefaultCampaign();
        int minimumArrival = 0;
        for (int i = 0; i < floors.Length; i++)
        {
            var floor = floors[i];
            if (floor == null || floor.stopPoint == null || floor.doorController == null ||
                floor.spawnPoint == null || floor.queueStart == null || !missions[i].IsFeasibleFrom(minimumArrival))
            {
                Debug.LogError("Invalid scene wiring or unattainable mission for floor " + (i + 1), this);
                return false;
            }
            minimumArrival = missions[i].capacityTarget;
        }
        return true;
    }

    private void PlaceCameras()
    {
        Vector3 forward = CabinForward();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Bounds cabin = elevatorZone.bounds;
        Vector3 entrance = cabin.center - forward * cabin.extents.z;
        entrance.y = floors[0].queueStart.position.y;

        SetCameraPose(cameraSwitcher.cameraLobby,
            entrance - forward * 6.45f - right * 2.5f + Vector3.up * 3.96f,
            entrance - forward * 1.04f + right * 1.42f + Vector3.up * 1.04f, 60f);
        SetCameraPose(cameraSwitcher.cameraElevator,
            entrance + forward * (2 * cabin.extents.z - .39f) + right * .5f + Vector3.up * 2.87f,
            entrance - forward * .55f - right * .12f + Vector3.up * .92f, 71f);
    }

    private static void SetCameraPose(GameObject host, Vector3 position, Vector3 focus, float fov)
    {
        if (host == null) return;
        Camera camera = host.GetComponentInChildren<Camera>(true);
        if (camera == null) return;
        camera.transform.position = position;
        camera.transform.LookAt(focus);
        camera.fieldOfView = fov;
        camera.nearClipPlane = .08f;
    }

    private void OnDestroy()
    {
        if (cameraSwitcher != null) cameraSwitcher.CanChangeView = null;
        if (grabController == null) return;
        grabController.PassengerReleased -= TrackRelease;
        grabController.CanGrabPassenger = null;
        grabController.HandoffPath = null;
    }

    private IEnumerator Arrive()
    {
        FloorSetup floor = floors[floorIndex];
        FloorMission mission = missions[floorIndex];
        ResetFloorProgress();
        spawnBudget = Random.Range(mission.spawnMin, mission.spawnMax + 1);
        unloadTarget = Random.Range(mission.unloadMin, mission.unloadMax + 1);
        queueManager.SetMissionLighting(1u << floorIndex);
        floor.doorController.PlayMotionSound = true;
        floor.doorController.OpenDoors();
        ShiftAudio.Play(ShiftCue.Arrive);
        yield return WaitForDoor(floor.doorController, true);
        if (blackFade != null) blackFade.gameObject.SetActive(false);

        if (unloadTarget > 0)
        {
            Phase = ShiftPhase.Unload;
            SetInteraction(true, false);
            cameraSwitcher.ShowElevator();
            BeginTimer(mission.unloadSeconds);
        }
        else BeginLoading();
    }

    private void ResetFloorProgress()
    {
        released.Clear();
        credited.Clear();
        unloaded = 0;
        combo = 0;
        lastBoardedAt = -10;
        OnboardCount = manifest.Count;
    }

    private void BeginLoading()
    {
        FloorMission mission = missions[floorIndex];
        Phase = ShiftPhase.Load;
        released.Clear();
        queueManager.PrepareMission(spawnBudget, mission.loadSeconds);
        SetInteraction(true, true);
        cameraSwitcher.ShowLobby();
        BeginTimer(mission.loadSeconds);
    }

    private void BeginTimer(float seconds)
    {
        lastTick = -1;
        feedback = null;
        countdown.StartCountdown(seconds);
        ShowHud();
    }

    private bool MayGrab(NPCQueueAgent agent)
    {
        return !agent.IsDelivered && (Phase == ShiftPhase.Load || (Phase == ShiftPhase.Unload && manifest.Contains(agent)));
    }

    private Vector3[] FindHandoffPath(NPCQueueAgent agent)
    {
        if (!CanHandoff(agent)) return null;
        Bounds cabin = elevatorZone.bounds;
        Vector3 forward = CabinForward();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 doorway = cabin.center - forward * cabin.extents.z;
        doorway.y = agent.transform.position.y;
        if (Phase == ShiftPhase.Unload)
            return new[] { doorway + forward * .45f, doorway - forward * 1.3f };

        Vector3 best = doorway + forward * 1.7f;
        float mostRoom = -1;
        for (int row = 0; row < 5; row++)
        {
            for (int column = -2; column <= 2; column++)
            {
                Vector3 candidate = doorway + forward * (.54f + row * .49f) + right * (column * .44f);
                float clearance = 4f;
                foreach (var passenger in queueManager.SpawnedAgents)
                {
                    if (passenger == null || passenger == agent || passenger.IsDelivered) continue;
                    clearance = Mathf.Min(clearance, Vector3.ProjectOnPlane(
                        passenger.transform.position - candidate, Vector3.up).sqrMagnitude);
                }
                if (clearance <= mostRoom) continue;
                best = candidate;
                mostRoom = clearance;
            }
        }
        return new[] { doorway - forward * .57f, doorway + forward * .44f, best };
    }

    public bool CanHandoff(NPCQueueAgent agent)
    {
        if (agent == null || !MayGrab(agent)) return false;
        Bounds cabin = elevatorZone.bounds;
        Vector3 doorway = cabin.center - CabinForward() * cabin.extents.z;
        return Vector3.ProjectOnPlane(agent.transform.position - doorway, Vector3.up).sqrMagnitude <= 16f;
    }

    public bool CanDropHere(NPCQueueAgent agent) => agent != null &&
        (Phase == ShiftPhase.Load && IsInsideCabin(agent) || Phase == ShiftPhase.Unload && IsInLobby(agent));

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        if (grabController == null) return;
        grabController.InputSuspended = paused;
        if (paused) grabController.ReleaseHeldPassenger();
    }

    public void Dispatch()
    {
        if (CanDispatch) StartCoroutine(ResolveStage());
    }

    private void TrackRelease(NPCQueueAgent agent)
    {
        if (!MayGrab(agent)) return;
        released.Add(agent);
    }

    private void Update()
    {
        if (hud == null || IsPaused) return;
        if (Phase != ShiftPhase.Unload && Phase != ShiftPhase.Load) return;
        ReadOccupancy();

        if (Phase == ShiftPhase.Unload && unloaded >= unloadTarget)
        {
            int bonus = Mathf.CeilToInt(countdown.RemainingTime) * 20;
            Score += bonus;
            BeginLoading();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && CanDispatch)
        {
            Dispatch();
            return;
        }

        if (!countdown.IsRunning)
        {
            StartCoroutine(ResolveStage());
            return;
        }

        int seconds = Mathf.CeilToInt(countdown.RemainingTime);
        if (seconds != lastTick && seconds <= 5 && seconds > 0) ShiftAudio.Play(ShiftCue.Tick);
        lastTick = seconds;
        ShowHud();
    }

    private void ReadOccupancy()
    {
        seated.Clear();
        deliveries.Clear();
        var agents = queueManager.SpawnedAgents;
        for (int i = 0; i < agents.Count; i++)
        {
            NPCQueueAgent agent = agents[i];
            if (agent == null || agent.IsDelivered || agent == heldAtDeadline || agent.IsGrabbed) continue;
            if (IsInsideCabin(agent))
            {
                if (!manifest.Contains(agent) && !released.Contains(agent)) continue;
                seated.Add(agent);
                if (Phase == ShiftPhase.Load && !manifest.Contains(agent) && credited.Add(agent))
                {
                    combo = Time.time - lastBoardedAt <= 2.5f ? Mathf.Min(combo + 1, 4) : 1;
                    lastBoardedAt = Time.time;
                    int points = 100 + (combo - 1) * 25;
                    totalBoarded++;
                    Score += points;
                    if (agent == queueManager.PriorityPassenger)
                    {
                        priorityFares++;
                        Score += 250;
                        SetFeedback("PRIORITY FARE   +" + (points + 250));
                    }
                    else SetFeedback(combo > 1 ? combo + "× STREAK   +" + points : "BOARDED   +" + points);
                    ShiftAudio.Play(ShiftCue.Board);
                }
            }
            else if (Phase == ShiftPhase.Unload && manifest.Contains(agent) && released.Contains(agent) && IsInLobby(agent))
                deliveries.Add(agent);
        }
        OnboardCount = seated.Count;
        foreach (var agent in deliveries)
        {
            if (unloaded == unloadTarget) break;
            manifest.Remove(agent);
            Vector3 left = -Vector3.Cross(Vector3.up, CabinForward());
            agent.MarkDelivered(agent.transform.position + left * 5f);
            unloaded++;
            totalDelivered++;
            Score += 125;
            SetFeedback("PASSENGER DELIVERED   +125");
            ShiftAudio.Play(ShiftCue.Unload);
        }
    }

    private IEnumerator ResolveStage()
    {
        ShiftPhase resolved = Phase;
        var mission = missions[floorIndex];
        int timeBonus = Mathf.CeilToInt(countdown.RemainingTime) * 20;
        countdown.StopCountdown();
        heldAtDeadline = grabController.HeldPassenger;
        SetInteraction(false, false);
        // Allow recently released passengers to reach the floor before tallying.
        yield return Settle();
        Phase = resolved;
        ReadOccupancy();
        bool success = resolved == ShiftPhase.Unload ? unloaded >= unloadTarget : OnboardCount >= mission.capacityTarget;
        heldAtDeadline = null;
        if (!success)
        {
            string reason = resolved == ShiftPhase.Unload
                ? "Delivered " + unloaded + " of " + unloadTarget + " passengers."
                : OnboardCount + " passengers on board. The quota was " + mission.capacityTarget + ".";
            Finish(false, reason);
            yield break;
        }
        if (resolved == ShiftPhase.Unload)
        {
            BeginLoading();
            yield break;
        }

        Score += 500 + timeBonus;
        manifest.Clear();
        foreach (var agent in seated) manifest.Add(agent);
        Phase = ShiftPhase.Travel;
        ShowHud();
        FloorSetup current = floors[floorIndex];
        current.doorController.CloseDoors();
        yield return WaitForDoor(current.doorController, false);
        current.doorController.PlayMotionSound = false;
        if (CurrentFloor == floors.Length)
        {
            Finish(true, "All seven floors cleared. " + OnboardCount + " passengers on the final ride.");
            yield break;
        }

        ShiftAudio.Play(ShiftCue.Travel);
        yield return FadeToBlack();
        yield return new WaitForSeconds(Mathf.Clamp(travelDuration, .6f, 1.8f));
        FloorSetup next = floors[floorIndex + 1];
        Vector3 movement = next.stopPoint.position - current.stopPoint.position;
        MoveElevatorObjects(movement);
        queueManager.ConfigureForFloor(next.spawnPoint, next.queueStart, manifest);
        floorIndex++;
        Physics.SyncTransforms();
        yield return Arrive();
    }

    private IEnumerator Settle()
    {
        // Set the state first so the update loop cannot start another resolution.
        Phase = ShiftPhase.Travel;
        ShowHud();
        yield return new WaitForSeconds(.42f);
    }

    private bool IsInsideCabin(NPCQueueAgent agent)
    {
        Vector3 p = agent.transform.position;
        Bounds b = elevatorZone.bounds;
        p.y += .65f;
        return b.Contains(p) && Vector3.Dot(p - b.center, CabinForward()) >= -b.extents.z + .24f;
    }

    private bool IsInLobby(NPCQueueAgent agent)
    {
        Vector3 delta = agent.transform.position - elevatorZone.bounds.center;
        return Vector3.Dot(delta, CabinForward()) <= -elevatorZone.bounds.extents.z - .78f;
    }

    private Vector3 CabinForward()
    {
        if (elevatorMoveDirection != null)
        {
            Vector3 direction = Vector3.ProjectOnPlane(elevatorMoveDirection.forward, Vector3.up);
            if (direction.sqrMagnitude > .01f) return direction.normalized;
        }
        return Vector3.forward;
    }

    private void SetInteraction(bool grab, bool spawn)
    {
        grabController.enabled = grab;
        queueManager.enabled = spawn;
        cameraSwitcher.AllowManualSwitch = grab;
    }

    private void MoveElevatorObjects(Vector3 movement)
    {
        var targets = new HashSet<Transform> { elevatorZone.transform };
        if (cameraSwitcher.cameraLobby != null) targets.Add(cameraSwitcher.cameraLobby.transform);
        if (cameraSwitcher.cameraElevator != null) targets.Add(cameraSwitcher.cameraElevator.transform);
        if (elevatorMoveDirection != null) targets.Add(elevatorMoveDirection);
        if (extraMovingObjects != null)
            foreach (Transform extra in extraMovingObjects) if (extra != null) targets.Add(extra);
        foreach (var target in targets) if (!HasMovedAncestor(target, targets)) target.position += movement;
        foreach (var agent in manifest)
        {
            if (agent == null) continue;
            if (agent.TryGetComponent(out PassengerRagdoll ragdoll)) ragdoll.Stop();
            if (!HasMovedAncestor(agent.transform, targets) && !targets.Contains(agent.transform))
                agent.transform.position += movement;
        }
    }

    private static bool HasMovedAncestor(Transform child, HashSet<Transform> targets)
    {
        for (Transform t = child.parent; t != null; t = t.parent) if (targets.Contains(t)) return true;
        return false;
    }

    private IEnumerator WaitForDoor(ElevatorDoorController door, bool open)
    {
        float end = Time.time + 4;
        while (Time.time < end && !(open ? door.IsFullyOpen : door.IsFullyClosed)) yield return null;
    }

    private IEnumerator FadeToBlack()
    {
        if (blackFade == null) yield break;
        blackFade.gameObject.SetActive(true);
        blackFade.blocksRaycasts = false;
        blackFade.alpha = 0;
        float elapsed = 0;
        float seconds = Mathf.Max(.01f, fadeDuration);
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            blackFade.alpha = Mathf.Clamp01(elapsed / seconds);
            yield return null;
        }
    }

    private void SetFeedback(string message)
    {
        feedback = message;
        feedbackUntil = Time.time + 1.4f;
    }

    private void ShowHud()
    {
        FloorMission mission = missions[floorIndex];
        hud.Refresh(CurrentFloor, mission.name, Phase, countdown.RemainingTime,
            Phase == ShiftPhase.Unload ? mission.unloadSeconds : mission.loadSeconds,
            Phase == ShiftPhase.Unload ? unloaded : OnboardCount,
            Phase == ShiftPhase.Unload ? unloadTarget : mission.capacityTarget,
            queueManager.MissionSpawned, spawnBudget, Score,
            Time.time < feedbackUntil ? feedback : null);
    }

    private void Finish(bool won, string message)
    {
        Won = won;
        Phase = ShiftPhase.Finished;
        SetInteraction(false, false);
        countdown.StopCountdown();
        ShiftAudio.Play(won ? ShiftCue.Win : ShiftCue.Lose);
        int best = Mathf.Max(Score, PlayerPrefs.GetInt("ElevatorShiftBest", 0));
        PlayerPrefs.SetInt("ElevatorShiftBest", best);
        PlayerPrefs.Save();
        hud.ShowResult(won, CurrentFloor, Score, best, message, totalBoarded, totalDelivered, priorityFares);
    }
}
