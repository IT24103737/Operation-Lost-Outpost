
using UnityEngine;

public class WarehouseGenerator : MonoBehaviour
{
    [ContextMenu("Generate Warehouse 13")]
    public void GenerateWarehouse()
    {
        GameObject old = GameObject.Find("Generated_Warehouse13");
        if (old != null) DestroyImmediate(old);

        GameObject root = new GameObject("Generated_Warehouse13");
        transform.position = Vector3.zero;

        Material concrete = Mat("Concrete", new Color(0.27f, 0.30f, 0.32f));
        Material wall = Mat("Steel Walls", new Color(0.35f, 0.40f, 0.43f));
        Material dark = Mat("Dark Metal", new Color(0.10f, 0.13f, 0.15f));
        Material rack = Mat("Rack Blue", new Color(0.10f, 0.28f, 0.40f));
        Material wood = Mat("Crate Wood", new Color(0.48f, 0.31f, 0.17f));
        Material yellow = Mat("Safety Yellow", new Color(1f, 0.65f, 0.05f));
        Material green = Mat("Exit Green", new Color(0.05f, 0.65f, 0.25f));
        Material screen = Mat("Console Screen", new Color(0.05f, 0.8f, 0.9f));

        // Floor and perimeter walls
        Cube(root, "Concrete Floor", new Vector3(0, -0.3f, 0),
            new Vector3(40, 0.6f, 30), concrete);

        Cube(root, "Back Wall", new Vector3(0, 3, 15),
            new Vector3(40, 6, 0.5f), wall);

        Cube(root, "Left Wall", new Vector3(-20, 3, 0),
            new Vector3(0.5f, 6, 30), wall);

        Cube(root, "Right Wall", new Vector3(20, 3, 0),
            new Vector3(0.5f, 6, 30), wall);

        // Front wall with a central entrance gap
        Cube(root, "Front Left Wall", new Vector3(-13, 3, -15),
            new Vector3(14, 6, 0.5f), wall);

        Cube(root, "Front Right Wall", new Vector3(13, 3, -15),
            new Vector3(14, 6, 0.5f), wall);

        Cube(root, "Entrance Header", new Vector3(0, 5.5f, -15),
            new Vector3(12, 1, 0.5f), wall);

        // Industrial roof beams
        for (int x = -16; x <= 16; x += 8)
        {
            Cube(root, "Roof Beam",
                new Vector3(x, 5.7f, 0),
                new Vector3(0.25f, 0.25f, 29), dark);
        }

        // Storage rack rows
        for (int x = -12; x <= 12; x += 8)
        {
            for (int z = -7; z <= 9; z += 4)
            {
                // Rack supports
                Cube(root, "Rack Upright",
                    new Vector3(x - 1.1f, 1.5f, z),
                    new Vector3(0.18f, 3, 0.18f), rack);

                Cube(root, "Rack Upright",
                    new Vector3(x + 1.1f, 1.5f, z),
                    new Vector3(0.18f, 3, 0.18f), rack);

                // Shelves
                for (int level = 0; level < 3; level++)
                {
                    Cube(root, "Storage Shelf",
                        new Vector3(x, 0.7f + level, z),
                        new Vector3(2.3f, 0.12f, 0.9f), dark);
                }

                // Boxes on the shelves
                for (int level = 0; level < 3; level++)
                {
                    Cube(root, "Storage Crate",
                        new Vector3(x - 0.45f, 1.05f + level, z),
                        new Vector3(0.7f, 0.55f, 0.65f), wood);

                    Cube(root, "Storage Crate",
                        new Vector3(x + 0.4f, 1.05f + level, z),
                        new Vector3(0.65f, 0.55f, 0.65f), wood);
                }
            }
        }

        // Extra floor crates near the loading area
        for (int i = 0; i < 6; i++)
        {
            float x = -16 + (i % 3) * 2;
            float z = 9 + (i / 3) * 2;

            Cube(root, "Loading Crate",
                new Vector3(x, 0.65f, z),
                new Vector3(1.5f, 1.3f, 1.5f), wood);

            Cube(root, "Pallet",
                new Vector3(x, 0.12f, z),
                new Vector3(1.7f, 0.2f, 1.7f), dark);
        }

        // Enclosed control room in the back-right corner
        Cube(root, "Control Room Back",
            new Vector3(13, 2, 12),
            new Vector3(10, 4, 0.3f), dark);

        Cube(root, "Control Room Side",
            new Vector3(18, 2, 7),
            new Vector3(0.3f, 4, 10), dark);

        Cube(root, "Control Room Left",
            new Vector3(8, 2, 7),
            new Vector3(0.3f, 4, 10), dark);

        Cube(root, "Control Room Console",
            new Vector3(13, 1, 10),
            new Vector3(3, 1, 1), rack);

        Cube(root, "Power Console Screen",
            new Vector3(13, 1.8f, 9.5f),
            new Vector3(1.4f, 0.5f, 0.12f), screen);

        // Emergency exit marker on the back wall
        Cube(root, "Emergency Exit Sign",
            new Vector3(-15, 4.5f, 14.65f),
            new Vector3(3, 0.8f, 0.15f), green);

        // Safety markings along the main walkway
        for (int z = -12; z <= 10; z += 3)
        {
            Cube(root, "Walkway Marking",
                new Vector3(0, 0.015f, z),
                new Vector3(3, 0.025f, 0.15f), yellow);
        }

        // Ceiling lights
        for (int x = -12; x <= 12; x += 8)
        {
            for (int z = -8; z <= 8; z += 8)
            {
                Cube(root, "Ceiling Light Fixture",
                    new Vector3(x, 5.45f, z),
                    new Vector3(2, 0.12f, 0.7f), yellow);

                GameObject lightObject = new GameObject("Warehouse Light");
                lightObject.transform.SetParent(root.transform);
                lightObject.transform.position = new Vector3(x, 5.1f, z);

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 10;
                light.intensity = 3;
                light.color = new Color(0.75f, 0.85f, 1f);
            }
        }

        // Player starting point and destination marker
        Cube(root, "Player Start Marker",
            new Vector3(0, 0.08f, -12),
            new Vector3(1, 0.12f, 1), green);

        Debug.Log("Warehouse 13 generated! Inspect the Scene view.");
    }

    GameObject Cube(GameObject root, string objectName,
        Vector3 position, Vector3 scale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = objectName;
        obj.transform.SetParent(root.transform);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    Material Mat(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = materialName;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        return material;
    }
}
