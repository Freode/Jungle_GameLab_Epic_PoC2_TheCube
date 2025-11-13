using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(SphereCollider))]
public class EnemyAI_Independent : MonoBehaviour
{
    // AI Behavior
    public float attackRange = 2f;
    public float detectionRadius = 15f;
    public int attackDamage = 1;
    public float attackCooldown = 1.5f;

    // Private state
    private NavMeshAgent agent;
    private Transform target;
    private float lastAttackTime;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        
        // Configure the detection sphere
        SphereCollider detectionCollider = GetComponent<SphereCollider>();
        detectionCollider.isTrigger = true;
        detectionCollider.radius = detectionRadius;
    }

    void Start()
    {
        lastAttackTime = -attackCooldown;
    }

    // Using OnTriggerStay to continuously check for targets in range
    void OnTriggerStay(Collider other)
    {
        // If we don't have a target and the object is a player unit, assign it.
        if (target == null && other.CompareTag("Unit"))
        {
            target = other.transform;
        }
        // If we already have a target, we could add logic here to switch to a closer one if needed,
        // but for now, we'll just stick to the first one we see.
    }

    void OnTriggerExit(Collider other)
    {
        // If the current target leaves our detection radius, lose the target.
        if (target != null && other.transform == target)
        {
            target = null;
        }
    }

    void Update()
    {
        // If target is destroyed, it will become null.
        if (target == null)
        {
            // Stop moving if there's no target
            if (agent.hasPath)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);

        // If target is outside attack range, chase it.
        if (distance > attackRange)
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
        }
        // If target is in attack range, stop and attack.
        else
        {
            agent.isStopped = true;
            
            // Face the target
            Vector3 direction = (target.position - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);

            // Check if we can attack
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                Attack();
            }
        }
    }

    void Attack()
    {
        lastAttackTime = Time.time;
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);
        }
    }
}
