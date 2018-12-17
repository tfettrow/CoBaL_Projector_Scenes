// This static class makes sure that certain Unity settings are set even if they aren't turned on in the project settings.
// Some of these items can be overridden using the Command Line Parms.
//
// Note that some suggested settings cannot be set from here, since they are Editor only. These are:
// PlayerSettings -> Other Settings
//    Rendering Path (forward)
//    Auto Graphics Api (off, limit to only DirectX9)
//    Static Batching (on)
//    Dynamic Batching (on)
//    Graphic Jobs (on - ymmv)
//    Disable HW Stats (on)
//    API Compatibility (Net 2.0, not the subset)


using UnityEngine;

static public class EnforceDefaults
{
   static public void Enforce()
   {
      // The target frame rate will try to hit what is passed, but it will typically float above that (ex: 120 is about 121 or 122, 500 is around 526, etc)
      // Setting this to -1 will turn this off and let you achieve stupidly-high frame rates in the thousands but will also drive the system pretty hard.
      // 120 fps == ~~ 8.2ms
      // 200 fps == ~~ 4.9ms
      // 300 fps == ~~ 3.2ms
      // 400 fps == ~~ 2.4ms
      // 500 fps == ~~ 1.9ms
      // 1000 fps == ~~0.9ms -> this appears to be the max, anything higher seems to top out here
      // 3200+ ~~ 0.3ms (only possible if targetFrameRate set to -1)

      QualitySettings.vSyncCount = 0;  // disable locking to 60 or 30 hz; otherwise targetFrameRate is ignored
      Application.targetFrameRate = CommandLineParms.Int("framerate", 120); // can be overridden by passing -framerate NNN

      // We always want the protocol to run in the background; otherwise when the main app gets focus we stop
      Application.runInBackground = true;
   }
}

