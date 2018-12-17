/// <summary>
/// This is an example class on how to use the ForceSocket class. This particular class interfaces with the weight data stream.
/// This class is currently implemented as a static singleton, but could be made a standard class if so desired.
/// </summary>

using System;


public class WeightFZData : EventArgs
{
   public float fz = 0; // the fz load value in newtons
   public string weight;   // the same value but as a string in the current ui display weight (ex: "120 lbs" or "3.47 kg")
}

//////////////////////////////////////////////////////////////////////////

public class WeightFZDataReader
{
   public static void Start()
   {
      if (!_instance.alreadyStarted)
      {
         _instance.alreadyStarted = true;
         ForceSocket.Start();
         _instance.uuid = ForceSocket.IssuePerodicResponseCommand(ForceSocket.KnownCommands.StartWeightMon,
                                                                  new { Rate = 1000.0f / 30.0f },// 30 data frames per second
                                                                  _instance._OnData, typeof(WeightFZData));
      }
   }

   public static void Stop()
   {
      if (_instance.alreadyStarted)
      {
         ForceSocket.IssueStopAndRemove(_instance.uuid, ForceSocket.KnownCommands.StopWeightMon);
         _instance.alreadyStarted = false;
         _instance.uuid = "";
      }
   }

   public static event Action<WeightFZData> OnData = delegate { };

   //////////////////////////////////////////////////////////////////////////

   private static readonly WeightFZDataReader _instance = new WeightFZDataReader();

   private bool alreadyStarted = false;
   private string uuid = "";

   //////////////////////////////////////////////////////////////////////////

   private WeightFZDataReader()
   {
   }

   private void _OnData(string json, object obj)
   {
      OnData(obj as WeightFZData);
   }
}
