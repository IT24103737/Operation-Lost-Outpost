using UnityEngine;
using Species = FacilityEncounter.Species;
using StepSurface = FacilityEncounter.StepSurface;

internal sealed class FacilityEffects
{
    private readonly AudioClip[] voices = new AudioClip[4];
    private readonly AudioClip[] roomTails = new AudioClip[4];
    private readonly AudioClip[] roomTone = new AudioClip[4];
    private readonly Material[] skin = new Material[4];
    private readonly Material bone, eyes, black, panelOn, panelOff;
    internal Material Lamp { get; }
    internal Material SignBacking { get; }
    private readonly Mesh flesh;
    private readonly AudioClip arc, metalHit, softHit;
    private Material weatheredConcrete;
    private Texture2D concreteNormal;
    internal AudioClip Shot { get; }
    internal AudioClip Reload { get; }
    private readonly AudioClip[,] footsteps = new AudioClip[5, 3];
    internal Mesh Flesh => flesh;
    internal Material Skin(Species species) => skin[(int)species];
    internal Material Bone => bone;
    internal Material Eye => eyes;
    internal Material Dark => black;

    internal FacilityEffects()
    {
        flesh = BuildFlesh();
        skin[0] = CreatureSurface("Goliath", .36f, .83f);
        skin[1] = CreatureSurface("Leech", .59f, .8f);
        skin[2] = CreatureSurface("Stalker", .34f, .9f);
        skin[3] = CreatureSurface("Swarmer", .16f, .54f);
        bone = Flat(new Color(.55f, .43f, .35f));
        black = Flat(new Color(.14f, .045f, .045f));
        eyes = Flat(new Color(.48f, .19f, .07f), new Color(.75f, .18f, .025f));
        panelOn = Flat(new Color(.13f, .53f, .42f), new Color(.15f, 1.2f, .8f));
        panelOff = Flat(new Color(.45f, .11f, .085f), new Color(.75f, .07f, .025f));
        Lamp = Flat(new Color(.79f, .87f, .87f), new Color(.62f, .83f, .91f));
        SignBacking = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(.028f, .047f, .065f) };

