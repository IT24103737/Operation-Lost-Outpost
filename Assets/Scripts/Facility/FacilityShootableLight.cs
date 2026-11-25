using UnityEngine;

public sealed class FacilityShootableLight : MonoBehaviour
{
    private Light source;
    private Renderer lens;
    private Material lit, unlit;
    internal bool Broken { get; private set; }
    internal Light Source => source;

    internal void Setup(Light fixture, Material glass, Material brokenGlass)
    {
        source = fixture;
        lens = GetComponent<Renderer>();
        lit = glass;
        unlit = brokenGlass;
        SetPowered(source.enabled);
    }

    internal void SetPowered(bool powered)
    {
        source.enabled = powered && !Broken;
        lens.sharedMaterial = source.enabled ? lit : unlit;
    }

    internal void Hit(FacilityEffects effects, Vector3 point, Vector3 normal)
    {
        if (Broken) return;
        Broken = true;
        SetPowered(false);
        effects.BreakLight(point, normal);
    }
}

