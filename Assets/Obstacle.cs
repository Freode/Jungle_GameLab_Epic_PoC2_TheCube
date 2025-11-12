using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Obstacle : MonoBehaviour
{
    public int mSides = 3; // Number of sides for the obstacle (3 to 8)
    public TextMeshPro textSides;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Obstacle requires a Rigidbody component.", this);
        }
        textSides.text = mSides.ToString();
    }

    public void ApplyKnockback(Vector3 hitDirection, float force)
    {
        if (rb != null)
        {
            // Apply force in the opposite direction of the hit
            rb.AddForce(hitDirection.normalized * force * 0.1f, ForceMode.Impulse);
        }
    }
}
