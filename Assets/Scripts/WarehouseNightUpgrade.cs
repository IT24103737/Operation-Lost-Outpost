
using UnityEngine;

public class WarehouseNightUpgrade : MonoBehaviour
{
    [Header("Night Atmosphere")]
    public Color nightAmbient = new Color(0.075f, 0.09f, 0.15f);
    public Color fogColor = new Color(0.035f, 0.045f, 0.075f);
    [Range(0.001f, 0.08f)] public float fogDensity = 0.018f;

    [Header("Warehouse Layout")]
    public float warehouseWidth = 40f;
    public float warehouseLength = 30f;
    public int debrisCount = 22;

    private const string RootName = "NightUpgrade_Objects";
    private Transform upgradeRoot;

    [ContextMenu("Generate Night Upgrade")]
    public void GenerateNightUpgrade()
    {
        // Only replace objects created by this upgrade.
        Transform oldRoot = transform.Find(RootName);

        if (oldRoot != null)
            DestroyImmediate(oldRoot.gameObject);

        GameObject rootObject = new GameObject(RootName);
        rootObject.transform.SetParent(transform, false);
        upgradeRoot = rootObject.transform;

        CreateMaterials();
        SetupNightLighting();
        CreateEmergencyLights();
        CreateCargoPiles();
        CreateDebris();
        CreateWallDetails();

        Debug.Log("Night warehouse upgrade generated successfully.");
    }

    private Material floorMaterial;
    private Material metalMaterial;
    private Material crateMaterial;
    private Material hazardMaterial;
    private Material redMaterial;

    private Material MakeMaterial(string materialName, Color color,
        float metallic, float smoothness, bool emission = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);

