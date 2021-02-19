using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieCtrl : MonoBehaviour
{
    private ZombieController zc;
    public float hp = 100.0f;
    public float power = 10.0f;

    void Start()
    {
        zc = GetComponent<ZombieController>();
    }

    void Update()
    {
        if(hp<=0)
        {
            zc.state = ZombieController.State.DIE;
        }
    }
}
