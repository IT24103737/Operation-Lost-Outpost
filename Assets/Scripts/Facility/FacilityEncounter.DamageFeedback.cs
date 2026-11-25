using UnityEngine;

public sealed partial class FacilityEncounter
{
    private struct BloodSplash
    {
        internal Vector2 Position;
        internal float Size, Rotation, Stretch, Started, Opacity;
    }

    private const float BloodLifetime = 1.15f;
    private readonly BloodSplash[] bloodSplashes = new BloodSplash[40];
    private int nextBloodSplash;
    private Texture2D bloodVignette, bloodDroplet;
    private float damageStarted = -10, damageStrength;
    private Vector3 damageCameraOffset;

    private void InitializeDamageFeedback()
    {
        const int size = 192;
        bloodVignette = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Damage edge vignette", wrapMode = TextureWrapMode.Clamp };
        bloodDroplet = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Damage blood droplet", wrapMode = TextureWrapMode.Clamp };
        Color32[] vignettePixels = new Color32[size * size];
        Color32[] dropletPixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 point = new Vector2(x, y) / (size - 1) * 2 - Vector2.one;
            float radius = point.magnitude;
            float edge = Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y));
            float haze = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.50f, 1, edge));
            vignettePixels[y * size + x] = new Color(.34f, .008f, .025f, haze * .62f);
            float angle = Mathf.Atan2(point.y, point.x);
            float contour = .67f + .07f * Mathf.Sin(angle * 5 + .4f) + .035f * Mathf.Sin(angle * 11);
            float opacity = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(contour - .07f, contour + .025f, radius));
            float highlight = Mathf.Clamp01(1 - (point - new Vector2(-.16f, .18f)).magnitude * 2);
            dropletPixels[y * size + x] = new Color(.32f + highlight * .15f, .006f, .018f, opacity * .9f);
        }
        bloodVignette.SetPixels32(vignettePixels);
        bloodVignette.Apply(false, true);
        bloodDroplet.SetPixels32(dropletPixels);
        bloodDroplet.Apply(false, true);
        ResetDamageFeedback();
    }

    private void TriggerDamageFeedback(int damage)
    {
        damageStarted = Time.unscaledTime;
        damageStrength = Mathf.Lerp(.68f, 1, Mathf.Clamp01(damage / 40f));
        int edge = Random.Range(0, 4);
        float alongEdge = Random.Range(.28f, .72f);
        Vector2 impact = edge switch
        {
            0 => new Vector2(.065f, alongEdge),
            1 => new Vector2(.935f, alongEdge),
            2 => new Vector2(alongEdge, .095f),
            _ => new Vector2(alongEdge, .905f)
        };
        for (int i = 0; i < 9; i++)
        {
            Vector2 position = impact + Random.insideUnitCircle * (i < 2 ? .025f : .1f);
            position.x = Mathf.Clamp01(position.x);
            position.y = Mathf.Clamp01(position.y);
            bloodSplashes[nextBloodSplash] = new BloodSplash
            {
                Position = position,
                Size = i < 2 ? Random.Range(.15f, .24f) : Random.Range(.025f, .09f),
                Rotation = Random.Range(-180, 180),
                Stretch = Random.Range(.85f, 1.6f),
                Started = Time.unscaledTime,
                Opacity = damageStrength * Random.Range(.65f, 1)
            };
            nextBloodSplash = (nextBloodSplash + 1) % bloodSplashes.Length;
        }
    }

    private void DrawDamageFeedback(float w, float h)
    {
        if (!bloodVignette || !bloodDroplet) return;
        float elapsed = Time.unscaledTime - damageStarted;
        Color previousColor = GUI.color;
        Matrix4x4 previousMatrix = GUI.matrix;
        try
        {
            float pulse = Mathf.Clamp01(1 - elapsed / .65f);
            GUI.color = new Color(1, 1, 1, pulse * damageStrength);
            GUI.DrawTexture(new Rect(0, 0, w, h), bloodVignette);
            foreach (BloodSplash splash in bloodSplashes)
            {
                if (splash.Opacity <= 0) continue;
                float progress = (Time.unscaledTime - splash.Started) / BloodLifetime;
                if (progress < 0 || progress >= 1) continue;
                float spread = Mathf.Lerp(.6f, 1.12f, Mathf.Clamp01(progress * 7));
                float diameter = splash.Size * h * spread;
                Vector2 center = new(splash.Position.x * w, splash.Position.y * h + progress * h * .014f);
                GUI.color = new Color(1, 1, 1, Mathf.Pow(1 - progress, 1.4f) * splash.Opacity);
                GUIUtility.RotateAroundPivot(splash.Rotation, center);
                GUI.DrawTexture(new Rect(center.x - diameter * .5f, center.y - diameter * splash.Stretch * .5f, diameter, diameter * splash.Stretch), bloodDroplet);
                GUI.matrix = previousMatrix;
            }
        }
        finally
        {
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }
    }

    private void LateUpdate()
    {
        if (!view) return;
        view.transform.localPosition -= damageCameraOffset;
        damageCameraOffset = Vector3.zero;
        float elapsed = Time.unscaledTime - damageStarted;
        if (presentation == Presentation.Playing && elapsed >= 0 && elapsed < .24f)
        {
            float amplitude = (1 - elapsed / .24f) * damageStrength * .028f;
            damageCameraOffset = new Vector3(Mathf.Sin(elapsed * 74), -Mathf.Sin(elapsed * 48), 0) * amplitude;
        }
        view.transform.localPosition += damageCameraOffset;
    }

    private void ResetDamageFeedback()
    {
        damageStarted = -10;
        damageStrength = 0;
        nextBloodSplash = 0;
        System.Array.Clear(bloodSplashes, 0, bloodSplashes.Length);
        if (view) view.transform.localPosition -= damageCameraOffset;
        damageCameraOffset = Vector3.zero;
    }

    private void DisposeDamageFeedback()
    {
        if (bloodVignette) Destroy(bloodVignette);
        if (bloodDroplet) Destroy(bloodDroplet);
    }
}
