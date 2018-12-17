#region PLATFORM_DEFINES

#if UNITY_2_6   //Platform define for the major version of Unity 2.6.
#define NO_SCREENHEIGHT_SUPPORT
#define USE_CAMERA_NEAR_FAR
#define NO_CURSORCLASS_SUPPORT
#define OLDSHADOWSYNTAX
#define OLD_CreateFromMemoryImmediate
#define OLDOBJECTFINDER
#define CAN_READSHADER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning  "Unity 26"
#elif UNITY_3_0 //Platform define for the major version of Unity 3.0.
#define NO_SCREENHEIGHT_SUPPORT
#define USE_CAMERA_NEAR_FAR
#define NO_CURSORCLASS_SUPPORT
#define OLD_CreateFromMemoryImmediate
#define OLDSHADOWSYNTAX
#define CAN_READSHADER
#define OLDOBJECTFINDER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning  "Unity 30"
#elif UNITY_3_1 //Platform define for major version of Unity 3.1.
#define USE_CAMERA_NEAR_FAR
#define NO_CURSORCLASS_SUPPORT
#define OLDSHADOWSYNTAX
#define OLD_CreateFromMemoryImmediate
#define CAN_READSHADER
#define OLDOBJECTFINDER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning ( "Unity 31"
#elif UNITY_3_2 //Platform define for major version of Unity 3.2.
#define USE_CAMERA_NEAR_FAR
#define NO_CURSORCLASS_SUPPORT
#define OLDSHADOWSYNTAX
#define OLD_CreateFromMemoryImmediate
#define CAN_READSHADER
#define OLDOBJECTFINDER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning  "Unity 32"
#elif UNITY_3_3 //Platform define for major version of Unity 3.3.
#define USE_CAMERA_NEAR_FAR
#define NO_CURSORCLASS_SUPPORT
#define OLDSHADOWSYNTAX
#define OLD_CreateFromMemoryImmediate
#define CAN_READSHADER
#define OLDOBJECTFINDER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning  "Unity 33"
#elif UNITY_3_4 //Platform define for major version of Unity 3.4.
#define USE_CAMERA_NEAR_FAR
#define NO_CURSORCLASS_SUPPORT
#define OLDSHADOWSYNTAX
#define OLD_CreateFromMemoryImmediate
#define CAN_READSHADER
#define OLDOBJECTFINDER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning  "Unity 34"
#elif UNITY_3_5 //Platform define for major version of Unity 3.5.
#define USE_CAMERA_NEAR_FAR
#define NO_CURSORCLASS_SUPPORT
#define OLDSHADOWSYNTAX
#define OLD_CreateFromMemoryImmediate
#define CAN_READSHADER
#define OLDOBJECTFINDER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning  "Unity 35"
#elif UNITY_4_0 //Platform define for major version of Unity 4.0.
#define USE_CAMERA_NEAR_FAR
#define NO_CURSORCLASS_SUPPORT
#define OLDSHADOWSYNTAX
#define CAN_READSHADER
#define OLDOBJECTFINDER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning  "Unity 40"
#elif UNITY_4_1 //Platform define for major version of Unity 4.1.
#define USE_CAMERA_NEAR_FAR
#define NO_CURSORCLASS_SUPPORT
#define OLDSHADOWSYNTAX
#define CAN_READSHADER
#define OLDOBJECTFINDER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning  "Unity 41"
#elif UNITY_4_2 //Platform define for major version of Unity 4.2.
#define NO_CURSORCLASS_SUPPORT
#define OLDSHADOWSYNTAX
#define CAN_READSHADER
#define OLDOBJECTFINDER
#define RENDERTEXTURE_ANTIALIASING_SUPPORT
#define CAN_READSHADER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

#elif UNITY_4_3 //Platform define for major version of Unity 4.3.
//#warning  "Unity 43"
#define RENDERTEXTURE_ANTIALIASING_SUPPORT
#define OLDSHADOWSYNTAX
#define NO_CURSORCLASS_SUPPORT
#define CAN_READSHADER
#define OLDOBJECTFINDER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

