using UnityEngine;
using TMPro;


public class Text3D : MonoBehaviour
{

   public TextMeshPro text;
   private string[] colorNames = { "black", "white", "red", "blue", "green", "yellow", "grey" };
   private bool isMoving = false;
   private bool activeWord = false;
   private float upTime, upTimeLimit;

   // Use this for initialization
   void Start()
   {
      upTime = 0;
      upTimeLimit = .120f;
      text.gameObject.SetActive(false);
   }

   // Update is called once per frame
   void Update()
   {
      if (Input.GetKeyDown(KeyCode.L)) //this is a hotkey but well want to respond to a remote input to trigger these!!
      {
         //pick a new color and word
         int temp = Random.Range(1, 8);
         switch (temp)
         {
            case 1:
               text.color = Color.black;
               break;
            case 2:
               text.color = Color.white;
               break;
            case 3:
               text.color = Color.red;
               break;
            case 4:
               text.color = Color.blue;
               break;
            case 5:
               text.color = Color.green;
               break;
            case 6:
               text.color = Color.yellow;
               break;
            case 7:
               text.color = Color.grey;
               break;
         }

         text.text = colorNames[Random.Range(0, 7)];
         text.transform.position = new Vector3(0, 4.5f, -62);
         isMoving = true;
         activeWord = true;
         text.gameObject.SetActive(true);
         upTime = 0f;
      }

      if (isMoving)
      {
         //we need to move the text by the speed of the scene
         transform.Translate(0, 0, -Mover.step);
         if (transform.position.z <= -90)
            isMoving = false;
      }

      if (activeWord)
      {
         upTime += Time.deltaTime;

         if (upTime >= upTimeLimit)
         {
            text.gameObject.SetActive(false);
            upTime = 0;
            activeWord = false;
         }
      }
   }
}
