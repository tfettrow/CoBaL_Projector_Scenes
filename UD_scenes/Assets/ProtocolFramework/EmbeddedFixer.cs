// Use this class to address a problem with embedding a window of an app that was last launched on a different display; Unity wants to
// reposition this "off screen" from the hosted window, resulting in no visuals at all. This code will only kick in if the app was launched
// with the embedding parm (-parentHWND) *and* the current user's pref "UnitySelectMonitor" set to non-zero.
// This is only a safeguard against the main host app not repositioning the Unity guest app's child window.

using UnityEngine;

static public class EmbeddedFixer
{
   static public void Fix()
   {
      // Check if the program was launched with an embedded window parm
      if (CommandLineParms.isSet("parentHWND"))
      {
         int dispNum = PlayerPrefs.GetInt("UnitySelectMonitor");

         if (dispNum != 0)
         {
            Debug.Log("App launched as embedded window with a non-primary display, forcing display size and position back to primary");

            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            // We can reset this back to zero which will skip running this code next time, but it doesn't appear to cause any problems to leave it alone.
            //PlayerPrefs.SetInt("UnitySelectMonitor", 0); // Select monitor 1

            Display.displays[0].Activate(screenWidth, screenHeight, 0);
            Display.displays[0].SetParams(screenWidth, screenHeight, 0, 0);
         }
      }
   }
}
