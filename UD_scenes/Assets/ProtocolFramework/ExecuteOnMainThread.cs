using System;
using System.Collections.Generic;

/// <summary>
/// This is a static class that provides a handy functionality to resolve the gap with processing functions from one thread into the
/// other. By using ExecuteOnMainThread.AddAction with the function in question and having your main loop code call 
/// ExecuteOnMainThread.ProcessActions it will "stack" the function calls until the main thread dequeues them.
/// This is useful when processing incoming TCP or WebSocket data and needing to use a function that would manipulate a GUI or 3d object.
/// </summary>
public static class ExecuteOnMainThread
{
   /// <summary>
   /// Adds the passed Action object to the queue to execute later on the main thread.
   /// Make sure your main Update() loop calls ExecuteOnMainThread.ProcessActions()
   /// </summary>
   /// <param name="act">The action to perform. Ex: () => { CommandAddObject(connectionID, keyValuePairs); }</param>
   public static void AddAction(Action act)
   {
      lock (q)
      {
         q.Enqueue(act);
      }
   }

   /// <summary>
   /// Processes the Action queue and executes them. This should be called from your main Update() or FixedUpdate() function.
   /// </summary>
   public static void ProcessActions()
   {
      lock (q)
      {
         while (q.Count > 0)
         {
            q.Dequeue().Invoke();
         }
      }
   }

   /// <summary>
   /// Removes all queue actions from the main thread execute queue.
   /// </summary>
   public static void ClearActions()
   {
      lock (q)
      {
         q.Clear();
      }
   }

   ///////////////////////////////////////////////////

   internal readonly static Queue<Action> q = new Queue<Action>();

}
