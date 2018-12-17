using UnityEngine;
using System.Collections;

public class UpdateONcollide : MonoBehaviour {

    Vector3 currentposition;
    GameObject OrigObject;

    void Start()
    {
        OrigObject = GameObject.Find("OrigCube");
        transform.parent = OrigObject.transform;
        currentposition = transform.position;
    }

    void OnTriggerEnter(Collider ObjectTriggered)
    {
        if(ObjectTriggered.gameObject.name == "ObjectBoundary")
        {
            currentposition.z = 125; // Find a way to automate.. possibly globabl Distance variable.
            transform.position = currentposition;
        }       
  	}
}
