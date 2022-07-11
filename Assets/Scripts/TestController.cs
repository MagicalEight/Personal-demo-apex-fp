using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestController : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //transform.rotation = Quaternion.AngleAxis(30, Vector3.up);
        transform.localRotation = Quaternion.Euler(0, 2, 0);
    }
}
