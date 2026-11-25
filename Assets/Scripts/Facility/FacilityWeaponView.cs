using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

internal sealed class FacilityWeaponView
{
    internal const float ReloadDuration = 1.55f;
    private static readonly string[] SkinNames = { "Factory Black", "Sandstone", "Olive Field" };
    private const uint ArmLightLayer = 1u << 7;
    private readonly Transform root;
    private readonly Transform magazine;
    private readonly Renderer[] renderers;
    private readonly Material[][] finishes;
    private readonly List<Material> materials = new();
    private readonly Hand right, left;
    private readonly Vector3 magazineHome, magazineGrip, triggerGrip, supportGrip;
    private float stride, sprintBlend, recoil;
    private float recoilVelocity;
    private float obstructionBlend, strideBlend;
    private readonly Texture2D receiverGrain, sightGrain, sightNormal;
    private readonly List<Mesh> sightMeshes = new();
    private readonly float sightHeight;
    private int skin;

    internal Transform Muzzle { get; }
    internal float AimBlend { get; private set; }

    internal FacilityWeaponView(Transform camera, Transform gun, Mesh handMesh, Material surface)
    {
        root = new GameObject("Two-handed weapon rig").transform;
        root.SetParent(camera, false);
        gun.SetParent(root, false);
        gun.localPosition = Vector3.zero;
        gun.localRotation = Quaternion.Euler(0, 90, 0);
        gun.name = "First-person weapon";
        foreach (Transform part in gun.GetComponentsInChildren<Transform>())
            if (part.name == "HK_MP5_Bullet") part.gameObject.SetActive(false);
        foreach (Collider shape in gun.GetComponentsInChildren<Collider>())
            shape.enabled = false;
        renderers = gun.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Bounds bounds = WeaponBounds();
        gun.localScale *= .62f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        bounds = WeaponBounds();
        gun.localPosition -= bounds.center;
        bounds = WeaponBounds();
        sightHeight = bounds.max.y + .015f;
        (sightGrain, sightNormal) = CreateSightTextures();
        Material sightSteel = MaterialOf(surface, "Parkerized iron sight steel", new Color(.34f, .35f, .36f), .37f);
        sightSteel.mainTexture = sightGrain;
        sightSteel.SetFloat("_Metallic", .65f);
        sightSteel.SetTexture("_BumpMap", sightNormal);
        sightSteel.SetFloat("_BumpScale", .52f);
        sightSteel.EnableKeyword("_NORMALMAP");
        BuildIronSights(bounds, sightSteel);

        foreach (Transform part in gun.GetComponentsInChildren<Transform>())
            if (part.name == "HK_MP5_Magazine") magazine = part;
        if (magazine)
        {
            magazineHome = magazine.localPosition;
            magazineGrip = root.InverseTransformPoint(magazine.GetComponent<Renderer>().bounds.center);
        }
        else magazineGrip = new Vector3(0, -.075f, .025f);

        triggerGrip = new Vector3(.018f, -.041f, magazineGrip.z - .093f);
        supportGrip = new Vector3(-.026f, -.044f, Mathf.Min(bounds.max.z - .075f, magazineGrip.z + .14f));
        Muzzle = new GameObject("Muzzle flash").transform;
        Muzzle.SetParent(root, false);
        Muzzle.localPosition = new Vector3(0, .047f, bounds.max.z + .025f);
        var flash = new GameObject("Muzzle flash core");
        flash.transform.SetParent(Muzzle, false);
        flash.transform.localPosition = Vector3.forward * .065f;
        flash.transform.localScale = new Vector3(.034f, .038f, .15f);
        flash.AddComponent<MeshFilter>().sharedMesh = handMesh;
        var flashRenderer = flash.AddComponent<MeshRenderer>();
        var flashMaterial = MaterialOf(surface, "Hot muzzle gas", new Color(1, .74f, .25f), 0);
        flashMaterial.EnableKeyword("_EMISSION");
        flashMaterial.SetColor("_EmissionColor", new Color(3f, 1.7f, .44f));
        flashRenderer.sharedMaterial = flashMaterial;
        flashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        receiverGrain = new Texture2D(128, 64, TextureFormat.RGB24, true) { name = "Machined receiver finish" };
        var metal = new Color[128 * 64];
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 128; x++)
        {
            float grain = .79f + .11f * Mathf.PerlinNoise(x * .28f, y * .9f) + .1f * Mathf.PerlinNoise(x * .055f, y * .089f);
            metal[y * 128 + x] = new Color(grain, grain, grain);
        }
        receiverGrain.SetPixels(metal);
        receiverGrain.Apply(true, true);
        Material handSkin = Resources.Load<Material>("FacilityArms/Skin");
        Material sleeve = Resources.Load<Material>("FacilityArms/Workwear");
        left = new Hand("Left", "Left supporting hand", root, handSkin, sleeve);
        right = new Hand("Right", "Right trigger hand", root, handSkin, sleeve);
        var bounce = new GameObject("First-person arm bounce").AddComponent<Light>();
        bounce.transform.SetParent(camera, false);
        bounce.transform.localPosition = new Vector3(.12f, .15f, .25f);
        bounce.type = LightType.Point;
        bounce.color = new Color(1, .91f, .82f);
        bounce.intensity = .34f;
        bounce.range = .95f;
        bounce.GetUniversalAdditionalLightData().renderingLayers = ArmLightLayer;

