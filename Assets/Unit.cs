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

    public void MoveTo(Vector3 position, float speedMultiplier = 1f)
    {
        if (agent.enabled) // Only use NavMeshAgent if it's enabled
        {
            agent.speed = originalSpeed * speedMultiplier;
            agent.acceleration = originalAcceleration * speedMultiplier;
            agent.SetDestination(position);

            if (speedMultiplier > 1f)
            {
                StartCoroutine(ResetSpeedAfterArrival());
            }
        }
        else // Smoothly move to position if NavMeshAgent is disabled
        {
            StopAllCoroutines(); // Stop any previous movement coroutine
            StartCoroutine(SmoothMoveCoroutine(position, speedMultiplier));
        }
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
        // Wait until the agent is close to the destination
        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f)
        {
            yield return null;
        }

        // Reset speed and acceleration
        agent.speed = originalSpeed;
        agent.acceleration = originalAcceleration;
    }

    public void RotateTo(Vector3 direction)
    {
        if (direction == Vector3.zero) return;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        StartCoroutine(RotateCoroutine(targetRotation));
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
            // Move towards the target position relative to the leader
            Vector3 targetPosition = _leader.transform.position + _offsetFromLeader;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, originalSpeed * Time.deltaTime);
        }
    }
}