#elif UNITY_4_5 //Platform define for major version of Unity 4.5.
//#warning  "Unity 45"
#define RENDERTEXTURE_ANTIALIASING_SUPPORT
#define NO_CURSORCLASS_SUPPORT
#define OLDSHADOWSYNTAX
#define CAN_READSHADER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

#elif UNITY_4_6 //Platform define for major version of Unity 4.6.
#define ENCODE_TO_JPG_SUPPORT
#define RENDERTEXTURE_ANTIALIASING_SUPPORT
#define OLDSHADOWSYNTAX
#define NO_CURSORCLASS_SUPPORT
#define CAN_READSHADER
#define AB_CREATEFROMMEMORY
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning  "Unity 46"
#elif UNITY_5_0
#define ENCODE_TO_JPG_SUPPORT
#define RENDERTEXTURE_ANTIALIASING_SUPPORT
#define CAN_READSHADER
#define AB_CREATEFROMMEMORY
#define OLDSHADOWSYNTAX2
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning  "Unity 50"
#elif UNITY_5_1
#define ENCODE_TO_JPG_SUPPORT
#define RENDERTEXTURE_ANTIALIASING_SUPPORT
#define CAN_READSHADER
#define AB_CREATEFROMMEMORY
#define OLDSHADOWSYNTAX2
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning  "Unity 51"
#elif UNITY_5_2
#define ENCODE_TO_JPG_SUPPORT
#define RENDERTEXTURE_ANTIALIASING_SUPPORT
#define AB_CREATEFROMMEMORY
#define OLDSHADOWSYNTAX2
#define INVERTCULLING_NOTSUPPORTED
#define TEXTEDITOR_TEXT_NOTSUPPORTED

//#warning  "Unity 52"
#elif UNITY_5_3
#define ENCODE_TO_JPG_SUPPORT
#define RENDERTEXTURE_ANTIALIASING_SUPPORT
#define OLDSHADOWSYNTAX2

//#warning  "Unity 53"
#elif UNITY_5_4
#define ENCODE_TO_JPG_SUPPORT
#define RENDERTEXTURE_ANTIALIASING_SUPPORT
//#warning  "Unity 54"
#else
#define ENCODE_TO_JPG_SUPPORT
#define RENDERTEXTURE_ANTIALIASING_SUPPORT
//#warning  "ALL NEWER"
#endif

#if UNITY_WEBPLAYER
#define LOADSAVE_NOTSUPORTED
#else
#endif

#endregion PLATFORM_DEFINES

using System;
using UnityEngine;

static public class OmnityPlatformDefines
{
#if false
    static public System.Func<bool> LoadSaveSupported = null;

    static public System.Action<RenderTexture, int> SetAntiAliasing = null;

    static public System.Action<Camera, bool> SetOcclusionCulling = null;

    static public System.Action<MeshRenderer> TurnOffShadows = null;

    public delegate void GetCameraWidthHeightDelegate(ref float width, ref float height);

    public static GetCameraWidthHeightDelegate GetCameraWidthHeight = null;//(ref float width, ref float height)

    public static System.Action<Renderer> DisableShadowsAndLightProbes = null;

    public static System.Action<OmnityPerspectiveMatrix, Camera, float> CopyClipPlanes = null; //(OmnityPerspectiveMatrix omnityPerspectiveMatrix, Camera sourceCamera, float nearClipScaleToMakeSmaller)

    static public System.Func<bool> EncodeToJPGSupport = null;

    static public System.Func<Texture2D, byte[]> EncodeToJPG = null;

    public static System.Func<bool> CanReadShader = null;

    internal static System.Action TryReadShader = null;

    static public System.Func<bool> GetCursor_visible = null;

    static public System.Action<bool> SetCursor_visible = null;

    //public static T[] FindObjectsOfType<T>() where T : UnityEngine.Object;

    public static System.Func<System.Type, UnityEngine.Object> FindObjectOfType = null;
}


static public class LocalOmnityManager {

