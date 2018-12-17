using UnityEngine;
using System;

public class ProtocolData : EventArgs
{
   public bool TestRunning = false;
   public int Elapsed = 0;

   public float SwayAngle = 0, TransForce = 0, RotPos = 0, TransPos = 0;

   public float FZ = 0, COGX = 0, COGY = 0;
}

//////////////////////////////////////////////////////////////////////////

public class ProtocolDataReader
{
   /// <summary>
   /// Starts the live data process streamer on the server side, asking for the data rate at 30 fps.
   /// </summary>
   /// <returns></returns>
   public static void Start()
   {
      if (!_instance.alreadyStarted)
      {
         _instance.alreadyStarted = true;
         _instance.uuid = _instance.rpc.StartListener("live-data-process", new { Rate = 1000.0f / 30.0f }, _instance._OnData, typeof(ProtocolData));
      }
   }

   public static void Stop()
   {
      if (_instance.alreadyStarted)
      {
         _instance.rpc.StopListener(_instance.uuid);
         _instance.alreadyStarted = false;
         _instance.uuid = "";
      }
   }

   public static void UpdateComplianceScore(double newScore)
   {
      if (_instance.alreadyStarted)
      {
         _instance.rpc.IssueCommand("update-compliance-score", new { Score = newScore });
      }
   }

   public static void UpdateCompletedObstacles(int passed, int total)
   {
      if (_instance.alreadyStarted)
      {
         _instance.rpc.IssueCommand("update-completed-obstacles", new { Passed = passed, Total = total });
      }
   }

   public static void UpdateCorrectResponses(int correct, int total)
   {
      if (_instance.alreadyStarted)
      {
         _instance.rpc.IssueCommand("update-correct-responses", new { Correct = correct, Total = total });
      }
   }

   public static event Action<ProtocolData> OnData = delegate { };

   //////////////////////////////////////////////////////////////////////////

   private static readonly ProtocolDataReader _instance = new ProtocolDataReader();

   private ProtocolRPC rpc = new ProtocolRPC();

   private bool alreadyStarted = false;
   private string uuid = "";

   //////////////////////////////////////////////////////////////////////////

   private ProtocolDataReader()
   {

   }
   private void _OnData(string json, object obj)
   {
      OnData(obj as ProtocolData);
   }
}
