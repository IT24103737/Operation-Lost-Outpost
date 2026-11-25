using UnityEngine;

[DisallowMultipleComponent]
public sealed class FacilityShootableProp : MonoBehaviour
{
    private enum Surface { Cardboard, Plastic, Wood, Steel }
    private Surface surface;
    private Rigidbody body;
    private int hitsRemaining;
    internal FacilityEncounter.StepSurface FootstepSurface => surface switch
    {
        Surface.Wood => FacilityEncounter.StepSurface.Wood,
        Surface.Steel => FacilityEncounter.StepSurface.Steel,
        Surface.Plastic => FacilityEncounter.StepSurface.Plastic,
        _ => FacilityEncounter.StepSurface.Cardboard
    };

    internal static void Register(Transform target)
    {
        string label = target.name.Replace("(Clone)", "").Trim();
        Surface surface;
        float mass;
        int durability;
        if (label == "ShippedParcel" || label == "AssembledCardboardBox")
            (surface, mass, durability) = (Surface.Cardboard, 2f, 3);
        else if (label == "StackingTote")
            (surface, mass, durability) = (Surface.Plastic, 4.5f, 0);
        else if (label is "EuroPallet" or "Pallet" or "Breakable transit crate" or "Storage Crate" or "Loading Crate" or "Worn_Cargo_Crate" or "Raised crate barricade" ||
            IsNumbered(label, "crate-"))
            (surface, mass, durability) = (Surface.Wood, label == "Breakable transit crate" ? 14f : 9f, 6);
        else if (label is "Scattered_Warehouse_Debris" or "Pallet splinter" or "Cardboard offcut")
            (surface, mass, durability) = (Surface.Wood, 1.2f, 2);
        else if (IsNumbered(label, "barrel-"))
            (surface, mass, durability) = (Surface.Steel, 19f, 0);
        else return;

        if (target.GetComponent<FacilityShootableProp>()) return;
        Collider collider = target.GetComponentInChildren<Collider>();
        if (!collider)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = new(target.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (Renderer part in renderers)
            {
                Bounds mesh = part.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new((i & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (i & 2) == 0 ? mesh.min.y : mesh.max.y, (i & 4) == 0 ? mesh.min.z : mesh.max.z);
                    bounds.Encapsulate(target.InverseTransformPoint(part.transform.TransformPoint(corner)));
                }
            }
            BoxCollider box = target.gameObject.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }
        var prop = target.gameObject.AddComponent<FacilityShootableProp>();
        prop.surface = surface;
        prop.hitsRemaining = durability;
        prop.body = target.GetComponent<Rigidbody>();
        if (!prop.body) prop.body = target.gameObject.AddComponent<Rigidbody>();
        prop.body.mass = mass;
        prop.body.isKinematic = true;
        prop.body.interpolation = RigidbodyInterpolation.Interpolate;
        prop.body.linearDamping = .24f;
        prop.body.angularDamping = .45f;
    }

    private static bool IsNumbered(string name, string prefix) =>
        name.StartsWith(prefix, System.StringComparison.Ordinal) && int.TryParse(name.Substring(prefix.Length), out _);

    internal void Hit(FacilityEffects effects, Vector3 point, Vector3 normal, Vector3 direction)
    {
        Color debris = surface switch
        {
            Surface.Steel => new Color(1, .73f, .28f),
            Surface.Plastic => new Color(.61f, .71f, .76f),
            _ => new Color(.69f, .46f, .23f)
        };
        effects.Impact(point, normal, debris);
        effects.PropImpact(point, surface == Surface.Steel);
        if (hitsRemaining > 0 && --hitsRemaining == 0)
        {
            Break(effects, point, direction);
            return;
        }

        float kick = surface switch { Surface.Cardboard => 2.25f, Surface.Plastic => 1.65f, Surface.Wood => 1.15f, _ => .85f };
        Push(point, (direction + Vector3.up * .17f) * kick);
    }

    internal void Push(Vector3 point, Vector3 kick)
    {
        Release();
        body.AddForceAtPosition(kick, point, ForceMode.VelocityChange);
    }

    internal void Shove(Vector3 direction)
    {
        Release();
        float speed = Mathf.Lerp(4.6f, 2.6f, Mathf.InverseLerp(2, 19, body.mass));
        Vector3 velocity = body.linearVelocity;
        float boost = Mathf.Max(0, speed - Vector3.Dot(direction, velocity));
        body.linearVelocity = velocity + direction * boost + Vector3.up * .48f;
        body.WakeUp();
    }

    private void Release()
    {
        if (body.isKinematic)
        {
            // Dynamic rigidbodies require convex mesh colliders.
            foreach (MeshCollider mesh in GetComponentsInChildren<MeshCollider>())
                mesh.convex = true;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    internal void Break(FacilityEffects effects, Vector3 point, Vector3 direction)
    {
        effects.Impact(point, -direction, new Color(.79f, .57f, .33f));
        effects.Impact(point + direction * .18f, Vector3.up, new Color(.55f, .31f, .17f));
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}

