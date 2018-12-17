// This component class when attached to a "Camera Container" or "Player Stick" object that has a Main Camera will use the Omnity projection code
// to add (or not add) the dome projection script.

using UnityEngine;


//////////////////////////////////////////////////////////////////////////

public static class ProjectionCamera
{
   public static Omnity Attach(Camera camObj)
   {
      GameObject go = camObj.gameObject;
      Omnity omi = go.AddComponent<Omnity>();
      // This bit is needed to get the screen to actually update properly; without it we don't see anything
      omi.ConfigLoaded += (oc) =>
      {
         oc.forceRefresh = true;
      };
 
      return omi;
   }

   public static void Detach(Camera camObj, Omnity existingOmnity)
   {
      existingOmnity.UnloadOmnity();
      Object.Destroy(existingOmnity);
   }
}

//////////////////////////////////////////////////////////////////////////

public class CameraContainer : MonoBehaviour
{
   public bool UseProjection = true;

   // Use this for initialization
   void Start()
   {
      UseProjection = CommandLineParms.Bool("UseProjection", UseProjection);  // allow the command line to override it
      if (UseProjection)
      {
         _cacheOmnity = ProjectionCamera.Attach(GetMainCamera());
      }
   }

   internal Camera _cachedCam = null;
   internal Omnity _cacheOmnity = null;

   public Camera GetMainCamera()
   {
      if (_cachedCam==null)
         _cachedCam = GetComponentInChildren<Camera>();
      return _cachedCam;
   }
		
}