        Shot = Clip("MP5 close report and cycling action", .17f, 4, (t, n) =>
            .73f * n * Mathf.Exp(-t * 86) + .64f * Wave(102, t) * Mathf.Exp(-t * 57)
            + .25f * Wave(194, t) * Mathf.Exp(-t * 49) + .22f * Click(t, .028f, n)
            + .13f * Click(t, .054f, n));
        Reload = Clip("Magazine and bolt foley", 1.5f, 9, (t, n) =>
            Click(t, .07f, n) * .75f + Click(t, .57f, n) + Click(t, 1.15f, n) * .6f + Click(t, 1.24f, n) * .85f);
        for (int surface = 0; surface < footsteps.GetLength(0); surface++)
        for (int variant = 0; variant < footsteps.GetLength(1); variant++)
            footsteps[surface, variant] = CreateFootstep((StepSurface)surface, variant);
        arc = Clip("Volt-Leech discharge", .34f, 24, (t, n) =>
            Envelope(t, .34f) * (.58f * n + .38f * Wave(590 - t * 1150, t)) * (.5f + .5f * Mathf.Sin(t * 83)));
        metalHit = Clip("Hollow drum impact", .26f, 35, (t, n) =>
            .37f * n * Mathf.Exp(-t * 57) + (.38f * Wave(743, t) + .26f * Wave(380, t)) * Mathf.Exp(-t * 19));
        softHit = Clip("Cargo thud", .16f, 12, (t, n) =>
            (.51f * n + .38f * Wave(115, t)) * Mathf.Exp(-t * 39));
        voices[0] = Clip("Goliath throat bellow", 1.18f, 18, (t, n) =>
            Envelope(t, 1.18f) * (.46f * Wave(51 - 13 * t, t) + .21f * Wave(96 + 8 * Mathf.Sin(t * 21), t) + .26f * n));
        voices[1] = Clip("Leech electrical arcing", .72f, 12, (t, n) =>
            Envelope(t, .72f) * (.32f * Wave(370 + 105 * Mathf.Sin(t * 70), t) + .48f * n) * Mathf.Pow(Mathf.Sin(t * 54), 2));
        voices[2] = Clip("Stalker hiss and clicks", .91f, 31, (t, n) =>
            Envelope(t, .91f) * n * .45f + Click(t, .07f, n) * .48f + Click(t, .24f, n) * .33f + Click(t, .42f, n) * .45f);
        voices[3] = Clip("Swarmer squeal", .36f, 17, (t, n) =>
            Envelope(t, .36f) * (Wave(950 - 770 * t + 138 * Mathf.Sin(t * 31), t) * .49f + n * .12f));
        for (int i = 0; i < roomTails.Length; i++)
        {
            int room = i;
            float reflection = new[] { .074f, .105f, .036f, .055f }[i];
            float decay = new[] { 6f, 4.6f, 10f, 7.5f }[i];
            roomTails[i] = Clip("Report reflections " + i, 1.05f, 21 + i, (t, n) =>
            {
                if (t < reflection) return 0;
                float elapsed = t - reflection;
                return (.16f * n + .12f * Wave(94 + room * 17, elapsed)) * Mathf.Exp(-elapsed * decay) +
                    .21f * Click(t, reflection, n) + .13f * Click(t, reflection * 2.2f, n) +
                    .07f * Click(t, reflection * 3.9f, n);
            });
            roomTone[i] = Clip("Facility ventilation floor " + (i + 1), 4, 71 + i, (t, n) =>
            {
                float fan = (.11f + .05f * Wave(1 + room * .25f, t)) * Wave(43 + room * 7, t);
                return fan + .045f * Wave(92 + room * 11, t) + .013f * n * Mathf.Pow(Mathf.Sin(t * Mathf.PI / 4), 2);
            });
        }
    }

    internal Material Indicator(bool online) => online ? panelOn : panelOff;
    internal AudioClip Voice(Species species) => voices[(int)species];
    internal AudioClip RoomTail(int floor) => roomTails[floor];
    internal AudioClip RoomTone(int floor) => roomTone[floor];
    internal AudioClip Footstep(StepSurface surface) => footsteps[(int)surface, Random.Range(0, footsteps.GetLength(1))];

    private static AudioClip CreateFootstep(StepSurface surface, int variant)
    {
        float offset = variant * .004f;
        float pitch = variant * 9;
        return Clip($"Boot on {surface} {variant + 1}", surface == StepSurface.Steel ? .33f : .23f,
            66 + (int)surface * 18 + variant, (t, noise) =>
            {
                float heel = Mathf.Exp(-t * 48);
                float toe = Click(t, .045f + offset, noise);
                return surface switch
                {
                    StepSurface.Steel => .31f * noise * heel + .27f * Wave(212 + pitch, t) * Mathf.Exp(-t * 25)
                        + .16f * Wave(687 + pitch, t) * Mathf.Exp(-t * 16) + .26f * toe,
                    StepSurface.Wood => (.25f * noise + .48f * Wave(135 + pitch, t)) * Mathf.Exp(-t * 35)
                        + .15f * Wave(275 + pitch, t) * Mathf.Exp(-t * 39) + .28f * toe,
                    StepSurface.Plastic => (.24f * noise + .34f * Wave(181 + pitch, t)) * heel
                        + .15f * Wave(842 + pitch, t) * Mathf.Exp(-t * 29) + .29f * toe,
                    StepSurface.Cardboard => .45f * noise * Mathf.Exp(-t * 31)
                        + .23f * Wave(78 + pitch, t) * heel + .16f * toe + .13f * Click(t, .076f + offset, noise),
                    _ => (.43f * noise + .35f * Wave(94 + pitch, t)) * Mathf.Exp(-t * 36) + .4f * toe
                };
            });
    }

    private static Material CreatureSurface(string name, float smoothness, float relief)
    {
        Material material = Flat(Color.white);
        material.name = name + " skin";
        material.mainTexture = Resources.Load<Texture2D>("FacilityCreatures/" + name + "Albedo");
        material.SetTexture("_BumpMap", Resources.Load<Texture2D>("FacilityCreatures/" + name + "Normal"));
        material.EnableKeyword("_NORMALMAP");
        material.SetFloat("_BumpScale", relief);
        material.SetFloat("_Smoothness", smoothness);
        return material;
    }
    internal void PropImpact(Vector3 point, bool metal) =>
        AudioSource.PlayClipAtPoint(metal ? metalHit : softHit, point, .48f);

    internal void BreakLight(Vector3 point, Vector3 normal)
    {
        AudioSource.PlayClipAtPoint(arc, point, .6f);
        Impact(point, normal, new Color(.65f, .88f, 1));
        Impact(point, Vector3.down, new Color(1, .67f, .29f));
    }

    internal void Discharge(Vector3 start, Vector3 end)
    {
        AudioSource.PlayClipAtPoint(arc, start, .8f);
        if ((end - start).sqrMagnitude < .0025f) return;
        GameObject bolt = new("Volt-Leech arc");
        LineRenderer line = bolt.AddComponent<LineRenderer>();
        line.sharedMaterial = panelOn;
        line.widthMultiplier = .065f;
        line.positionCount = 10;
        line.SetPosition(0, start);
        for (int i = 1; i < line.positionCount - 1; i++)
            line.SetPosition(i, Vector3.Lerp(start, end, i / (float)(line.positionCount - 1)) + Random.insideUnitSphere * .26f);
        line.SetPosition(line.positionCount - 1, end);
        Light burst = bolt.AddComponent<Light>();
        bolt.transform.position = start;
        burst.type = LightType.Point;
        burst.color = new Color(.23f, .83f, 1);
        burst.intensity = 11;
        burst.range = 4;
        Object.Destroy(bolt, .13f);
    }

    internal Material WeatheredConcrete(Material source)
    {
        weatheredConcrete = Surface("Stained facility concrete", new Color(.48f, .47f, .42f),
            new Color(.25f, .25f, .22f), 51, .21f);
        weatheredConcrete.color = Color.Lerp(source.color, Color.white, .72f);
        weatheredConcrete.mainTextureScale = new Vector2(6, 5);
        const int width = 128;
        concreteNormal = new Texture2D(width, width, TextureFormat.RGBA32, true, true) { name = "Concrete grain normals" };
        var pixels = new Color[width * width];
        for (int y = 0; y < width; y++)
        for (int x = 0; x < width; x++)
        {
            float xSlope = Mathf.PerlinNoise((x + 1) * .44f, y * .44f) - Mathf.PerlinNoise((x - 1) * .44f, y * .44f);
            float ySlope = Mathf.PerlinNoise(x * .44f, (y + 1) * .44f) - Mathf.PerlinNoise(x * .44f, (y - 1) * .44f);
            Vector3 normal = new Vector3(xSlope * .6f, ySlope * .6f, 1).normalized;
            pixels[y * width + x] = new Color(.5f + normal.x * .5f, .5f + normal.y * .5f, .5f + normal.z * .5f, 1);
        }
        concreteNormal.SetPixels(pixels);
        concreteNormal.Apply(true, true);
        weatheredConcrete.SetTexture("_BumpMap", concreteNormal);
        weatheredConcrete.EnableKeyword("_NORMALMAP");
        return weatheredConcrete;
    }

    private static float Wave(float frequency, float time) => Mathf.Sin(2 * Mathf.PI * frequency * time);
    private static float Envelope(float t, float length) => Mathf.Clamp01(t * 47) * Mathf.Pow(Mathf.Clamp01(1 - t / length), .75f);
    private static float Click(float t, float at, float noise) =>
        t < at ? 0 : (.65f * noise + .28f * Wave(520, t - at)) * Mathf.Exp(-(t - at) * 53);

    private static AudioClip Clip(string name, float duration, int seed, System.Func<float, float, float> sample)
    {
        const int rate = 44100;
        float[] wave = new float[Mathf.CeilToInt(rate * duration)];
        var random = new System.Random(seed);
        for (int i = 0; i < wave.Length; i++)
            wave[i] = Mathf.Clamp(sample(i / (float)rate, (float)random.NextDouble() * 2 - 1), -1, 1);
        AudioClip clip = AudioClip.Create(name, wave.Length, 1, rate, false);
        clip.SetData(wave, 0);
        return clip;
    }

    private static Material Flat(Color color, Color emission = default)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        if (emission.maxColorComponent > 0)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
        }
        return mat;
    }

    private static Material Surface(string name, Color light, Color dark, int seed, float smooth)
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGB24, true);
        texture.name = name + " albedo";
        var pixels = new Color[size * size];
        for (int z = 0; z < size; z++)
        for (int x = 0; x < size; x++)
        {
            float blotch = Mathf.PerlinNoise(x * .063f + seed, z * .065f);
            float pores = Mathf.PerlinNoise(x * .71f + seed, z * .74f);
            float vein = Mathf.Abs(Mathf.Sin(x * .12f + z * .05f + 11 * blotch));
            pixels[z * size + x] = Color.Lerp(dark, light, Mathf.Clamp01(blotch * .72f + pores * .32f - (vein < .12f ? .24f : 0)));
        }
        texture.SetPixels(pixels);
        texture.Apply(true, true);
        Material material = Flat(Color.white);
        material.name = name;
        material.mainTexture = texture;
        material.SetFloat("_Smoothness", smooth);
        return material;
    }

    private static Mesh BuildFlesh()
    {
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        source.SetActive(false);
        Mesh mesh = Object.Instantiate(source.GetComponent<MeshFilter>().sharedMesh);
        Object.Destroy(source);
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            float coarse = Mathf.PerlinNoise(v.x * 6 + v.z * 2 + 3, v.y * 7 + v.z * 4 + 9);
            float ridge = Mathf.PerlinNoise(v.x * 16 + v.y * 9, v.z * 12 + 33);
            vertices[i] *= .95f + .14f * coarse + .05f * ridge;
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Material sparks;

    internal void Impact(Vector3 point, Vector3 direction, Color color)
    {
        GameObject puff = new("Impact debris");
        puff.transform.SetPositionAndRotation(point, Quaternion.LookRotation(direction));
        ParticleSystem particles = puff.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.startColor = color;
        main.startLifetime = .22f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1, 4);
        main.startSize = new ParticleSystem.MinMaxCurve(.026f, .065f);
        main.gravityModifier = .6f;
        main.loop = false;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.angle = 32;
        if (!sparks) sparks = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        puff.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparks;
        particles.Emit(9);
        Object.Destroy(puff, .4f);
    }

    internal void Dispose()
    {
        foreach (AudioClip clip in voices) Object.Destroy(clip);
        foreach (AudioClip clip in roomTails) Object.Destroy(clip);
        foreach (AudioClip clip in roomTone) Object.Destroy(clip);
        Object.Destroy(Shot);
        Object.Destroy(Reload);
        foreach (AudioClip clip in footsteps) Object.Destroy(clip);
        Object.Destroy(arc);
        Object.Destroy(metalHit);
        Object.Destroy(softHit);
        foreach (Material material in skin) Object.Destroy(material);
        foreach (Material material in new[] { bone, eyes, black, panelOn, panelOff, Lamp, SignBacking, sparks })
            if (material) Object.Destroy(material);
        if (weatheredConcrete)
        {
            Object.Destroy(weatheredConcrete.mainTexture);
            Object.Destroy(concreteNormal);
            Object.Destroy(weatheredConcrete);
        }
        Object.Destroy(flesh);
    }
}

