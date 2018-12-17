/// <summary>
/// This is an example class on how to use the ForceSocket class. This particular class interfaces with the COP data stream.
/// This class is currently implemented as a static singleton, but could be made a standard class if so desired.
/// </summary>

using System;


public class COPData : EventArgs
{
   public int row = 0;  // row counter
   public long ts = 0;  // timestamp

   public float z = 0, x = 0, y = 0;
}

//////////////////////////////////////////////////////////////////////////

public class COPDataReader
{
   public static void Start()
   {
      if (!_instance.alreadyStarted)
      {
         _instance.alreadyStarted = true;
         ForceSocket.Start();
         _instance.uuid = ForceSocket.IssuePerodicResponseCommand(ForceSocket.KnownCommands.StartCOP,
                                                                  new { UseCOG=false, Rate = 1000.0f / 30.0f },// 30 data frames per second
                                                                  _instance._OnData, typeof(COPData));
      }
   }

   public static void Stop()
   {
      if (_instance.alreadyStarted)
      {
         ForceSocket.IssueStopAndRemove(_instance.uuid, ForceSocket.KnownCommands.StopCOP);
         _instance.alreadyStarted = false;
         _instance.uuid = "";
      }
   }

   public static event Action<COPData> OnData = delegate { };

   //////////////////////////////////////////////////////////////////////////

   private static readonly COPDataReader _instance = new COPDataReader();

   private bool alreadyStarted = false;
   private string uuid = "";

   //////////////////////////////////////////////////////////////////////////

   private COPDataReader()
   {
   }

   private void _OnData(string json, object obj)
   {
      OnData(obj as COPData);
   }
}
