using System;


//////////////////////////////////////////////////////////////////////////

class ListenForTestOptionChanged
{
   /// <summary>
   /// Starts listening for options to be changed on the other side (via the html page)
   /// </summary>
   /// <returns></returns>
   public static void Start()
   {
      if (!_instance.alreadyStarted)
      {
         _instance.alreadyStarted = true;
         TestOptionsReader.OptionChanged += _instance.OnOptionChanged;
         TestOptionsReader.Start();
      }
   }

   public static void Stop()
   {
      if (_instance.alreadyStarted)
      {
         TestOptionsReader.OptionChanged -= _instance.OnOptionChanged;
         TestOptionsReader.Stop();
         _instance.alreadyStarted = false;
      }
   }


   public static event Action<string> SpeedChanged = delegate { };
   public static event Action<float>  MovementGainChanged = delegate { };
   public static event Action<float>  SwayGainChanged = delegate { };
   public static event Action<int>    TestTimeChanged = delegate { };
   public static event Action<string> ObstaclesChanged = delegate { };
   public static event Action<string> mStroopTestChanged = delegate { };
   public static event Action<bool>   mStroopObstaclesChanged = delegate { };
   public static event Action<bool>   DisplayTargetCircleChanged = delegate { };
   public static event Action<bool>   DisplayCOGCursorChanged = delegate { };
   public static event Action<bool>   AudioFeedbackChanged = delegate { };
   public static event Action<float>  SubjectHeightChanged = delegate { };

   //////////////////////////////////////////////////////////////////////////

   private static readonly ListenForTestOptionChanged _instance = new ListenForTestOptionChanged();

   private bool alreadyStarted = false;

   //////////////////////////////////////////////////////////////////////////

   private ListenForTestOptionChanged()
   {
   }


   private void OnOptionChanged(string s, object value)
   {
      switch (s)
      {
         case "speed":
            SpeedChanged(ForceSocket.toLowerString(value));
            break;
         case "movementgain":
            MovementGainChanged(ForceSocket.toSingle(value));
            break;
         case "swaygain":
            SwayGainChanged(ForceSocket.toSingle(value));
            break;
         case "testtime":
            TestTimeChanged(ForceSocket.toInt(value));
            break;
         case "obstacles":
            ObstaclesChanged(ForceSocket.toLowerString(value));
            break;
         case "mstrooptest":
            mStroopTestChanged(ForceSocket.toLowerString(value));
            break;
         case "mstroopobstacles":
            mStroopObstaclesChanged(ForceSocket.toBool(value));
            break;
         case "displaytargetcircle":
            DisplayTargetCircleChanged(ForceSocket.toBool(value));
            break;
         case "displaycogcursor":
            DisplayCOGCursorChanged(ForceSocket.toBool(value));
            break;
         case "audiofeedback":
            AudioFeedbackChanged(ForceSocket.toBool(value));
            break;
         case "heightmm":
            SubjectHeightChanged(ForceSocket.toSingle(value));
            break;
      }
   }
}
