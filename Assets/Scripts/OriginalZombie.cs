using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class OriginalZombie : MonoBehaviour
{
    Animator animator;
    NavMeshAgent nm;
    public Transform target;
    public Transform rayPos;
    public float rayLen = 10.0f;

    private float fieldOfViewAngle = 90.0f;
    private float viewDistance = 20.0f;

    private int hp = 100;

    public enum AIState { IDLE, CHASING, PATROL};

    public AIState aiState = AIState.IDLE;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        nm = GetComponent<NavMeshAgent>();
        StartCoroutine(Think());
    }

    // Update is called once per frame
    void Update()
    {
        if (hp <= 0)
        {
            animator.SetBool("IsDeath", true);
        }
        View();
        Debug.DrawRay(rayPos.position, transform.forward * viewDistance, Color.green);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Enemy")
        {
            animator.SetBool("Attack", true);
        }
    }
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.tag == "Enemy")
        {
            animator.SetBool("Attack", false);
        }
    }

    private void NearZombieAttack(Vector3 pos)
    {
        Collider[] colls = Physics.OverlapSphere(pos, 10.0f);

        for (int i = 0; i < colls.Length; ++i)
            colls[i].SendMessage("Detect", SendMessageOptions.DontRequireReceiver);
    }

    private void Detect()
    {
        animator.SetBool("Detect", true);
        nm.SetDestination(target.position);
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

        Vector3 direction = (target.position - transform.position).normalized;
        float angle = Vector3.Angle(direction, transform.forward);

        if(angle < fieldOfViewAngle * 0.5f)
        {
            RaycastHit hit;
            if(Physics.Raycast(rayPos.position, direction, out hit, viewDistance))
            {
                if(hit.transform.gameObject.tag == "Enemy")
                {
                    Debug.DrawRay(rayPos.position, direction * viewDistance, Color.blue);
                    aiState = AIState.CHASING;
                }
            }
        }
    }

    IEnumerator Think()
    {
        while (true)
        {
            switch (aiState)
            {
                case AIState.IDLE:
                    float dist = Vector3.Distance(target.position, transform.position);
                    /*if(dist < 7f) //초 근접 상태일시 공격
                    {
                        aiState = AIState.CHASING;
                    }*/
                    break;
                case AIState.CHASING:
                    NearZombieAttack(transform.position);
                    break;
                case AIState.PATROL:
                    //animator.SetBool("Patrol", true);
                    break;
                default:
                    break;
            }
            yield return new WaitForSeconds(1f);
        }
    }
}
