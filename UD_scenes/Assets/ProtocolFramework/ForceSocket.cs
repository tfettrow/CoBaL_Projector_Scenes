using UnityEngine;
using System;
using System.Collections.Generic;
using WebSocketSharp;
using Newtonsoft.Json;

/// <summary>
/// This is a static class that the Web Socket interface to the Workbench framework. It connects using either the default
/// web socket interface url or the value passed on the command line. Using it will automatically issue a connect command
/// to the server. All communication is done via JSON and uses callback ids and Action functions.
/// This class closely mimics the Javascript version which does the same thing for the HTML pages.
/// </summary>
public class ForceSocket
{
   /// <summary>
   /// The web socket interface url. Setting this after a connection is already exstablished will not force a reconnect;
   /// in order to do that you will need to first call Shutdown and then re-call Start (note that doing so will break all
   /// existing command id callbacks)
   /// </summary>
   public static string Url
   {
      get
      {
         return _instance._ws_url;
      }
      set
      {
         _instance._ws_url = value;
      }
   }

   /// <summary>
   /// Returns TRUE if the web socket interface is currently connected. This does not mean that communication is actually taking
   /// place but just that a server of some type exists on the other end.
   /// </summary>
   public static bool IsConnected
   {
      get
      {
         if (_instance._ws != null)
            return _instance._ws.IsConnected;
         else
            return false;
      }
   }

   /// <summary>
   /// Currently there are a handful of known commands that the Websocket interface knows about. These are defined here for reference.
   /// </summary>
   public static class KnownCommands
   {
      /// <summary>
      /// Starts sending Center of Pressure data to the client. Supports the following parm values:
      ///   Rate: how frequent, in milliseconds, the data will be sent. Defaults to 1 and a suggested value is 33 or higher (30fps)
      ///   UseCOG: if set, the command will return the center of Gravity instead of Pressure.
      /// The command will send a JSON string containing the following:
      ///   row: a row counter
      ///   ts: timestamp of the data frame
      ///   z: the fz value
      ///   x: the cop or cog X value
      ///   y: the cop or cog Y value
      /// </summary>
      public static string StartCOP {  get { return "start-cop"; } }
      /// <summary>
      /// Stops sending the COP values.
      /// </summary>
      public static string StopCOP { get { return "stop-cop"; } }

      /// <summary>
      /// Starts sending live weight values to the client. Supports the following parm values:
      ///   Rate: how frequent, in milliseconds, the data will be sent. Defaults to 1 and a suggested value is 33 or higher (30fps)
      /// The command will send a JSON string containing the following:
      ///   z: the fz value
      ///   weight: the corresponding pounds or kilograms for the fz value, depending on the user interface settings
      /// </summary>
      public static string StartWeightMon { get { return "start-weight-monitor"; } }
      /// <summary>
      /// Stops sending the weight values.
      /// </summary>
      public static string StopWeightMon { get { return "stop-weight-monitor"; } }

      /// <summary>
      /// Starts sending raw channel data to the client. Supports the following parm values:
      ///   Rate: how frequent, in milliseconds, the data will be sent. Defaults to 1 and a suggested value is 33 or higher (30fps)
      ///   Channels: an array of channels to send. If this is not present/empty/set to *, all channels will be used. If the given
      ///             channel being requested does not exist for the current device, then a value of zero will be filled in the data stream.
      /// The command will send a JSON string containing the following:
      ///   row: a row counter
      ///   ts: timestamp of the data frame
      ///   CHANNELNAME1: the value of CHANNELNAME1
      ///   CHANNELNAME2: the value of CHANNELNAME2
      ///   CHANNELNAME3: the value of CHANNELNAME3
      ///   CHANNELNAME4: the value of CHANNELNAME4
      ///   etc
      /// </summary>
      public static string StartChannelMon { get { return "start-channel-monitor"; } }
      /// <summary>
      /// Stops sending the channel values.
      /// </summary>
      public static string StopChannelMon { get { return "stop-channel-monitor"; } }

