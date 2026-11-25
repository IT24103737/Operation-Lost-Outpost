using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PushableCrate : MonoBehaviour
{
    [Header("Goliath Impact Settings")]
    public bool destroyOnCharge = false;
    public float launchForce = 18f;
    public GameObject shatterEffectPrefab;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void OnGoliathChargeHit(Vector3 chargeDirection)
    {
        if (destroyOnCharge)
        {
            if (shatterEffectPrefab != null)
            {
                Instantiate(shatterEffectPrefab, transform.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }
        else
        {
            Vector3 pushDir = (chargeDirection + Vector3.up * 0.25f).normalized;
            rb.AddForce(pushDir * launchForce, ForceMode.Impulse);
        }
    }
}