    public static void Connect() {
        OmnityPlatformDefines.LoadSaveSupported = LocalOmnityManager.LoadSaveSupported;
        OmnityPlatformDefines.SetAntiAliasing = LocalOmnityManager.SetAntiAliasing;
        OmnityPlatformDefines.SetOcclusionCulling = LocalOmnityManager.SetOcclusionCulling;
        OmnityPlatformDefines.TurnOffShadows = LocalOmnityManager.TurnOffShadows;
        OmnityPlatformDefines.GetCameraWidthHeight = LocalOmnityManager.GetCameraWidthHeight;
        OmnityPlatformDefines.DisableShadowsAndLightProbes = LocalOmnityManager.DisableShadowsAndLightProbes;
        OmnityPlatformDefines.CopyClipPlanes = LocalOmnityManager.CopyClipPlanes;
        OmnityPlatformDefines.EncodeToJPGSupport = LocalOmnityManager.EncodeToJPGSupport;
        OmnityPlatformDefines.EncodeToJPG = LocalOmnityManager.EncodeToJPG;
        OmnityPlatformDefines.CanReadShader = LocalOmnityManager.CanReadShader;
        OmnityPlatformDefines.TryReadShader = LocalOmnityManager.TryReadShader;
        OmnityPlatformDefines.GetCursor_visible = LocalOmnityManager.GetCursor_visible;
        OmnityPlatformDefines.SetCursor_visible = LocalOmnityManager.SetCursor_visible;
        OmnityPlatformDefines.FindObjectOfType = LocalOmnityManager.FindObjectOfType;
    }
#endif

   static public bool LoadSaveSupported()
   {
#if LOADSAVE_NOTSUPORTED
            return false;
#else
      return true;
#endif
   }

   static public void SetAntiAliasing(RenderTexture rt, int antiAliasing)
   {
#if RENDERTEXTURE_ANTIALIASING_SUPPORT
      rt.antiAliasing = (int)antiAliasing;
#endif
   }

   static public void SetOcclusionCulling(Camera myCamera, bool oc)
   {
#if !OLDSHADOWSYNTAX
      myCamera.useOcclusionCulling = oc;
#endif
   }

   static public void TurnOffShadows(MeshRenderer renderer)
   {
#if OLDSHADOWSYNTAX
        renderer.castShadows = false;
#elif OLDSHADOWSYNTAX2
        renderer.useLightProbes = false;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
#else
      renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
      renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
#endif
   }

   public static void GetCameraWidthHeight(ref float width, ref float height)
   {
#if NO_SCREENHEIGHT_SUPPORT
               height = myFinalPassCameraProxy.camera.GetScreenHeight();
               width = myFinalPassCameraProxy.camera.GetScreenWidth();
#else
      height = Screen.height;
      width = Screen.width;
#endif
   }

   public static void DisableShadowsAndLightProbes(Renderer renderer)
   {
#if UNITY_HASLIGHTPROBES
      // figure out shadow casing and light probes
      renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      renderer.receiveShadows = false;
      renderer.useLightProbes = false;
#endif
   }

   public static void CopyClipPlanes(OmnityPerspectiveMatrix omnityPerspectiveMatrix, Camera sourceCamera, float nearClipScaleToMakeSmaller)
   {
#if USE_CAMERA_NEAR_FAR
      omnityPerspectiveMatrix.near = sourceCamera.near * nearClipScaleToMakeSmaller;
      omnityPerspectiveMatrix.far = sourceCamera.far;
#else
      omnityPerspectiveMatrix.near = sourceCamera.nearClipPlane * nearClipScaleToMakeSmaller;
      omnityPerspectiveMatrix.far = sourceCamera.farClipPlane;
#endif
   }

   static public bool EncodeToJPGSupport()
   {
#if ENCODE_TO_JPG_SUPPORT
      return true;
#else
            return false;
#endif
   }

   static public byte[] EncodeToJPG(Texture2D tex)
   {
#if ENCODE_TO_JPG_SUPPORT
      return tex.EncodeToJPG();
#else
        Debug.LogError("No support for Encode To JPG in this version of unity");
        return null;
#endif
   }

   public static bool CanReadShader()
   {
#if CAN_READSHADER
            return true;
#else
      return false;
#endif
   }

