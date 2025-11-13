using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int health = 5;

    public void TakeDamage(int damage)
    {
        health -= damage;
        Debug.Log(gameObject.name + " took " + damage + " damage. Remaining health: " + health);

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log(gameObject.name + " has been destroyed.");
        Destroy(gameObject);
    }
}
