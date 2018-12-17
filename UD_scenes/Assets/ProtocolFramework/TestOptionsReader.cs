using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;

class TestOptionsReader
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
         _instance.uuid = _instance.rpc.StartListener(ForceSocket.KnownCommands.ListenOptionChangedCommand, null, _instance._OnData);
      }
   }

   public static void Stop()
   {
      if (_instance.alreadyStarted)
      {
         _instance.rpc.IssueCommand(ForceSocket.KnownCommands.StopListenOptionChangedCommand);
         _instance.rpc.StopListener(_instance.uuid);
         _instance.alreadyStarted = false;
         _instance.uuid = "";
      }
   }

   public static void SetOption(string propertyName, object newValue)
   {
      if (_instance.alreadyStarted)
      {
         Dictionary<string, object> dict = new Dictionary<string, object>();
         dict.Add(propertyName, newValue);

         _instance.rpc.IssueCommand(ForceSocket.KnownCommands.SetOptionCommand, dict);
      }
   }



   public static event Action<string,object> OptionChanged = delegate { }; // this will be called with each key:value pair.


   //////////////////////////////////////////////////////////////////////////

   private static readonly TestOptionsReader _instance = new TestOptionsReader();

   private ProtocolRPC rpc = new ProtocolRPC();

   private bool alreadyStarted = false;
   private string uuid = "";

   //////////////////////////////////////////////////////////////////////////

   private TestOptionsReader()
   {
   }

   private void _OnData(string json, object obj)
   {
      Dictionary<string, object> dict = obj as Dictionary<string, object>;
      foreach (KeyValuePair<string, object> kvp in dict)
      {
         try
         {
            OptionChanged(kvp.Key.ToLower(), kvp.Value);
         }
         catch (System.Exception ex)
         {
            UnityEngine.Debug.Log("Got exception while processing option key " + kvp.Key + ", excp=" + ex.ToString());         	
         }
      }
   }
}

