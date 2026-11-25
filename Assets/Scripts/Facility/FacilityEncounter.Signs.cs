using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class FacilityEncounter
{
    private void ImproveExistingSigns(string group)
    {
        GameObject root = GameObject.Find(group);
        if (!root) return;
        foreach (Transform placard in root.GetComponentsInChildren<Transform>())
        {
            Transform backing = placard.Find("Painted steel backing");
            if (!backing) continue;
            BuildPlacard(placard, backing);
        }
    }

    private void MountSign(string title, string subtitle, Vector3 position)
    {
        Transform placard = new GameObject(title + " wall sign").transform;
        placard.SetParent(temporaryFloor);
        placard.position = position;
        Transform backing = Box("Painted steel backing", position, new Vector3(2.9f, .82f, .04f), effects.SignBacking, false);
        backing.SetParent(placard, true);
        MakeSignLine(placard, title, .16f);
        MakeSignLine(placard, subtitle, -.19f);
        BuildPlacard(placard, backing);
    }

    private static void MakeSignLine(Transform parent, string phrase, float height)
    {
        var line = new GameObject(phrase).AddComponent<TextMesh>();
        line.transform.SetParent(parent, false);
        line.transform.localPosition = new Vector3(0, height, -.036f);
        line.text = phrase;
    }

    private void BuildPlacard(Transform placard, Transform backing)
    {
        backing.localScale = new Vector3(2.9f, .84f, .04f);
        var panelRenderer = backing.GetComponent<Renderer>();
        panelRenderer.sharedMaterial = effects.SignBacking;
        Transform stripe = placard.Find("Safety stripe");
        if (stripe)
        {
            stripe.localPosition = new Vector3(-1.36f, 0, -.026f);
            stripe.localScale = new Vector3(.09f, .68f, .014f);
        }
        foreach (TextMesh line in placard.GetComponentsInChildren<TextMesh>())
        {
            bool isHeading = line.transform.localPosition.y > 0;
            line.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            line.fontSize = 100;
            line.fontStyle = FontStyle.Bold;
            line.anchor = TextAnchor.MiddleCenter;
            line.alignment = TextAlignment.Center;
            line.characterSize = .1f;
            line.color = isHeading ? Color.white : new Color(1, .86f, .55f);
            line.transform.localPosition = new Vector3(.025f, isHeading ? .16f : -.19f, -.034f);
            MeshRenderer renderer = line.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = line.font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Bounds measured = renderer.localBounds;
            if (measured.size.x > 0 && measured.size.y > 0)
                line.characterSize *= Mathf.Min(2.58f / measured.size.x, (isHeading ? .27f : .17f) / measured.size.y);
        }
    }
}

