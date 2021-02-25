﻿using System.Collections;
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
    [Header("Ray Position")]
    public Transform rayPos;

    private WaitForSeconds ws;

    private float fieldOfViewAngle = 90.0f;
    [SerializeField]
    private float viewDistance = 20.0f;

    public enum State { IDLE,  PATROL, CHASING, ATTACK, DIE};
    public enum Mode { Basic, Spread}

    [Header("Zombie State & Move Logic")]
    public State state = State.IDLE;
    public Mode mode = Mode.Basic;

    private bool isDie = false;

    [Header("Zombie Info")]
    public float attackDist = 1.0f;
    public float spreadDist = 2.5f;
    public float patrolRadius = 15.0f;
    public float callZombies = 7.0f;
    public float patrolSpeed = 1.5f;
    public float chaseSpeed = 6.0f;
    public float damping = 2.0f;
    public float speed
    {
        get { return agent.velocity.magnitude; }
    }

    private float attackTime;
    private float idleTime;

    private Vector3 forward = Vector3.zero;

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
        agent.avoidancePriority = Random.Range(30, 50);
        agent.stoppingDistance = 0.3f;
    }

    void OnEnable()
    {
        StartCoroutine(Action());
        StartCoroutine(RandomState());
    }

    void FixedUpdate()
    {
        if(!isDie)
        {
            animator.SetFloat("Speed", speed);
            View();
            damping = ((state != State.ATTACK && state != State.CHASING) ? 3.0f : 7.0f);
    
            Quaternion rot = Quaternion.LookRotation(state != State.ATTACK ? agent.desiredVelocity : (targetPos - transform.position).normalized);
            transform.rotation = ((state != State.IDLE)?Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * damping):Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(forward), Time.deltaTime * damping));
   
            if (Vector3.Distance(targetPos, transform.position) <= attackDist && state == State.CHASING)
                state = State.ATTACK;
            else if (Vector3.Distance(targetPos, transform.position) > attackDist && state == State.ATTACK)
                state = State.CHASING;
        }
    }

    private void NearZombieAttack()
    {
        GameObject[] zombies = GameObject.FindGameObjectsWithTag("Zombie");

        foreach(var i in zombies)
        {
            if(Vector3.Distance(i.transform.position,transform.position) <= callZombies)
            {
                i.GetComponent<ZombieController>().SetTarget(target);
                i.SendMessage("Detect", SendMessageOptions.DontRequireReceiver);
            }
        }     
    }

    private void Detect()
    {
        if(state != State.ATTACK && state != State.CHASING)
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

        SetLocation();
        Vector3 direction = (targetPos - transform.position).normalized;
        
        float angle = Vector3.Angle(direction, transform.forward);

        if (angle < fieldOfViewAngle * 0.5f)
        {
            RaycastHit hit;
            if(Physics.Raycast(rayPos.position, direction, out hit, viewDistance))
            {
                if(hit.transform.gameObject.tag == "Player")
                {
                    target = hit.transform;
                    SetLocation();
                    direction = (targetPos - transform.position).normalized;
                    Debug.DrawRay(rayPos.position, direction * Vector3.Distance(targetPos,transform.position), Color.blue);
                    if(mode == Mode.Spread && randomPos != null)
                    {
                        Vector3 directPos = (randomPos - transform.position).normalized;
                        Debug.DrawRay(rayPos.position, directPos * Vector3.Distance(randomPos, transform.position), Color.green);
                    }
                    Detect();
                }
            }
        }
    }

    private bool RandomPoint(Vector3 center, float range,out Vector3 result, bool normalized = false)
    {
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomPoint = (normalized? center + Random.insideUnitSphere.normalized * range:center + Random.insideUnitSphere * range);
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

    private Vector3 SetRandomPoint(Transform point = null, float radius = 0, bool normalized = false)
    {
        Vector3 _point;

        if (RandomPoint(point.position, radius,out _point, normalized))
        {
            Debug.DrawRay(_point, Vector3.up, Color.black, 3);
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
                    agent.ResetPath();
                    animator.SetBool("Move", false);
                    animator.SetTrigger("Die");
                    isDie = true;
                    break;
                case State.IDLE:
                    agent.isStopped = true;
                    animator.SetBool("Move", false);
                    break;
                case State.PATROL:
                    agent.isStopped = false;
                    animator.SetBool("Move", true);
                    agent.speed = patrolSpeed;
                    if (!agent.hasPath || Vector3.Distance(agent.pathEndPosition, transform.position) <= agent.stoppingDistance)
                    {
                        agent.SetDestination(SetRandomPoint(transform,patrolRadius));
                    }
                    break;
                case State.CHASING:
                    agent.isStopped = false;
                    NearZombieAttack();
                    animator.SetBool("Move", true);
                    agent.speed = chaseSpeed;
                    if (mode == Mode.Spread)
                    {
                        if (randomPos == null || Vector3.Distance(targetPos, randomPos) > spreadDist)
                        {
                            CheckTargetPosition();
                        }
                    }
                    agent.SetDestination((mode == Mode.Basic || Vector3.Distance(targetPos,transform.position) <= spreadDist)?targetPos:randomPos);
                    break;
                case State.ATTACK:
                    agent.isStopped = true;
                    animator.SetBool("Move", false);
                    animator.SetTrigger("Attack");
                    NearZombieAttack();
                    yield return new WaitForSeconds(attackTime);
                    break;
                default:
                    break;
            }
        }
    }

    IEnumerator RandomState()
    {
        while (state == State.IDLE || state == State.PATROL)
        {
            yield return ws;

            var dice = Random.Range(0, 10);

            switch (dice)
            {
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                    state = State.IDLE;
                    forward = transform.forward;
                    agent.ResetPath();
                    yield return new WaitForSeconds(idleTime);
                    break;
                default:
                    state = State.PATROL;
                    yield return new WaitUntil(() => Vector3.Distance(transform.position, agent.destination) <= (agent.stoppingDistance + 0.1f));
                    break;
            }
        }
    }

    private void CheckTargetPosition()
    {
        randomPos = SetRandomPoint(target, spreadDist, true);
        RaycastHit hit;
        Vector3 direction = (targetPos - randomPos).normalized;
        if (Physics.Raycast(randomPos, direction, out hit, Vector3.Distance(randomPos, targetPos)))
        {
            Debug.DrawRay(randomPos, direction, Color.magenta, 1);
            if (hit.transform.gameObject !=  target.gameObject)
            {
                CheckTargetPosition();
            }
        }
    }

    private void SetLocation()
    {
        targetPos = new Vector3(target.position.x, transform.position.y, target.position.z);
    }

    public void SetTarget(Transform tr)
    {
        target = tr;
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