        finishes = new Material[SkinNames.Length][];
        Color[] colors = { new(.21f, .23f, .25f), new(.66f, .46f, .29f), new(.33f, .43f, .26f) };
        for (int style = 0; style < finishes.Length; style++)
        {
            finishes[style] = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material original = renderers[i].sharedMaterial;
                bool receiver = renderers[i].name == "HK_MP5_Body";
                Material finish = MaterialOf(surface, SkinNames[style], receiver ? colors[style] : colors[0] * .64f, receiver ? .48f : .28f);
                if (original && original.HasProperty("_MainTex")) finish.mainTexture = original.mainTexture;
                if (receiver && !finish.mainTexture) finish.mainTexture = receiverGrain;
                finish.SetFloat("_Metallic", receiver ? .48f : .11f);
                finishes[style][i] = finish;
            }
        }
        ApplySkin();
        Pose(false, false, -1, 0);
    }

    private Bounds WeaponBounds()
    {
        Bounds bounds = default;
        bool first = true;
        foreach (Renderer renderer in renderers)
        {
            Bounds local = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new((i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z);
                Vector3 point = root.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }

    private void BuildIronSights(Bounds bounds, Material metal)
    {
        float rear = bounds.min.z + .20f;
        float front = bounds.max.z - .055f;
        SightRing("Rear diopter aperture", new Vector3(0, sightHeight, rear), .016f, .0045f, metal);
        SightRing("Front sight hood", new Vector3(0, sightHeight, front), .019f, .003f, metal);
        SightBlock("Front sight post", new Vector3(0, sightHeight - .009f, front),
            new Vector3(.0037f, .018f, .006f), metal);
        SightBlock("Front sight base", new Vector3(0, sightHeight - .021f, front),
            new Vector3(.026f, .014f, .016f), metal);
        SightBlock("Rear sight base", new Vector3(0, sightHeight - .022f, rear),
            new Vector3(.025f, .014f, .023f), metal);
    }

    private void SightBlock(string name, Vector3 position, Vector3 size, Material metal)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(root, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.GetComponent<Collider>().enabled = false;
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = metal;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void SightRing(string name, Vector3 position, float radius, float thickness, Material metal)
    {
        const int sides = 48, bevel = 12;
        const int stride = bevel + 1;
        var vertices = new Vector3[(sides + 1) * stride];
        var normals = new Vector3[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var indices = new List<int>(sides * bevel * 6);
        for (int side = 0; side <= sides; side++)
        {
            float phase = side * Mathf.PI * 2 / sides;
            Vector3 radial = new(Mathf.Cos(phase), Mathf.Sin(phase), 0);
            for (int edge = 0; edge <= bevel; edge++)
            {
                float roll = edge * Mathf.PI * 2 / bevel;
                int index = side * stride + edge;
                Vector3 normal = radial * Mathf.Cos(roll) + Vector3.forward * Mathf.Sin(roll);
                vertices[index] = radial * radius + normal * thickness;
                normals[index] = normal;
                uv[index] = new Vector2(side / (float)sides, edge / (float)bevel);
                if (side == sides || edge == bevel) continue;
                indices.Add(index);
                indices.Add(index + stride);
                indices.Add(index + 1);
                indices.Add(index + stride);
                indices.Add(index + stride + 1);
                indices.Add(index + 1);
            }
        }
        var mesh = new Mesh { name = name, vertices = vertices, normals = normals, uv = uv, triangles = indices.ToArray() };
        mesh.RecalculateTangents();
        sightMeshes.Add(mesh);
        var part = new GameObject(name);
        part.transform.SetParent(root, false);
        part.transform.localPosition = position;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = metal;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static (Texture2D, Texture2D) CreateSightTextures()
    {
        const int size = 1024;
        var albedo = new Texture2D(size, size, TextureFormat.RGB24, true) { name = "Iron sight 1K steel grain", anisoLevel = 4 };
        var normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "Iron sight 1K machining normals", anisoLevel = 4 };
        var color = new Color32[size * size];
        var relief = new Color32[color.Length];
        var noise = new System.Random(83);
        for (int y = 0; y < size; y++)
        {
            float band = Mathf.PerlinNoise(y * .057f, 7.23f);
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                int fleck = noise.Next(-15, 16);
                int brushing = (int)(Mathf.Sin(y * 1.91f + x * .014f) * 5);
                byte gray = (byte)(175 + Mathf.RoundToInt(band * 34) + brushing + fleck);
                color[index] = new Color32(gray, gray, (byte)(gray + 3), 255);
                relief[index] = new Color32((byte)(128 + fleck / 2), (byte)(128 + brushing * 2 + fleck / 2), 253, 255);
            }
        }
        albedo.SetPixels32(color);
        normal.SetPixels32(relief);
        albedo.Apply(true, true);
        normal.Apply(true, true);
        return (albedo, normal);
    }

    private Material MaterialOf(Material template, string label, Color color, float shine)
    {
        var material = new Material(template) { name = label, color = color, mainTexture = null };
        material.SetFloat("_Smoothness", shine);
        materials.Add(material);
        return material;
    }

    internal string CycleSkin()
    {
        skin = (skin + 1) % finishes.Length;
        ApplySkin();
        return SkinNames[skin];
    }

    private void ApplySkin()
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].sharedMaterial = finishes[skin][i];
    }

    internal void Kick()
    {
        recoil = Mathf.Min(.102f, recoil + .052f);
        recoilVelocity = .18f;
    }

    private static float Ease(float value) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(value));

    internal void Pose(bool moving, bool sprinting, float reload, float delta, float wall = 0, bool aiming = false)
    {
        sprintBlend = Mathf.MoveTowards(sprintBlend, sprinting && reload < 0 ? 1 : 0, delta * 6);
        obstructionBlend = Mathf.MoveTowards(obstructionBlend, wall, delta * (wall > obstructionBlend ? 9 : 5));
        strideBlend = Mathf.MoveTowards(strideBlend, moving ? 1 : 0, delta * 6);
        stride += delta * Mathf.Lerp(5, 14, sprintBlend);
        float bob = Mathf.Lerp(.003f, Mathf.Lerp(.009f, .028f, sprintBlend), strideBlend);
        float tilt = reload < 0 ? 0 : Ease(reload / .18f) * Ease((1 - reload) / .16f);
        float sprint = sprintBlend * (1 - obstructionBlend);
        root.localPosition = new Vector3(.17f + Mathf.Sin(stride) * bob - tilt * .045f, -.158f - sprint * .085f + Mathf.Cos(stride * 2) * bob + tilt * .052f - obstructionBlend * .13f,
            .66f - recoil - obstructionBlend * .24f);
        root.localRotation = Quaternion.Euler(-recoil * 83 + sprint * 21 + tilt * 12 + obstructionBlend * 49,
            -sprint * 26 - tilt * 8 + obstructionBlend * 12, -sprint * 28 - tilt * 23);
        AimBlend = Mathf.MoveTowards(AimBlend, aiming ? 1 : 0, delta * (aiming ? 6.7f : 8.5f));
        float ads = Ease(AimBlend);
        root.localPosition = Vector3.Lerp(root.localPosition, new Vector3(0, -sightHeight, .50f - recoil * .42f), ads);
        root.localRotation = Quaternion.Slerp(root.localRotation, Quaternion.Euler(-recoil * 25, 0, 0), ads);
        recoil = Mathf.SmoothDamp(recoil, 0, ref recoilVelocity, .083f, 3f, delta);

        float pull = reload < 0 ? 0 : Ease((reload - .18f) / .21f) * Ease((.74f - reload) / .24f);
        Vector3 magazineTravel = new(-pull * .092f, -pull * .21f, -pull * .04f);
        if (magazine)
            magazine.localPosition = magazineHome + magazine.parent.InverseTransformVector(root.TransformVector(magazineTravel));

        Vector3 leftGrip = supportGrip;
        float leftRoll = 104;
        float leftCurl = 95;
        if (reload >= 0)
        {
            float reach = Ease(reload / .18f) * Ease((.82f - reload) / .13f);
            leftGrip = Vector3.Lerp(supportGrip, magazineGrip + magazineTravel + new Vector3(-.044f, -.014f, .004f), reach);
            float rack = Ease((reload - .73f) / .09f) * Ease((1 - reload) / .12f);
            leftGrip = Vector3.Lerp(leftGrip, supportGrip + new Vector3(-.025f, .091f, -.065f * rack), rack);
            leftRoll = Mathf.Lerp(leftRoll, 62, reach);
            leftCurl = Mathf.Lerp(95, 60, rack);
        }
        Place(left, leftGrip, new Vector3(-.21f, -.40f, .23f), new Vector3(.22f, .40f, .88f), leftRoll, leftCurl);
        Place(right, triggerGrip, new Vector3(.32f, -.41f, .21f), new Vector3(0, .65f, .76f), -66, 92 + recoil * 70);
    }

    private void Place(Hand hand, Vector3 grip, Vector3 elbowInCamera, Vector3 fingers, float roll, float curl)
    {
        Vector3 elbow = root.InverseTransformPoint(root.parent.TransformPoint(elbowInCamera));
        hand.Pose(grip, elbow, fingers, roll, curl);
    }

    internal void SetVisible(bool visible) => root.gameObject.SetActive(visible);

    internal void Dispose()
    {
        foreach (Material material in materials) Object.Destroy(material);
        Object.Destroy(receiverGrain);
        Object.Destroy(sightGrain);
        Object.Destroy(sightNormal);
        foreach (Mesh mesh in sightMeshes) Object.Destroy(mesh);
    }

    private sealed class Hand
    {
        private const float HandScale = .91f;
        private readonly Transform wrist, sleeve;
        private readonly SkinnedMeshRenderer skin;
        private readonly float sleeveLength;

        internal Hand(string side, string name, Transform parent, Material flesh, Material fabric)
        {
            wrist = new GameObject(name).transform;
            wrist.SetParent(parent, false);
            wrist.localScale = Vector3.one * HandScale;
            skin = wrist.gameObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = Resources.Load<Mesh>("FacilityArms/" + side + "Hand");
            skin.sharedMaterial = flesh;
            skin.localBounds = skin.sharedMesh.bounds;
            skin.updateWhenOffscreen = true;
            skin.renderingLayerMask |= ArmLightLayer;
            skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            sleeve = new GameObject(side + " workwear sleeve").transform;
            sleeve.SetParent(parent, false);
            Mesh sleeveMesh = Resources.Load<Mesh>("FacilityArms/" + side + "Sleeve");
            sleeve.gameObject.AddComponent<MeshFilter>().sharedMesh = sleeveMesh;
            var cloth = sleeve.gameObject.AddComponent<MeshRenderer>();
            cloth.sharedMaterial = fabric;
            cloth.renderingLayerMask |= ArmLightLayer;
            cloth.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sleeveLength = -sleeveMesh.bounds.min.z;
        }

        internal void Pose(Vector3 grip, Vector3 elbow, Vector3 fingers, float roll, float curl)
        {
            wrist.localRotation = Quaternion.LookRotation(fingers) * Quaternion.Euler(0, 0, roll);
            wrist.localPosition = grip - wrist.localRotation * new Vector3(0, -.012f, .068f);
            skin.SetBlendShapeWeight(0, curl);
            Vector3 forearm = wrist.localPosition - elbow;
            sleeve.SetLocalPositionAndRotation(wrist.localPosition - forearm.normalized * .014f,
                Quaternion.LookRotation(forearm, wrist.localRotation * Vector3.up));
            sleeve.localScale = new Vector3(.87f, .87f, Mathf.Clamp(forearm.magnitude / sleeveLength, .72f, 1.3f));
        }
    }
}


