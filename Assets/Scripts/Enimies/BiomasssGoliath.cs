using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BiomassGoliathAI : MonoBehaviour
{
    public enum State { Patrol, WindUp, Charge, Stunned }
    public State currentState = State.Patrol;

    [Header("References")]
    public Transform player;
    public Transform[] patrolWaypoints;

    [Header("Detection & Charge")]
    public float sightRange = 15f;
    public float patrolSpeed = 1.2f;
    public float windUpDuration = 1.2f;
    public float chargeSpeed = 4.5f;
    public float maxChargeDuration = 2.5f;
    public float stunDuration = 3.0f;

    [Header("Collision Settings")]
    public float bodyRadius = 0.45f;
    public LayerMask obstacleMask; // Include Wall, Crate, and Default
    public LayerMask wallMask;     // Include only Wall

    private NavMeshAgent agent;
    private Animator anim;
    private int currentWaypointIndex = 0;
    private Vector3 lockedChargeDirection;
    private float stateTimer;
    private bool willHitWall;
    private float defaultAcceleration;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        agent.speed = patrolSpeed;
        defaultAcceleration = agent.acceleration;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case State.Patrol:
                HandlePatrol();
                break;
            case State.WindUp:
                HandleWindUp();
                break;
            case State.Charge:
                HandleCharge();
                break;
            case State.Stunned:
                HandleStunned();
                break;
        }
    }

    private void HandlePatrol()
    {
        if (anim != null) anim.speed = 1f;

        if (patrolWaypoints != null && patrolWaypoints.Length > 0 && patrolWaypoints[currentWaypointIndex] != null)
        {
            agent.isStopped = false;
            agent.speed = patrolSpeed;
            agent.acceleration = defaultAcceleration;
            agent.SetDestination(patrolWaypoints[currentWaypointIndex].position);

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
            }
        }

        if (HasDirectLineOfSight())
        {
            StartWindUp();
        }
    }

    private bool HasDirectLineOfSight()
    {
        if (player == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Vector3 target = player.position + Vector3.up * 1.0f;
        Vector3 dir = target - origin;
        float distanceToPlayer = dir.magnitude;

        if (distanceToPlayer > sightRange) return false;

        if (Physics.Raycast(origin, dir.normalized, distanceToPlayer, wallMask))
        {
            return false;
        }
        return true;
    }

    private void StartWindUp()
    {
        currentState = State.WindUp;
        stateTimer = windUpDuration;
        agent.isStopped = true;
        agent.ResetPath();

        if (anim != null) anim.speed = 0.3f;
    }

    private void HandleWindUp()
    {
        if (player != null)
        {
            Vector3 lookDir = player.position - transform.position;
            lookDir.y = 0f;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f
                );
            }
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            lockedChargeDirection = transform.forward;
            lockedChargeDirection.y = 0f;
            lockedChargeDirection.Normalize();

            float maxDistance = chargeSpeed * maxChargeDuration;
            Vector3 chargeEnd = transform.position + lockedChargeDirection * maxDistance;

            if (NavMesh.Raycast(transform.position, chargeEnd, out NavMeshHit navHit, NavMesh.AllAreas))
            {
                chargeEnd = navHit.position;
                willHitWall = true;
            }
            else
            {
                willHitWall = false;
            }

            agent.isStopped = false;
            agent.speed = chargeSpeed;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.1f;
            agent.SetDestination(chargeEnd);

            currentState = State.Charge;
            stateTimer = maxChargeDuration;

            if (anim != null) anim.speed = 2.0f;
        }
    }

    private void HandleCharge()
    {
        float checkDistance = (chargeSpeed * Time.deltaTime) + 0.4f;
        Vector3 castOrigin = transform.position + Vector3.up * 0.8f;

        if (Physics.SphereCast(castOrigin, bodyRadius, lockedChargeDirection, out RaycastHit hit, checkDistance, obstacleMask))
        {
            PushableCrate crate = hit.collider.GetComponent<PushableCrate>();
            if (crate != null)
            {
                crate.OnGoliathChargeHit(lockedChargeDirection);
            }
            else if (((1 << hit.collider.gameObject.layer) & wallMask) != 0)
            {
                EnterStunnedState();
                return;
            }
        }

        stateTimer -= Time.deltaTime;
        float arrivalThreshold = Mathf.Max(0.4f, agent.radius + 0.15f);

        if (!agent.pathPending && agent.remainingDistance <= arrivalThreshold)
        {
            if (willHitWall)
                EnterStunnedState();
            else
                RecoverToPatrol();
            return;
        }

        if (stateTimer <= 0f)
        {
            RecoverToPatrol();
        }
    }

    private void EnterStunnedState()
    {
        currentState = State.Stunned;
        stateTimer = stunDuration;
        agent.isStopped = true;
        agent.ResetPath();
        if (anim != null) anim.speed = 0f;
    }

    private void HandleStunned()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            RecoverToPatrol();
        }
    }

    private void RecoverToPatrol()
    {
        agent.isStopped = false;
        agent.speed = patrolSpeed;
        agent.acceleration = defaultAcceleration;
        currentState = State.Patrol;
        if (anim != null) anim.speed = 1f;
    }
}