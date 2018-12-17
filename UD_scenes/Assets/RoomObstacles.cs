using UnityEngine;
using System.Collections.Generic;

public class RoomObstacles : MonoBehaviour
{
   public Obstacle pfb_obstacle1, pfb_obstacle2, pfb_obstacle3;
   public GameObject origin;
   private static Vector3[] spawnPositions =
       {new Vector3(-1.5f, .124f, 0), new Vector3(0, .124f, 0), new Vector3(1.5f, .124f, 0)};
   private List<Obstacle> spawnedObstacles = new List<Obstacle>();
   private float spawnScale = 1.5f;

   // Use this for initialization
   void Start()
   {

   }

   // Update is called once per frame
   void Update()
   {
   }

   public void SpawnObstacles()
   {
      //nuke any previous obstacles
      foreach (Obstacle obst in spawnedObstacles)
         Destroy(obst.gameObject);
      //clear the list
      spawnedObstacles.Clear();

      //The number we spawn is dependent on the difficulty
      int count = CastleManager.Current_Obstacle_Difficulty;
      for (int x = 0; x < count; x++)
      {
         //randomly get an obstacle prefab
         Obstacle temp = null;
         int rand = Random.Range(0, 3);
         switch (rand)
         {
            case 0:
               temp = Instantiate(pfb_obstacle1);
               break;
            case 1:
               temp = Instantiate(pfb_obstacle2);
               break;
            case 2:
               temp = Instantiate(pfb_obstacle3);
               break;
         }

         temp.transform.SetParent(origin.transform);
         spawnedObstacles.Add(temp);

         //things can possibly get wacky with where the models will spawn..
         //we'll want some logic here at some point
         int pos = Random.Range(0, 3);
         float z = Random.Range(0, 30);
         z *= -1;
         temp.transform.localPosition = new Vector3(spawnPositions[pos].x, spawnPositions[pos].y, z);
      }
   }

   /// <summary>
   /// Stand up any characters that were previously knocked down
   /// </summary>
   public void StandUpModels()
   {
      spawnedObstacles.ForEach(t => t.PlayAnimation("idle"));
   }
}
