
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Warehouse2EnvironmentGenerator : MonoBehaviour
{
    [Header("Warehouse Floor")]
    public Transform warehouseFloor;

    [Header("Prefab Collections")]
    public GameObject[] cratePrefabs;
    public GameObject[] barrelPrefabs;
    public GameObject[] palletPrefabs;
    public GameObject[] equipmentPrefabs;

    [Header("Generation")]
    [Range(10, 100)]
    public int totalObjects = 40;

    public int randomSeed = 25;

    [Header("Layout")]
    public float edgePadding = 2f;
    public float entranceClearDepth = 8f;
    public float centralPathWidth = 6f;
    public float minimumSpacing = 2f;

    [Header("Variation")]
    public Vector2 randomScale = new Vector2(0.9f, 1.1f);
    public bool randomRotation = true;

    private const string GeneratedName =
        "Generated_Warehouse2_Environment";

    [ContextMenu("GENERATE WAREHOUSE 2")]
    public void GenerateWarehouse()
    {
        if (warehouseFloor == null)
        {
            Debug.LogError("Assign Warehouse2_Floor first!");
            return;
        }

        Renderer floorRenderer =
            warehouseFloor.GetComponent<Renderer>();

        if (floorRenderer == null)
        {
            Debug.LogError("Floor needs a Renderer!");
            return;
        }

        if (!HasAnyPrefabs())
        {
            Debug.LogError("Assign prefab assets first!");
            return;
        }

        ClearGenerated();
        Random.InitState(randomSeed);

        Bounds floor = floorRenderer.bounds;

        GameObject container =
            new GameObject(GeneratedName);

        container.transform.SetParent(transform, false);

        List<Vector3> occupied = new List<Vector3>();

        for (int i = 0; i < totalObjects; i++)
        {
            GameObject prefab = ChoosePrefab();

            if (prefab == null) continue;

            for (int attempt = 0; attempt < 50; attempt++)
            {
                float x = Random.Range(
                    floor.min.x + edgePadding,
                    floor.max.x - edgePadding);

                float z = Random.Range(
                    floor.min.z + entranceClearDepth,
                    floor.max.z - edgePadding);

                if (Mathf.Abs(x) < centralPathWidth / 2f)
                    continue;

                Vector3 position =
                    new Vector3(x, floor.max.y, z);

                bool blocked = false;

                foreach (Vector3 previous in occupied)
                {
                    if (Vector3.Distance(
                        position, previous) < minimumSpacing)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (blocked) continue;

                Quaternion rotation = Quaternion.Euler(
                    0,
                    randomRotation
                        ? Random.Range(0f, 360f) : 0f,
                    0);

                GameObject item = CreatePrefab(
                    prefab, position, rotation,
                    container.transform);

                if (item == null) continue;

                item.transform.localScale *= Random.Range(
                    randomScale.x, randomScale.y);

                Renderer[] renderers =
                    item.GetComponentsInChildren<Renderer>();

                if (renderers.Length > 0)
                {
                    Bounds itemBounds = renderers[0].bounds;

                    foreach (Renderer r in renderers)
                        itemBounds.Encapsulate(r.bounds);

                    float correction =
                        floor.max.y - itemBounds.min.y;

                    item.transform.position +=
                        Vector3.up * (correction + 0.02f);
                }

                occupied.Add(position);
                break;
            }
        }

        Debug.Log("Warehouse 2 generation complete!");
    }

    private bool HasAnyPrefabs()
    {
        return HasItems(cratePrefabs) ||
               HasItems(barrelPrefabs) ||
               HasItems(palletPrefabs) ||
               HasItems(equipmentPrefabs);
    }

    private bool HasItems(GameObject[] array)
    {
        if (array == null) return false;

        foreach (GameObject item in array)
            if (item != null) return true;

        return false;
    }

    private GameObject ChoosePrefab()
    {
        List<GameObject[]> collections =
            new List<GameObject[]>();

        if (HasItems(cratePrefabs))
            collections.Add(cratePrefabs);

        if (HasItems(barrelPrefabs))
            collections.Add(barrelPrefabs);

        if (HasItems(palletPrefabs))
            collections.Add(palletPrefabs);

        if (HasItems(equipmentPrefabs))
            collections.Add(equipmentPrefabs);

        GameObject[] selected = collections[
            Random.Range(0, collections.Count)];

        List<GameObject> available =
            new List<GameObject>();

        foreach (GameObject item in selected)
            if (item != null) available.Add(item);

        return available[
            Random.Range(0, available.Count)];
    }

    private GameObject CreatePrefab(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent)
    {
        GameObject item = null;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            item = (GameObject)PrefabUtility.InstantiatePrefab(
                prefab, parent);

            if (item != null)
            {
                item.transform.SetPositionAndRotation(
                    position, rotation);

                Undo.RegisterCreatedObjectUndo(
                    item, "Generate Warehouse Props");
            }
        }
        else
#endif
        {
            item = Instantiate(
                prefab, position, rotation, parent);
        }

        return item;
    }

    [ContextMenu("GENERATE WAREHOUSE 2")]
    private void GenerateFromMenu()
    {
        GenerateWarehouse();
    }

    [ContextMenu("CLEAR GENERATED WAREHOUSE")]
    public void ClearGenerated()
    {
        Transform old = transform.Find(GeneratedName);

        if (old != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.DestroyObjectImmediate(old.gameObject);
            else
#endif
                Destroy(old.gameObject);
        }
    }
}
