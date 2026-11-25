using UnityEngine;

public sealed partial class FacilityEncounter
{
    private static readonly Color HudInk = new(.035f, .048f, .055f, .98f);
    private static readonly Color HudBorder = new(.23f, .33f, .34f, .88f);
    private static readonly Color HudText = new(.94f, .97f, .94f);
    private static readonly Color HudMuted = new(.86f, .92f, .91f);
    private static readonly Color HudMint = new(.52f, .96f, .76f);
    private static readonly Color HudAmber = new(1, .68f, .29f);
    private static readonly Color HudRed = new(1, .35f, .28f);
    private GUIStyle caption, heading, number, centered, title;
    private float displayedHealth = 1;

    private void OnGUI()
    {
        if (!view || !liftDoor) return;
        if (caption == null)
        {
            caption = MakeStyle(16, TextAnchor.MiddleLeft);
            heading = MakeStyle(26, TextAnchor.MiddleLeft);
            number = MakeStyle(36, TextAnchor.MiddleLeft);
            centered = MakeStyle(17, TextAnchor.MiddleCenter);
            title = MakeStyle(35, TextAnchor.MiddleCenter);
        }

        float scale = Mathf.Min(Screen.width / 940f, Screen.height / 680f, 1.8f);
        float w = Screen.width / scale, h = Screen.height / scale;
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        try
        {
            if (presentation != Presentation.Playing)
            {
                if (presentation == Presentation.Defeat && Event.current.type == EventType.Repaint)
                    DrawDamageFeedback(w, h);
                DrawPresentation(w, h);
                return;
            }
            if (Event.current.type != EventType.Repaint) return;
            DrawDamageFeedback(w, h);
            DrawStatus(w, h);
            DrawObjectiveMarkers(w, h);
            DrawEnemyHealth(w, h);
            DrawCombat(w, h);
        }
        finally { GUI.matrix = previous; }
    }

    private void DrawStatus(float w, float h)
    {
        var floorCard = new Rect(22, 22, 370, 145);
        PanelFrame(floorCard, HudMint);
        Text(new Rect(38, 29, 332, 20), $"FACILITY  /  FLOOR {floor + 1:00}", caption, HudMint);
        Text(new Rect(38, 46, 332, 35), FloorNames[floor], heading, HudText);
        Text(new Rect(38, 84, 332, 25), CurrentObjective(), caption, !powered ? HudAmber : HudMint);
        Text(new Rect(38, 110, 332, 23), $"{RemainingEnemies()} threats  /  Power {(powered ? "online" : "offline")}", caption, HudMuted);
        Text(new Rect(38, 136, 332, 22), "Floor-entry checkpoint  /  Esc to pause", MakeStyle(13, TextAnchor.MiddleLeft), HudMuted);

        var vitals = new Rect(22, h - 119, 230, 97);
        Color healthColor = health <= 30 ? HudRed : HudMint;
        PanelFrame(vitals, healthColor);
        Text(new Rect(38, vitals.y + 6, 126, 20), "VITALS", caption, HudMuted);
        Text(new Rect(38, vitals.y + 24, 94, 42), $"{health:00}", number, healthColor);
        Text(new Rect(138, vitals.y + 34, 72, 26), "/ 100", caption, HudMuted);
        displayedHealth = Mathf.MoveTowards(displayedHealth, health / 100f, Time.unscaledDeltaTime * 1.6f);
        Meter(new Rect(38, vitals.y + 75, 198, 7), displayedHealth, healthColor);

        var loadout = new Rect(w - 382, h - 148, 360, 126);
        Color ammoColor = ammo < 6 || reloadEnd > 0 ? HudAmber : HudMint;
        PanelFrame(loadout, ammoColor);
        Text(new Rect(loadout.x + 16, loadout.y + 6, 190, 20), reloadEnd > 0 ? "RELOADING" : "MP5  /  MAGAZINE", caption, HudMuted);
        Text(new Rect(loadout.x + 16, loadout.y + 24, 84, 42), $"{ammo:00}", number, ammoColor);
        Text(new Rect(loadout.x + 101, loadout.y + 34, 185, 26), $"/ {reserve} RESERVE", caption, HudMuted);
        float gauge = reloadEnd > 0 ? 1 - (reloadEnd - Time.time) / FacilityWeaponView.ReloadDuration : ammo / 30f;
        Meter(new Rect(loadout.x + 16, loadout.y + 72, 328, 7), gauge, ammoColor);
        DrawControl(loadout.x + 15, loadout.y + 91, "R", "Reload");
        DrawControl(loadout.x + 127, loadout.y + 91, "F", "Light");
        DrawControl(loadout.x + 239, loadout.y + 91, "V", "Finish");
    }

