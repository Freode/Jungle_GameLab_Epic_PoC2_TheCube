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
    private float originalStoppingDistance; // Added to store the original stopping distance
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
        originalStoppingDistance = agent.stoppingDistance; // Store original stopping distance
    }

    public void ToggleSelection(bool selected)
    {
        isSelected = selected;
        rend.material.color = selected ? Color.green : Color.white;
    }

    public IEnumerator MoveTo(Vector3 position, float speedMultiplier = 1f)
    {
        if (agent.enabled) // Only use NavMeshAgent if it's enabled
        {
            StopAllCoroutines(); // Stop any previous movement coroutines, including Reset...AfterArrival
            agent.stoppingDistance = originalStoppingDistance; // Ensure stopping distance is reset immediately
            agent.isStopped = false; // Explicitly ensure the agent is not stopped
            
            agent.speed = originalSpeed * speedMultiplier;
            agent.acceleration = originalAcceleration * speedMultiplier;
            
            agent.ResetPath(); // Explicitly clear any previous path
            agent.SetDestination(position);

            if (speedMultiplier > 1f)
            {
                yield return StartCoroutine(ResetSpeedAndStoppingDistanceAfterArrival()); // New coroutine
            }
            else
            {
                // If not using speed multiplier, still need to reset stopping distance
                yield return StartCoroutine(ResetStoppingDistanceAfterArrival()); // New coroutine
            }
        }
        else // Smoothly move to position if NavMeshAgent is disabled
        {
            StopAllCoroutines(); // Stop any previous movement coroutine
            yield return StartCoroutine(SmoothMoveCoroutine(position, speedMultiplier));
        }
    }

    public void StopMovement()
    {
        if (agent.enabled)
        {
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            agent.ResetPath();
            agent.stoppingDistance = originalStoppingDistance; // Ensure stopping distance is reset on stop
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

    // Modified coroutine to also reset stoppingDistance
    private IEnumerator ResetSpeedAndStoppingDistanceAfterArrival()
    {
        // Wait until the agent is close to the destination, but exit if the agent gets disabled.
        while (agent.enabled && (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f))
        {
            yield return null;
        }

        // Only reset speed and stopping distance if the agent is still enabled and has reached its destination.
        if (agent.enabled)
        {
            agent.speed = originalSpeed;
            agent.acceleration = originalAcceleration;
            agent.stoppingDistance = originalStoppingDistance; // Restore original
        }
    }

    // New coroutine for cases without speed multiplier, just resets stoppingDistance
    private IEnumerator ResetStoppingDistanceAfterArrival()
    {
        // Wait until the agent is close to the destination, but exit if the agent gets disabled.
        while (agent.enabled && (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f))
        {
            yield return null;
        }

        // Only reset stopping distance if the agent is still enabled and has reached its destination.
        if (agent.enabled)
        {
            agent.stoppingDistance = originalStoppingDistance; // Restore original
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

    private Vector3 _lastKnownVelocity;

    public void EnableNavMeshAgent(bool enable)
    {
        if (!enable && agent.enabled)
        {
            _lastKnownVelocity = agent.velocity;
        }
        
        agent.enabled = enable;

        if (enable)
        {
            agent.Warp(transform.position); // Force agent to snap to current position on NavMesh
            agent.velocity = _lastKnownVelocity; // Restore velocity for smoother transition
        }
    }

    public void SetNavMeshAgentControl(bool enable)
    {
        agent.updatePosition = enable;
        agent.updateRotation = enable;
    }

    public IEnumerator FallAndScatter(Vector3 targetPosition, float speedMultiplier, Vector3 scatterDirection)
    {
        EnableNavMeshAgent(false);
        SetNavMeshAgentControl(false);

        Vector3 startPosition = transform.position;
        float horizontalDistance = Vector3.Distance(new Vector3(startPosition.x, 0, startPosition.z), new Vector3(targetPosition.x, 0, targetPosition.z));
        
        // Adjust duration based on speed, but also consider vertical distance for a more natural fall time
        float verticalDistance = Mathf.Abs(startPosition.y - targetPosition.y);
        float duration = (horizontalDistance + verticalDistance) / (originalSpeed * speedMultiplier);

        if (duration < 0.2f) duration = 0.2f; // Ensure a minimum duration for the animation to be visible

        float time = 0;
        float startY = startPosition.y;
        float targetY = targetPosition.y;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            // Interpolate XZ position linearly
            Vector3 newPos = Vector3.Lerp(new Vector3(startPosition.x, 0, startPosition.z), new Vector3(targetPosition.x, 0, targetPosition.z), t);
            
            // Interpolate Y position with an ease-in curve to simulate acceleration
            float y_t = t * t; // Ease-in curve (t-squared)
            newPos.y = Mathf.Lerp(startY, targetY, y_t);

            transform.position = newPos;
            yield return null;
        }

        transform.position = targetPosition;
        yield return null; 

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
    [SerializeField] private Unit _leader;
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
        
        if (currentMode == FormationMode.Individual)
        {
            CheckGroundedStatus();
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
        if (IsGrounded)
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