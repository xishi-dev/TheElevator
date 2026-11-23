using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ShiftHud : MonoBehaviour
{
    private static readonly Color Ink = new Color32(20, 25, 23, 255);
    private static readonly Color Panel = new Color32(24, 31, 29, 245);
    private static readonly Color Line = new Color32(62, 73, 67, 255);
    private static readonly Color Paper = new Color32(246, 242, 225, 255);
    private static readonly Color Quiet = new Color32(187, 198, 187, 255);
    private static readonly Color Orange = new Color32(255, 177, 107, 255);
    private static readonly Color Green = new Color32(185, 230, 172, 255);
    private static readonly Color Red = new Color32(255, 141, 126, 255);
    private static readonly Vector2 Center = new Vector2(.5f, .5f);
    private static readonly Vector2 TopLeft = new Vector2(0, 1);
    private static readonly Vector2 TopRight = Vector2.one;
    private static readonly Vector2 Bottom = new Vector2(.5f, 0);

    private static Font bodyFont;
    private static Font displayFont;
    private RectTransform surface, gameplay, modal, dispatch, pointer, priority;
    private CanvasScaler scaler;
    private CanvasGroup screenControls;
    private Button menuSettings;
    private RawImage lobbyImage;
    private Texture2D menuShade;
    private Text missionName, floorNumber, objective, quota, arrivals, timer, score, cameraName, feedbackLabel, hint, audioStatus;
    private Image timerFill, quotaFill;
    private Graphic[] pointerCorners;
    private readonly Text[] stops = new Text[7];
    private FloorProgressionManager shift;
    private CameraSwitcher cameras;
    private NPCGrabController grab;
    private MainMenu mainMenu;
    private bool finished;
    private bool settingsOpen;
    private bool ownsPause;
    private float resumeTimeScale;
    private string feedback;
    private float feedbackStarted;

    public static ShiftHud OpenMenu(MainMenu menu)
    {
        ShiftHud view = menu.gameObject.AddComponent<ShiftHud>();
        view.mainMenu = menu;
        view.BuildMenu();
        return view;
    }

    public void Bind(FloorProgressionManager controller, CameraSwitcher views, NPCGrabController input)
    {
        shift = controller;
        cameras = views;
        grab = input;
    }

    private void Update()
    {
        FitScreen();
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.mKey.wasPressedThisFrame) ToggleAudio();
        if (!keyboard.escapeKey.wasPressedThisFrame || finished) return;
        if (modal != null)
        {
            if (settingsOpen) CloseSettings();
            else if (shift != null) Resume();
        }
        else if (shift != null) OpenPause();
    }

    private void FitScreen()
    {
        if (scaler == null || Screen.height == 0) return;
        float aspect = Screen.width / (float)Screen.height;
        float referenceAspect = scaler.referenceResolution.x / scaler.referenceResolution.y;
        scaler.matchWidthOrHeight = aspect > referenceAspect ? 1f : 0f;
        if (lobbyImage == null) return;
        float artAspect = lobbyImage.texture.width / (float)lobbyImage.texture.height;
        float width = Mathf.Min(1, aspect / artAspect);
        float height = Mathf.Min(1, artAspect / aspect);
        lobbyImage.uvRect = new Rect((1 - width) * .72f, (1 - height) * .5f, width, height);
    }

    private void ToggleAudio()
    {
        ShiftAudio.ToggleMute();
        Set(audioStatus, ShiftAudio.Muted ? "SOUND OFF" : "SOUND ON");
    }

    public void Refresh(int floor, string mission, FloorProgressionManager.ShiftPhase phase,
        float remaining, float duration, int count, int target, int spawned, int total, int points, string message)
    {
        EnsureHud();
        bool transit = phase == FloorProgressionManager.ShiftPhase.Travel;
        bool unload = phase == FloorProgressionManager.ShiftPhase.Unload;
        bool met = count >= target;
        int seconds = Mathf.CeilToInt(remaining);
        Color stageColor = unload ? Orange : Green;
        Set(floorNumber, floor.ToString("00"));
        Set(missionName, mission.ToUpperInvariant());
        Set(objective, transit ? "WAIT FOR DOORS" : unload ? "DROP OFF IN THE LOBBY" : "BOARD THE ELEVATOR");
        Set(quota, transit ? "IN TRANSIT" : count + " / " + target);
        quota.color = met && !transit ? stageColor : Paper;
        quotaFill.color = stageColor;
        quotaFill.rectTransform.localScale = new Vector3(transit ? 0 : Mathf.Clamp01(count / (float)Mathf.Max(1, target)), 1, 1);
        Set(arrivals, transit ? count + " passengers aboard" : met ? "Quota met" : unload
            ? (target - count) + " more to drop off"
            : spawned + " of " + total + " arrivals");
        Set(timer, transit ? "–" : seconds.ToString("00"));
        timer.color = seconds <= 5 && !transit ? Red : Paper;
        timerFill.color = seconds <= 5 && !transit ? Red : Orange;
        timerFill.rectTransform.localScale = new Vector3(transit ? 0 : Mathf.Clamp01(remaining / Mathf.Max(.01f, duration)), 1, 1);
        Set(score, points.ToString("N0"));
        for (int i = 0; i < stops.Length; i++)
            stops[i].color = i + 1 == floor ? Orange : i + 1 < floor ? Green : Quiet;
        if (feedback != message)
        {
            feedback = message;
            feedbackStarted = Time.time;
            Set(feedbackLabel, message ?? "");
        }
    }

    private void LateUpdate()
    {
        if (gameplay == null || finished || modal != null) return;
        if (shift == null || grab == null || cameras == null) return;
        bool moving = shift.Phase == FloorProgressionManager.ShiftPhase.Travel;
        NPCQueueAgent held = grab.HeldPassenger;
        NPCQueueAgent hovered = grab.HoveredPassenger;
        NPCQueueAgent fare = shift.ActivePriority;
        Camera camera = cameras.ActiveCamera;
        bool safe = shift.CanDropHere(held);
        bool handoff = shift.CanHandoff(held);
        dispatch.gameObject.SetActive(shift.CanDispatch);
        Set(cameraName, cameras.IsElevatorView ? "CABIN VIEW" : "LOBBY VIEW");
        Set(hint, moving ? "" : held == null
            ? (hovered == null ? "" : hovered == fare ? "Priority fare   +250" :
                shift.CanHandoff(hovered) ? "Press E to hand off" : "Hold to lift")
            : grab.IsHandingOff ? "Handing off…" : safe ? "Release to count" : handoff
                ? "Press E to hand off" : "Drag toward the doors");
        hint.color = safe ? Green : Paper;
        NPCQueueAgent tracked = held != null ? held : hovered;
        PlaceMarker(pointer, camera, tracked, new Vector3(0, .95f, 0), false);
        foreach (Graphic corner in pointerCorners)
            corner.color = safe ? Green : tracked != null && tracked == fare ? Orange : Paper;
        PlaceMarker(priority, camera, fare, new Vector3(0, 2.08f, 0), true);
        float age = Time.time - feedbackStarted;
        feedbackLabel.color = new Color(Green.r, Green.g, Green.b, Mathf.Clamp01((1.4f - age) * 4f));
        feedbackLabel.rectTransform.anchoredPosition = new Vector2(0, 120 + Mathf.Min(age, .35f) * 28);
    }

    private void PlaceMarker(RectTransform marker, Camera camera, NPCQueueAgent passenger, Vector3 offset, bool testVisibility)
    {
        if (camera == null || passenger == null)
        {
            marker.gameObject.SetActive(false);
            return;
        }
        Vector3 world = passenger.transform.position + offset;
        Vector3 frame = camera.WorldToScreenPoint(world);
        bool visible = frame.z > 0 && frame.x > 20 && frame.x < Screen.width - 20 &&
            frame.y > 62 && frame.y < Screen.height - 22 && (!testVisibility || grab.IsVisibleFrom(camera, world));
        marker.gameObject.SetActive(visible);
        if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, frame, null, out Vector2 local))
            marker.anchoredPosition = local;
    }

    private void EnsureHud()
    {
        if (surface != null) return;
        CreateSurface();
        gameplay = Stretch(surface, "Gameplay HUD");
        screenControls = gameplay.gameObject.AddComponent<CanvasGroup>();
        pointer = Rect(gameplay, "Passenger focus", Center, Vector2.zero, new Vector2(50, 70));
        pointerCorners = new Graphic[8];
        for (int i = 0; i < 4; i++)
        {
            float x = (i & 1) == 0 ? -1 : 1;
            float y = (i & 2) == 0 ? -1 : 1;
            pointerCorners[i * 2] = Plate(pointer, "Horizontal corner", Center, new Vector2(x * 22, y * 29), new Vector2(12, 3), Paper);
            pointerCorners[i * 2 + 1] = Plate(pointer, "Vertical corner", Center, new Vector2(x * 27, y * 24), new Vector2(3, 12), Paper);
        }
        pointer.gameObject.SetActive(false);
        priority = Rect(gameplay, "Priority fare marker", Center, Vector2.zero, new Vector2(94, 28));
        Plate(priority, "Marker backing", Center, Vector2.zero, new Vector2(94, 24), Ink);
        Plate(priority, "Fare diamond", Center, new Vector2(-32, 0), new Vector2(8, 8), Orange)
            .rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        TextAt(priority, "Fare label", "PRIORITY", Center, new Vector2(7, 0), new Vector2(67, 23), 14, Orange, display: true);
        priority.gameObject.SetActive(false);

        var manifest = Plate(gameplay, "Passenger manifest", TopLeft, new Vector2(177, -112), new Vector2(310, 180), Panel).rectTransform;
        Plate(manifest, "Floor divider", TopLeft, new Vector2(155, -61), new Vector2(270, 1), Line);
        floorNumber = TextAt(manifest, "Current floor", "01", TopLeft, new Vector2(45, -32), new Vector2(54, 47), 37, Orange, display: true);
        TextAt(manifest, "Floor caption", "FLOOR", TopLeft, new Vector2(200, -20), new Vector2(210, 17), 13, Quiet, TextAnchor.MiddleLeft);
        missionName = TextAt(manifest, "Mission", "", TopLeft, new Vector2(197, -39), new Vector2(220, 27), 22, Paper, TextAnchor.MiddleLeft, true);
        objective = TextAt(manifest, "Current task", "", TopLeft, new Vector2(155, -81), new Vector2(270, 24), 17, Quiet, TextAnchor.MiddleLeft);
        quota = TextAt(manifest, "Passenger count", "", TopLeft, new Vector2(155, -112), new Vector2(270, 40), 36, Paper, TextAnchor.MiddleLeft, true);
        quotaFill = FillTrack(manifest, "Passenger progress", new Vector2(155, -142), 270, Green);
        arrivals = TextAt(manifest, "Next arrivals", "", TopLeft, new Vector2(155, -162), new Vector2(270, 22), 16, Quiet, TextAnchor.MiddleLeft);

        var clock = Plate(gameplay, "Time remaining and score", TopRight, new Vector2(-124, -89), new Vector2(204, 134), Panel).rectTransform;
        TextAt(clock, "Timer caption", "TIME LEFT", TopLeft, new Vector2(104, -20), new Vector2(166, 18), 13, Quiet, TextAnchor.MiddleLeft);
        timer = TextAt(clock, "Seconds remaining", "", TopLeft, new Vector2(79, -49), new Vector2(116, 50), 45, Paper, TextAnchor.MiddleLeft, true);
        TextAt(clock, "Time unit", "SEC", TopLeft, new Vector2(81, -61), new Vector2(35, 19), 14, Quiet, TextAnchor.MiddleLeft);
        timerFill = FillTrack(clock, "Time bar", new Vector2(102, -78), 164, Orange);
        TextAt(clock, "Score caption", "SCORE", TopLeft, new Vector2(59, -104), new Vector2(78, 22), 13, Quiet, TextAnchor.MiddleLeft);
        score = TextAt(clock, "Score", "", TopRight, new Vector2(-75, -104), new Vector2(110, 29), 25, Paper, TextAnchor.MiddleRight, true);
        AddButton(clock, "Pause", "II", TopRight, new Vector2(-30, -32), new Vector2(36, 35), false, OpenPause, size: 19);

        var route = Plate(gameplay, "Floor route", TopRight, new Vector2(-124, -185), new Vector2(204, 40), Panel).rectTransform;
        for (int i = 0; i < stops.Length; i++)
            stops[i] = TextAt(route, "Stop " + (i + 1), (i + 1).ToString(), TopLeft,
                new Vector2(24 + i * 26, -20), new Vector2(21, 23), 19, Quiet, display: true);

        var controls = Plate(gameplay, "Controls", Bottom, new Vector2(0, 40), new Vector2(614, 40), Panel).rectTransform;
        Control(controls, -232, "LMB", "Lift / drop", 42);
        Control(controls, -82, "E", "Handoff", 29);
        Control(controls, 57, "WHEEL", "Depth", 63);
        Control(controls, 208, "TAB", "Switch view", 42);
        cameraName = TextAt(gameplay, "Camera angle", "", Vector2.zero, new Vector2(107, 40), new Vector2(170, 30), 15, Paper, TextAnchor.MiddleLeft, true);
        hint = TextAt(gameplay, "Interaction hint", "", Bottom, new Vector2(0, 76), new Vector2(540, 28), 20, Paper, display: true);
        AddShadow(hint);
        feedbackLabel = TextAt(gameplay, "Fare feedback", "", Bottom, new Vector2(0, 120), new Vector2(490, 36), 26, Green, display: true);
        AddShadow(feedbackLabel);
        dispatch = (RectTransform)AddButton(gameplay, "Dispatch", "DEPART   [SPACE]", new Vector2(1, 0),
            new Vector2(-120, 41), new Vector2(196, 41), true, () => shift.Dispatch()).transform;
        dispatch.gameObject.SetActive(false);
    }

    private void BuildMenu()
    {
        CreateSurface();
        RectTransform menu = Stretch(surface, "Main menu");
        screenControls = menu.gameObject.AddComponent<CanvasGroup>();
        var backdrop = Stretch(menu, "Backdrop").gameObject.AddComponent<Image>();
        backdrop.color = Ink;
        backdrop.raycastTarget = false;
        var art = Resources.Load<Texture2D>("ShiftUI/LobbyCover");
        if (art != null)
        {
            lobbyImage = Stretch(menu, "North Tower lobby").gameObject.AddComponent<RawImage>();
            lobbyImage.texture = art;
            lobbyImage.raycastTarget = false;
            RawImage shade = Stretch(menu, "Title shading").gameObject.AddComponent<RawImage>();
            shade.texture = CreateMenuShade();
            shade.raycastTarget = false;
        }
        Plate(menu, "Top rule", TopLeft, new Vector2(172, -73), new Vector2(198, 2), Orange);
        TextAt(menu, "Tower", "NORTH TOWER", TopLeft, new Vector2(223, -54), new Vector2(300, 26), 19, Paper, TextAnchor.MiddleLeft, true);

        var title = Rect(menu, "Start screen", new Vector2(0, .5f), new Vector2(73, -1), Vector2.zero);
        TextAt(title, "Game title first line", "THE", Center, new Vector2(260, 147), new Vector2(520, 81), 79, Paper, TextAnchor.MiddleLeft, true);
        TextAt(title, "Game title second line", "ELEVATOR", Center, new Vector2(295, 49), new Vector2(590, 136), 123, Paper, TextAnchor.MiddleLeft, true);
        TextAt(title, "Game premise", "Get everyone to their floor.\nKeep the lift moving across seven stops.", Center,
            new Vector2(263, -49), new Vector2(526, 68), 22, Paper, TextAnchor.MiddleLeft);
        Button start = AddButton(title, "START SHIFT", "START SHIFT    ›", Center, new Vector2(148, -136), new Vector2(296, 59), true, mainMenu.PlayGame, size: 24);
        menuSettings = AddButton(title, "SETTINGS", "SETTINGS", Center, new Vector2(90, -203), new Vector2(180, 45), false, OpenSettings);
        AddButton(title, "QUIT", "QUIT", Center, new Vector2(244, -203), new Vector2(104, 45), false, mainMenu.QuitGame);

        int best = PlayerPrefs.GetInt("ElevatorShiftBest", 0);
        if (best > 0)
            TextAt(menu, "High score", "PERSONAL BEST   " + best.ToString("N0"), Vector2.zero,
                new Vector2(283, 54), new Vector2(420, 30), 18, Quiet, TextAnchor.MiddleLeft, true);
        FitScreen();
        start.Select();
    }

    private Texture2D CreateMenuShade()
    {
        menuShade = new Texture2D(128, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color[] colors = new Color[128];
        for (int i = 0; i < colors.Length; i++)
        {
            float x = i / 127f;
            float alpha = Mathf.Lerp(.985f, .05f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.38f, .82f, x)));
            colors[i] = new Color(Ink.r, Ink.g, Ink.b, alpha);
        }
        menuShade.SetPixels(colors);
        menuShade.Apply(false, true);
        return menuShade;
    }

    public void ShowResult(bool won, int floor, int points, int best, string detail, int boarded, int delivered, int fares)
    {
        EnsureHud();
        finished = true;
        gameplay.gameObject.SetActive(false);
        RectTransform sheet = Sheet("Shift report", new Vector2(696, 508));
        Color stamp = won ? Green : Orange;
        TextAt(sheet, "Report floor", won ? "NORTH TOWER   /   7 FLOORS CLEARED" : "NORTH TOWER   /   STOPPED ON FLOOR " + floor.ToString("00"),
            Center, new Vector2(0, 196), new Vector2(596, 24), 16, stamp, TextAnchor.MiddleLeft, true);
        TextAt(sheet, "Shift result", won ? "SHIFT COMPLETE" : "SHIFT FAILED", Center,
            new Vector2(0, 137), new Vector2(602, 80), 64, Paper, TextAnchor.MiddleLeft, true);
        TextAt(sheet, "Result detail", detail, Center, new Vector2(0, 82), new Vector2(602, 32), 20, Quiet, TextAnchor.MiddleLeft);
        Plate(sheet, "Report divider", Center, new Vector2(0, 52), new Vector2(602, 1), Line);
        ReportStat(sheet, -194, -20, "FINAL SCORE", points.ToString("N0"), 50, stamp, 210);
        ReportStat(sheet, 40, -20, "PERSONAL BEST", best.ToString("N0"), 39, Paper, 196);
        string fareSummary = fares + (fares == 1 ? " priority fare" : " priority fares");
        TextAt(sheet, "Passenger summary", boarded + " boarded     /     " + delivered + " delivered     /     " + fareSummary,
            Center, new Vector2(0, -104), new Vector2(602, 32), 19, Quiet, TextAnchor.MiddleLeft);
        AddButton(sheet, "RETRY SHIFT", "RETRY SHIFT", Center, new Vector2(-161, -180), new Vector2(279, 54), true,
            () => SceneManager.LoadScene(SceneManager.GetActiveScene().name)).Select();
        AddButton(sheet, "MAIN MENU", "MAIN MENU", Center, new Vector2(150, -180), new Vector2(300, 54), false,
            () => SceneManager.LoadScene("mn"));
    }

    private static void ReportStat(RectTransform sheet, float x, float y, string caption, string value, int size, Color color, int width)
    {
        TextAt(sheet, caption, caption, Center, new Vector2(x, y + 27), new Vector2(width, 22), 15, Quiet, TextAnchor.MiddleLeft);
        TextAt(sheet, caption + " value", value, Center, new Vector2(x, y - 10), new Vector2(width, 58), size, color, TextAnchor.MiddleLeft, true);
    }

    private void OpenPause()
    {
        if (modal != null || shift == null || finished) return;
        if (!ownsPause)
        {
            resumeTimeScale = Time.timeScale;
            ownsPause = true;
            shift.SetPaused(true);
            Time.timeScale = 0;
        }
        RectTransform sheet = Sheet("Pause", new Vector2(536, 436));
        TextAt(sheet, "Pause heading", "SHIFT PAUSED", Center, new Vector2(0, 142), new Vector2(438, 68), 54, Paper, TextAnchor.MiddleLeft, true);
        AddButton(sheet, "Resume", "RESUME SHIFT", Center, new Vector2(0, 56), new Vector2(438, 50), true, Resume).Select();
        AddButton(sheet, "Settings", "SETTINGS", Center, new Vector2(0, -6), new Vector2(438, 48), false, OpenSettings);
        AddButton(sheet, "Restart", "RESTART SHIFT", Center, new Vector2(0, -66), new Vector2(438, 48), false,
            () => { Resume(); SceneManager.LoadScene(SceneManager.GetActiveScene().name); });
        AddButton(sheet, "Return to menu", "MAIN MENU", Center, new Vector2(0, -126), new Vector2(438, 48), false,
            () => { Resume(); SceneManager.LoadScene("mn"); });
    }

    private void OpenSettings()
    {
        CloseModal();
        settingsOpen = true;
        RectTransform sheet = Sheet("Audio settings", new Vector2(536, 443));
        TextAt(sheet, "Settings heading", "SOUND", Center, new Vector2(0, 148), new Vector2(438, 68), 55, Paper, TextAnchor.MiddleLeft, true);
        AddSlider(sheet, "Music", 70, ShiftAudio.MusicLevel, ShiftAudio.SetMusicLevel);
        AddSlider(sheet, "Sound effects", -12, ShiftAudio.EffectsLevel, ShiftAudio.SetEffectsLevel);
        Button mute = AddButton(sheet, "Mute toggle", ShiftAudio.Muted ? "SOUND OFF" : "SOUND ON",
            Center, new Vector2(-118, -108), new Vector2(201, 46), false, ToggleAudio);
        audioStatus = mute.GetComponentInChildren<Text>();
        AddButton(sheet, "Close settings", "BACK", Center, new Vector2(118, -108), new Vector2(201, 46), true, CloseSettings).Select();
    }

    private void AddSlider(RectTransform sheet, string name, float y, float current, Action<float> changed)
    {
        TextAt(sheet, name, name, Center, new Vector2(-80, y + 26), new Vector2(277, 27), 21, Paper, TextAnchor.MiddleLeft);
        Text value = TextAt(sheet, name + " value", "", Center, new Vector2(181, y + 26), new Vector2(75, 27), 19, Orange, TextAnchor.MiddleRight, true);
        Image hitArea = Plate(sheet, name + " slider", Center, new Vector2(0, y), new Vector2(438, 30), Color.clear);
        hitArea.raycastTarget = true;
        RectTransform track = hitArea.rectTransform;
        Plate(track, "Track", Center, Vector2.zero, new Vector2(438, 6), Line);
        var fillArea = Stretch(track, "Fill area");
        Image fill = Stretch(fillArea, "Fill").gameObject.AddComponent<Image>();
        fill.color = Orange;
        fill.rectTransform.offsetMin = new Vector2(0, 12);
        fill.rectTransform.offsetMax = new Vector2(0, -12);
        fill.raycastTarget = false;
        var handleArea = Stretch(track, "Handle area");
        handleArea.offsetMin = new Vector2(0, 4);
        handleArea.offsetMax = new Vector2(0, -4);
        Image handle = Plate(handleArea, "Handle", Center, Vector2.zero, new Vector2(14, 0), Paper);
        handle.raycastTarget = true;
        Slider slider = track.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.value = current;
        value.text = Mathf.RoundToInt(current * 100) + "%";
        slider.onValueChanged.AddListener(level =>
        {
            changed(level);
            value.text = Mathf.RoundToInt(level * 100) + "%";
        });
    }

    private void CloseSettings()
    {
        PlayerPrefs.Save();
        CloseModal();
        if (ownsPause) OpenPause();
        else if (menuSettings != null) menuSettings.Select();
    }

    private void Resume()
    {
        CloseModal();
        if (!ownsPause) return;
        ownsPause = false;
        Time.timeScale = resumeTimeScale;
        if (shift != null) shift.SetPaused(false);
    }

    private void CloseModal()
    {
        if (modal == null) return;
        modal.gameObject.SetActive(false);
        Destroy(modal.gameObject);
        modal = null;
        settingsOpen = false;
        audioStatus = null;
        screenControls.interactable = true;
    }

    private RectTransform Sheet(string name, Vector2 size)
    {
        screenControls.interactable = false;
        modal = Stretch(surface, name + " overlay");
        var veil = modal.gameObject.AddComponent<Image>();
        veil.color = new Color(0.035f, .053f, .05f, .77f);
        var sheet = Plate(modal, name, Center, Vector2.zero, size, new Color(Panel.r, Panel.g, Panel.b)).rectTransform;
        Plate(sheet, "Header rule", new Vector2(0, 1), new Vector2(size.x * .5f, -2), new Vector2(size.x, 4), Orange);
        return sheet;
    }

    private void CreateSurface()
    {
        var root = new GameObject("Shift interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366, 768);
        surface = root.GetComponent<RectTransform>();
        if (EventSystem.current == null)
            new GameObject("Shift event system", typeof(EventSystem), typeof(InputSystemUIInputModule));
        FitScreen();
    }

    private static Image FillTrack(RectTransform parent, string name, Vector2 position, float width, Color color)
    {
        RectTransform background = Plate(parent, name, TopLeft, position, new Vector2(width, 4), Line).rectTransform;
        Image fill = Plate(background, "Fill", new Vector2(0, .5f), Vector2.zero, new Vector2(width, 4), color);
        fill.rectTransform.pivot = new Vector2(0, .5f);
        return fill;
    }

    private static void Control(RectTransform parent, float x, string key, string description, float keyWidth)
    {
        var cap = Plate(parent, key, Center, new Vector2(x - 34, 0), new Vector2(keyWidth, 23), Line).rectTransform;
        TextAt(cap, "Binding", key, Center, Vector2.zero, new Vector2(keyWidth, 23), 15, Paper, display: true);
        TextAt(parent, description, description, Center, new Vector2(x + 19 + keyWidth * .5f, 0), new Vector2(98, 25), 16, Quiet, TextAnchor.MiddleLeft);
    }

    private static RectTransform Rect(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform Stretch(RectTransform parent, string name)
    {
        RectTransform rect = Rect(parent, name, Center, Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        return rect;
    }

    private static Image Plate(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        Image image = Rect(parent, name, anchor, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text TextAt(RectTransform parent, string name, string content, Vector2 anchor, Vector2 position, Vector2 size,
        int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter, bool display = false)
    {
        if (bodyFont == null) bodyFont = Resources.Load<Font>("ShiftUI/Body") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (displayFont == null) displayFont = Resources.Load<Font>("ShiftUI/Display") ?? bodyFont;
        Text label = Rect(parent, name, anchor, position, size).gameObject.AddComponent<Text>();
        label.font = display ? displayFont : bodyFont;
        label.fontSize = fontSize;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.alignment = alignment;
        label.color = color;
        label.text = content;
        label.raycastTarget = false;
        return label;
    }

    private static Button AddButton(RectTransform parent, string name, string caption, Vector2 anchor, Vector2 position, Vector2 dimensions,
        bool primary, Action action, int size = 20)
    {
        Image image = Plate(parent, name, anchor, position, dimensions, primary ? Orange : new Color32(57, 68, 62, 255));
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(.9f, .92f, .9f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.71f, .74f, .71f);
        button.colors = colors;
        button.onClick.AddListener(() => { ShiftAudio.Play(ShiftCue.Confirm); action(); });
        TextAt(image.rectTransform, "Caption", caption, Center, Vector2.zero, dimensions, size, primary ? Ink : Paper, display: true);
        return button;
    }

    private static void AddShadow(Text label)
    {
        var shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .85f);
        shadow.effectDistance = new Vector2(1.5f, -2);
    }

    private static void Set(Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }

    private void OnDestroy()
    {
        if (ownsPause) Time.timeScale = resumeTimeScale;
        if (menuShade != null) Destroy(menuShade);
    }
}
