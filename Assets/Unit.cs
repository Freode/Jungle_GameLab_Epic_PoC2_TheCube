using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Unit : MonoBehaviour
{
    public bool isSelected;
    public FormationGroup formationGroup;
    private NavMeshAgent agent;
    private Renderer rend;
    private float originalSpeed;
    private float originalAcceleration;
    private const float CUBE_SIZE = 1.0f;
    public float fallSpeed = 5f;

    public bool IsGrounded { get; private set; }
    public bool AreAllCornersGrounded { get; private set; }
    public FormationMode currentMode;

    public List<GameObject> corners = new List<GameObject>();

    public void Fall()
    {
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;
        if (agent.enabled)
        {
            EnableNavMeshAgent(false);
        }
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rend = GetComponent<Renderer>();
        originalSpeed = agent.speed;
        originalAcceleration = agent.acceleration;
    }

    public void ToggleSelection(bool selected)
    {
        isSelected = selected;
        rend.material.color = selected ? Color.green : Color.white;
    }

    public Coroutine MoveTo(Vector3 position, float speedMultiplier = 1f)
    {
        if (agent.enabled) // Only use NavMeshAgent if it's enabled
        {
            agent.speed = originalSpeed * speedMultiplier;
            agent.acceleration = originalAcceleration * speedMultiplier;
            agent.SetDestination(position);

            if (speedMultiplier > 1f)
            {
                return StartCoroutine(ResetSpeedAfterArrival());
            }
            return null;
        }
        else // Smoothly move to position if NavMeshAgent is disabled
        {
            StopAllCoroutines(); // Stop any previous movement coroutine
            return StartCoroutine(SmoothMoveCoroutine(position, speedMultiplier));
        }
    }

    public void StopMovement()
    {
        if (agent.enabled)
        {
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            agent.ResetPath();
        }
        StopAllCoroutines();
    }

    private IEnumerator SmoothMoveCoroutine(Vector3 targetPosition, float speedMultiplier)
    {
        float currentSpeed = originalSpeed * speedMultiplier;
        while (Vector3.Distance(transform.position, targetPosition) > 0.05f) // Small threshold
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, currentSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPosition;
    }

    private IEnumerator ResetSpeedAfterArrival()
    {
        // Wait until the agent is close to the destination, but exit if the agent gets disabled.
        while (agent.enabled && (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f))
        {
            yield return null;
        }

        // Only reset speed if the agent is still enabled and has reached its destination.
        if (agent.enabled)
        {
            agent.speed = originalSpeed;
            agent.acceleration = originalAcceleration;
        }
    }

    public Coroutine RotateTo(Vector3 direction)
    {
        if (direction == Vector3.zero) return null;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        return StartCoroutine(RotateCoroutine(targetRotation));
    }

    private IEnumerator RotateCoroutine(Quaternion targetRotation)
    {
        float time = 0;
        float duration = 0.5f; // Half a second to rotate
        Quaternion startRotation = transform.rotation;

        while (time < duration)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRotation;
    }

    public void LockRotation()
    {
        agent.updateRotation = false;
    }

    public void UnlockRotation()
    {
        agent.updateRotation = true;
    }

    public void EnableNavMeshAgent(bool enable)
    {
        agent.enabled = enable;
    }

    public void SetNavMeshAgentControl(bool enable)
    {
        agent.updatePosition = enable;
        agent.updateRotation = enable;
    }

    public IEnumerator FallAndScatter(Vector3 targetPosition, float speedMultiplier, Vector3 scatterDirection)
    {
        EnableNavMeshAgent(false); // Ensure NavMeshAgent is disabled for direct transform manipulation
        SetNavMeshAgentControl(false);

        float currentSpeed = originalSpeed * speedMultiplier;
        
        // Create a ground target position directly below the unit
        Vector3 groundTarget = new Vector3(transform.position.x, 0.5f, transform.position.z); // Assuming cube pivot is center, 0.5f is ground level

        // Fall to the ground first
        while (Vector3.Distance(transform.position, groundTarget) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, groundTarget, currentSpeed * 2 * Time.deltaTime); // Fall faster
            yield return null;
        }
        transform.position = groundTarget;

        // Then move to the final scatter position on the ground
        while (Vector3.Distance(transform.position, targetPosition) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, currentSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPosition;

        // NEW: Move 1 unit further in the scatter direction after reaching the target position
        Vector3 finalPushTarget = targetPosition + scatterDirection.normalized * CUBE_SIZE;
        while (Vector3.Distance(transform.position, finalPushTarget) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, finalPushTarget, currentSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = finalPushTarget;

        // Once scattered, re-enable NavMeshAgent
        EnableNavMeshAgent(true);
        SetNavMeshAgentControl(true);
        UnlockRotation();
        ClearLeader();
        IsLeader = false;
    }

    public IEnumerator MoveForwardAndFall(Vector3 targetAirPosition, float moveSpeed, float fallSpeed)
    {
        // Ensure agent is off
        if (agent.enabled) EnableNavMeshAgent(false);

        // 1. Move forward in the air
        while (Vector3.Distance(transform.position, targetAirPosition) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetAirPosition, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetAirPosition;

        // 2. Fall until ground is detected (self-contained logic)
        while (true)
        {
            // Raycast down from the cube's center to find the ground
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 0.6f)) // Raycast distance slightly more than half the cube's height
            {
                if (hit.collider.CompareTag("Ground") || hit.collider.CompareTag("Unit"))
                {
                    break; // Ground detected, stop falling.
                }
            }

            // If no ground, continue falling
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;
            yield return null;
        }

        // 3. Enable agent on the ground
        EnableNavMeshAgent(true);
        UnlockRotation();
    }

    public bool IsLeader { get; set; } = false;
    private Unit _leader;
    private Vector3 _offsetFromLeader; // Offset from leader's position

    public void SetLeader(Unit leader, Vector3 offset)
    {
        _leader = leader;
        _offsetFromLeader = offset;
        IsLeader = false; // Ensure this unit is not marked as leader
    }

    public void ClearLeader()
    {
        _leader = null;
        _offsetFromLeader = Vector3.zero;
    }

    void Update()
    {
        if (!agent.enabled && _leader != null) // If NavMeshAgent is disabled and we have a leader
        {
            // Rotate the offset by the leader's current rotation
            Vector3 rotatedOffset = _leader.transform.rotation * _offsetFromLeader;
            // Move towards the target position relative to the leader
            Vector3 targetPosition = _leader.transform.position + rotatedOffset;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, originalSpeed * 2f * Time.deltaTime); // Move faster to keep up
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _leader.transform.rotation, agent.angularSpeed * Time.deltaTime); // Match leader's rotation
        }
        
        CheckGroundedStatus();

        if (currentMode == FormationMode.Individual)
        {
            if (!IsGrounded)
            {
                Fall();
            }
        }
    }

    private void CheckGroundedStatus()
    {
        if (corners == null || corners.Count == 0)
        {
            IsGrounded = false; 
            AreAllCornersGrounded = false;
            Debug.LogWarning($"[Debug] Unit '{gameObject.name}' has no corners assigned. It is considered ungrounded.", this);
            return;
        }

        Vector3 rayDirection = Vector3.down;
        float rayDistance = 0.1f;

        int groundedCorners = 0;

        foreach (GameObject cornerObject in corners)
        {
            if (cornerObject == null) continue;

            Vector3 rayOrigin = cornerObject.transform.position;
            if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, rayDistance))
            {
                if (hit.collider.CompareTag("Ground") || hit.collider.CompareTag("Unit"))
                {
                    groundedCorners++;
                }
            }
        }

        IsGrounded = groundedCorners > 0;
        AreAllCornersGrounded = groundedCorners == corners.Count;
        
        // This part handles re-enabling the agent if it was falling but is now grounded again.
        // It should only run in individual mode to avoid conflicts with formation logic.
        if (IsGrounded && currentMode == FormationMode.Individual)
        {
            if (!agent.enabled)
            {
                EnableNavMeshAgent(true);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (corners == null || corners.Count == 0) return;

        float rayDistance = 0.01f; // 1 cm
        Gizmos.color = Color.red;

        foreach (GameObject cornerObject in corners)
        {
            if (cornerObject == null) continue; // Skip if GameObject is null

            Vector3 rayOrigin = cornerObject.transform.position;
            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * rayDistance);
        }
    }
}