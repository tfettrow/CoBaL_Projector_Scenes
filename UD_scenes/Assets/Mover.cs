using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Mover handles moving the objects in the scene
/// </summary>
public class Mover : MonoBehaviour
{
   /// <summary>
   /// Prefab for the rooms to spawn
   /// </summary>
   public GameObject pfb_roomSet;
   /// <summary>
   /// How many of our prefabs to spawn
   /// </summary>
   public int setNumber = 8;
   /// <summary>
   /// Current move speed of the scene (changed by the options websocket command)
   /// </summary>
   public float moveSpeed = 0;

   public static float step;
   /// <summary>
   /// the position at which a room set will be moved to be back of the line, this should be behind the camera completely out of view
   /// </summary>
   public float resetZPosition = -100.0f;
   /// <summary>
   /// How far apart the room sets need to be, this depends on the actual size of the prefab, should be set in inspector
   /// </summary>
   public float spacingIncrement = 83.5f;
   /// <summary>
   /// List of objects after spawning
   /// </summary>
   private List<GameObject> setList;
   /// <summary>
   /// The list index of the current prefab that is most forward
   /// </summary>
   private int frontIndex = 0;

   void Start()
   {
      //initial position
      Vector3 currentPos = Vector3.zero;
      //create the list and spawn the prefabs
      setList = new List<GameObject>();
      for (int x = 0; x < setNumber; x++)
      {
         //make the prefab
         GameObject temp = Instantiate(pfb_roomSet);
         temp.transform.parent = this.transform;
         //add to the list
         setList.Add(temp);
         //set initial position
         temp.transform.position = currentPos;
         //increment the position
         currentPos.z += spacingIncrement;
      }
   }

   public void Move()
   {
      //clamp the speed to 0 to prevent the scene from moving backwards
      if (moveSpeed < 0)
         moveSpeed = 0;
      //return if we aren't moving
      if (moveSpeed <= 0)
         return;

      //see if this set has hit our limit to be moved to the back of the line
      if (setList[frontIndex].transform.position.z <= resetZPosition)
      {

         //when moving a room to the back, we have to stand up all of the obstacle in those rooms, if any..
         //get the room obstacles from each room in the given set
         RoomObstacles[] temp = setList[frontIndex].GetComponentsInChildren<RoomObstacles>();
         foreach (RoomObstacles t in temp) t.StandUpModels();

         //if we moved it, assign a new front index
         frontIndex++;
         //...clamped to the list length
         if (frontIndex >= setList.Count) frontIndex = 0;
      }

      //step is the increment we'll move the scene this update frame
      step = moveSpeed * Time.deltaTime;
      //move the foremost room a distance according to our movespeed and deltatime
      setList[frontIndex].transform.Translate(0, 0, -step);

      //now adjust every set based on the position of the front one
      //movement using the step variable above SHOULD be framerate independent, but small rounding errors occasionally pop up,
      //especially if the scene has been running for a while..
      //this is why we don't just move every room chunk according to that step.  We only move the foremost chunk according to
      //deltatime, and then adjust all the following chunks based on the actual new position of the first chunk.  This does a pretty
      //good job of keeping everything aligned without any noticable 'hitches' as the chunks adjust or reset position
      //extreme movement speeds can cause a 'jerk' in the scnee occasionally, this is usually because of dropped frames or just bad rounding
      //in the step calculation

      //our reference position is the position of the foremost chunk
      Vector3 refPos = setList[frontIndex].transform.position;
      //start moving the chunks @ the index after the foremost
      int nextIndex = frontIndex + 1;
      //clamped to the list size..
      if (nextIndex >= setList.Count) nextIndex = 0;
      //now go through the entire list
      for (int x = 1; x < setList.Count; x++)
      {
         //set the new position.  X and Y should never change, and the Z is a factor of our reference position
         //and the predefined spacing between the chunks
         setList[nextIndex].transform.position = new Vector3(0, 0, refPos.z + spacingIncrement * x);
         //increment the index
         nextIndex++;
         //...clamped to the list size
         if (nextIndex >= setList.Count) nextIndex = 0;
      }
   }

   public void SpawnObstacles()
   {
      foreach (GameObject chunk in setList)
      {
         RoomObstacles[] temp = chunk.GetComponentsInChildren<RoomObstacles>();
         foreach (RoomObstacles rmobst in temp)
            rmobst.SpawnObstacles();

      }
   }
}
