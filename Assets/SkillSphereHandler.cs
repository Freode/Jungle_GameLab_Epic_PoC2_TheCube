using UnityEngine;

public class SkillSphereHandler : MonoBehaviour
{
    public int playerN; // The 'n' value (unitCount) from the player's formation
    public float knockbackForce = 100f; // Force applied to the obstacle

    // PlayerController reference will be set externally
    public PlayerController playerController;

    void OnTriggerEnter(Collider other)
    {
        Obstacle obstacle = other.GetComponent<Obstacle>();
        if (obstacle != null)
        {
            // Check the condition: player's n value >= obstacle's m value
            if (playerN >= obstacle.mSides)
            {
                // Calculate hit direction from the skill sphere's center to the obstacle
                // The skill sphere's center is this GameObject's transform.position
                Vector3 hitDirection = (obstacle.transform.position - transform.position).normalized;
                
                // Apply knockback to the obstacle
                obstacle.ApplyKnockback(hitDirection, knockbackForce);
            }
        }
    }
}
