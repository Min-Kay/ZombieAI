using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieController : MonoBehaviour
{

    private Transform target;
    private Vector3 targetPos;
    private Vector3 randomPos;
    
    private Animator animator;
    private NavMeshAgent agent;
    public Transform rayPos;

    private WaitForSeconds ws;

    private float fieldOfViewAngle = 90.0f;
    [SerializeField]
    private float viewDistance = 20.0f;

    public enum State { IDLE,  PATROL, CHASING, ATTACK, DIE};
    public enum Mode { Basic, Spread}

    public State state = State.IDLE;
    public Mode mode = Mode.Basic;

    private bool isDie = false;

    public float attackDist = 5.0f;
    public float patrolRadius = 15.0f;
    public float callZombies = 10.0f;
    public float patrolSpeed = 1.5f;
    public float chaseSpeed = 5.0f;
    public float damping = 1.0f;

    private float attackTime;
    private float idleTime;
    private float dieTime;

    public float speed 
    {
        get { return agent.velocity.magnitude; }
    }

     void Awake()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        ws = new WaitForSeconds(0.3f);
        UpdateAnimClipTimes();

        agent.updateRotation = false;
        agent.autoBraking = false;
    }

    void OnEnable()
    {
        StartCoroutine(Action());
    }

    void FixedUpdate()
    {
        animator.SetFloat("Speed", speed);

        if(state != State.ATTACK)
        {
            View();
        }

        if(agent.isStopped == false)
        {
            Quaternion rot = Quaternion.LookRotation(agent.desiredVelocity);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * damping);
        }
    }

    private void NearZombieAttack(Vector3 pos)
    {
        GameObject[] zombies = GameObject.FindGameObjectsWithTag("Zombie");

        foreach(var i in zombies)
        {
            if(Vector3.Distance(i.transform.position,transform.position) <= callZombies)
                i.SendMessage("Detect", SendMessageOptions.DontRequireReceiver);
        }     
    }

    private void Detect()
    {
        state = State.CHASING;
    }

    private Vector3 BoundaryAngle(float angle) //시야각 계산
    {
        angle += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
    }

    private void View()
    {
        Vector3 left = BoundaryAngle(-fieldOfViewAngle * 0.5f); //부채꼴 왼쪽
        Vector3 right = BoundaryAngle(fieldOfViewAngle * 0.5f); //부채꼴 오른쪽

        Debug.DrawRay(rayPos.position, left * viewDistance, Color.red);
        Debug.DrawRay(rayPos.position, right * viewDistance, Color.red);

        targetPos.x = target.position.x;
        targetPos.y = transform.position.y;
        targetPos.z = target.position.z;

        randomPos.x = target.position.x + (Random.insideUnitCircle.normalized.x * attackDist);
        randomPos.y = transform.position.y;
        randomPos.z = target.position.z + (Random.insideUnitCircle.normalized.y * attackDist);

        Vector3 direction = (targetPos - transform.position).normalized;
        Vector3 directPos = (randomPos - transform.position).normalized;

        float angle = Vector3.Angle(direction, transform.forward);

        if (angle < fieldOfViewAngle * 0.5f)
        {
            RaycastHit hit;
            if(Physics.Raycast(rayPos.position, direction, out hit, viewDistance))
            {
                if(hit.transform.gameObject.tag == "Player")
                {
                    direction = (hit.transform.position - transform.position).normalized;

                    Debug.DrawRay(rayPos.position, direction * viewDistance, Color.blue);
                    Debug.DrawRay(rayPos.position, directPos * viewDistance, Color.green);

                    if(state != State.CHASING && state != State.ATTACK)
                    {
                        state = State.CHASING;
                    }
                }
            }
        }
    }

    private bool RandomPoint(Vector3 center, float range, out Vector3 result)
    {
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomPoint = center + Random.insideUnitSphere * range;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, 1.0f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = Vector3.zero;
        return false;
    }

    private Vector3 GetRandomPoint(Transform point = null, float radius = 0)
    {
        Vector3 _point;

        if (RandomPoint(transform.position, radius, out _point))
        {
            Debug.DrawRay(_point, Vector3.up, Color.black, 1);
            return _point;
        }
        return Vector3.zero;
    }

    IEnumerator Action()
    {
        while (!isDie)
        {
            yield return ws;

            switch (state)
            {
                case State.DIE:
                    agent.isStopped = true;
                    animator.SetBool("Move", false);
                    animator.SetBool("Die", true);
                    isDie = true;
                    break;
                case State.IDLE:
                    agent.isStopped = true;
                    animator.SetBool("Move", false);
                    RandomState();
                    break;
                case State.PATROL:
                    agent.isStopped = false;
                    damping = 1.0f;
                    if (!agent.hasPath)
                    {
                        agent.speed = patrolSpeed;
                        animator.SetBool("Move", true);
                        agent.SetDestination(GetRandomPoint(transform,patrolRadius)); 
                    }
                    else if(Vector3.Distance(agent.pathEndPosition,transform.position) < 0.1f)
                    {
                        agent.speed = patrolSpeed;
                        animator.SetBool("Move", true);
                        agent.SetDestination(GetRandomPoint(transform, patrolRadius));
                        RandomState();
                    }
                    yield return ws;
                    break;
                case State.CHASING:
                    agent.isStopped = false;
                    damping = 7.0f;
                    NearZombieAttack(transform.position);
                    animator.SetBool("Move", true);
                    agent.speed = chaseSpeed;
                    if (Vector3.Distance(targetPos, transform.position) <= attackDist)
                    {
                        state = State.ATTACK;
                        continue;
                    }
                    else
                        agent.SetDestination(randomPos); 
                    break;
                case State.ATTACK:
                    agent.isStopped = true;
                    NearZombieAttack(transform.position);
                    animator.SetBool("Move", false);
                    animator.SetTrigger("Attack");
                    if (Vector3.Distance(targetPos, transform.position) > attackDist)
                    {
                        state = State.CHASING;
                        continue;
                    }
                    break;
                default:
                    break;
            }
        }
    }

    private void RandomState()
    {
        var dice = Random.Range(0, 3);

        switch (dice)
        {
            case 0:
                //agent.SetDestination(transform.position);
                state = State.IDLE;
                break;
            default:
                animator.SetBool("Move", true);
                state = State.PATROL;
                break;
        }
    }

    private void UpdateAnimClipTimes()
    {
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        foreach (AnimationClip clip in clips)
        {
            switch (clip.name)
            {
                case "Z_Attack":
                    attackTime = clip.length;
                    break;
                case "Z_Idle":
                    idleTime = clip.length;
                    break;
                case "Z_FallingBack":
                    dieTime = clip.length;
                    break;
            }
        }
    }

#if UNITY_EDITOR

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }
#endif

}
