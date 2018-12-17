using UnityEngine;

public class CogCursor : MonoBehaviour
{

   // Use this for initialization
   void Start()
   {

   }

   // Update is called once per frame
   void Update()
   {

   }

   void OnTriggerEnter(Collider other)
   {
      Debug.Log("Cursor hit" + other.gameObject.name);
      //this needs to be handled here to get the mesh collider oriented the right way...
      //see if we hit an obstacle
      if (other.gameObject.name == "Mesh_Knight" || other.gameObject.name == "Mesh_Orc" || other.gameObject.name == "Mesh_Golem")
      {
         //knock him over!
         //have to do on parent b/c collider is on a chiuld object...
         other.transform.parent.GetComponent<Obstacle>().PlayAnimation("die", false);
         CastleManager.ObstaclesHit++;
      }
   }
}