        if (emission && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2.5f);
        }

        return material;
    }

    private void CreateMaterials()
    {
        floorMaterial = MakeMaterial(
            "Night_DirtyConcrete",
            new Color(0.19f, 0.20f, 0.21f), 0.05f, 0.22f);

        metalMaterial = MakeMaterial(
            "Night_OldMetal",
            new Color(0.16f, 0.18f, 0.20f), 0.72f, 0.30f);

        crateMaterial = MakeMaterial(
            "Night_WornCargo",
            new Color(0.25f, 0.19f, 0.12f), 0.05f, 0.18f);

        hazardMaterial = MakeMaterial(
            "Night_HazardYellow",
            new Color(0.75f, 0.43f, 0.06f), 0.12f, 0.22f);

        redMaterial = MakeMaterial(
            "Night_EmergencyRed",
            new Color(0.8f, 0.015f, 0.01f), 0f, 0.25f, true);
    }

    private void SetupNightLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = nightAmbient;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;

        // Dim the existing sun instead of creating another one.
        Light[] lights = FindObjectsByType<Light>();

        foreach (Light lightSource in lights)
        {
            if (lightSource.type == LightType.Directional &&
                lightSource.gameObject.scene == gameObject.scene)
            {
                lightSource.color = new Color(0.43f, 0.52f, 0.78f);
                lightSource.intensity = 0.22f;
            }
        }

        // Soft blue industrial lights.
        CreatePointLight("Blue_Overhead_01",
            new Vector3(-10f, 5.5f, 0f),
            new Color(0.28f, 0.42f, 1f), 5f, 10f);

        CreatePointLight("Blue_Overhead_02",
            new Vector3(10f, 5.5f, 0f),
            new Color(0.30f, 0.45f, 1f), 5f, 10f);

        CreatePointLight("Cold_Back_Light",
            new Vector3(0f, 5f, 11f),
            new Color(0.30f, 0.48f, 0.8f), 4f, 9f);
    }

    private void CreateEmergencyLights()
    {
        Vector3[] positions =
        {
            new Vector3(-17f, 4.1f, -12f),
            new Vector3(17f, 4.1f, -12f),
            new Vector3(-17f, 4.1f, 12f),
            new Vector3(17f, 4.1f, 12f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject lamp = GameObject.CreatePrimitive(
                PrimitiveType.Cube);

            lamp.name = "Emergency_Lamp_" + (i + 1);
            lamp.transform.SetParent(upgradeRoot, false);
            lamp.transform.position = positions[i];
            lamp.transform.localScale =
                new Vector3(0.45f, 0.18f, 0.3f);

            lamp.GetComponent<Renderer>().sharedMaterial = redMaterial;

            Collider lampCollider = lamp.GetComponent<Collider>();
            if (lampCollider != null)
                DestroyImmediate(lampCollider);

            CreatePointLight("Emergency_Red_Light_" + (i + 1),
                positions[i] + Vector3.down * 0.2f,
                new Color(1f, 0.025f, 0.015f), 3f, 6f);
        }
    }

    private void CreateCargoPiles()
    {
        // Keep cargo near the perimeter so the main play area stays open.
        CreateCargoStack(new Vector3(-15f, 0f, -7f), 3);
        CreateCargoStack(new Vector3(15f, 0f, 7f), 3);
        CreateCargoStack(new Vector3(-15f, 0f, 9f), 2);
        CreateCargoStack(new Vector3(15f, 0f, -9f), 2);
    }

    private void CreateCargoStack(Vector3 basePosition, int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject crate = GameObject.CreatePrimitive(
                PrimitiveType.Cube);

            crate.name = "Worn_Cargo_Crate";
            crate.transform.SetParent(upgradeRoot, false);

            float width = Random.Range(0.9f, 1.5f);
            float height = Random.Range(0.8f, 1.3f);
            float depth = Random.Range(0.8f, 1.3f);

            crate.transform.localScale =
                new Vector3(width, height, depth);

            crate.transform.position = basePosition +
                new Vector3(
                    Random.Range(-0.7f, 0.7f),
                    height * 0.5f + (i / 2) * 1.1f,
                    Random.Range(-0.7f, 0.7f));

            crate.transform.rotation = Quaternion.Euler(
                0f, Random.Range(-12f, 12f),
                Random.Range(-3f, 3f));

            crate.GetComponent<Renderer>().sharedMaterial =
                crateMaterial;
        }
    }

    private void CreateDebris()
    {
        Random.InitState(1313);

        for (int i = 0; i < debrisCount; i++)
        {
            // Scatter small pieces around the outside edges.
            bool leftSide = i % 2 == 0;

            float x = leftSide
                ? Random.Range(-18f, -12f)
                : Random.Range(12f, 18f);

            float z = Random.Range(
                -warehouseLength * 0.42f,
                warehouseLength * 0.42f);

            float width = Random.Range(0.18f, 0.65f);
            float height = Random.Range(0.06f, 0.20f);
            float depth = Random.Range(0.2f, 0.7f);

            GameObject piece = GameObject.CreatePrimitive(
                PrimitiveType.Cube);

            piece.name = "Scattered_Warehouse_Debris";
            piece.transform.SetParent(upgradeRoot, false);
            piece.transform.position =
                new Vector3(x, height * 0.5f, z);

            piece.transform.localScale =
                new Vector3(width, height, depth);

            piece.transform.rotation = Quaternion.Euler(
                0f, Random.Range(0f, 180f),
                Random.Range(-4f, 4f));

            piece.GetComponent<Renderer>().sharedMaterial =
                Random.value > 0.5f ? metalMaterial : crateMaterial;

            Collider debrisCollider = piece.GetComponent<Collider>();
            if (debrisCollider != null)
                DestroyImmediate(debrisCollider);
        }

        // A few yellow industrial floor markings near the sides.
        for (int i = 0; i < 6; i++)
        {
            GameObject marking = GameObject.CreatePrimitive(
                PrimitiveType.Cube);

            marking.name = "Worn_Hazard_Marking";
            marking.transform.SetParent(upgradeRoot, false);
            marking.transform.position =
                new Vector3(-11f + i * 4.4f, 0.015f, -13f);

            marking.transform.localScale =
                new Vector3(1.5f, 0.025f, 0.10f);

            marking.GetComponent<Renderer>().sharedMaterial =
                hazardMaterial;

            Collider markingCollider =
                marking.GetComponent<Collider>();

            if (markingCollider != null)
                DestroyImmediate(markingCollider);
        }
    }

    private void CreateWallDetails()
    {
        // Metal supports along the side walls.
        for (int i = 0; i < 7; i++)
        {
            float z = -12f + i * 4f;

            CreateMetalSupport(
                new Vector3(-19.1f, 2.4f, z));

            CreateMetalSupport(
                new Vector3(19.1f, 2.4f, z));
        }
    }

    private void CreateMetalSupport(Vector3 position)
    {
        GameObject support = GameObject.CreatePrimitive(
            PrimitiveType.Cube);

        support.name = "Old_Wall_Support";
        support.transform.SetParent(upgradeRoot, false);
        support.transform.position = position;
        support.transform.localScale =
            new Vector3(0.22f, 4.8f, 0.22f);

        support.GetComponent<Renderer>().sharedMaterial =
            metalMaterial;

        Collider supportCollider = support.GetComponent<Collider>();
        if (supportCollider != null)
            DestroyImmediate(supportCollider);
    }

    private void CreatePointLight(
        string lightName, Vector3 position,
        Color color, float intensity, float range)
    {
        GameObject lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(upgradeRoot, false);
        lightObject.transform.position = position;

        Light pointLight = lightObject.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = color;
        pointLight.intensity = intensity;
        pointLight.range = range;
        pointLight.shadows = LightShadows.Soft;
    }
}