      /// <summary>
      /// Invokes a specific protocol command for the current protocol. What that command is and what it does is up to the protocol itself.
      /// The parms are passed as-is and are directly interpreted by the protocol handler. Any data returned back from it also depends on the protocol.
      /// </summary>
      public static string ProtocolCommand { get { return "protocol-command"; } }
      /// <summary>
      /// Stops whatever the protocol command was doing.
      /// </summary>
      public static string StopProtocolCommand { get { return "stop-protocol-command"; } }

      /// <summary>
      /// Sub-command for the ProtocolCommand. Starts listening for the options to be changed
      /// </summary>
      public static string ListenOptionChangedCommand { get { return "listen-option-changed"; } }
      /// <summary>
      /// Stops listening
      /// </summary>
      public static string StopListenOptionChangedCommand { get { return "stop-listen-option-changed"; } }
      /// <summary>
      /// Sets an option to a new value
      /// </summary>
      public static string SetOptionCommand { get { return "set-option"; } }


      /// <summary>
      /// Sub-command for the ProtocolCommand. Starts listening for the properties to be changed
      /// </summary>
      public static string ListenPropertyChangedCommand { get { return "listen-property-changed"; } }
      /// <summary>
      /// Stops listening
      /// </summary>
      public static string StopListenPropertyChangedCommand { get { return "stop-listen-property-changed"; } }
      /// <summary>
      /// Sets a property to a new value
      /// </summary>
      public static string SetPropertyCommand { get { return "set-property"; } }

   }

   //////////////////////////////////////////////////////////////////////////

   /// <summary>
   /// Connect to the web socket interface server. This function is called automatically when needed, and can be safely
   /// called multiple times (will only connect once).
   /// </summary>
   public static void Start()
   {
      _instance.Connect();
   }

   /// <summary>
   /// Force a disconnect from the web socket interface server. This is normally only called during app shutdown and generally not even then
   /// (exiting the app will close the socket and disconnect from the server which will in turn unwind all the connections and ids)
   /// </summary>
   public static void Shutdown()
   {
      _instance.Disconnect();
   }

