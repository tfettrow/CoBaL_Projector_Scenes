using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// This class connects to the protocol-command web socket interface and allows you to issue commands over that.
/// Some protocols support the ProtocolCommand interface, allowing for commands to be sent to them. These commands
/// can be simple things (ex: SetScore) with no response or more complex functions that return a single future result
/// (ex: GetScoreForAgeGroup). Other commands return *periodic* updates, similar to the COPDataReader and friends.
/// These periodic updates can be considered either event signals (ex: some condition has changed) or continual
/// data updates (ex: live calculations against the data flow)
/// </summary>
public class ProtocolRPC
{
   /// <summary>
   /// Issues a command with no completion callback. Aka InvokeMethod
   /// </summary>
   /// <param name="command">the protocol-specific command to issue</param>
   public void IssueCommand(string command)
   {
      ForceSocket.Start();
      ForceSocket.IssueCommand(ForceSocket.KnownCommands.ProtocolCommand, concatCommandToParms(command, null));
   }

   // 
   /// <summary>
   /// Issues a command with parms and no completion callback. Aka InvokeMethod
   /// </summary>
   /// <param name="command">the protocol-specific command to issue</param>
   /// <param name="parms">parms for the command</param>
   public void IssueCommand(string command, object parms)
   {
      ForceSocket.Start();
      ForceSocket.IssueCommand(ForceSocket.KnownCommands.ProtocolCommand, concatCommandToParms(command, parms));
   }

   /// <summary>
   /// Issues a command with a completion callback that takes a generic object data type. 
   /// </summary>
   /// <param name="command">the protocol-specific command to issue</param>
   /// <param name="completionCallback">The method that should be invoked when the server returns back a result. The method will be called
   /// with the raw JSON string and an generic object parsed from the JSON string.</param>
   /// <returns>The UUID that was assigned to this command. Can be used for internal bookkeeping or for later use by RemoveCallback.</returns>
   public string IssueCommand(string command, Action<string, object> completionCallback)
   {
      ForceSocket.Start();
      return ForceSocket.IssueCommand(ForceSocket.KnownCommands.ProtocolCommand, concatCommandToParms(command, null), completionCallback);
   }

   /// <summary>
   /// Issues a command with a completion callback that takes a generic object data type. 
   /// </summary>
   /// <param name="command">the protocol-specific command to issue</param>
   /// <param name="parms">parms for the command</param>
   /// <param name="completionCallback">The method that should be invoked when the server returns back a result. The method will be called
   /// with the raw JSON string and an generic object parsed from the JSON string.</param>
   /// <returns>The UUID that was assigned to this command. Can be used for internal bookkeeping or for later use by RemoveCallback.</returns>
   public string IssueCommand(string command, object parms, Action<string, object> completionCallback)
   {
      ForceSocket.Start();
      return ForceSocket.IssueCommand(ForceSocket.KnownCommands.ProtocolCommand, concatCommandToParms(command, parms), completionCallback);
   }

   /// <summary>
   /// Issues a command with a completion callback that takes a specific data type. 
   /// </summary>
   /// <param name="command">the protocol-specific command to issue</param>
   /// <param name="completionCallback">The method that should be invoked when the server returns back a result. The method will be called
   /// with the raw JSON string and object of the given dataType parsed from the JSON string.</param>
   /// <param name="datatype">The Type that the JSON parser should convert the incoming result data into. This new data object will be passed
   /// in the completionCallback's object parm</param>
   /// <returns>The UUID that was assigned to this command. Can be used for internal bookkeeping or for later use by RemoveCallback.</returns>
   public string IssueCommand(string command, Action<string, object> completionCallback, Type datatype)
   {
      ForceSocket.Start();
      return ForceSocket.IssueCommand(ForceSocket.KnownCommands.ProtocolCommand, concatCommandToParms(command, null), completionCallback, datatype);
   }
   
   /// <summary>
   /// Issues a command with a completion callback that takes a specific data type. 
   /// </summary>
   /// <param name="command">the protocol-specific command to issue</param>
   /// <param name="parms">parms for the command</param>
   /// <param name="completionCallback">The method that should be invoked when the server returns back a result. The method will be called
   /// with the raw JSON string and object of the given dataType parsed from the JSON string.</param>
   /// <param name="datatype">The Type that the JSON parser should convert the incoming result data into. This new data object will be passed
   /// in the completionCallback's object parm</param>
   /// <returns>The UUID that was assigned to this command. Can be used for internal bookkeeping or for later use by RemoveCallback.</returns>
   public string IssueCommand(string command, object parms, Action<string, object> completionCallback, Type datatype)
   {
      ForceSocket.Start();
      return ForceSocket.IssueCommand(ForceSocket.KnownCommands.ProtocolCommand, concatCommandToParms(command, parms), completionCallback, datatype);
   }

   /// <summary>
   /// Issues a command with a periodic completion callback that takes a generic object data type. 
   /// </summary>
   /// <param name="command">the protocol-specific command to issue</param>
   /// <param name="parms">parms for the command</param>
   /// <param name="completionCallback">The method that should be invoked when the server returns back a result. The method will be called
   /// with the raw JSON string and object of the given dataType parsed from the JSON string.</param>
   /// <param name="datatype">The Type that the JSON parser should convert the incoming result data into. This new data object will be passed
   /// in the completionCallback's object parm</param>
   /// <returns>The UUID that was assigned to this command. Can be used for internal bookkeeping or for later use by RemoveCallback.</returns>
   public string StartListener(string command, object parms, Action<string, object> completionCallback)
   {
      ForceSocket.Start();
      return ForceSocket.IssuePerodicResponseCommand(ForceSocket.KnownCommands.ProtocolCommand, concatCommandToParms(command, parms), completionCallback);
   }

   /// <summary>
   /// Issues a command with a periodic completion callback that takes a specific data type. 
   /// </summary>
   /// <param name="command">the protocol-specific command to issue</param>
   /// <param name="parms">parms for the command</param>
   /// <param name="completionCallback">The method that should be invoked when the server returns back a result. The method will be called
   /// with the raw JSON string and object of the given dataType parsed from the JSON string.</param>
   /// <param name="datatype">The Type that the JSON parser should convert the incoming result data into. This new data object will be passed
   /// in the completionCallback's object parm</param>
   /// <returns>The UUID that was assigned to this command. Can be used for internal bookkeeping or for later use by RemoveCallback.</returns>
   public string StartListener(string command, object parms, Action<string, object> completionCallback, Type datatype)
   {
      ForceSocket.Start();
      return ForceSocket.IssuePerodicResponseCommand(ForceSocket.KnownCommands.ProtocolCommand, concatCommandToParms(command, parms), completionCallback, datatype);
   }

   /// <summary>
   /// Stops the given periodic response by issuing a stop command to the server and removing the registered callback from the table.
   /// </summary>
   /// <param name="uuid"></param>
   /// <returns></returns>
   public void StopListener(string uuid)
   {
      ForceSocket.IssueStopAndRemove(uuid, ForceSocket.KnownCommands.StopProtocolCommand);
   }

   // Take the given command string and inject it into the parms object under the Command keyword.
   private object concatCommandToParms(string command, object parms)
   {
      JObject job = JObject.FromObject(parms==null ? new object() : parms);   // FromObject doesn't like null so fake it
      job["Command"] = command;
      return job.ToObject(typeof(object)); // should return a generic object
   }
}
