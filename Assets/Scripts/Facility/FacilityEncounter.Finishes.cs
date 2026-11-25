using UnityEngine;

public sealed partial class FacilityEncounter
{
    [SerializeField] private Texture2D steelPlate;
    [SerializeField] private Texture2D wallSheet;
    [SerializeField] private Texture2D wallRelief;
    [SerializeField] private Texture2D crateAtlas;
    [SerializeField] private Texture2D crateRelief;

    private Material brushedSteel, corrugatedSteel, crateBoards;

    private void BuildEnvironmentFinishes()
    {
        brushedSteel = Textured(steel, "Worn diamond plate", steelPlate, null, new Color(.86f, .89f, .91f), .46f, .42f);
        corrugatedSteel = Textured(steel, "Corrugated warehouse steel", wallSheet, wallRelief,
            new Color(.92f, .94f, .94f), .37f, .44f);
        crateBoards = Textured(wood, "Scuffed crate boards", crateAtlas, crateRelief, new Color(.92f, .86f, .77f), .02f, .29f);
        // Sample one complete timber face from the existing crate's UV atlas.
        Vector2 scale = new(.46f, .44f);
        Vector2 origin = new(.018f, .54f);
        crateBoards.mainTextureScale = scale;
        crateBoards.mainTextureOffset = origin;
        crateBoards.SetTextureScale("_BumpMap", scale);
        crateBoards.SetTextureOffset("_BumpMap", origin);
    }

    private static Material Textured(Material source, string label, Texture2D albedo, Texture2D normal,
        Color color, float metallic, float smoothness)
    {
        var material = new Material(source) { name = label, mainTexture = albedo, color = color };
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        if (normal)
        {
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", .74f);
            material.EnableKeyword("_NORMALMAP");
        }
        return material;
    }

    private Material EnvironmentFinish(string label, Material material)
    {
        if (material == wood) return crateBoards;
        if (material != steel) return material;
        return label.Contains("shutter") || label.Contains("bulkhead") || label.Contains("divider") ||
            label.Contains("wall") || label.Contains("corridor") ? corrugatedSteel : brushedSteel;
    }

    private void ApplyWarehouseFinish(Transform prop)
    {
        if (!prop.TryGetComponent(out MeshRenderer renderer)) return;
        Material current = renderer.sharedMaterial;
        // Retain the authored mapping on imported props and signs.
        if (current && (current.mainTexture || renderer.sharedMaterials.Length != 1)) return;

        string label = prop.name;
        if (label is "Storage Crate" or "Loading Crate" or "Worn_Cargo_Crate" or "Pallet")
            renderer.sharedMaterial = crateBoards;
        else if (label is "Concrete Floor" or "Warehouse2_Floor")
            renderer.sharedMaterial = texturedConcrete;
        else if (label is "Storage Shelf" or "Rack Upright" or "Roof Beam" or "Old_Wall_Support" or "Control Room Console")
            renderer.sharedMaterial = brushedSteel;
        else if (label.Contains("Wall") || label == "Control Room Back" || label == "Control Room Side" || label == "Control Room Left")
            renderer.sharedMaterial = corrugatedSteel;
    }
}