   /// <summary>
   /// Sends the command and the parms over in a fire-and-forget manner; no callbacks are set up to be handled and there is no return value. 
   /// </summary>
   /// <param name="cmd">The command to be sent to the web socket interface; these are currently defined on the server.</param>
   /// <param name="parms">An object (typically an anonmyous type) of parms to be sent. These will be converted to a JSON object string.</param>
   /// <returns>No return value</returns>
   public static void IssueCommand(string cmd, object parms)
   {
      Start(); // starts the server if not already running

      string jsonText = JsonConvert.SerializeObject(new { cmd = cmd, parms = parms },
                                                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
      _instance.SendTextData(jsonText);
   }


   /// <summary>
   /// Sends the command and the parms with a callback id that will invoke the completionCallback method if and when the host returns a value.
   /// This is expected to result in a single updates which will call the completionCallback once and then remove the uuid and callback from the internal table.
   /// </summary>
   /// <param name="cmd">The command to be sent to the web socket interface; these are currently defined on the server.</param>
   /// <param name="parms">An object (typically an anonmyous type) of parms to be sent. These will be converted to a JSON object string.</param>
   /// <param name="completionCallback">The method that should be invoked when the server returns back a result. The method will be called
   /// with the raw JSON string and an generic object parsed from the JSON string.</param>
   /// <returns>The UUID that was assigned to this command. Can be used for internal bookkeeping or for later use by RemoveCallback.</returns>
   public static string IssueCommand(string cmd, object parms, Action<string, object> completionCallback)
   {
      Start(); // starts the server if not already running

      string uuid = System.Guid.NewGuid().ToString();

      lock (_instance._completionCallbacks)
      {
         _instance._completionCallbacks.Add(uuid, new callbackData(completionCallback, false));   // this will throw an exception if the given uuid already exists (which is probably a good thing)
      }

      string jsonText = JsonConvert.SerializeObject(new { cmd = cmd, parms = parms, _id_ = uuid },
                                                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
      _instance.SendTextData(jsonText);

      return uuid;
   }

   /// <summary>
   /// Sends the command and the parms with a callback id that will invoke the completionCallback method if and when the host returns a value.
   /// This is expected to result in a single updates which will call the completionCallback once and then remove the uuid and callback from the internal table.
   /// </summary>
   /// <param name="cmd">The command to be sent to the web socket interface; these are currently defined on the server.</param>
   /// <param name="parms">An object (typically an anonmyous type) of parms to be sent. These will be converted to a JSON object string.</param>
   /// <param name="completionCallback">The method that should be invoked when the server returns back a result. The method will be called
   /// with the raw JSON string and object of the given dataType parsed from the JSON string.</param>
   /// <param name="datatype">The Type that the JSON parser should convert the incoming result data into. This new data object will be passed
   /// in the completionCallback's object parm</param>
   /// <returns>The UUID that was assigned to this command. Can be used for internal bookkeeping or for later use by RemoveCallback.</returns>
   public static string IssueCommand(string cmd, object parms, Action<string, object> completionCallback, Type datatype)
   {
      Start(); // starts the server if not already running

      string uuid = System.Guid.NewGuid().ToString();

      lock (_instance._completionCallbacks)
      {
         _instance._completionCallbacks.Add(uuid, new callbackData(completionCallback, datatype, false));   // this will throw an exception if the given uuid already exists (which is probably a good thing)
      }

      string jsonText = JsonConvert.SerializeObject(new { cmd = cmd, parms = parms, _id_ = uuid },
                                                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
      _instance.SendTextData(jsonText);

      return uuid;
   }

   /// <summary>
   /// Sends the command and the parms with a callback id that will invoke the completionCallback method if and when the host returns a value.
   /// This is expected to result in periodic updates which will call the completionCallback multiple times.
   /// Your code will probably need to call IssueStopAndRemove with the uuid and the appropriate command when done.
   /// </summary>
   /// <param name="cmd">The command to be sent to the web socket interface; these are currently defined on the server.</param>
   /// <param name="parms">An object (typically an anonmyous type) of parms to be sent. These will be converted to a JSON object string.</param>
   /// <param name="completionCallback">The method that should be invoked when the server returns back a result. The method will be called
   /// with the raw JSON string and an generic object parsed from the JSON string.</param>
   /// <returns>The UUID that was assigned to this command. Can be used for internal bookkeeping or for later use by RemoveCallback.</returns>
   public static string IssuePerodicResponseCommand(string cmd, object parms, Action<string, object> completionCallback)
   {
      Start(); // starts the server if not already running

      string uuid = System.Guid.NewGuid().ToString();

      lock (_instance._completionCallbacks)
      {
         _instance._completionCallbacks.Add(uuid, new callbackData(completionCallback, true));   // this will throw an exception if the given uuid already exists (which is probably a good thing)
      }

      string jsonText = JsonConvert.SerializeObject(new { cmd = cmd, parms = parms, _id_ = uuid },
                                                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
      _instance.SendTextData(jsonText);

      return uuid;
   }

   /// <summary>
   /// Sends the command and the parms with a callback id that will invoke the completionCallback method if and when the host returns a value.
   /// This is expected to result in periodic updates which will call the completionCallback multiple times.
   /// Your code will probably need to call IssueStopAndRemove with the uuid and the appropriate command when done.
   /// </summary>
   /// <param name="cmd">The command to be sent to the web socket interface; these are currently defined on the server.</param>
   /// <param name="parms">An object (typically an anonmyous type) of parms to be sent. These will be converted to a JSON object string.</param>
   /// <param name="completionCallback">The method that should be invoked when the server returns back a result. The method will be called
   /// with the raw JSON string and object of the given dataType parsed from the JSON string.</param>
   /// <param name="datatype">The Type that the JSON parser should convert the incoming result data into. This new data object will be passed
   /// in the completionCallback's object parm</param>
   /// <returns>The UUID that was assigned to this command. Can be used for internal bookkeeping or for later use by RemoveCallback.</returns>
   public static string IssuePerodicResponseCommand(string cmd, object parms, Action<string, object> completionCallback, Type datatype)
   {
      Start(); // starts the server if not already running

      string uuid = System.Guid.NewGuid().ToString();

      lock (_instance._completionCallbacks)
      {
         _instance._completionCallbacks.Add(uuid, new callbackData(completionCallback, datatype, true));   // this will throw an exception if the given uuid already exists (which is probably a good thing)
      }

      string jsonText = JsonConvert.SerializeObject(new { cmd = cmd, parms = parms, _id_ = uuid },
                                                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
      _instance.SendTextData(jsonText);

      return uuid;
   }

   /// <summary>
   /// Removes the callback for the given command. This does *NOT* tell the server to stop sending results for the command that was
   /// issued aganst the given uuid; you should first invoke whatever matching "stop" command for the previous command, if applicable.
   /// </summary>
   /// <param name="uuid">The UUID value from IssueCommand</param>
   public static void RemoveCallback(string uuid)
   {
      lock (_instance._completionCallbacks)
      {
         _instance._completionCallbacks.Remove(uuid);
      }
   }

   /// <summary>
   /// Issues the given command and the parms with the already-existing uuid and removes any callbacks logged against it.
   /// </summary>
   /// <param name="uuid">The UUID value from IssueCommand</param>
   /// <param name="cmd">The "stop" command to be sent to the web socket interface that is appropriate to the command.</param>
   /// <param name="parms">An object (typically an anonmyous type) of parms to be sent. These will be converted to a JSON object string.</param>
   public static void IssueStopAndRemove(string uuid, string cmd, object parms)
   {
      RemoveCallback(uuid);

      if (_instance._ws != null)
      {
         string jsonText = JsonConvert.SerializeObject(new { cmd = cmd, parms = parms, _id_ = uuid },
                                                       new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
         _instance.SendTextData(jsonText);
      }
   }

   /// <summary>
   /// Issues the given command and the parms with the already-existing uuid and removes any callbacks logged against it.
   /// </summary>
   /// <param name="uuid">The UUID value from IssueCommand</param>
   /// <param name="cmd">The "stop" command to be sent to the web socket interface that is appropriate to the command.</param>
   public static void IssueStopAndRemove(string uuid, string cmd)
   {
      RemoveCallback(uuid);

      if (_instance._ws != null)
      {
         string jsonText = JsonConvert.SerializeObject(new { cmd = cmd, _id_ = uuid },
                                                       new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
         _instance.SendTextData(jsonText);
      }
   }

   /// <summary>
   /// Some websocket interface commands support having the current command being changed or "reissued". Only use this if the command supports
   /// this functionality and you know what the ramifications of using it are.
   /// </summary>
   /// <param name="uuid">The UUID value from IssueCommand</param>
   /// <param name="cmd">Should be the same command as previously sent to IssueCommand.</param>
   /// <param name="parms">An object (typically an anonmyous type) of parms to be sent. These will be converted to a JSON object string.</param>
   public static void ReissueCommand(string uuid, string cmd, object parms)
   {
      Start();
      string jsonText = JsonConvert.SerializeObject(new { cmd = cmd, parms = parms, _id_ = uuid },
                                                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
      _instance.SendTextData(jsonText);
   }

   /// <summary>
   /// Some websocket interface commands support having the current command being changed or "reissued". Only use this if the command supports
   /// this functionality and you know what the ramifications of using it are.
   /// </summary>
   /// <param name="uuid">The UUID value from IssueCommand</param>
   /// <param name="cmd">Should be the same command as previously sent to IssueCommand.</param>
   public static void ReissueCommand(string uuid, string cmd)
   {
      Start();
      string jsonText = JsonConvert.SerializeObject(new { cmd = cmd, _id_ = uuid },
                                                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
      _instance.SendTextData(jsonText);
   }

   //////////////////////////////////////////////////////////////////////////

   #region Helper Functions
   // Helper functions to let you convert the key:value pair object into something more useful.

   // This is used instead of Convert.ToBoolean because that function only accepts case-sensitive True or False, not JSON-compatible values or numbers 
   public static bool toBool(object o)
   {
      string s = o as string;
      if (s == null)
         s = o.ToString();

      s = s.ToLower();
      if (s == "true" || s=="checked")
         return true;
      if (s == "false" || s == "unchecked")
         return false;

      try
      {
         return Convert.ToInt32(s) != 0;
      }
      catch (System.Exception)
      {
         return false;
      }
   }

   public static float toSingle(object o)
   {
      string s = o as string;
      if (s == null)
         s = o.ToString();

      try
      {
         return Convert.ToSingle(s);
      }
      catch (System.Exception)
      {
         return 0.0f;
      }
   }

   public static int toInt(object o)
   {
      return (int)toSingle(o);   // we do it this way because Convert.ToInt doesn't handle decimals
   }

   public static string toString(object o)
   {
      try
      {
         return o.ToString();
      }
      catch (System.Exception)
      {
         return "";      	
      }
   }

   public static string toLowerString(object o)
   {
      return o.ToString().ToLower();
   }

   #endregion Helper Functions

   //////////////////////////////////////////////////////////////////////////

   private static readonly ForceSocket _instance = new ForceSocket();

   private WebSocket _ws;
   private string _ws_url = "ws://127.0.0.1:61715/$wsi"; // this is the default url, and will be used by Connect if not explicitly set
   private List<string> _commandBacklog = new List<string>();

   private class callbackData
   {
      public Action<string, object> callback;
      public bool oneshot = true;
      public bool hasDatatype = false;
      public Type datatype;

      public callbackData(Action<string, object> cb, bool perodic)
      {
         callback = cb;
         hasDatatype = false;
         oneshot = !perodic;
      }

      public callbackData(Action<string, object> cb, Type dt, bool perodic)
      {
         callback = cb;
         datatype = dt;
         hasDatatype = true;
         oneshot = !perodic;
      }
   }

   private Dictionary<string, callbackData> _completionCallbacks = new Dictionary<string, callbackData>();

   //////////////////////////////////////////////////////////////////////////

   private ForceSocket()
   {
      string wsurl = CommandLineParms.String("wsserver");
      if (wsurl != "")
         _ws_url = wsurl;
   }

   private void Connect()
   {
      if (_ws==null)
      {
         Debug.Log("ForceSocket: connecting to " + _ws_url);

         _ws = new WebSocket(_ws_url);
         _ws.OnOpen += Ws_OnOpen;
         _ws.OnClose += Ws_OnClose;
         _ws.OnMessage += Ws_OnMessage;
         _ws.ConnectAsync();
      }
   }


   private void Disconnect()
   {
      if (_ws != null)
      {
         _ws.Close();
         _ws.OnOpen -= Ws_OnOpen;
         _ws.OnClose -= Ws_OnClose;
         _ws.OnMessage -= Ws_OnMessage;
         ((IDisposable)_ws).Dispose();
         _ws = null;
      }

      lock (_instance._completionCallbacks)
      {
         _completionCallbacks.Clear();
      }
   }

   private void SendTextData(string jsonText)
   {
      if (IsConnected)
         _ws.Send(jsonText);
      else
      {
         lock (_commandBacklog)
         {
            _commandBacklog.Add(jsonText);   // will get sent when the connection is finally open
         }
      }
   }

   // The socket is now open; check if we have any command backlogs that need to be sent.
   private void Ws_OnOpen(object sender, EventArgs e)
   {
      Debug.Log("ForceSocket: connection established");

      lock (_commandBacklog)
      {
         _commandBacklog.ForEach(delegate (String txt)
         {
            _ws.Send(txt);
         });

         _commandBacklog.Clear();
      }
   }

   private void Ws_OnClose(object sender, CloseEventArgs e)
   {
      Debug.Log("ForceSocket: connection closed");
   }

   // Process the value; if there is an .id value in the incoming stream then look up the matching callback and invoke it.
   // If the return has a _nofuther_ key set, then the callback list has the id removed.
   private void Ws_OnMessage(object sender, MessageEventArgs e)
   {
      var obj1 = JsonConvert.DeserializeAnonymousType(e.Data, new { _id_ = "", _nofurther_=false },
                                                      new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore });

      callbackData cb;
      lock (_instance._completionCallbacks)
      {
         if (_completionCallbacks.TryGetValue(obj1._id_, out cb))
         {
            if (cb.hasDatatype)
               cb.callback(e.Data, JsonConvert.DeserializeObject(e.Data, cb.datatype, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore }));
            else
            {
               Dictionary<string, object> d = JsonConvert.DeserializeObject<Dictionary<string, object>>(e.Data);
               d.Remove("_id_"); // don't want to clutter the callback with an internal id value
               cb.callback(e.Data, d);
            }
            if (obj1._nofurther_ || cb.oneshot)
               _instance._completionCallbacks.Remove(obj1._id_);
         }
      }
   }
}
