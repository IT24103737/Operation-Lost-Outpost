using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class FacilityEncounter
{
    private enum Presentation { MainMenu, Briefing, Playing, Paused, Defeat, Outro, Debrief }

    private Presentation presentation;
    private Camera storyCamera;
    private Light storyLight;
    private Transform liftStation;
    private TextMesh powerLabel, liftLabel;
    private Renderer powerBeacon, liftBeacon;
    private Material markerAmber, markerMint;
    private float presentationStarted, missionSeconds;
    private int checkpointAmmo, checkpointReserve, shotsFired, enemiesDefeated;
    private bool bodyWasKinematic;
    private bool controlsOpen;
    private GUIStyle narrative, menuButton, menuTitle, markerHeading, markerDetail, controlsText;
    private static readonly Vector3[] StoryOffsets =
    {
        new(2.2f, .6f, -3.5f), new(-2.2f, .6f, -3.5f),
        new(3.6f, .4f, 0), new(-3.6f, .4f, 0), new(0, .3f, 4)
    };
    private const float BriefingDuration = 24f;
    private const float BriefingShotDuration = BriefingDuration / 3;

    private static readonly string[] ChapterStory =
    {
        "Outpost's distress signal ended during a containment failure. You are the recovery operative sent to reopen the facility's evacuation route.",
        "Quarantine lost its power first. Four parasites now feed on the grid, trapping the remaining evacuation systems behind locked doors.",
        "The research wing bred a predator that hides above its victims. Its camouflage failed under the lab lights. Bring those lights back.",
        "The outbreak spread through the waste tunnels. The final freight lift leads to the surface. Clear the pack and open the extraction route."
    };

    private static readonly string[] ThreatAdvice =
    {
        "BIOMASS GOLIATH  /  600 HP\nBait a charge into a wall, then fire while it is stunned. Back away from its raised fist. Below half health it enrages and attacks faster.",
        "VOLT-LEECH  /  FOUR HOSTILES\nWatch for electrical attacks. Leeches evade your aim and drain the power panel. Eliminate all four before making your final power restoration.",
        "SHADOW STALKER  /  THREE HOSTILES\nCheck the ceiling. Looking at a Stalker slows its advance. Restore power to expose it and force it out of hiding.",
        "SWARMERS  /  SIX HOSTILES\nKeep space between you and the pack. Three together move faster, and they can slip under cargo barricades."
    };

    private int RemainingEnemies()
    {
        int count = 0;
        foreach (FacilityCreature enemy in enemies)
            if (enemy && enemy.Alive) count++;
        return count;
    }

    private string CurrentObjective()
    {
        if (!powered) return "Use the marked panel to restore power";
        if (!Cleared()) return $"Eliminate {RemainingEnemies()} remaining threats";
        return floor == 3 ? "Use the marked lift to extract" : $"Use the lift to reach Floor {floor + 2}";
    }

    private void SetPresentation(Presentation next)
    {
        if (presentation == Presentation.Playing && next != Presentation.Playing)
            bodyWasKinematic = playerBody.isKinematic;
        presentation = next;
        presentationStarted = Time.unscaledTime;
        bool playing = next == Presentation.Playing;
        Time.timeScale = playing ? 1 : 0;
        movement.enabled = playing;
        if (mouseLook) mouseLook.enabled = playing;
        if (!playerBody.isKinematic) playerBody.linearVelocity = Vector3.zero;
        playerBody.isKinematic = !playing || bodyWasKinematic;
        Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !playing;
        weaponView.SetVisible(playing);
        powerLabel.gameObject.SetActive(playing);
        liftLabel.gameObject.SetActive(playing);
        flash.gameObject.SetActive(false);
        if (!playing) playerAudio.Stop();
        bool cinema = next == Presentation.Briefing || next == Presentation.Outro;
        if (cinema) EnsureStoryCamera();
        view.enabled = !cinema;
        if (storyCamera) storyCamera.gameObject.SetActive(cinema);
        if (cinema) UpdateStoryCamera();
        else FaceStationLabels(view.transform.position);
    }

    private void BeginBriefing()
    {
        controlsOpen = false;
        SetPresentation(Presentation.Briefing);
    }

    private void BeginPlaying()
    {
        SetPresentation(Presentation.Playing);
        nextShot = Time.time + .25f;
        Say("CHECKPOINT  /  Full health - follow the objective marker");
    }

    private bool UpdatePresentation()
    {
        bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        switch (presentation)
        {
            case Presentation.Playing:
                return false;
            case Presentation.MainMenu:
                if (confirm) BeginBriefing();
                break;
            case Presentation.Briefing:
                UpdateStoryCamera();
                if (confirm || Input.GetKeyDown(KeyCode.Space) || Time.unscaledTime - presentationStarted >= BriefingDuration)
                    BeginPlaying();
                break;
            case Presentation.Paused:
                if (Input.GetKeyDown(KeyCode.Escape) || confirm) SetPresentation(Presentation.Playing);
                break;
            case Presentation.Defeat:
                if (confirm) RetryCheckpoint();
                break;
            case Presentation.Outro:
                UpdateStoryCamera();
                if (confirm || Input.GetKeyDown(KeyCode.Space) || Time.unscaledTime - presentationStarted >= 9f)
                    SetPresentation(Presentation.Debrief);
                break;
            case Presentation.Debrief:
                if (confirm) RestartMission();
                break;
        }
        return true;
    }

    private void RetryCheckpoint()
    {
        ammo = checkpointAmmo;
        reserve = checkpointReserve;
        finished = false;
        EnterFloor();
        BeginBriefing();
    }

    private void RestartMission()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(gameObject.scene.name);
    }

    private void QuitMission()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnsureStoryCamera()
    {
        if (storyCamera) return;
        storyCamera = new GameObject("Story cinematic camera").AddComponent<Camera>();
        storyCamera.CopyFrom(view);
        storyCamera.nearClipPlane = .08f;
        storyCamera.fieldOfView = 55;
        storyLight = new GameObject("Cinematic subject light").AddComponent<Light>();
        storyLight.transform.SetParent(storyCamera.transform, false);
        storyLight.type = LightType.Spot;
        storyLight.color = new Color(.7f, .85f, 1);
        storyLight.intensity = 5;
        storyLight.range = 18;
        storyLight.spotAngle = 85;
    }

    private void UpdateStoryCamera()
    {
        float elapsed = Time.unscaledTime - presentationStarted;
        int shot = Mathf.Min(2, Mathf.FloorToInt(elapsed / BriefingShotDuration));
        Vector3 target;
        if (presentation == Presentation.Outro)
            target = liftStation.position + Vector3.up * .6f;
        else if (shot == 0 && enemies.Count > 0 && enemies[0])
            target = enemies[0].AimPoint;
        else if (shot == 1)
            target = panel.position + Vector3.up * .3f;
        else
            target = liftStation.position + Vector3.up * .6f;

        Vector3 offset = Vector3.zero;
        float longest = 0;
        foreach (Vector3 candidate in StoryOffsets)
        {
            Vector3 drift = Vector3.right * (Mathf.Sin(elapsed * .22f) * .25f);
            Vector3 direction = candidate + drift;
            float distance = direction.magnitude;
            if (Physics.SphereCast(target, .15f, direction.normalized, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(.35f, hit.distance - .25f);
            if (distance <= longest) continue;
            longest = distance;
            offset = direction.normalized * distance;
            if (distance >= direction.magnitude - .01f) break;
        }
        storyLight.enabled = longest > 1.5f;
        storyCamera.transform.SetPositionAndRotation(target + offset, Quaternion.LookRotation(-offset));
        FaceStationLabels(storyCamera.transform.position);

    }

    private void BuildObjectiveStations()
    {
        if (!markerAmber)
        {
            markerAmber = MarkerMaterial("Power station amber", new Color(1, .57f, .12f));
            markerMint = MarkerMaterial("Lift station mint", new Color(.25f, 1, .67f));
        }
        powerLabel = StationLabel("POWER CONTROL", panel.position + Vector3.up * 1.35f);
        liftLabel = StationLabel("FREIGHT LIFT", liftStation.position + Vector3.up * 1.45f);
        powerBeacon = Box("Power control beacon", panel.position + new Vector3(0, .94f, 0),
            new Vector3(.46f, .10f, .46f), markerAmber, false).GetComponent<Renderer>();
        liftBeacon = Box("Lift call beacon", liftStation.position + Vector3.up * .84f,
            new Vector3(.32f, .10f, .32f), markerAmber, false).GetComponent<Renderer>();
        Box("Power station floor marking", new Vector3(panel.position.x, Elevation + .015f, panel.position.z - .8f),
            new Vector3(1.5f, .02f, .09f), markerAmber, false);
        Box("Lift checkpoint floor marking", new Vector3(liftStation.position.x, Elevation + .016f, liftStation.position.z - .8f),
            new Vector3(1.5f, .02f, .09f), markerMint, false);
        UpdateObjectiveStations();
    }

    private Material MarkerMaterial(string label, Color color)
    {
        Material material = new(Shader.Find("Universal Render Pipeline/Unlit"));
        material.name = label;
        material.SetColor("_BaseColor", color);
        return material;
    }

    private TextMesh StationLabel(string label, Vector3 position)
    {
        TextMesh text = new GameObject(label + " objective label").AddComponent<TextMesh>();
        text.transform.SetParent(temporaryFloor);
        text.transform.position = position;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 80;
        text.characterSize = .01f;
        text.transform.localScale = Vector3.one * .05f;
        text.fontStyle = FontStyle.Bold;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.GetComponent<Renderer>().sharedMaterial = text.font.material;
        return text;
    }

    private void UpdateObjectiveStations()
    {
        bool ready = powered && Cleared();
        SetStationText(powerLabel, powered ? "POWER CONTROL\nGRID ONLINE" : "POWER CONTROL\n[E] RESTORE POWER", powered ? HudMint : HudAmber);
        powerBeacon.sharedMaterial = powered ? markerMint : markerAmber;
        string liftText = ready ? (floor == 3 ? "EXTRACTION LIFT\n[E] COMPLETE MISSION" : $"FREIGHT LIFT\n[E] GO TO FLOOR {floor + 2}")
            : (!powered ? "LIFT LOCKED\nRESTORE POWER FIRST" : $"LIFT LOCKED\n{RemainingEnemies()} THREAT{(RemainingEnemies() == 1 ? "" : "S")} REMAIN");
        SetStationText(liftLabel, liftText, ready ? HudMint : HudAmber);
        liftBeacon.sharedMaterial = ready ? markerMint : markerAmber;
        FaceStationLabels(view.transform.position);
    }

    private static void SetStationText(TextMesh label, string value, Color color)
    {
        if (label.text == value) return;
        label.text = value;
        label.color = color;
    }

    private void FaceStationLabels(Vector3 eye)
    {
        FaceStationLabel(powerLabel, eye);
        FaceStationLabel(liftLabel, eye);
    }

    private static void FaceStationLabel(TextMesh label, Vector3 eye)
    {
        label.transform.rotation = Quaternion.LookRotation(label.transform.position - eye);
        // TextMesh updates its geometry after assigning text. Scale the transform
        // against local bounds so later frames can fit the finished mesh consistently.
        Vector3 size = label.GetComponent<Renderer>().localBounds.size;
        if (size.x > 0 && size.y > 0)
            label.transform.localScale = Vector3.one * Mathf.Min(1.5f / size.x, .4f / size.y);
    }

    private void DrawObjectiveMarkers(float w, float h)
    {
        markerHeading ??= MakeStyle(14, TextAnchor.MiddleLeft);
        markerDetail ??= MakeStyle(13, TextAnchor.MiddleLeft);
        Rect powerRect = new();
        if (!powered || Near(Panel))
            powerRect = DrawWaypoint(panel.position + Vector3.up * .5f, powered ? "POWER ONLINE" : "POWER CONTROL", powered ? HudMint : HudAmber, w, h, 0);
        Vector3 target = liftStation.position + Vector3.up * .7f;
        Rect liftRect = WaypointRect(target, w, h);
        float shift = powerRect.Overlaps(liftRect) ? 58 : 0;
        DrawWaypoint(target, powered && Cleared() ? (floor == 3 ? "EXTRACTION READY" : $"LIFT TO FLOOR {floor + 2}") : "FREIGHT LIFT / LOCKED",
            powered && Cleared() ? HudMint : HudAmber, w, h, shift);
    }

    private Rect WaypointRect(Vector3 target, float w, float h)
    {
        Vector3 point = view.WorldToViewportPoint(target);
        if (point.z <= 0) { point.x = point.x < .5f ? 0 : 1; point.y = .5f; }
        return new Rect(Mathf.Clamp(point.x * w - 118, 22, w - 258), Mathf.Clamp((1 - point.y) * h, 238, h - 250), 236, 53);
    }

    private Rect DrawWaypoint(Vector3 target, string label, Color color, float w, float h, float shift)
    {
        Rect rect = WaypointRect(target, w, h);
        rect.y += shift;
        PanelFrame(rect, color);
        Text(new Rect(rect.x + 10, rect.y + 3, 220, 23), label, markerHeading, color);
        Vector3 local = view.transform.InverseTransformPoint(target);
        string direction = local.z < 0 ? (local.x < 0 ? "Turn left / behind" : "Turn right / behind")
            : Mathf.Abs(local.x) > local.z ? (local.x < 0 ? "To your left" : "To your right") : "Ahead";
        Text(new Rect(rect.x + 10, rect.y + 26, 220, 22), $"{Vector3.Distance(Player, target):0} m  /  {direction}", markerDetail, HudText);
        return rect;
    }

    private void DrawPresentation(float w, float h)
    {
        if (narrative == null)
        {
            narrative = MakeStyle(18, TextAnchor.UpperLeft);
            narrative.wordWrap = true;
            controlsText = MakeStyle(16, TextAnchor.UpperLeft);
            controlsText.wordWrap = true;
            menuButton = MakeStyle(18, TextAnchor.MiddleCenter);
            menuButton.normal.background = Texture2D.whiteTexture;
            menuButton.hover.background = Texture2D.whiteTexture;
            menuButton.active.background = Texture2D.whiteTexture;
            menuButton.normal.textColor = Color.white;
            menuButton.hover.textColor = Color.white;
            menuButton.active.textColor = Color.white;
            menuTitle = MakeStyle(44, TextAnchor.UpperLeft);
        }
        if (presentation == Presentation.Briefing || presentation == Presentation.Outro)
        {
            DrawCinematic(w, h);
            return;
        }
        Fill(new Rect(0, 0, w, h), new Color(.008f, .017f, .023f, .92f));
        float x = (w - 820) / 2;
        float y = (h - 580) / 2;
        Fill(new Rect(x, y, 50, 4), HudMint);
        Text(new Rect(x, y + 20, 820, 24), "OUTPOST RECOVERY COMMAND", caption, HudMint);
        string headline = presentation switch
        {
            Presentation.MainMenu => "OPERATION\nLOST OUTPOST",
            Presentation.Paused => "MISSION PAUSED",
            Presentation.Defeat => "SIGNAL LOST",
            _ => "EVACUATION\nROUTE SECURED"
        };
        Text(new Rect(x, y + 62, 820, 112), headline, menuTitle, HudText);
        if (presentation == Presentation.MainMenu)
        {
            Text(new Rect(x, y + 186, 466, 100), "A containment breach silenced the facility. Restore its power, survive four mutated threats, and reopen the surface evacuation route.", narrative, HudMuted);
            if (MenuAction(new Rect(x, y + 310, 360, 54), "BEGIN OPERATION  /  ENTER", HudMint)) BeginBriefing();
            if (MenuAction(new Rect(x, y + 378, 360, 48), controlsOpen ? "HIDE CONTROLS" : "CONTROLS & OBJECTIVES", HudBorder)) controlsOpen = !controlsOpen;
            if (MenuAction(new Rect(x, y + 440, 360, 48), "QUIT", HudBorder)) QuitMission();
            PanelFrame(new Rect(x + 500, y + 186, 320, 330), HudAmber);
            Text(new Rect(x + 518, y + 202, 284, 290), controlsOpen
                ? "FIELD CONTROLS\n\nWASD  Move    Mouse  Look\nShift  Sprint    Space  Jump\nLeft click  Fire\nRight click  Aim down sights\nR  Reload    F  Flashlight\nE  Use panel / lift / push\nV  Gun finish    Esc  Pause\n\nRestore power, clear threats, then use the marked lift."
                : "RECOVERY ROUTE\n\nOutbound Dispatch\nBiomass Goliath\n\nGrid & Quarantine\nFour Volt-Leeches\n\nBlackout Research\nShadow Stalkers\n\nWaste Processing\nSwarmers / extraction", controlsText, HudText);
        }
        else
        {
            string body = presentation == Presentation.Paused ? $"Floor {floor + 1} / {FloorNames[floor]}\n\n{CurrentObjective()}\nThe mint lift marker becomes ready when power is online and every threat is eliminated."
                : presentation == Presentation.Defeat ? $"Recovery signal interrupted on Floor {floor + 1}.\n\nRetry from this floor's entry checkpoint with full health and your arrival ammunition. The floor's enemy encounter restarts."
                : "The four sectors are clear. You reopened the surface route and transmitted Outpost's containment logs to Recovery Command. The evacuation beacon is live.\n\nOperation Lost Outpost is complete.";
            Text(new Rect(x, y + 188, 480, 150), body, narrative, HudMuted);
            if (presentation == Presentation.Paused)
            {
                if (MenuAction(new Rect(x, y + 370, 360, 52), "RESUME  /  ESC", HudMint)) SetPresentation(Presentation.Playing);
            }
            else if (presentation == Presentation.Defeat)
            {
                if (MenuAction(new Rect(x, y + 370, 360, 52), "RETRY CHECKPOINT  /  ENTER", HudMint)) RetryCheckpoint();
            }
            else if (MenuAction(new Rect(x, y + 370, 360, 52), "PLAY AGAIN  /  ENTER", HudMint)) RestartMission();
            if (MenuAction(new Rect(x, y + 436, 360, 48), "MAIN MENU", HudBorder)) RestartMission();
            if (MenuAction(new Rect(x, y + 498, 360, 48), "QUIT", HudBorder)) QuitMission();
            PanelFrame(new Rect(x + 520, y + 188, 300, 160), HudMint);
            int seconds = Mathf.FloorToInt(missionSeconds);
            Text(new Rect(x + 540, y + 208, 260, 125), $"MISSION RECORD\n\nTime  {seconds / 60:00}m {seconds % 60:00}s\nShots fired  {shotsFired}\nThreats eliminated  {enemiesDefeated}", narrative, HudText);
        }
        Text(new Rect(x, y + 557, 820, 23), "Each floor begins with a checkpoint and a skippable mission briefing.", caption, HudMuted);
    }

    private bool MenuAction(Rect rect, string label, Color accent)
    {
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = accent == HudMint ? new Color(.10f, .34f, .27f) : new Color(.12f, .19f, .22f);
        bool pressed = GUI.Button(rect, label, menuButton);
        GUI.backgroundColor = previous;
        Fill(new Rect(rect.x, rect.y, 3, rect.height), accent);
        return pressed;
    }

    private void DrawCinematic(float w, float h)
    {
        bool ending = presentation == Presentation.Outro;
        float elapsed = Time.unscaledTime - presentationStarted;
        int shot = Mathf.Min(2, Mathf.FloorToInt(elapsed / BriefingShotDuration));
        Fill(new Rect(0, 0, w, 98), new Color(.008f, .014f, .02f, .96f));
        Fill(new Rect(0, h - 236, w, 236), new Color(.008f, .014f, .02f, .96f));
        Text(new Rect(36, 18, w - 72, 25), ending ? "RECOVERY COMMAND / FINAL TRANSMISSION" : $"MISSION BRIEFING / FLOOR {floor + 1:00}", caption, HudMint);
        Text(new Rect(36, 47, w - 72, 40), ending ? "The evacuation beacon is live" : FloorNames[floor], heading, HudText);
        string copy = ending ? "Recovery Command, the route is open. All four containment sectors are clear. Outpost's logs are transmitting. Bring the evacuation team home."
            : shot == 0 ? ChapterStory[floor] + "\n\n" + EnemyNames[floor].ToUpperInvariant()
            : shot == 1 ? ThreatAdvice[floor]
            : "YOUR OBJECTIVE\n" + (powered ? "Eliminate every threat on this floor." : "Use E at the amber POWER CONTROL marker to restore the grid, then eliminate every threat.")
                + "\nWhen power is online and the floor is clear, use E at the mint lift marker. You reach a new checkpoint on the next floor.";
        Text(new Rect(36, h - 214, w - 72, 130), copy, narrative, HudText);
        Meter(new Rect(36, h - 65, w - 330, 3), elapsed / (ending ? 9f : BriefingDuration), HudMint);
        if (MenuAction(new Rect(w - 268, h - 75, 232, 43), ending ? "CONTINUE / SPACE" : "SKIP BRIEFING / SPACE", HudMint))
        {
            if (ending) SetPresentation(Presentation.Debrief);
            else BeginPlaying();
        }
    }

    private void ReleasePresentation()
    {
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (storyCamera) Destroy(storyCamera.gameObject);
        if (markerAmber) Destroy(markerAmber);
        if (markerMint) Destroy(markerMint);
    }
}
