using UnityEngine;

public sealed class FacilityCrateImpact : MonoBehaviour
{
    private FacilityEncounter encounter;

    internal void Setup(FacilityEncounter owner) => encounter = owner;

    private void OnCollisionEnter(Collision collision)
    {
        if (encounter && collision.relativeVelocity.magnitude > 3 && collision.collider.GetComponentInParent<PlayerMovement>())
            encounter.Hurt(21);
    }
}