   internal static bool TryReadShader(string shaderFileName, UnityEngine.Renderer renderer)
   {
#if CAN_READSHADER
        try {
            System.IO.StreamReader streamReader = new System.IO.StreamReader(shaderFileName);
            renderer.sharedMaterial = new Material(streamReader.ReadToEnd());
            streamReader.Close();
            return true;
        } catch (System.Exception e) {
            Debug.Log("COULD NOT LOAD SHADER FILE " + shaderFileName + " ERROR CODE FOLLOWS:\r\n" + e + "\r\nUSING FALLBACK");

            return false;
        }
#else
      Debug.Log("COULD NOT LOAD SHADER FILE " + shaderFileName + " not supported in this unity " + renderer.ToString());
      return false;
#endif
   }

   /*
   public static T[] FindObjectsOfType<T>() where T : UnityEngine.Object {
#if OLDOBJECTFINDER
       return GameObject.FindObjectsOfType(typeof(T)) as T[];
#else
       return GameObject.FindObjectsOfType<T>();
#endif
   }
   */

   static public bool GetCursor_visible()
   {
#if NO_CURSORCLASS_SUPPORT
        return Screen.showCursor;
#else
      return Cursor.visible;
#endif
   }

   static public void SetCursor_visible(bool val)
   {
#if NO_CURSORCLASS_SUPPORT
        Screen.showCursor = val;
#else
      Cursor.visible = val;
#endif
   }

   // this function is for finding singletons.
   // unity has a function called GameObject.FindObjectOfType(T) that does it
   // but it changed over the years to GameObject.FindObjectOfType<T>()
   // so we have to use reflection to get it.  We can supply via pointer since its impossible to make a delegate that is generic
   // so this code forces the new versions of unity to use the old prototype
   public static UnityEngine.Object FindObjectOfType(System.Type T)
   {
#if OLDOBJECTFINDER
        return GameObject.FindObjectOfType(T);
#else
      System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy;
      System.Reflection.MethodInfo method = null;
      foreach (var m in typeof(GameObject).GetMethods(flags))
      {
         if (m.Name == "FindObjectOfType" && m.GetParameters().Length == 0)
         {
            method = m;
            break;
         }
      }
      System.Reflection.MethodInfo _FindObjectOfType = method.MakeGenericMethod(T);
      return (UnityEngine.Object)_FindObjectOfType.Invoke(null, null);
#endif
   }


   static public System.Collections.IEnumerator ProcessAB(byte[] bytes, omnitymono.DLLExports.ABHold abhold)
   {
#if AB_CREATEFROMMEMORY
        AssetBundleCreateRequest abr = AssetBundle.CreateFromMemory(bytes);
#else
      AssetBundleCreateRequest abr = AssetBundle.LoadFromMemoryAsync(bytes);
#endif
      while (!abr.isDone)
      {
         yield return null;
      }
      abhold.ab = abr.assetBundle;
   }
   static public void SetTextEditorText(TextEditor te, string s)
   {
#if TEXTEDITOR_TEXT_NOTSUPPORTED
        te.content = new GUIContent(s);
#else
      te.text = s;
#endif
   }


   static public void Init()
   {
      if (omnitymono.DLLExports.OMNI_IMPORT.funcProcessAB == null)
      {
         omnitymono.DLLExports.OMNI_IMPORT.funcProcessAB = ProcessAB;
         omnitymono.DLLExports.OMNI_IMPORT.funcSetTextEditorText = SetTextEditorText;
      }
   }

   static public void DirectoryDelete(string v1, bool v2)
   {
#if LOADSAVE_NOTSUPORTED
#else
      System.IO.Directory.Delete(v1, v2);
#endif
   }

   internal static void WriteAllBytes(string filename, byte[] bytes)
   {
#if LOADSAVE_NOTSUPORTED
#else
      System.IO.File.WriteAllBytes(filename, bytes);
#endif
   }

   internal static void SetInvertCulling(bool v)
   {
#if INVERTCULLING_NOTSUPPORTED
        GL.SetRevertBackfacing(v);
#else
      GL.invertCulling = v;
#endif
   }
}