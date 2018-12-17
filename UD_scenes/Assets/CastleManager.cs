using System;
using UnityEngine;
using UnityEngine.UI;
public class CastleManager : MonoBehaviour
{
   public Mover roomMover;
   public GameObject image_cogCursor, image_targetArea;
   public GameObject camerasContainer;

   public const float SCENESPEED_SLOW = 1.8f;
   public const float SCENESPEED_MEDIUM = 2.5f;
   public const float SCENESPEED_FAST = 3.6f;
   public const float SCENESPEED_STOP = 0f;

   public static int Current_Test_Length = 2;   // in minutes

   public const int OBSTACLEDIFF_OFF = 0;
   public const int OBSTACLEDIFF_EASY = 1;
   public const int OBSTACLEDIFF_MEDIUM = 2;
   public const int OBSTACLEDIFF_HARD = 3;
   public static int Current_Obstacle_Difficulty = OBSTACLEDIFF_OFF;

   public static float Current_Movement_Gain = 1.5f;
   public static float Current_Sway_Gain = 0.0f;

   public static int ObstaclesHit = 0;

   public float playerStickAmplitude = 1.0f; // these two will come from the parms sent to the exe (if present)
   public float cameraGain = -1.0f;

   //misc
   private static bool
       Display_COG_Cursor = true,
       Audio_Feedback = false,
       isRunning = false,
       Display_Word_Canvas = false,
       Display_TargetArea = false,
       InvisibleComplianceMode = false,
       StroopTestRunning = false;
   public Canvas wordCanvas;
   public Text text_compliance;

   private float swayAngleOffset = 0;
   private float swayAngleSum = 0; // used to compute the offset
   private int swayAngleCount = 0;
   private int swayAngleCountNeeded = 5;

   private float currentSwayAngle = 0.0f;
   private float subjectHeight = 1.5f;
   private float baseHeightOffset = 0.7f;   // used when computing the camera stick position


   // Use this for initialization
   void Start()
   {
      //plate stuff
      EmbeddedFixer.Fix();
      EnforceDefaults.Enforce();

      //BA UI
      ListenForTestOptionChanged.SpeedChanged += SetSceneMovementSpeed;
      ListenForTestOptionChanged.MovementGainChanged += SetMovementGain;
      ListenForTestOptionChanged.SwayGainChanged += SetSwayGain;
      ListenForTestOptionChanged.AudioFeedbackChanged += ToggleAudioFeedback;
      ListenForTestOptionChanged.DisplayCOGCursorChanged += ToggleCogCursor;
      ListenForTestOptionChanged.DisplayTargetCircleChanged += ToggleTargetArea;
      ListenForTestOptionChanged.ObstaclesChanged += SetObstacleDifficutly;
      ListenForTestOptionChanged.TestTimeChanged += SetTestLength;
      ListenForTestOptionChanged.mStroopObstaclesChanged += SetStroopObstacles;
      ListenForTestOptionChanged.mStroopTestChanged += SetStroopTest;
      ListenForTestOptionChanged.SubjectHeightChanged += SubjectHeightChanged;
      ListenForTestOptionChanged.Start();

      SetObstacleDifficutly("off");
      image_cogCursor.SetActive(Display_COG_Cursor);
      wordCanvas.gameObject.SetActive(Display_Word_Canvas);
      HandleBPCursor(0, 0);
      SwayAngleChanged();

      ProtocolDataReader.OnData += UpdateProtocolData;
      ProtocolDataReader.Start();
   }

   public static void OnApplicationQuit()
   {
      Debug.Log("OnApplicationQuit");
      ListenForTestOptionChanged.Stop();
      ProtocolDataReader.Stop();
      ForceSocket.Shutdown();
   }

   private void UpdateProtocolData(ProtocolData d)
   {
      if (d.TestRunning != isRunning)
         ExecuteOnMainThread.AddAction(() => { StartStopTest(d.TestRunning); });

      float x = d.COGX * Current_Movement_Gain;
      float y = -d.COGY * Current_Movement_Gain;
      if (Math.Abs(x) < 0.003)
         x = 0;

      if (Math.Abs(y) < 0.003)
         y = 0;

      ExecuteOnMainThread.AddAction(() => { HandleBPCursor(x, y); });

      if (swayAngleCount >= swayAngleCountNeeded)
      {
         float newSwayAngle = d.SwayAngle - swayAngleOffset;
         if (newSwayAngle != currentSwayAngle)
         {
            currentSwayAngle = newSwayAngle;
            ExecuteOnMainThread.AddAction(() => { SwayAngleChanged(); });
         }
      }
      else
      {
         swayAngleSum += d.SwayAngle;
         ++swayAngleCount;
         if (swayAngleCount == swayAngleCountNeeded)
            swayAngleOffset = swayAngleSum / (float)swayAngleCountNeeded;
      }
   }

   // Update is called once per frame
   void Update()
   {
      ExecuteOnMainThread.ProcessActions();

      #region Keyboard Inputs

      //quit
      if (Input.GetKeyDown(KeyCode.Escape))
         Application.Quit();

      //level 3 toggle invisible compliance mode
      if (Input.GetKeyDown(KeyCode.V))
         ToggleInvisibleComplianceMode(InvisibleComplianceMode ? "off" : "on");

      //reset compliance
      if (Input.GetKeyDown(KeyCode.R))
      {
         TargetArea.Reset();
         ObstaclesHit = 0;
      }

      #endregion

      if (isRunning)
         roomMover.Move();

      //for now...
      //update compliance
      float comp = (TargetArea.counter / TargetArea.TimeSinceReset) * 100;
      text_compliance.text = "CoG: " + comp.ToString("00") + "%\nObst: " + ObstaclesHit;
   }

