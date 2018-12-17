using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// If you don't have access to the PHP file that you're going to put on your server,
/// I would highly recommend setting up a new email account to catch these reports to
/// prevent your main email account from getting spammed. If you have access to the PHP
/// file you can change the email that will receive these reports.
/// </summary>
public class CrashReporter
{
   /// <summary>
   /// Where to send the crash report text to; by default will use the set server line
   /// </summary>
   public static string ReportURL = "";   // typically gets built to http://127.0.0.1:61710/$playercrash

   /// <summary>
   /// This message will appear as part of the report email. Set this to things like current level, position, etc.
   /// </summary>
   public static string AdditionalMessageText = "";

   /// <summary>
   /// Should Debug.Log calls be included in the error reports? (this can be quite noisy so by default they are turned off)
   /// </summary>
   public static bool IncludeDebugLogCalls = false;

   /// <summary>
   /// Should Debug.LogWarning be included calls in the error reports?
   /// </summary>
   public static bool IncludeDebugLogWarningCalls = true;

   /// <summary>
   /// Should Debug.LogError be included calls in the error reports?
   /// </summary>
   public static bool IncludeDebugLogErrorCalls = true;

   /// <summary>
   /// How many log messages prior to a crash should be recorded. Older messages are tossed out
   /// </summary>
   public static int LogDepth = 500;

   public static event Action<string, string> AppCrashed;   // this is called whenever an exception event happens that gets logged

   /////////////////////////////////////////////

   private List<string> outputList = new List<string>();

   private static readonly CrashReporter _instance = new CrashReporter();
   private bool started = false;

   /////////////////////////////////////////////

   private CrashReporter()
   {
      // if the ReportUrl is empty then try to pull the root server from the command line
      if (string.IsNullOrEmpty(ReportURL))
      {
         string serverRoot = CommandLineParms.String("server");   // this should be something like http://127.0.0.1:61710
         if (string.IsNullOrEmpty(serverRoot))
         {
            Debug.LogWarning("CrashReporter URL not set, reports will not be enabled.");
            return;
         }

         ReportURL = serverRoot += "/$playercrash";
         // the $playercrash handler parses and redirects to "http://bertec.com/_WBServices/unitycrashreport.php"
      }

      Application.logMessageReceivedThreaded += HandleLog;
   }

   ~CrashReporter()
   {
      Application.logMessageReceivedThreaded -= HandleLog;
   }

   public static void Init()
   {
      // all this does is instance the constructor which brings things up.
      if (!_instance.started)
         _instance.started = true;
   }

   void HandleLog(string logString, string stackTrace, LogType logType)
   {
      lock (_instance)
      {
         try
         {
            //If we disabled the current LogType, don't add it to our output.
            if ((logType == LogType.Log && !IncludeDebugLogCalls) ||
                (logType == LogType.Warning && !IncludeDebugLogWarningCalls) ||
                (logType == LogType.Error && !IncludeDebugLogErrorCalls))
               return;

            string systemStackTraceStr = new System.Diagnostics.StackTrace(2,true).ToString(); // skip the last two items which is this handler and its caller

            //Insert the log to the front of our list
            outputList.Insert(
                0,
                "---------------------------------------------------\n" +
                logType.ToString() + " at " + Time.realtimeSinceStartup.ToString() + " seconds\n" +
                logString + "\n" +
                stackTrace + "\n" + systemStackTraceStr);


            //Trim our list so it only keeps track of the X most recent log calls
            if (outputList.Count > LogDepth)
            {
               //Remove everything after LogDepth
               outputList.RemoveRange(LogDepth, outputList.Count - LogDepth);
            }

            //When we hit an Exception we want to send our error report
            if (logType == LogType.Exception)
            {
               SendDebugToServer();
               AppCrashed(logString, stackTrace);
            }
         }
         catch (System.Exception )
         {

         }
      }
   }


   void SendDebugToServer()
   {
      string textToSend = Application.companyName + " " + Application.productName + " Crash Log:\n\n";
      textToSend += "Application Version: " + Application.version + "\n";
      textToSend += "Unity Version: " + Application.unityVersion + "\n";
      textToSend += "Platform: " + Application.platform.ToString() + "\n";
      textToSend += "Data folder: " + Application.dataPath + "\n";

      if (!string.IsNullOrEmpty(AdditionalMessageText))
         textToSend += "\nMessage Text:\n" + AdditionalMessageText + "\n";

      textToSend += "\nLast Recorded Log Outputs:\n\n";
      
      for (int i = 0; i < outputList.Count; i++)
         textToSend += outputList[i] + "\n";

      outputList.Clear();

      System.Net.ServicePointManager.Expect100Continue = false;

      WWW www1 = new WWW(ReportURL, System.Text.ASCIIEncoding.ASCII.GetBytes(textToSend));
      //yield return www1;
   }
}