    private void DrawCombat(float w, float h)
    {
        bool confirmed = Time.time < hitEnd || Time.time < objectHitEnd;
        Color reticle = Time.time < hitEnd ? HudRed : Time.time < objectHitEnd ? HudAmber : aimedProp ? HudMint : HudText;
        float gap = confirmed ? 7 : 5;
        float cx = w * .5f, cy = h * .5f;
        if (wallClearance >= .72f) Fill(new Rect(cx - 2, cy - 2, 4, 4), HudMuted);
        else if (weaponView.AimBlend < .82f)
        {
            Fill(new Rect(cx - gap - 4, cy - .5f, 4, 1), reticle);
            Fill(new Rect(cx + gap, cy - .5f, 4, 1), reticle);
            Fill(new Rect(cx - .5f, cy - gap - 4, 1, 4), reticle);
            Fill(new Rect(cx - .5f, cy + gap, 1, 4), reticle);
        }

        if (!powered && Near(Panel)) DrawPrompt(cx, cy + 70, "E", "Restore power at this panel", HudAmber);
        else if (NearLift()) DrawPrompt(cx, cy + 70, "E", powered && Cleared() ? (floor == 3 ? "Extract and finish mission" : $"Take lift to Floor {floor + 2}") : (!powered ? "Lift locked - restore power" : "Lift locked - clear threats"), powered && Cleared() ? HudMint : HudAmber);
        else if (aimedProp) DrawPrompt(cx, cy + 70, "E", "Push cargo", HudMint);
        else if (wallClearance >= .72f)
            Text(new Rect(cx - 150, cy + 40, 300, 26), "WEAPON LOWERED", centered, HudMuted);

        if (Time.time < messageEnd)
        {
            float alpha = Mathf.Clamp01((messageEnd - Time.time) * 3);
            Color previous = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            PanelFrame(new Rect(cx - 260, 184, 520, 42), Time.time < panelUse ? HudMint : HudAmber);
            Text(new Rect(cx - 250, 186, 500, 38), message, centered, HudText);
            GUI.color = previous;
        }
    }

    private void DrawEnemyHealth(float w, float h)
    {
        foreach (FacilityCreature enemy in enemies)
        {
            if (!enemy || !enemy.Alive || enemy.Hidden) continue;
            Vector3 point = view.WorldToViewportPoint(enemy.AimPoint + Vector3.up * .54f);
            if (point.z < .3f || point.z > 35 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1 ||
                !enemy.VisibleFrom(view.transform.position)) continue;
            float width = Mathf.Lerp(86, 42, point.z / 35);
            Meter(new Rect(point.x * w - width * .5f, (1 - point.y) * h, width, 5), enemy.HealthFraction, HudRed);
        }
    }

    private void DrawPrompt(float x, float y, string key, string label, Color accent)
    {
        float keyWidth = Mathf.Max(36, Mathf.Ceil(centered.CalcSize(new GUIContent(key)).x) + 20);
        float labelStart = 22 + keyWidth;
        float bodyWidth = Mathf.Max(240, labelStart + caption.CalcSize(new GUIContent(label)).x + 16);
        var body = new Rect(x - bodyWidth / 2, y, bodyWidth, 38);
        PanelFrame(body, accent);
        Fill(new Rect(body.x + 10, body.y + 7, keyWidth, 24), HudBorder);
        Text(new Rect(body.x + 10, body.y + 6, keyWidth, 26), key, centered, HudText);
        Text(new Rect(body.x + labelStart, body.y + 6, bodyWidth - labelStart - 12, 25), label, caption, accent);
    }

    private void DrawControl(float x, float y, string key, string label)
    {
        Fill(new Rect(x, y, 23, 22), HudBorder);
        Text(new Rect(x, y, 23, 22), key, centered, HudText);
        Text(new Rect(x + 29, y, 81, 22), label, caption, HudMuted);
    }

    private static GUIStyle MakeStyle(int size, TextAnchor anchor) => new(GUI.skin.label)
    {
        alignment = anchor,
        fontSize = size,
        fontStyle = FontStyle.Bold,
        clipping = TextClipping.Clip,
        normal = { textColor = Color.white }
    };

    private static void Text(Rect rect, string value, GUIStyle style, Color color)
    {
        Color previous = GUI.contentColor;
        GUI.contentColor = new Color(0, 0, 0, .8f);
        GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), value, style);
        GUI.contentColor = color;
        GUI.Label(rect, value, style);
        GUI.contentColor = previous;
    }

    private static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        color.a *= previous.a;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void PanelFrame(Rect rect, Color accent)
    {
        Fill(rect, HudInk);
        Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 1), HudBorder);
        Fill(new Rect(rect.x, rect.y, 3, rect.height), accent);
    }

    private static void Meter(Rect rect, float value, Color color)
    {
        Fill(rect, HudBorder);
        Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), color);
    }
}