   private void HandleBPCursor(float x,float y)
   {
      if (Display_COG_Cursor)
      {
         float scaledX = (x * 10.0f);
         float scaledY = (y * 10.0f) + 4.0f;
         //now multiply by our scaling factor (But there is no scaling factor....)
         scaledX *= 1f;
         scaledY *= 1f;
         //now just place the sprite at those coords
         image_cogCursor.transform.position = new Vector3(scaledX, scaledY, -75);
      }
   }

   void SwayAngleChanged()
   {
      if (this != null && this.isActiveAndEnabled)
      {
         Vector3 fuAngle = transform.localEulerAngles;

         fuAngle.x = currentSwayAngle * playerStickAmplitude;
         transform.localEulerAngles = fuAngle;

         if (camerasContainer != null)
         {
            fuAngle = camerasContainer.transform.localEulerAngles;
            fuAngle.x = currentSwayAngle * cameraGain;
            camerasContainer.transform.localEulerAngles = fuAngle;
         }
      }
   }


   public void SetSceneMovementSpeed(string newSpeed)
   {
      switch (newSpeed)
      {
         case "slow":
            roomMover.moveSpeed = SCENESPEED_SLOW;
            break;
         case "medium":
            roomMover.moveSpeed = SCENESPEED_MEDIUM;
            break;
         case "fast":
            roomMover.moveSpeed = SCENESPEED_FAST;
            break;
         case "stop":
            roomMover.moveSpeed = SCENESPEED_STOP;
            break;
      }

      Debug.Log("Speed set to: " + newSpeed);
   }

   public void SetTestLength(int newLength)
   {
      int time = newLength;

      Current_Test_Length = time;

      Debug.Log("Test length set to: " + newLength);
   }

   public void StartStopTest(bool newRunningFlag)
   {
      isRunning = newRunningFlag;
      if (isRunning)
         TargetArea.Reset();
      Debug.Log("isRunning:" + isRunning);
   }

   public void ToggleAudioFeedback(bool state)
   {
      //Audio_Feedback = state == "on";

      Debug.Log("Play audio feedback set to: " + Audio_Feedback);
   }

   private void ToggleTargetArea(bool state)
   {
      Display_TargetArea = state;

      ExecuteOnMainThread.AddAction(() => { image_targetArea.gameObject.SetActive(Display_TargetArea); });
      Debug.Log("Display target area set to: " + Display_TargetArea);
   }

   public void ToggleCogCursor(bool state)
   {
      Display_COG_Cursor = state;
      ExecuteOnMainThread.AddAction(() => { image_cogCursor.gameObject.SetActive(Display_COG_Cursor); });

      Debug.Log("Display cog cursor set to: " + Display_COG_Cursor);
   }



   public void ToggleInvisibleComplianceMode(string state)
   {
      InvisibleComplianceMode = (state.ToLower() == "on");

      ExecuteOnMainThread.AddAction(() => {
         image_targetArea.GetComponent<SpriteRenderer>().enabled = InvisibleComplianceMode;
         image_cogCursor.GetComponent<SpriteRenderer>().enabled = InvisibleComplianceMode;
      });

      Debug.Log("invisible compliance mode set to " + InvisibleComplianceMode);
   }

   public void SetObstacleDifficutly(string state)
   {
      switch (state)
      {
         case "easy":
            Current_Obstacle_Difficulty = OBSTACLEDIFF_EASY;
            break;
         case "medium":
            Current_Obstacle_Difficulty = OBSTACLEDIFF_MEDIUM;
            break;
         case "hard":
            Current_Obstacle_Difficulty = OBSTACLEDIFF_HARD;
            break;
         case "off":
         default:
            Current_Obstacle_Difficulty = OBSTACLEDIFF_OFF;
            break;
      }

      Debug.Log("Obstacle difficulty set to: " + state);
      ExecuteOnMainThread.AddAction(() => { roomMover.SpawnObstacles(); });
   }

   private void SetMovementGain(float val)
   {
      Current_Movement_Gain = val;

      Debug.Log("Movement gain set to: " + Current_Movement_Gain);
   }

   private void SetSwayGain(float val)
   {
      Current_Sway_Gain = val;

      Debug.Log("Sway gain set to: " + Current_Sway_Gain);
   }

   private void SubjectHeightChanged(float mm)
   {
      subjectHeight = mm / 1000.0f;  // mm to M

      mm -= 1000; // start from a base of zero
      if (mm < 0)
         mm = 0;
      if (mm > 1000)
         mm = 1000;  // max two meters (6.5 feet)

      mm /= 1000.0f; // scale it to fit (0.000 to 1.000 inclusive; could also make it scale from 0 to 1.5 or 2.7)

      // Vector3 sz = playerStick.transform.localPosition;
      // sz.y = mm + 0.7f;
      // playerStick.transform.localPosition = sz;
   }

   private void SetStroopTest(string str)
   {

   }

   private void SetStroopObstacles(bool state)
   {
      //unused at the moment..
   }
}
