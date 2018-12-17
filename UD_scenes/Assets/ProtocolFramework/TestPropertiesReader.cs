using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;

class TestPropertiesReader
{
   /// <summary>
   /// Starts listening for properties to be changed on the other side (via the html page)
   /// </summary>
   /// <returns></returns>
   public static void Start()
   {
      if (!_instance.alreadyStarted)
      {
         _instance.alreadyStarted = true;
         _instance.uuid = _instance.rpc.StartListener(ForceSocket.KnownCommands.ListenPropertyChangedCommand, null, _instance._OnData);
      }
   }

   public static void Stop()
   {
      if (_instance.alreadyStarted)
      {
         _instance.rpc.IssueCommand(ForceSocket.KnownCommands.StopListenPropertyChangedCommand);
         _instance.rpc.StopListener(_instance.uuid);
         _instance.alreadyStarted = false;
         _instance.uuid = "";
      }
   }


   public static void SetProperty(string propertyName, object newValue)
   {
      if (_instance.alreadyStarted)
      {
         Dictionary<string, object> dict = new Dictionary<string, object>();
         dict.Add(propertyName, newValue);

         _instance.rpc.IssueCommand(ForceSocket.KnownCommands.SetPropertyCommand, dict);
      }
   }

   

   public static event Action<string,object> PropertyChanged = delegate { }; // this will be called with each key:value pair.


   //////////////////////////////////////////////////////////////////////////

   private static readonly TestPropertiesReader _instance = new TestPropertiesReader();

   private ProtocolRPC rpc = new ProtocolRPC();

   private bool alreadyStarted = false;
   private string uuid = "";

   //////////////////////////////////////////////////////////////////////////

   private TestPropertiesReader()
   {
   }

   private void _OnData(string json, object obj)
   {
      Dictionary<string, object> dict = obj as Dictionary<string, object>;
      foreach (KeyValuePair<string, object> kvp in dict)
      {
         PropertyChanged(kvp.Key, kvp.Value);
      }
   }
}

