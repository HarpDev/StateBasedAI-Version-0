using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public enum AIState
{
    WANDER,
    CHASE,
    SEARCHING
}

public class StateAI : MonoBehaviour
{
    [Header("Player")]
    public Transform Player;
    public Transform SmartAIBody;
    public Camera PlayerCam;

    private NavMeshAgent agent;
    private AIState currentState;
    private Coroutine chaseCoroutine;
    private Vector3 lastSeenPlayerPosition;

    [Header("AI Settings")]
    public float wanderRadius = 10f;
    public float chaseSpeed = 6f;
    public float wanderSpeed = 3.5f;
    public float searchSpeed = 4f;
    public float searchRadius = 5f;
    public float minSearchTime = 5f;
    public float maxSearchTime = 30f;
    public float chaseLostSightTime = 3f;

    [Header("AI Vision")]
    public float viewDistance = 10f;
    public float fieldOfViewAngle = 120f;

    [Header("AI Light")]
    public Light stateLight;

    private bool _isBlocked = false;
    private bool _isViewed = false;
    private bool _isBeingShinedByLight = false;
    private float aiDistance;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        currentState = AIState.WANDER;
        UpdateStateLight();
    }

    void Update()
    {
        switch (currentState)
        {
            case AIState.WANDER:
                Wander();
                break;
            case AIState.CHASE:
                Chase();
                break;
            case AIState.SEARCHING:
                Search();
                break;
        }

        IsVisibleToPlayerCheck();
        StateTransitions();
        IsPlayerInSight();
    }

    private void Wander()
    {
        agent.speed = wanderSpeed;
        if (!agent.hasPath)
        {
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection += transform.position;
            NavMeshHit hit;
            NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, 1);
            agent.SetDestination(hit.position);
        }
    }

    private void Chase()
    {
        agent.speed = chaseSpeed;
        agent.SetDestination(Player.position);
    }

    private void Search()
    {
        agent.speed = searchSpeed;
        if (!agent.hasPath)
        {
            Vector3 randomDirection = Random.insideUnitSphere * searchRadius;
            randomDirection += lastSeenPlayerPosition;
            NavMeshHit hit;
            NavMesh.SamplePosition(randomDirection, out hit, searchRadius, 1);
            agent.SetDestination(hit.position);
        }
    }

    private void IsVisibleToPlayerCheck()
    {
        bool isPathBlocked(Vector3 start, Vector3 end)
        {
            RaycastHit hit;
            if (Physics.Linecast(start, end, out hit))
            {
                if (hit.collider.isTrigger == false)
                {
                    return true;
                }
            }
            return false;
        }

        if (isPathBlocked(SmartAIBody.position, Player.position))
        {
            _isBlocked = true;
            Debug.DrawLine(SmartAIBody.position, PlayerCam.transform.position, Color.cyan, Time.fixedDeltaTime);
        }
        else
        {
            _isBlocked = false;
            Debug.DrawLine(SmartAIBody.position, PlayerCam.transform.position, _isViewed ? Color.green : Color.red, Time.fixedDeltaTime);
        }

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(PlayerCam);
        aiDistance = Vector3.Distance(SmartAIBody.position, Player.position);

        if (!_isBlocked && GeometryUtility.TestPlanesAABB(planes, this.gameObject.GetComponent<Renderer>().bounds))
        {
            _isViewed = true;
        }
        else
        {
            _isViewed = false;
            _isBeingShinedByLight = false;
        }
    }

    private bool IsPlayerInSight()
    {
        Vector3 directionToPlayer = Player.position - SmartAIBody.position;
        float angleToPlayer = Vector3.Angle(directionToPlayer, SmartAIBody.forward);

        if (angleToPlayer < fieldOfViewAngle * 0.5f && directionToPlayer.magnitude < viewDistance)
        {
            RaycastHit hit;
            if (Physics.Linecast(SmartAIBody.position, Player.position, out hit))
            {
                if (hit.collider.gameObject == Player.gameObject)
                {
                    lastSeenPlayerPosition = Player.position;
                    return true;
                }
            }
        }
        return false;
    }

    private void StateTransitions()
    {
        if (IsPlayerInSight())
        {
            if (chaseCoroutine != null)
            {
                StopCoroutine(chaseCoroutine);
                chaseCoroutine = null;
            }
            currentState = AIState.CHASE;
        }
        else if (currentState == AIState.CHASE)
        {
            if (chaseCoroutine == null)
            {
                chaseCoroutine = StartCoroutine(LoseSight());
            }
        }
        else if (currentState == AIState.SEARCHING)
        {
            // Already in searching state, continue searching
        }
        else
        {
            currentState = AIState.WANDER;
        }
        UpdateStateLight();
    }

    private IEnumerator LoseSight()
    {
        yield return new WaitForSeconds(chaseLostSightTime);
        if (!IsPlayerInSight())
        {
            currentState = AIState.SEARCHING;
            StartCoroutine(SearchForPlayer());
        }
        chaseCoroutine = null;
    }

    private IEnumerator SearchForPlayer()
    {
        float searchTime = Random.Range(minSearchTime, maxSearchTime);
        float elapsedTime = 0f;

        while (elapsedTime < searchTime)
        {
            if (IsPlayerInSight())
            {
                currentState = AIState.CHASE;
                yield break;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        currentState = AIState.WANDER;
    }

    private void UpdateStateLight()
    {
        if (stateLight == null) return;

        switch (currentState)
        {
            case AIState.WANDER:
                stateLight.color = Color.green;
                break;
            case AIState.CHASE:
                stateLight.color = Color.red;
                break;
            case AIState.SEARCHING:
                stateLight.color = Color.yellow;
                break;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(SmartAIBody.position, viewDistance);

        Vector3 fovLine1 = Quaternion.AngleAxis(fieldOfViewAngle * 0.5f, SmartAIBody.up) * SmartAIBody.forward * viewDistance;
        Vector3 fovLine2 = Quaternion.AngleAxis(-fieldOfViewAngle * 0.5f, SmartAIBody.up) * SmartAIBody.forward * viewDistance;

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(SmartAIBody.position, SmartAIBody.position + fovLine1);
        Gizmos.DrawLine(SmartAIBody.position, SmartAIBody.position + fovLine2);

        if (IsPlayerInSight())
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(SmartAIBody.position, Player.position);
        }
    }
}
