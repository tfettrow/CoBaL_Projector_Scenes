#if UNITY_WEBPLAYER
#define LOADSAVE_NOTSUPORTED
#else
#endif

using omnitymono.DLLExports;
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Omnity is the main class.  Typical usage would be to add the class as a component to the main camera.
/// As a result the main camera would be disabled, and a replaced with the Omnity style spherical camera system.
/// </summary>
/// <remarks>USES
/// Q) How do I disable Omnity?
/// The simplest way is to leave Omnity Unchecked in the inspector.  That way the script will not load at runtime.  Through scripting the developer can enable it for dome rendering by setting the component's enabled property to be true.
/// </remarks>
///

public class Omnity : MonoBehaviour
{

   /// <summary>
   /// The Omnity version
   /// </summary>
   public static int omnityVersion = 240;// 215 = 2.1.5

   /// <summary>
   /// Currently not used, but will be used to alert users of updates.
   /// </summary>
   public static string omnitySoftwareURL = "http://updates.elumenati.com/Omnity2/";

   /// <summary>
   /// Used in internal projects
   /// </summary>
   [System.NonSerialized]
   public bool isGlobe = false;

   /// <summary>
   /// If this is unchecked, it will not load the config file, and instead load the config settings in the Inspector.  This is ignored in web player build, since it doesn't allow for reading from file.
   /// </summary>
   public bool doLoadConfig = true;

   /// <summary>
   /// This class helps the gui pick the right directory for loading config files.
   /// </summary>
   public OmnityFileChooser configFileChooser = new OmnityFileChooser
   {
      // TCW
      //defaultConfigPath_relativeTo_Bundle2 = "Elumenati/Omnity",
      searchFilter = "*.xml",
      title = "ConfigFilename",
      configXMLFilename_Default = "config.ini"
   };

   /// <summary>
   /// this field helps the application know if there are any GUI's open.  For example if the application is listening for for mouse clicks, it should disable that if Omnity.anyGuiShowing is true
   /// </summary>
   /// <value><c>true</c> if [any GUI showing]; otherwise, <c>false</c>.</value>
   static public bool anyGuiShowing
   {
      get
      {
         if (anOmnity && anOmnity.myOmnityGUI)
         {
            // todo make this iterate enabled omnitys
            return anOmnity.myOmnityGUI.GUIEnabled;
         }
         else
         {
            return false;
         }
      }
   }

   public event Action<Omnity> ConfigLoaded;


   /// <summary>
   /// This is not available through the inspector, but you can use this to switch back to flat screen rendering on projects that have been previously built for dome rendering.  If you set disableOmnity to be true through the XML file, it will prevent omnity from loading.  The default is false.
   /// </summary>
   private bool disableOmnity = false;// disable Omnity through a dummy config file with this flag set to true

   /// <summary>
   /// The Omnity transform, provides a transform link to the Omnity Rig.
   /// Consider this an alternative transform to the main omnityClass transform which is shared with mainCamera.
   /// A best practice is to consider this the transform of the user and all other transforms within unity are relative to this.
   /// Why use omnityClass.omnityTransform instead of transform?
   /// If you want to create objects that are stationary in dome space, tilting view using the bracket keys "[", "]" will cause the the object to move relative to the dome.  By parenting the object to omnityClass.omnityTransform it will stay fixed relative to the dome when both moving and tilting the camera.
   /// If you want to the object move when the camera is tilted use omnityClass.transform
   ///
   /// Warning: omnityClass.omnityTransform is null at startup and also destroyed when ReloadFromFile() is called for example when "F12" is pressed.    Omnity provides two callbacks omnityClass.onReloadStartCallback and omnityClass.onReloadEndCallback that are fired when the omnity is reloading.  omnityClass.onReloadEndCallback is called after the transform is created so its a good time to add gameObjects and components to the omnityClass.omnityTransform .  omnityClass.onReloadStartCallback is called before the transform is destroyed, so if you need to call any cleanup functions this is the time to do it.
   /// </summary>
   [HideInInspector]
   public Transform omnityTransform = null;
   public Transform PlayerPerspectiveTransform = null;
	public Transform ProjectorTransform = null;
   /// <summary>
   /// Debug output verbosity that is logged to the console. You can lower this this to prevent unwanted console output.
   /// </summary>
   public DebugLevel debugLevel = DebugLevel.Low;

   /// <summary>
   /// The camera array
   /// This is a PerspectiveCamera array.  Each PerspectiveCamera element is the definition of a camera that generates part of the perspective, on load a Unity3D camera is generated that uses the parameters defined in the array element.
   /// Inside of the OmnityUpdatePhase() function, the each PerspectiveCamera captures the scene into a RenderTexture.
   /// This is considered the first pass of the Perspective Projection Mapping Algorithm as described in: http://www.clementshimizu.com/0009-Omnimap/Shimizu_ProjectivePerspectiveMapping.pdf
   /// </summary>
   public PerspectiveCamera[] cameraArray = PerspectiveCamera.getDefault_TheaterSimple();

   /// <summary>
   /// Internal Variable for GUI handling
   /// </summary>
   /// <exclude />
   private bool cameraArrayGUIExpanded = false;

   /// <summary>
   /// The screen shapes.  Typically this will be a single element array with the definition of the screen surface.  Usually its a dome, but its possible to use other shapes. The parameters should mimic the projection surface's physical layout.  See ScreenShape class for more info.
   /// </summary>
   public ScreenShape[] screenShapes = ScreenShape.GetDefaultScreenShapes();

   /// <summary>
   /// Internal Variable for GUI handling
   /// </summary>
   /// <exclude />
   private bool screenShapesGUIExpanded = false;

   /// <summary>
   /// This defines the final pass camera.  Although this is represented as a Camera inside of Unity3D, it really is the definition of the Fisheye Video Projector and the parameters should mimic the projection system hardware/physical layout.  Typically this will be a single camera.
   /// </summary>
   public FinalPassCamera[] finalPassCameras = FinalPassCamera.GetDefaultFinalPassCameras();

   /// <summary>
   /// The GUI position, where the F12 Gui is on the screen, you can drag it around, but and also set it's default position through the XML file.  The position is in normalized coordinates, and follows how the Unity3D GUI uses rect to position windows.
   /// </summary>
   public Rect guiPosition;

   /// <summary>
   /// Hint for the helping to place flat screen controls.  It follows how the Unity3D GUI uses normalized viewport rect to position windows.
   /// </summary>
   public OmnityWindowInfo windowInfo = new OmnityWindowInfo();

   /// <summary>
   /// Units for rect
   /// </summary>
   [System.NonSerialized]
   public bool usePixel = false;

   /// <summary>
   /// The omnity update phase.  Contains a list of options of when omnity updates itself.
   /// You may need to change this if the camera is jittering every frame.
   /// This specifies the timing of when the Unity3D perspective cameras render, as well as any other updates.
   /// Please see http://docs.unity3d.com/Documentation/Manual/ExecutionOrder.html
   /// for more information.
   /// </summary>
   /// <remarks>
   /// OmnityUpdatePhase.NONE allows you to take complete control of the OmnityUpdate call.  If you set NONE, you will need call omnityClass.OmnityUpdate() from your own script, or at least make sure all of the cameras are enabled.<br/>
   /// OmnityUpdatePhase.EndOfFrame:omnityClass.Update will be called inside of the the Update() Loop<br/>
   /// OmnityUpdatePhase.EndOfFrame:omnityClass.LateUpdate will be called inside of the the LateUpdate() Loop (this is a good choice)<br/>
   /// OmnityUpdatePhase.EndOfFrame: omnityClass.OmnityUpdate will be called via a coroutine that waits for the EndOfFrame to happen.
   /// </remarks>	public OmnityUpdatePhase omnityUpdatePhase = OmnityUpdatePhase.LateUpdate;
   public OmnityUpdatePhase omnityUpdatePhase = OmnityUpdatePhase.LateUpdate;

   /// <summary>
   /// The perspective cameras render phase.  Usually this is done in Omnity Update, but you can turn it to MANUAL and then you have to manually call render, or UNITYCONTROLLED, and it will set camera.enabled = true, and let unity take care of it.
   /// </summary>
   public OmnityCameraRenderPhase perspectiveCamerasRenderPhase = OmnityCameraRenderPhase.OMNITYUPDATE;

   /// <summary>
   /// The final pass camera RenderPhase.  Usually this is done in Omnity Update, but you can turn it to MANUAL and then you have to manually call render, or UNITYCONTROLLED, and it will set camera.enabled = true, and let unity take care of it.
   /// </summary>
   public OmnityCameraRenderPhase finalPassCamerasRenderPhase = OmnityCameraRenderPhase.UNITYCONTROLLED;

   /// <summary>
   /// Enum to specify the cameras Render Phase.  Usually this is done in Omnity Update, but you can turn it to MANUAL and then you have to manually call render, or UNITYCONTROLLED, and it will set camera.enabled = true, and let unity take care of it.
   /// </summary>
   public enum OmnityCameraRenderPhase
   {
      OMNITYUPDATE = 0,
      MANUAL = 1,
      UNITYCONTROLLED = 2,
   }

   /// <summary>
   /// Internal component for GUI handling
   /// </summary>
   /// <exclude />
   private bool finalPassCamerasGUIExpanded = false;

   /// <summary>
   /// Check this if your application changes variables inside of the finalPassCameras array.  It refreshes the final pass camera's transform, matrix, and other parameters.  There is some performance cost to this, so uncheck it if you are want to prevent unneeded computation.
   /// Two main situations when you want to enable this are 1) if you are playing with Omnity's parameters in the Omnity's inspector while running the project in the Unity Editor.  2) If you are changing the omnity parameters frequently at at runtime for example if using head tracking.
   /// Alternatively you can set omnityClass.forceRefresh = true if you are making an infrequent change.
   /// </summary>
   public bool keepUpdatingFinalPassCameras = false;

   /// <summary>
   /// Check this if your application changes variables inside of the camera array.  It refreshes the perspective camera's transform, matrix, and other parameters.  There is some performance cost to this, so uncheck it if you are want to prevent unneeded computation.
   /// Two main situations when you want to enable this are 1) if you are playing with Omnity's parameters in the Omnity's inspector while running the project in the Unity Editor.  2) If you are changing the omnity parameters frequently at at runtime for example if using head tracking.
   /// Alternatively you can set omnityClass.forceRefresh = true if you are making an infrequent change.
   /// </summary>
   public bool keepUpdatingCameraArray = false;

   /// <summary>
   /// Check this if your application changes variables inside of the screen surface definition.  It it has a check to see if the screen shape's dirty flag is checked and if it is, it regenerates the screen.  There is some performance cost to this, so uncheck it if you are want to prevent unneeded computation.
   /// Two main situations when you want to enable this are 1) if you are playing with Omnity's parameters in the Omnity's inspector while running the project in the Unity Editor.  2) If you are changing the omnity parameters frequently at at runtime for example if using head tracking.
   /// Alternatively you can set omnityClass.forceRefresh = true if you are making an infrequent change.
   /// </summary>
   public bool keepUpdatingScreen = false;

   /// <summary>
   /// This updates the shader parameters of the screen shape.
   /// Two main situations when you want to enable this are 1) if you are playing with Omnity's parameters in the Omnity's inspector while running the project in the Unity Editor.  2) If you are changing the omnity parameters frequently at at runtime for example if using head tracking.
   /// Alternatively you can set omnityClass.forceRefresh = true if you are making an infrequent change.
   /// </summary>
   public bool keepUpdatingShaders = true;

   /// <summary>
   /// Internal component for GUI handling
   /// </summary>
   /// <exclude />
   [HideInInspector]
   public OmnityGUI myOmnityGUI;

   /// <summary>
   /// This is an instance of Omnity.  Think if it like as a singleton instance provided for convenience.  It allows the programmer to easily access omnity from any class in Unity3D using "Omnity.anOmnity" without having to connect the scripts using the inspector.
   /// Warning: If there are more than one instance of Omnity that this will not be a valid way of accessing omnity.
   /// </summary>
   static public Omnity anOmnity = null;

   /// <summary>
   /// Enable tilt with bracket keys.  "[" and "]" will tilt the view up or down.  If your application uses the bracket keys, you will need to disable this by setting enableTiltWithBracketKeys = false in the inspector
   /// </summary>
   public bool enableTiltWithBracketKeys = false;

   /// <summary>
   /// The tilt of the fisheye camera.  The default is zero but "[" and "]" will adjust it at runtime (as long as enableTiltWithBracketKeys is true).  Use this if your view is too high or too low.  This parameter can be set in the XML file to change the default tilt.  It will also be saved if you use "Shift-F12" and save the current config.
   /// </summary>
   public float tilt = 0;

   /// <summary>
   /// The tilt of the fisheye camera.  The default is zero but "[" and "]" will adjust it at runtime (as long as enableTiltWithBracketKeys is true).  Use this if your view is too high or too low.  This parameter can be set in the XML file to change the default tilt.  It will also be saved if you use "Shift-F12" and save the current config.
   /// </summary>
   public float yaw = 0;

   /// <summary>
   /// Delegate OmnityEventDelegate type for functions callbacks that need to have a link to omnity...
   /// </summary>
   public delegate void OmnityEventDelegate(Omnity o);

   /// <summary>
   /// Delegate PerspectiveCameraDelegate type for functions callbacks that need to have a link to a perspective camera...
   /// </summary>
   /// <param name="pc">The pc.</param>
   public delegate void PerspectiveCameraDelegate(PerspectiveCamera pc);

   public delegate void FinalPassCameraDelegate(FinalPassCamera fpc);

   /// <summary>
   /// Omnity provides four callbacks omnityClass.onReloadStartCallback, onLoadInMemoryConfigStart, omnityClass.onReloadEndCallback that are fired when the omnity is reloading.
   ///  omnityClass.onReloadEndCallback is called after the transform is created so its a good time to add gameObjects and components to the omnityClass.omnityTransform.
   /// onLoadInMemoryConfigStart is called after the data is loaded but before its turned into game objects. omnityClass.onReloadStartCallback is called before the transform is destroyed, so if you need to call any cleanup functions this is the time to do it.
   /// This function is called at the start of reloading Omnity.  This is called before Omnity destroys itself.
   /// omnityClass.omnityTransform is null at startup and also destroyed after omnityClass.onReloadStartCallback is called
   /// omnityClass.omnityTransform is done being constructed after omnityClass.onReloadEndCallback is called
   /// onSaveCompleteCallback is called after omnity is done saving a config.  By registering that call you could save additional config info that is specific to a particular omnity config
   /// </summary>
   static public OmnityEventDelegate onReloadStartCallback = null;

   /// <summary>
   /// Omnity provides four callbacks omnityClass.onReloadStartCallback, onLoadInMemoryConfigStart, omnityClass.onReloadEndCallback that are fired when the omnity is reloading.
   ///  omnityClass.onReloadEndCallback is called after the transform is created so its a good time to add gameObjects and components to the omnityClass.omnityTransform.
   /// onLoadInMemoryConfigStart is called after the data is loaded but before its turned into game objects. omnityClass.onReloadStartCallback is called before the transform is destroyed, so if you need to call any cleanup functions this is the time to do it.
   /// This function is called at the start of reloading Omnity.  This is called before Omnity destroys itself.
   /// omnityClass.omnityTransform is null at startup and also destroyed after omnityClass.onReloadStartCallback is called
   /// omnityClass.omnityTransform is done being constructed after omnityClass.onReloadEndCallback is called
   /// onSaveCompleteCallback is called after omnity is done saving a config.  By registering that call you could save additional config info that is specific to a particular omnity config
   /// </summary>
   static public OmnityEventDelegate onLoadInMemoryConfigStart = null;

   /// <summary>
   /// Omnity provides four callbacks omnityClass.onReloadStartCallback, onLoadInMemoryConfigStart, omnityClass.onReloadEndCallback that are fired when the omnity is reloading.
   ///  omnityClass.onReloadEndCallback is called after the transform is created so its a good time to add gameObjects and components to the omnityClass.omnityTransform.
   /// onLoadInMemoryConfigStart is called after the data is loaded but before its turned into game objects. omnityClass.onReloadStartCallback is called before the transform is destroyed, so if you need to call any cleanup functions this is the time to do it.
   /// This function is called at the start of reloading Omnity.  This is called before Omnity destroys itself.
   /// omnityClass.omnityTransform is null at startup and also destroyed after omnityClass.onReloadStartCallback is called
   /// omnityClass.omnityTransform is done being constructed after omnityClass.onReloadEndCallback is called
   /// onSaveCompleteCallback is called after omnity is done saving a config.  By registering that call you could save additional config info that is specific to a particular omnity config
   /// </summary>
   static public OmnityEventDelegate onReloadEndCallback = null;

   /// <summary>
   /// Omnity provides four callbacks omnityClass.onReloadStartCallback, onLoadInMemoryConfigStart, omnityClass.onReloadEndCallback that are fired when the omnity is reloading.
   ///  omnityClass.onReloadEndCallback is called after the transform is created so its a good time to add gameObjects and components to the omnityClass.omnityTransform.
   /// onLoadInMemoryConfigStart is called after the data is loaded but before its turned into game objects. omnityClass.onReloadStartCallback is called before the transform is destroyed, so if you need to call any cleanup functions this is the time to do it.
   /// This function is called at the start of reloading Omnity.  This is called before Omnity destroys itself.
   /// omnityClass.omnityTransform is null at startup and also destroyed after omnityClass.onReloadStartCallback is called
   /// omnityClass.omnityTransform is done being constructed after omnityClass.onReloadEndCallback is called
   /// onSaveCompleteCallback is called after omnity is done saving a config.  By registering that call you could save additional config info that is specific to a particular omnity config
   /// if you need more granular control of timing, then use omnity.onReloadEndCallbackPriority to register functions by priority queue loading
   /// </summary>
   static public OmnityEventDelegate onSaveCompleteCallback = null;

   /// <summary>
   /// this class manages the specific order in which plugins are loaded.
   /// </summary>
   static public PriorityEventHandler onReloadEndCallbackPriority = new PriorityEventHandler();

   /// <summary>
   /// The _on resize window function callback, there can only be one subscriber, so we keep it private, and use a setter to set the value with = instead of +=
   /// </summary>
   static private OmnityEventDelegate _onResizeWindowFunctionCallback = null;

   /// <summary>
   /// Gets or sets onResizeWindowFunctionCallback.
   /// </summary>
   static public OmnityEventDelegate onResizeWindowFunctionCallback
   { // do not allow this function to be multicast
      set
      {
         _onResizeWindowFunctionCallback = value;
      }
      get
      {
         return _onResizeWindowFunctionCallback;
      }
   }

   /// <summary>
   /// Called if the gui is open, at the end of the 2nd gui call
   /// </summary>
   public OmnityEventDelegate onGUIEnd = null;

   /// <summary>
   /// Gets all omnity instances.   This is a SLOW function to call so only do when absolutely needed, or do it once and cache the results.
   /// </summary>
   /// <returns>Omnity[].</returns>
   static public Omnity[] GetAll()
   {
      Omnity[] classes = GameObject.FindObjectsOfType(typeof(Omnity)) as Omnity[];
      return classes;
   }

   /// <summary>
   /// The registration info.  See RegistrationInfo class
   /// </summary>
   /// <exclude />
   public RegistrationInfo registrationInfo = new RegistrationInfo();

   /// <summary>
   /// Internal function
   /// </summary>
   /// <exclude />
   private void InitSingleton()
   {
      if (anOmnity == null)
      {
         anOmnity = this;

         StartCoroutine(registrationInfo.InitCoroutine());
      }
   }

   /// <summary>
   /// varible to keep track if omnity is initalized yet.
   /// </summary>
   private bool initialized = false;

   private IEnumerator CoroutineLoader(Omnity anOmnity)
   {
      yield return StartCoroutine(FinalPassSetup.Init());
      LoadInMemoryConfig();
   }

   private void Awake()
   {
      OmnityPlatformDefines.Init();
      OmnityPluginManager.ApplyPlugins(gameObject);
      Omnity.onReloadEndCallbackPriority.AddLoaderFunction(PriorityEventHandler.Ordering.Order_OmniInit, CoroutineLoader);
   }

   private void OnDestroy()
   {
      Omnity.onReloadEndCallbackPriority.RemoveLoaderFunction(PriorityEventHandler.Ordering.Order_OmniInit, CoroutineLoader);
   }

   /// <summary>
   /// MonoBehavior Start function is called when omnity is first enabled.
   /// If the omnity class is not enabled (unchecked in the inspector), this will not be called until it is enabled.
   ///  That allows you to change what config file it uses at runtime.  The following describes how to do it:
   ///  1) Start with omnity disabled by unchecking it in the inspector
   ///  2) At runtime set the desired omnity config file by setting omnityClass.configFilename through a helper script
   ///  3) At runtime finally enable omnity by setting omnityClass.enabled = true through the helper script
   /// </summary>
   public void Start()
   {
      InitSingleton();
      if (initialized)
      {
         return;
      }
      initialized = true;
      guiPosition = new Rect(0, 0, 1, 1);

      if (windowInfo.GUIViewportPositionHint.height == 0 || windowInfo.GUIViewportPositionHint.width == 0)
      {
         windowInfo.GUIViewportPositionHint = new Rect(0, 0, 1, 1);
      }

      OmnityHelperFunctions.CallDelegate(this, onReloadStartCallback);
      myOmnityGUI = gameObject.AddComponent<OmnityGUI>();
      myOmnityGUI.Init(this);
      if (OmnityPlatformDefines.LoadSaveSupported())
      {
         configFileChooser.UpdateAllConfigs();

         if (!OmnityLoader.LoadingNow)
         {
            if (doLoadConfig)
            {
               configFileChooser.TryToUpdateConfigFilenameWithCurrentConfigINI(() =>
               {
               });
               OmnityLoader.LoadConfig(configFileChooser.configXMLFilename_FullPath, this);
               if (Omnity.anOmnity.debugLevel >= DebugLevel.High)
               {
                  Debug.Log("ConfigFilename :  " + configFileChooser.configXMLFilename_FullPath);
               }
            }
            else
            {
               OmnityLoader.LoadConfig(null, this);
            }
         }
      }
      Camera camera = gameObject.GetComponent<Camera>();
      if (camera != null)
      {
         camera.enabled = false;
      }
   }

   /*
   /// <summary>
   /// Depreciated...
   /// </summary>
   public void RefreshInMemoryConfig() {
       if (Omnity.anOmnity.debugLevel >= DebugLevel.High) {
           Debug.Log("Calling Load");
       }
       LoadInMemoryConfig();
       if (garbageCollectAfterLoad) {
           OmnityHelperFunctions.DoGarbageCollectNow();
       }
   }*/

   /// <summary>
   /// Unloads omnity.  This is called automatically when reloading omnity.  If you really want to unload omnity, you will need to call this function then disable omnity and re-enable your main camera.
   /// OmnityClass.anOmnity.UnloadOmnity();
   /// OmnityClass.anOmnity.enabled = false;
   /// Camera.main.enabled = true;
   /// </summary>
   public void UnloadOmnity()
   {
      if (Omnity.anOmnity.debugLevel >= DebugLevel.High)
      {
         Debug.Log("Calling unload");
      }
      foreach (PerspectiveCamera aCamera in cameraArray)
      {
         if (aCamera.myCamera && aCamera.myCamera.targetTexture)
         {
            GameObject.Destroy(aCamera.myCamera.targetTexture);
         }
         if (aCamera.myCamera)
         {
            GameObject.Destroy(aCamera.myCamera.gameObject);
         }
      }

      foreach (ScreenShape myScreenShape in screenShapes)
      {
         try
         {
            GameObject.Destroy(myScreenShape.trans.gameObject.GetComponent<MeshFilter>().sharedMesh);
         }
         catch
         {
         }
         try
         {
            GameObject.Destroy(myScreenShape.trans.gameObject);
         }
         catch
         {
         }
      }
      foreach (FinalPassCamera aCamera in finalPassCameras)
      {
         if (aCamera.myCamera && aCamera.myCamera.targetTexture)
         {
            GameObject.Destroy(aCamera.myCamera.targetTexture);
         }
         if (aCamera.myCamera)
         {
            GameObject.Destroy(aCamera.myCamera.gameObject);
         }
      }

      finalPassCameras = new FinalPassCamera[0];
      screenShapes = new ScreenShape[0];
      cameraArray = new PerspectiveCamera[0];
      pluginIDs.Clear();

      Transform t = transform.FindChild("Omnity");
      if (t != null)
      {
         GameObject.DestroyImmediate(t.gameObject);
      }
   }

   /// <summary>
   /// A link to the transform holding the non tilty RenderChannels
   /// </summary>
   [System.NonSerialized]
   public Transform transformRenderChannels = null;

   /// <summary>
   /// A link to the transform holding the non tilty ScreenShapes
   /// </summary>
   [System.NonSerialized]
   public Transform transformScreenShapes = null;

   /// <summary>
   /// A link to the transform holding the non tilty FinalPassCameras
   /// </summary>
   [System.NonSerialized]
   public Transform transformFinalPassCameras = null;

   /// <summary>
   /// A link to the transform holding all of the omnity objects that are effected by tilting
   /// </summary>


   /// <summary>
   /// A link to the transform holding the tilty RenderChannels
   /// </summary>


   /// <summary>
   /// A link to the transform holding the tilty ScreenShapes
   /// </summary>


   /// <summary>
   /// A link to the transform holding the tilty FinalPassCameras
   /// </summary>


   /// <summary>
   /// Generates the transforms for holding all of the omnity game objects
   /// </summary>
   public void GenerateTransforms()
   {
      if (!omnityTransform)
      {
         omnityTransform = transform.FindChild("Dome Position");
         if (!omnityTransform)
         {
			omnityTransform = (new GameObject("Dome Position")).transform;
            omnityTransform.parent = transform;
            omnityTransform.localScale = Vector3.one;
            omnityTransform.localEulerAngles = Vector3.zero;
            omnityTransform.localPosition = Vector3.zero;
         }
      }
		if (!PlayerPerspectiveTransform) 
		{
			PlayerPerspectiveTransform = transform.FindChild ("Player Perspective");
			if (!PlayerPerspectiveTransform) 
			{
				PlayerPerspectiveTransform = (new GameObject ("Player Perspective")).transform;
				PlayerPerspectiveTransform.parent = transform;
				PlayerPerspectiveTransform.localScale = Vector3.one;
				PlayerPerspectiveTransform.localEulerAngles = Vector3.zero;
				PlayerPerspectiveTransform.localPosition = Vector3.zero;
			}
		}
		if (!ProjectorTransform) 
		{
			ProjectorTransform = transform.FindChild ("Projector Position");
			if (!ProjectorTransform) 
			{
				ProjectorTransform = (new GameObject ("Projector Position")).transform;
				ProjectorTransform.parent = transform;
				ProjectorTransform.localScale = Vector3.one;
				ProjectorTransform.localEulerAngles = Vector3.zero;
				ProjectorTransform.localPosition = Vector3.zero;
			}

		}

      if (!transformRenderChannels)
      {
		 transformRenderChannels = PlayerPerspectiveTransform.FindChild("Render Channel Cameras");
         if (!transformRenderChannels)
         {
            transformRenderChannels = (new GameObject("Render Channel Cameras")).transform;
			transformRenderChannels.parent = PlayerPerspectiveTransform;
            transformRenderChannels.localScale = Vector3.one;
            transformRenderChannels.localEulerAngles = Vector3.zero;
            transformRenderChannels.localPosition = Vector3.zero;
         }
      }
      transformScreenShapes = omnityTransform.FindChild("Screen Shapes");
      if (!transformScreenShapes)
      {
         transformScreenShapes = (new GameObject("Screen Shapes")).transform;
         transformScreenShapes.parent = omnityTransform;
         transformScreenShapes.localScale = new Vector3(1, 1, 1);
         transformScreenShapes.localEulerAngles = new Vector3(0, 0, 0);
         transformScreenShapes.localPosition = new Vector3(0, 0, 0);
      }

      transformFinalPassCameras = omnityTransform.FindChild("Final Pass Cameras");
      if (!transformFinalPassCameras)
      {
         transformFinalPassCameras = (new GameObject("Final Pass Cameras")).transform;
			transformFinalPassCameras.parent = ProjectorTransform;
         transformFinalPassCameras.localScale = new Vector3(1, 1, 1);
         transformFinalPassCameras.localEulerAngles = new Vector3(0, 0, 0);
         transformFinalPassCameras.localPosition = new Vector3(0, 0, 0);
      }

   }

   /// <summary>
   /// Loads the in memory config.
   /// This is called automatically when starting and reloading omnity.
   /// </summary>
   public void LoadInMemoryConfig()
   {
      OmnityPluginManager.ApplyPlugins(gameObject, true);

      OmnityHelperFunctions.CallDelegate(this, onLoadInMemoryConfigStart);
      if (disableOmnity)
      {
         enabled = false;
         omnityTransform = transform.FindChild("Omnity");
         if (omnityTransform)
         {
            GameObject.Destroy(omnityTransform.gameObject);
            omnityTransform = null;
            transformRenderChannels = null;
            transformScreenShapes = null;
            transformFinalPassCameras = null;
         }

         Camera camera = gameObject.GetComponent<Camera>();
         if (camera)
         {
            camera.enabled = true;
         }
         return;
      }
      GenerateTransforms();

      foreach (PerspectiveCamera aCamera in cameraArray)
      {
         aCamera.SpawnCameraAround(this);
         aCamera.myCamera.enabled = shouldPerspectiveCamerasBeEnabled;
      }

      foreach (ScreenShape myScreenShape in screenShapes)
         myScreenShape.SpawnScreenShape(anOmnity);

      foreach (FinalPassCamera myFinalPassCamera in finalPassCameras)
      {
         myFinalPassCamera.SpawnCameraAround(anOmnity);
      }

      DoConnectTextures();
      DoUpdateShaders();
      DoUpdateCameraArray();
      DoUpdateFinalPassCameras();
      DoKeepUpdatingScreen();
      DoUpdateShaders();

      windowInfo.OnFinishLoadInMemoryConfig(this);

      guiPosition = windowInfo.GUIViewportPositionHint;
   }

   public void DoConnectTextures()
   {
      if (debugLevel >= DebugLevel.High)
      {
         Debug.Log("DoConnectTextures->");
      }
      foreach (ScreenShape myScreenShape in screenShapes)
      {
         if (myScreenShape.automaticallyConnectTextures)
         {
            if (debugLevel >= DebugLevel.High)
            {
               Debug.Log("DoConnectTextures->" + myScreenShape.name);
            }

            int i = 0;
            foreach (PerspectiveCamera aCamera in cameraArray)
            {
               if (aCamera.automaticallyConnectTextures)
               {
                  myScreenShape.renderer.sharedMaterial.SetTexture("_Cam" + i + "Tex", aCamera.myCamera.targetTexture);
                  i++;
                  if (debugLevel >= DebugLevel.High)
                  {
                     Debug.Log("DoConnectTextures->" + myScreenShape.name + "->" + aCamera.name + " -> " + aCamera.myCamera.targetTexture.width);
                  }
               }
            }
         }
         else
         {
            if (debugLevel >= DebugLevel.High)
            {
               Debug.Log("Do NOT ConnectTextures->" + myScreenShape.name);
            }
         }
      }
   }

   // update the shaders etc.
   // IMPORTANT... if you want to do head tracking... make the system update the head position in "UPDATE" not late update...
   /// <summary>
   /// helper function calls UpdateCamera on each PerspectiveCamera in the cameraArray.
   /// </summary>
   public void DoUpdateCameraArray()
   {
      for (int i = 0; i < cameraArray.Length; i++)
      {
         cameraArray[i].UpdateCamera();
      }
   }

   /// <summary>
   /// Internal Function: helper function calls UpdateCamera on each of the final pass cameras.
   /// </summary>
   /// <exclude />
   private void DoUpdateFinalPassCameras()
   {
      for (int i = 0; i < finalPassCameras.Length; i++)
      {
         finalPassCameras[i].UpdateCamera();
      }
   }

   /// <summary>
   /// Internal Function: Helper function to connect the matrix used the the final pass of the Projective Projection Mapping algorithm.
   /// </summary>
   /// <param name="myScreenShape">screen shape to apply matrix</param>
   /// <param name="i">the index</param>
   /// <param name="myMVP">projection mapping matrix</param>
   /// <exclude />
   public void DoSetShaderMatrix(ScreenShape myScreenShape, int i, Matrix4x4 myMVP)
   {
      if (myScreenShape.renderer.sharedMaterial == null)
      {
         return;
      }
      myScreenShape.renderer.sharedMaterial.SetMatrix(GetMatrixStringFast(i), myMVP);
   }

   static public string GetMatrixStringFast(int i)
   {
      switch (i)
      { // this funny switch function is used to prevent a memory allocation that happens when concatenating strings..
         case 0:
            return "_Cam0Matrix";

         case 1:
            return "_Cam1Matrix";

         case 2:
            return "_Cam2Matrix";

         case 3:
            return "_Cam3Matrix";

         case 4:
            return "_Cam4Matrix";

         case 5:
            return "_Cam5Matrix";

         case 6:
            return "_Cam6Matrix";

         case 7:
            return "_Cam7Matrix";

         case 8:
            return "_Cam8Matrix";

         case 9:
            return "_Cam9Matrix";

         case 10:
            return "_Cam10Matrix";

         case 11:
            return "_Cam11Matrix";

         default:
            return "_Cam" + i + "Matrix";
      }
   }

   /// <summary>
   /// Internal Function: Update shader matrices, needed to synchronize the position of the projectively textured render channels with the camera's used to generate them
   /// </summary>
   /// <exclude />
   public void DoUpdateShaders()
   {
      foreach (ScreenShape myScreenShape in screenShapes)
      {
         if (myScreenShape.startEnabled)
         {
            if (myScreenShape == null)
            {
               if (Omnity.anOmnity.debugLevel >= DebugLevel.High)
               {
                  Debug.Log("ScreenShape null (questionable during reset)");
               }
               return;
            }
            else if (myScreenShape.renderer == null)
            {
               if (Omnity.anOmnity.debugLevel >= DebugLevel.High)
               {
                  Debug.Log("ScreenShape.renderer null (questionable during reset)");
               }
               return;
            }

            int i = 0;
            foreach (PerspectiveCamera aCamera in cameraArray)
            {
               DoSetShaderMatrix(myScreenShape, i++, GenerateProjectivePerspectiveProjectionMatrix(aCamera.myCamera, myScreenShape.trans));
            }
            myScreenShape.SetJunkMatrices(i);
         }
      }
   }

   /// <summary>
   ///  Internal Function: Fixes the shader matrix, called camera array is resized through the GUI menu
   /// </summary>
   /// <exclude />
   private void FixShaderMatrix()
   {
      foreach (ScreenShape myScreenShape in screenShapes)
      {
         int i = 0;
         foreach (PerspectiveCamera aCamera in cameraArray)
         {
            DoSetShaderMatrix(myScreenShape, i++, GenerateProjectivePerspectiveProjectionMatrix(aCamera.myCamera, myScreenShape.trans));
         }
         myScreenShape.SetJunkMatrices(i);
      }
      DoUpdateShaders();
   }

   /// <summary>
   /// helper function to update the settings of the screen shape(s), called by OmnityUpdate
   /// </summary>
   public void DoKeepUpdatingScreen()
   {
      foreach (ScreenShape myScreenShape in screenShapes)
      {
         myScreenShape.UpdateScreen();
      }
   }

   /// <summary>
   /// MonoBehavior Update loop.
   /// </summary>
   private void Update()
   {
      if (!OmnityLoader.LoadingNow)
      {
         bool macKeyCode = ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) && Input.GetKeyDown(KeyCode.O);
         bool winKeyCode = Input.GetKeyDown(KeyCode.F12);
         if (macKeyCode | winKeyCode)
         {
            if (OmnityPlatformDefines.LoadSaveSupported())
            {
               if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
               {
                  myOmnityGUI.GUIEnabled = !myOmnityGUI.GUIEnabled;
               }
               else
               {
                  ReloadFromFile();
                  return;
               }
            }
            else
            {
               myOmnityGUI.GUIEnabled = !myOmnityGUI.GUIEnabled;
            }
         }

         if (OmnityUpdatePhase.Update == omnityUpdatePhase)
         {
            OmnityUpdate();
         }
      }
   }

   /// <summary>
   /// MonoBehavior LateUpdate loop.
   /// </summary>
   private void LateUpdate()
   {
      if (OmnityUpdatePhase.LateUpdate == omnityUpdatePhase)
      {
         OmnityUpdate();
      }
      else if (OmnityUpdatePhase.EndOfFrame == omnityUpdatePhase && myEndOfFrame == null)
      {
         myEndOfFrame = StartCoroutine(EndOfFrame());
      }
   }

   /// <summary>
   /// For rendering first pass cameras using a unity camera settings for simpler rendering.  This is only used if Omnity.omnityUpdatePhase should be OmnityUpdatePhase.MANUAL
   /// </summary>
   public void RenderUsingCameraSettings(Camera sourceCamera, int? sourceCullingMask = null, PerspectiveCameraDelegate preAction = null, PerspectiveCameraDelegate postAction = null, float nearClipScaleToMakeSmaller = 1.0f)
   {
      if (sourceCamera == null)
      {
         return;
      }
      if (OmnityCameraRenderPhase.MANUAL != perspectiveCamerasRenderPhase)
      {
         Debug.LogWarning("Warning, Omnity.omnityUpdatePhase should be OmnityUpdatePhase.MANUAL, skipping over rendering...");
         return;
      }
      for (int i = 0; i < cameraArray.Length; i++)
      {
         cameraArray[i].RenderUsingCameraSettings(sourceCamera, sourceCullingMask, preAction, postAction, nearClipScaleToMakeSmaller);
      }
   }

   /// <summary>
   /// Internal variable: This holds a link to the Coroutine that renders at the EndOfFrame Phase.  This is only used if (OmnityUpdatePhase.EndOfFrame == omnityUpdatePhase).
   /// </summary>
   /// <exclude />
   private Coroutine myEndOfFrame = null;

   /// <summary>
   /// Internal function: This is the Coroutine that renders at the EndOfFrame Phase.  This is only used if (OmnityUpdatePhase.EndOfFrame == omnityUpdatePhase).
   /// </summary>
   /// <returns>IEnumerator.</returns>
   private IEnumerator EndOfFrame()
   {
      do
      {
         if (enabled)
         {
            OmnityUpdate();
         }
         yield return new WaitForEndOfFrame();
      } while (OmnityUpdatePhase.EndOfFrame == omnityUpdatePhase && myEndOfFrame != null);
      myEndOfFrame = null;
   }

   /// <summary>
   /// helper function that adjusts the camera tilt if enableTiltWithBracketKeys is set to true
   /// </summary>


   /// <summary>
   /// Checks the bracket keys for tilt
   /// </summary>

   /// <summary>
   /// This refreshes the parameters to the algorithms, as well as renders the perspective channels. The timing of this function's call depends on omnity.omnityUpdatePhase
   /// </summary>
   public void OmnityUpdate()
   {
      RefreshIfNeeded();
      foreach (PerspectiveCamera aCamera in cameraArray)
      {
         if (aCamera != null && aCamera.myCamera != null)
         {
            aCamera.myCamera.enabled = shouldPerspectiveCamerasBeEnabled;
            switch (perspectiveCamerasRenderPhase)
            {
               case OmnityCameraRenderPhase.MANUAL:
                  break;

               case OmnityCameraRenderPhase.UNITYCONTROLLED:
                  break;

               case OmnityCameraRenderPhase.OMNITYUPDATE:
                  bool wasEnabled = aCamera.myCamera.enabled;
                  aCamera.myCamera.enabled = true;
                  aCamera.myCamera.Render();
                  aCamera.myCamera.enabled = wasEnabled;
                  break;

               default:
                  Debug.Log("unknown perspectiveCamerasRenderPhase phase " + perspectiveCamerasRenderPhase);
                  break;
            }
         }
      }

      foreach (FinalPassCamera aCamera in finalPassCameras)
      {
         if (aCamera != null && aCamera.myCamera != null)
         {
            aCamera.myCamera.enabled = shouldFinalPassCamerasBeEnabled;
            switch (finalPassCamerasRenderPhase)
            {
               case OmnityCameraRenderPhase.MANUAL:
                  break;

               case OmnityCameraRenderPhase.UNITYCONTROLLED:
                  break;

               case OmnityCameraRenderPhase.OMNITYUPDATE:
                  aCamera.myCamera.enabled = true;
                  aCamera.myCamera.Render(); // Warning this doesn't always work for some reason (i think no need to enable camera before render since only screen, no gui)
                  aCamera.myCamera.enabled = shouldFinalPassCamerasBeEnabled;
                  break;

               default:
                  Debug.Log("unknown finalPassCamerasRenderPhase phase " + finalPassCamerasRenderPhase);
                  break;
            }
         }
      }
   }

   /// <summary>
   /// Internal function for testing if the cameras should be enable.  Since omnity controlled rendering manually calls camera rendering
   /// </summary>
   private bool shouldPerspectiveCamerasBeEnabled
   {
      get
      {
         switch (perspectiveCamerasRenderPhase)
         {
            case OmnityCameraRenderPhase.MANUAL:
               return false;

            case OmnityCameraRenderPhase.UNITYCONTROLLED:
               return true;

            case OmnityCameraRenderPhase.OMNITYUPDATE:
               return false;

            default:
               Debug.Log("unknown perspectiveCamerasRenderPhase phase " + perspectiveCamerasRenderPhase);
               return false;
         }
      }
   }

   /// <summary>
   /// Internal function for testing if the cameras should be enable.  Since omnity controlled rendering manually calls camera rendering
   /// </summary>
   private bool shouldFinalPassCamerasBeEnabled
   {
      get
      {
         switch (finalPassCamerasRenderPhase)
         {
            case OmnityCameraRenderPhase.MANUAL:
               return false;

            case OmnityCameraRenderPhase.UNITYCONTROLLED:
               return true;

            case OmnityCameraRenderPhase.OMNITYUPDATE:
               return false;

            default:
               Debug.Log("unknown finalPassCamera phase " + finalPassCamerasRenderPhase);
               return false;
         }
      }
   }

   /// <summary>
   /// force a refresh of all of the cameras, screens, shaders, etc
   /// </summary>
   [System.NonSerialized]
   public bool forceRefresh = false;

   /// <summary>
   /// Refreshes if needed.
   /// </summary>
   public void RefreshIfNeeded()
   {

      if (keepUpdatingCameraArray || forceRefresh)
      {
         DoUpdateCameraArray();
      }

      if (keepUpdatingFinalPassCameras || forceRefresh)
      {
         DoUpdateFinalPassCameras();
      }

      if (keepUpdatingScreen || forceRefresh)
      {
         DoKeepUpdatingScreen();
      }

      if (keepUpdatingShaders || forceRefresh)
      {
         DoUpdateShaders();
      }
      forceRefresh = false;
   }

   /// <summary>
   /// helper variable, for vector that set to .5,.5,.5
   /// TODO: replace with const
   /// </summary>
   private static Vector3 half3 = new Vector3(.5f, .5f, .5f);

   /// <summary>
   /// The scale matrix represented by Ms in the perspective projection mapping algorithm.
   /// </summary>
   private static Matrix4x4 scale = Matrix4x4.Scale(half3);

   /// <summary>
   /// This Matrix4x4 represents the scale and bias transform of equations Ms and Mt in the perspective projection mapping algorithm.
   /// </summary>
   private static Matrix4x4 xshift = Matrix4x4.TRS(half3, Quaternion.identity, Vector3.one);

   // if DO_BUGFIX
   /// <summary>
   /// internal temporary variable
   /// </summary>
   /// <exclude />
   private Matrix4x4 matrixPositionRotationNoScale = new Matrix4x4();

   /// <summary>
   /// Generates the projective perspective projection matrix of the specific camera and object combo.
   /// </summary>
   /// <param name="theSourceCamera">The source camera.</param>
   /// <param name="theTargetObject">The target object.</param>
   /// <returns>Matrix4x4.</returns>
   public Matrix4x4 GenerateProjectivePerspectiveProjectionMatrix(Camera theSourceCamera, Transform theTargetObject)
   {
      if (theSourceCamera == null || theTargetObject == null)
      {
         return Matrix4x4.identity;
      }
      if (OmnityLoader.doBUGFIX)
      {
         if (theTargetObject == null)
         {
            if (Omnity.anOmnity.debugLevel >= DebugLevel.High)
            {
               Debug.Log("Camera invalid  (this is normal during reset)");
            }
            return scale;
         }

         //		Debug.Log("Scale " + TheTargetObject.localToWorldMatrix);
         Transform parenttemp = theTargetObject.parent;
         theTargetObject.parent = null;
         Vector3 temp = theTargetObject.localScale;
         theTargetObject.localScale = Vector3.one;
         matrixPositionRotationNoScale = theTargetObject.localToWorldMatrix;

         //	Debug.Log("No scale " + M_PositionRotationNoScale);

         theTargetObject.localScale = temp;
         theTargetObject.parent = parenttemp;
         //		M_PositionRotationNoScale.SetTRS(TheTargetObject.position,TheTargetObject.rotation,Vector3.one);
         Matrix4x4 V = theSourceCamera.worldToCameraMatrix;
         Matrix4x4 p = theSourceCamera.projectionMatrix;
         Matrix4x4 MVP = xshift * scale * p * V * matrixPositionRotationNoScale;
         return MVP;
      }
      else
      {
         Matrix4x4 M = theTargetObject.localToWorldMatrix;
         Matrix4x4 V = theSourceCamera.worldToCameraMatrix;
         Matrix4x4 p = theSourceCamera.projectionMatrix;
         Matrix4x4 MVP = xshift * scale * p * V * M;
         return MVP;
      }
   }

   public static bool garbageCollectAfterLoad = true;

   /// <summary>
   ///  Helper function for serializing the XML.
   /// </summary>
   /// <param name="xmlWriter">The XML writer.</param>
   public void WriteXML(System.Xml.XmlTextWriter xmlWriter)
   {
      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         return;
      }
      xmlWriter.WriteStartElement("OmnityInfo");
      xmlWriter.WriteElementString("KeepUpdatingFinalPassCameras", keepUpdatingFinalPassCameras.ToString());
      xmlWriter.WriteElementString("KeepUpdatingScreen", keepUpdatingScreen.ToString());
      xmlWriter.WriteElementString("KeepUpdatingShaders", keepUpdatingShaders.ToString());
      xmlWriter.WriteElementString("KeepUpdatingCameraArray", keepUpdatingCameraArray.ToString());
      xmlWriter.WriteElementString("enableTiltWithBracketKeys", enableTiltWithBracketKeys.ToString());
      xmlWriter.WriteElementString("isGlobe", isGlobe.ToString());
      xmlWriter.WriteElementString("tilt", tilt.ToString());
      xmlWriter.WriteElementString("yaw", yaw.ToString());

      //        xmlWriter.WriteElementString("guiPosition", guiPosition.ToString("R"));
      windowInfo.WriteXML(xmlWriter);

      xmlWriter.WriteElementString("disableOmnity", disableOmnity.ToString());

      xmlWriter.WriteElementString("CameraArrayGUIExpanded", cameraArrayGUIExpanded.ToString());
      xmlWriter.WriteElementString("ScreenShapesGUIExpanded", screenShapesGUIExpanded.ToString());
      xmlWriter.WriteElementString("FinalPassCamerasGUIExpanded", finalPassCamerasGUIExpanded.ToString());

      foreach (PerspectiveCamera aCamera in cameraArray)
         aCamera.WriteXML(xmlWriter);

      foreach (ScreenShape myScreenShape in screenShapes)
         myScreenShape.WriteXML(xmlWriter);

      foreach (FinalPassCamera myFinalPassCamera in finalPassCameras)
         myFinalPassCamera.WriteXML(xmlWriter);

      OmnityHelperFunctions.WriteList<int>(xmlWriter, "pluginIDs", pluginIDs);

      xmlWriter.WriteEndElement();
   }

   public void ReadXML(System.Xml.XPath.XPathNavigator nav)
   {
      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         return;
      }

      System.Xml.XPath.XPathNodeIterator OmnityInfos = nav.Select("(//OmnityInfo)");
      while (OmnityInfos.MoveNext())
      {
         // TODO put an array of OmnimapClasses  here if there is more than one
         guiPosition = new Rect(0, 0, 1, 1);

         System.Xml.XPath.XPathNavigator OmnityInfo = OmnityInfos.Current;

         keepUpdatingFinalPassCameras = OmnityHelperFunctions.ParseXPathNavigatorAsBooleanDefault(OmnityInfo.SelectSingleNode(".//KeepUpdatingFinalPassCameras"), keepUpdatingFinalPassCameras);
         keepUpdatingScreen = OmnityHelperFunctions.ParseXPathNavigatorAsBooleanDefault(OmnityInfo.SelectSingleNode(".//KeepUpdatingScreen"), keepUpdatingScreen);
         keepUpdatingShaders = OmnityHelperFunctions.ParseXPathNavigatorAsBooleanDefault(OmnityInfo.SelectSingleNode(".//KeepUpdatingShaders"), keepUpdatingShaders);
         keepUpdatingCameraArray = OmnityHelperFunctions.ParseXPathNavigatorAsBooleanDefault(OmnityInfo.SelectSingleNode(".//KeepUpdatingCameraArray"), keepUpdatingCameraArray);
         isGlobe = OmnityHelperFunctions.ParseXPathNavigatorAsBooleanDefault(OmnityInfo.SelectSingleNode(".//isGlobe"), false);

         enableTiltWithBracketKeys = OmnityHelperFunctions.ParseXPathNavigatorAsBooleanDefault(OmnityInfo.SelectSingleNode(".//enableTiltWithBracketKeys"), enableTiltWithBracketKeys);
         tilt = OmnityHelperFunctions.ReadElementFloatDefault(OmnityInfo, ".//tilt", tilt);
         yaw = OmnityHelperFunctions.ReadElementFloatDefault(OmnityInfo, ".//yaw", yaw);

         System.Xml.XPath.XPathNodeIterator myPerspectiveCameraArrayIterator = OmnityInfo.Select("(//PerspectiveCamera)");
         cameraArray = new PerspectiveCamera[myPerspectiveCameraArrayIterator.Count];

         int Index = 0;
         while (myPerspectiveCameraArrayIterator.MoveNext())
         {
            cameraArray[Index] = new PerspectiveCamera();
            cameraArray[Index].ReadXML(myPerspectiveCameraArrayIterator.Current);
            Index++;
         }

         cameraArrayGUIExpanded = OmnityHelperFunctions.ParseXPathNavigatorAsBooleanDefault(OmnityInfo.SelectSingleNode(".//CameraArrayGUIExpanded"), cameraArrayGUIExpanded);
         screenShapesGUIExpanded = OmnityHelperFunctions.ParseXPathNavigatorAsBooleanDefault(OmnityInfo.SelectSingleNode(".//ScreenShapesGUIExpanded"), screenShapesGUIExpanded);
         finalPassCamerasGUIExpanded = OmnityHelperFunctions.ParseXPathNavigatorAsBooleanDefault(OmnityInfo.SelectSingleNode(".//FinalPassCamerasGUIExpanded"), finalPassCamerasGUIExpanded);

         // guiPosition = OmnityHelperFunctions.ReadElementRectDefault(nav, ".//guiPosition", guiPosition);

         System.Xml.XPath.XPathNodeIterator myFlatScreenControlInfoIterator = OmnityInfo.Select("(//windowInfo)");

         while (myFlatScreenControlInfoIterator.MoveNext())
         {
            windowInfo.ReadXML(myFlatScreenControlInfoIterator.Current);
         }

         disableOmnity = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//disableOmnity", disableOmnity);

         System.Xml.XPath.XPathNodeIterator myScreenShapeArrayIterator = OmnityInfo.Select("(//ScreenShape)");
         screenShapes = new ScreenShape[myScreenShapeArrayIterator.Count];
         Index = 0;
         while (myScreenShapeArrayIterator.MoveNext())
         {
            screenShapes[Index] = new ScreenShape();
            screenShapes[Index++].ReadXML(myScreenShapeArrayIterator.Current);
         }

         System.Xml.XPath.XPathNodeIterator myFinalPassCameraArrayIterator = OmnityInfo.Select("(//FinalPassCamera)");
         finalPassCameras = new FinalPassCamera[myFinalPassCameraArrayIterator.Count];
         Index = 0;
         while (myFinalPassCameraArrayIterator.MoveNext())
         {
            finalPassCameras[Index] = new FinalPassCamera();
            finalPassCameras[Index++].ReadXML(myFinalPassCameraArrayIterator.Current);
         }
         pluginIDs = OmnityHelperFunctions.ReadElementIntListDefault(nav, ".//pluginIDs", new System.Collections.Generic.List<int>());

         if (!pluginIDs.Contains((int)OmnityPluginsIDs.EasyMultiDisplay))
         {
            pluginIDs.Add((int)OmnityPluginsIDs.EasyMultiDisplay);
         }
         if (!pluginIDs.Contains((int)OmnityPluginsIDs.HeartRateMonitor))
         {
            pluginIDs.Add((int)OmnityPluginsIDs.HeartRateMonitor);
         }
         if (isGlobe)
         {
            if (!pluginIDs.Contains((int)OmnityPluginsIDs.SphereRenderingExtensions))
            {
               pluginIDs.Add((int)OmnityPluginsIDs.SphereRenderingExtensions);
            }
         }
      }
   }

   /// <summary>
   /// Reloads from file.  Normally called at startup an when pressing "F12"
   /// </summary>
   public void ReloadFromFile()
   {
      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         return;
      }

      if (!OmnityLoader.LoadingNow)
      {
         string filename = configFileChooser.configXMLFilename_FullPath;

         if (OmnityLoader.DoesBaseConfigExist(filename))
         {
            OmnityHelperFunctions.CallDelegate(this, onReloadStartCallback);

            UnloadOmnity();
            OmnityLoader.LoadConfig(filename, this);

            if (garbageCollectAfterLoad)
            {
               OmnityHelperFunctions.DoGarbageCollectNow();
            }
         }
         else
         {
            Debug.Log("Warning, 	" + filename + " does not exist. Please press Shift+F12 and save the current config.");
         }
      }
   }

   /// <summary>
   /// Draws the GUI.  This function is called when the "Shift-F12" menu is up.  To optimize performance, this class is disabled when the GUI is not up.
   /// </summary>
   public void DrawGUI()
   {
      forceRefresh = true; // if gui is open.. set this flag to be true...
      GUILayout.Label("Omnity Settings");
      if (OmnityPlatformDefines.LoadSaveSupported())
      {
         GUILayout.BeginHorizontal();

         configFileChooser.OnGUI(this,
             (om) =>
             {
                OmnityLoader.SaveConfig(configFileChooser.configXMLFilename_FullPath, anOmnity);
                OmnityHelperFunctions.CallDelegate(anOmnity, Omnity.onSaveCompleteCallback);
                configFileChooser.UpdateAllConfigs();
             },
            (om) =>
            {
               anOmnity.ReloadFromFile();
               configFileChooser.UpdateAllConfigs();
               Debug.Log("load current config");
            },
            (om) =>
            {
               Debug.Log("Filename change");
            }
             );
         GUILayout.EndHorizontal();
      }

      keepUpdatingFinalPassCameras = OmnityHelperFunctions.BoolInputReset("KeepUpdatingFinalPassCameras", keepUpdatingFinalPassCameras, false);
      keepUpdatingCameraArray = OmnityHelperFunctions.BoolInputReset("KeepUpdatingCameraArray", keepUpdatingCameraArray, false);
      keepUpdatingScreen = OmnityHelperFunctions.BoolInputReset("KeepUpdatingScreen", keepUpdatingScreen, false);
      keepUpdatingShaders = OmnityHelperFunctions.BoolInputReset("KeepUpdatingShaders", keepUpdatingShaders, true);
      enableTiltWithBracketKeys = OmnityHelperFunctions.BoolInputReset("enableTiltWithBracketKeys", enableTiltWithBracketKeys, true);
      isGlobe = OmnityHelperFunctions.BoolInputReset("isGlobe", isGlobe, false);

      tilt = OmnityHelperFunctions.FloatInputReset("tilt", tilt, 0);
      yaw = OmnityHelperFunctions.FloatInputReset("yaw", yaw, 0);

      OmnityHelperFunctions.BeginExpander(ref cameraArrayGUIExpanded, "CameraArray[" + cameraArray.Length + "]");
      if (cameraArrayGUIExpanded)
      {
         CameraArrayResize(OmnityHelperFunctions.NumericUpDownReset("Size", cameraArray.Length, 3), true, true);
         foreach (PerspectiveCamera aCamera in cameraArray)
         {
            aCamera.OnGUI();
         }
      }
      else
      {
         GUILayout.BeginHorizontal();
         foreach (PerspectiveCamera aCamera in cameraArray)
         {
            aCamera.renderTextureSettings.OnGUI(false, 64);
         }
         GUILayout.EndHorizontal();
      }
      OmnityHelperFunctions.EndExpander();

      OmnityHelperFunctions.BeginExpander(ref screenShapesGUIExpanded, "ScreenShapes[" + screenShapes.Length + "]");
      if (screenShapesGUIExpanded)
      {
         ScreenShapeArrayResize(OmnityHelperFunctions.NumericUpDownReset("Size", screenShapes.Length, 1), true, true);
         foreach (ScreenShape myScreenShape in screenShapes)
         {
            myScreenShape.OnGUI();
         }
      }
      OmnityHelperFunctions.EndExpander();

      OmnityHelperFunctions.BeginExpander(ref finalPassCamerasGUIExpanded, "FinalPassCameras[" + finalPassCameras.Length + "]");

      if (finalPassCamerasGUIExpanded)
      {
         if (FinalPassCameraArrayResize(OmnityHelperFunctions.NumericUpDownReset("Size", finalPassCameras.Length, 1), true, false))
         {
            foreach (FinalPassCamera myFinalPassCamera in finalPassCameras)
            {
               myFinalPassCamera.myOmnityGUI = myOmnityGUI;
            }
         }
         foreach (FinalPassCamera myFinalPassCamera in finalPassCameras)
         {
            myFinalPassCamera.OnGUI();
         }
      }
      else
      {
         GUILayout.BeginHorizontal();
         foreach (FinalPassCamera myFinalPassCamera in finalPassCameras)
         {
            myFinalPassCamera.renderTextureSettings.OnGUI(false, 64);
         }
         GUILayout.EndHorizontal();
      }

      OmnityHelperFunctions.EndExpander();
      windowInfo.OnGUI(this);
      GUILayout.Label("Note: All vectors use \"Left Hand Rule\" and follow unity's convention.  (Rotation order for render channels are Z, then X, then Y and happen in global space.");

      OmnityHelperFunctions.MultiEnumInputClear<OmnityPluginsIDs>(expanderHash, "Plugins", pluginIDs);
      OmnityHelperFunctions.CallDelegate(this, onGUIEnd);
   }

   private string expanderHash = OmnityHelperFunctions.OmnityUniqueHash.uniqueHash;

   /// <summary>
   /// Internal Function for resizing camera arrays.  Only called when the GUI is up and the user resizes the camera array
   /// </summary>
   /// <param name="newsize">The newsize.</param>
   /// <exclude />
   public void CameraArrayResize(int newsize, bool _serialize = true, bool _automaticallyConnectTextures = true, bool spawnNow = true)
   {
      newsize = Mathf.Max(0, newsize);

      if (newsize == cameraArray.Length)
      {
         return;
      }
      else
      {
         int oldsize = cameraArray.Length;
         if (oldsize > newsize)
         {
            for (int i = newsize; i < oldsize; i++)
            {
               if (cameraArray.Length >= i + 1 && cameraArray[i].myCamera != null)
               {
                  GameObject.Destroy(cameraArray[i].myCamera.targetTexture);
                  GameObject.Destroy(cameraArray[i].myCameraTrans.gameObject);
               }
            }
            System.Array.Resize<PerspectiveCamera>(ref cameraArray, newsize);
            FixShaderMatrix();
         }
         else
         {
            System.Array.Resize<PerspectiveCamera>(ref cameraArray, newsize);
            for (int i = oldsize; i < newsize; i++)
            {
               if (i != 0)
               {
                  cameraArray[i] = cameraArray[i - 1].Clone();
               }
               else
               {
                  cameraArray[i] = new PerspectiveCamera();
               }
               cameraArray[i].serializeCamera = _serialize;
               cameraArray[i].automaticallyConnectTextures = _automaticallyConnectTextures;
               if (spawnNow)
               {
                  cameraArray[i].SpawnCameraAround(this);
                  cameraArray[i].myCamera.enabled = shouldPerspectiveCamerasBeEnabled;
               }
            }
         }
         FixShaderMatrix();
      }
   }

   public bool FinalPassCameraArrayResize(int newsize, bool _serialize = true, bool _copyRenderTextureSettings = false, bool spawnNow = true)
   {
      newsize = Mathf.Max(0, newsize);
      if (newsize == finalPassCameras.Length)
      {
         return false;
      }
      else
      {
         int oldsize = finalPassCameras.Length;
         if (oldsize > newsize)
         {
            for (int i = newsize; i < oldsize; i++)
            {
               if (finalPassCameras.Length > 0)
               {
                  if (finalPassCameras.Length >= i + 1 && finalPassCameras[i].myCamera != null)
                  {
                     if (finalPassCameras[i].myCamera && finalPassCameras[i].myCamera.targetTexture)
                     {
                        GameObject.Destroy(finalPassCameras[i].myCamera.targetTexture);
                     }
                     if (finalPassCameras[i].myCameraTransform != null)
                     {
                        GameObject.Destroy(finalPassCameras[i].myCameraTransform.gameObject);
                     }
                  }
               }
            }
            System.Array.Resize<FinalPassCamera>(ref finalPassCameras, newsize);
         }
         else
         {
            System.Array.Resize<FinalPassCamera>(ref finalPassCameras, newsize);
            for (int i = oldsize; i < newsize; i++)
            {
               if (i != 0)
               {
                  finalPassCameras[i] = finalPassCameras[i - 1].Clone(_copyRenderTextureSettings);
               }
               else
               {
                  finalPassCameras[i] = new FinalPassCamera();
               }
               finalPassCameras[i].serialize = _serialize;
               if (spawnNow)
               {
                  if (finalPassCameras[i].startEnabled)
                  {
                     finalPassCameras[i].SpawnCameraAround(anOmnity);
                  }
               }
            }
         }
         return true;
      }
   }

   public void RemoveAndDestroyScreenShape(ScreenShape s)
   {
      System.Collections.Generic.List<ScreenShape> shapes = new System.Collections.Generic.List<ScreenShape>(screenShapes);
      if (shapes.Contains(s))
      {
         shapes.Remove(s);
         shapes.Add(s);
         screenShapes = shapes.ToArray();
         ScreenShapeArrayResize(screenShapes.Length - 1);
      }
   }

   public ScreenShape AddScreenShape(bool _serialize = true, bool _automaticallyConnectTextures = true, OmnityScreenShapeType? _screenShapeType = null, FinalPassShaderType? _finalPassShaderType = null)
   {
      ScreenShapeArrayResize(screenShapes.Length + 1, _serialize, _automaticallyConnectTextures, _screenShapeType, _finalPassShaderType);
      return screenShapes[screenShapes.Length - 1];
   }

   public void ScreenShapeArrayResize(int newsize, bool _serialize = true, bool _automaticallyConnectTextures = true, OmnityScreenShapeType? _screenShapeType = null, FinalPassShaderType? _finalPassShaderType = null, bool spawnNow = true)
   {
      newsize = Mathf.Max(0, newsize);
      if (newsize == screenShapes.Length)
      {
         return;
      }
      else
      {
         int oldsize = screenShapes.Length;
         if (oldsize > newsize)
         {
            for (int i = newsize; i < oldsize; i++)
            {
               if (screenShapes.Length > 0)
               {
                  if (screenShapes.Length >= i + 1 && screenShapes[i].trans != null)
                  {
                     if (screenShapes[i].trans != null)
                     {
                        GameObject.Destroy(screenShapes[i].trans.gameObject);
                     }
                  }
               }
            }
            System.Array.Resize<ScreenShape>(ref screenShapes, newsize);
            FixShaderMatrix();
         }
         else
         {
            System.Array.Resize<ScreenShape>(ref screenShapes, newsize);
            for (int i = oldsize; i < newsize; i++)
            {
               if (i != 0)
               {
                  screenShapes[i] = screenShapes[i - 1].Clone();
               }
               else
               {
                  screenShapes[i] = new ScreenShape();
               }
               screenShapes[i].serialize = _serialize;
               screenShapes[i].automaticallyConnectTextures = _automaticallyConnectTextures;
               if (screenShapes[i].startEnabled)
               {
                  if (_screenShapeType != null)
                  {
                     screenShapes[i].screenShapeType = _screenShapeType.GetValueOrDefault();
                  }
                  if (_finalPassShaderType != null)
                  {
                     screenShapes[i].finalPassShaderType = _finalPassShaderType.GetValueOrDefault();
                  }
                  if (spawnNow)
                  {
                     screenShapes[i].SpawnScreenShape(anOmnity);
                  }
               }
            }
         }
      }
   }

   public static bool DisableCameraEventsMask = true;
   public System.Collections.Generic.List<int> pluginIDs = new System.Collections.Generic.List<int>();

   public bool PluginEnabled(OmnityPluginsIDs omnityPluginsIDs)
   {
      return pluginIDs.Contains((int)omnityPluginsIDs);
   }

   internal void FireConfigLoaded()
   {
      ConfigLoaded(this);
   }
}

/// <summary>
/// OmnityLoader represents the class used to load and save config files.  This is not used in web player builds.
/// </summary>

static public class OmnityLoader
{

   public static bool doBUGFIX
   {
      get
      {
         if (OmnityPlatformDefines.LoadSaveSupported())
         {
            return false;
         }
         else
         {
            return true;
         }
      }
   }

   static public string AddSpecialConfigPath(Omnity anOmnity, string newFilename)
   {
      if (OmnityPlatformDefines.LoadSaveSupported())
      {
         string newDirectory = anOmnity.configFileChooser.configXMLFilename_FullPath.Replace(".xml", "/");
         string newPath = System.IO.Path.Combine(newDirectory, newFilename);
         newDirectory = System.IO.Path.GetDirectoryName(newPath); // in case file name has a local path

         try
         {
            System.IO.Directory.CreateDirectory(newDirectory);
         }
         catch
         {
            Debug.LogError("Couldn't create directory " + newDirectory + " : " + newPath);
         }
         return newPath;
      }
      else
      {
         return newFilename;
      }
   }

   /// <summary>
   /// fixes a bug in the generation of the projection mapping matrices... this bug is not totally squashed, so this variable will remain an option until its fixed.
   /// </summary>

   /// <summary>
   /// The config directory.  Defaults to "Elumenati/Omnity" relative to the application install directory.  To change the Omnity Directory, set OmnityLoader.configDirectory to the desired directory.  Please note that the directory must be relative to the install path(IE Not a fully qualified path).
   /// </summary>
   static public string configDirectory = "Elumenati/Omnity";

   /// <summary>
   /// Helper function: Adds the default config path to a file name
   /// </summary>
   /// <param name="filename">The filename.</param>
   /// <returns>System.String.</returns>
   static public string AddDefaultConfigPath(string filename)
   {
      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         return filename;
      }
      return (GetInstallPath() + "/" + configDirectory + "/" + filename).Replace("//", "/");
   }

   /// <summary>
   /// Helper function: Gets the system dependent install path.
   /// </summary>
   /// <returns>System.String.</returns>
   static public string GetInstallPath()
   {
      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         return "";
      }
      string path = Application.dataPath;
      if (Application.platform == RuntimePlatform.OSXPlayer)
      {
         path += "/../../";
      }
      else if (Application.platform == RuntimePlatform.WindowsPlayer)
      {
         path += "/../";
      }
#if UNITY_WEBPLAYER
        else if (Application.platform == RuntimePlatform.OSXWebPlayer || Application.platform == RuntimePlatform.WindowsWebPlayer) {
            //			path = Application.absoluteURL;
        }
#endif
      else if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.OSXEditor)
      {
         path += "/../";
      }
      else
      {
         path += "/../";
         Debug.Log("Application type not accounted for in OmnityHelperFunctions.GetInstallPath()\r\nReturning " + path);
      }
      return path;
   }

   /// <summary>
   /// Helper function: Makes the sure config directory exists.
   /// </summary>
   static public void MakeSureConfigDirectoryExists()
   {
      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         return;
      }
      System.IO.Directory.CreateDirectory(configDirectory);
   }

   /// <summary>
   /// internal function check if this config is exists as a config file to prevent a non-existing file from being loaded
   /// </summary>
   /// <param name="filename">The filename.</param>
   static public bool DoesBaseConfigExist(string filename)
   {
      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         return false;
      }
      return System.IO.File.Exists(filename);
   }

   /// <summary>
   /// Loads a config file into the omnity class OC
   /// </summary>
   /// <param name="filename">The filename.</param>
   /// <param name="OC">The omnity class</param>
   static public void LoadConfig(string filename, Omnity OC)
   {
      // TCW: Check if the given filename sans extension is available in the resources as a TextAsset
      string resFilename = filename.Substring(0, filename.Length - 4);
      TextAsset xmlTextAsset = (TextAsset)Resources.Load(resFilename, typeof(TextAsset));
      if (xmlTextAsset != null)
      {
         OmnityHelperFunctions.LoadXML(filename, OC.ReadXML);  // this will do the same

         foreach (FinalPassCamera myFinalPassCamera in OC.finalPassCameras)
         {
            myFinalPassCamera.myOmnityGUI = OC.myOmnityGUI;
         }
         OC.StartCoroutine(CoroutinePostLoad(OC));
         return;
      }

      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         return;
      }
      if (OC == null)
      {
         Debug.LogError("Omnity not connected");
         return;
      }

      if (filename != null)
      {
         if (System.IO.File.Exists(filename))
         {
            OmnityHelperFunctions.LoadXML(filename, OC.ReadXML);
         }
         else
         {
            Debug.Log("Config file " + filename + " Missing.  Using built in config.");
         }
         foreach (FinalPassCamera myFinalPassCamera in OC.finalPassCameras)
         {
            myFinalPassCamera.myOmnityGUI = OC.myOmnityGUI;
         }
      }
      OC.StartCoroutine(CoroutinePostLoad(OC));
   }

   static private bool _LoadingNow = false;

   static public bool LoadingNow
   {
      get { return _LoadingNow; }
      private set { _LoadingNow = value; }
   }

   static private IEnumerator CoroutinePostLoad(Omnity OC)
   {
      OC.FireConfigLoaded();

      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         yield break;
      }

      if (!LoadingNow)
      {
         LoadingNow = true;
         yield return OC.StartCoroutine(Omnity.onReloadEndCallbackPriority.CoroutineCallPriorityEventHandler(OC));
         LoadingNow = false;
      }
      yield break;
   }

   /// <summary>
   /// Saves the config.
   /// </summary>
   /// <param name="filename">The filename.</param>
   /// <param name="anOmnity">The OC.</param>
   static public void SaveConfig(string filename, Omnity anOmnity)
   {
      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         return;
      }

      anOmnity.configFileChooser.configXMLFilename2 = System.IO.Path.GetFileNameWithoutExtension(filename) + ".xml";
      Debug.Log("Saving " + filename);
      MakeSureConfigDirectoryExists();
      OmnityHelperFunctions.SaveXML(filename, anOmnity.WriteXML);
      var path = System.IO.Path.Combine(configDirectory, System.IO.Path.GetFileNameWithoutExtension(filename));
      Debug.Log("Saving " + path);
      System.IO.Directory.CreateDirectory(path);
      anOmnity.StartCoroutine(CoroutineCaptureScreenshot(anOmnity));
   }

   static public bool takingScreenShot = false;

   static public IEnumerator CoroutineCaptureScreenshot(Omnity anOmnity)
   {
      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         yield break;
      }
      takingScreenShot = true;
      yield return null; //needed
      bool t = false;
      if (anOmnity.myOmnityGUI != null)
      {
         t = anOmnity.myOmnityGUI.enabled;
         anOmnity.myOmnityGUI.GUIEnabled = false;
      }
      yield return new WaitForEndOfFrame();

      try
      {
         string replace = "_";
         if (Application.isEditor)
         {
            replace += "editorshot";
         }
         else
         {
            replace += "screenshot";
         }
         if (OmnityGraphicsInfo.bDirectX)
         {
            replace += "_DX.png";
         }
         else
         {
            replace += "_GL.png";
         }
         CaptureScreenshot(OmnityLoader.AddSpecialConfigPath(anOmnity, anOmnity.configFileChooser.configXMLFilename2.Replace(".xml", replace)));
      }
      catch (System.Exception e)
      {
         Debug.LogError("CaptureScreenshotError : " + e.Message);
      }
      yield return new WaitForEndOfFrame();
      if (anOmnity.myOmnityGUI != null)
      {
         anOmnity.myOmnityGUI.GUIEnabled = t;
      }
      takingScreenShot = false;
   }

   static public void CaptureScreenshot(string filename)
   {
      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         return
             ;
      }
      try
      {
         Debug.Log("Saving " + filename);
         bool png = filename.EndsWith(".png");
         bool jpg = filename.EndsWith(".jpg");
         if (!OmnityPlatformDefines.EncodeToJPGSupport() && jpg)
         {
            filename = filename.Replace(".jpg", ".png");
            jpg = false;
            png = true;
            Debug.Log("Jpg encoding not supported on this version, fallback to png");
         }

         if (png || jpg)
         {
            var width = Screen.width;
            var height = Screen.height;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            // Read screen contents into the texture
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            // Encode texture into PNG
            byte[] bytes = null;
            if (png)
            {
               bytes = tex.EncodeToPNG();
            }
            else if (OmnityPlatformDefines.EncodeToJPGSupport() && jpg)
            {
               bytes = OmnityPlatformDefines.EncodeToJPG(tex);
            }
            MonoBehaviour.Destroy(tex);
            OmnityPlatformDefines.WriteAllBytes(filename, bytes);
         }
         else
         {
            Debug.LogError("Must save screenshot as .png(or .jpg)");
         }
      }
      catch (System.Exception e)
      {
         Debug.LogError(e.Message);
      }
   }

   public static void DeleteConfig(string v, Omnity omnity)
   {
      if (!OmnityPlatformDefines.LoadSaveSupported())
      {
         return;
      }
      string dir = v.Replace(".xml", "");
      if (System.IO.Directory.Exists(dir))
      {
         OmnityPlatformDefines.DirectoryDelete(v.Replace(".xml", ""), true);
      }
   }
}

/// <summary>
/// This class stores the important render texture settings.  This allows the one to tweak the way the render textures are generated at deployment.  For example, if the dome display had a very high resolution projector, setting renderTextureSettings.width = 4096 and renderTextureSettings.height = 4096 would produce a finer quality rendering.
/// Most the variables in this class are used build a Unity3d RenderTexture.  Please see the docs for more specific information on what these variables do.
/// http://docs.unity3d.com/Documentation/ScriptReference/RenderTexture.html
/// </summary>
[System.Serializable]
public class RenderTextureSettings
{

   public RenderTextureSettings(bool _enabled)
   {
      enabled = _enabled;
   }

   public bool enabled;

   /// <summary>
   /// The width of the render texture.  Use power of 2 numbers for best results.  2048 is recommended.  The maximum is system dependent but it is usually 4096.
   /// </summary>
   public int width = 2048;

   /// <summary>
   /// The height of the render texture.  Use power of 2 numbers for best results.  2048 is recommended.  The maximum is system dependent but it is usually 4096.
   /// </summary>
   public int height = 2048;

   /// <summary>
   /// use mip mapping in the render texture.  Experiment with turning this off for faster rendering.
   /// </summary>
   public bool mipmap = false;

   /// <summary>
   /// use antiAliasing in the render texture.
   /// The value is specified as a power of two ( 1, 2, 4 or 8) indicating the number of multisamples per pixel.
   /// </summary>
   public AntiAliasingSampleCount antiAliasing = AntiAliasingSampleCount.AASampleCount_8;

   /// <summary>
   /// The antialiasing level for the RenderTexture.
   /// The value is specified as a power of two ( 1, 2, 4 or 8) indicating the number of multisamples per pixel.
   /// </summary>
   public enum AntiAliasingSampleCount
   {
      AASampleCount_1 = 1,
      AASampleCount_2 = 2,
      AASampleCount_4 = 4,
      AASampleCount_8 = 8
   }

   /// <summary>
   /// </summary>
   public int depth = 24;

   /// <summary>
   /// </summary>
   public int anisoLevel = 2;

   /// <summary>
   /// </summary>
   public float mipMapBias = 0;

   /// <summary>
   /// </summary>
   public RenderTextureFormat myRenderTextureFormat = RenderTextureFormat.Default;

   /// <summary>
   /// </summary>
   public TextureWrapMode wrapMode = TextureWrapMode.Clamp;

   /// <summary>
   /// </summary>
   public FilterMode filterMode = FilterMode.Trilinear;

   /// <summary>
   /// The render texture for this camera.
   /// </summary>
   public RenderTexture rt = null;

   /// <summary>
   /// Internal function: Generates the render texture
   /// </summary>
   /// <returns>RenderTexture</returns>
   public RenderTexture GenerateRenderTexture()
   {
      if (rt == null)
      {
         rt = new RenderTexture(width, height, depth, myRenderTextureFormat);
         rt.useMipMap = mipmap;

         OmnityPlatformDefines.SetAntiAliasing(rt, (int)antiAliasing);
         rt.filterMode = filterMode;
         rt.wrapMode = wrapMode;
         rt.mipMapBias = mipMapBias;
         rt.anisoLevel = anisoLevel;
         rt.filterMode = filterMode;
      }
      return rt;
   }

   /// <summary>
   /// Internal function: Write the config file XML
   /// </summary>
   /// <param name="xmlWriter">The XML writer.</param>
   /// <exclude/>
   public void WriteXML(System.Xml.XmlTextWriter xmlWriter)
   {
      xmlWriter.WriteStartElement("RenderTextureSettings");
      xmlWriter.WriteElementString("enabled", enabled.ToString());
      xmlWriter.WriteElementString("width", width.ToString());
      xmlWriter.WriteElementString("height", height.ToString());
      xmlWriter.WriteElementString("mipmap", mipmap.ToString());
      xmlWriter.WriteElementString("antiAliasing", antiAliasing.ToString());
      xmlWriter.WriteElementString("depth", depth.ToString());
      xmlWriter.WriteElementString("anisoLevel", anisoLevel.ToString());
      xmlWriter.WriteElementString("mipMapBias", mipMapBias.ToString());

      xmlWriter.WriteElementString("myRenderTextureFormat", myRenderTextureFormat.ToString());
      xmlWriter.WriteElementString("wrapMode", wrapMode.ToString());
      xmlWriter.WriteElementString("filterMode", filterMode.ToString());
      xmlWriter.WriteElementString("guiExpanded", guiExpanded.ToString());

      xmlWriter.WriteEndElement();
   }

   private string expanderHash = OmnityHelperFunctions.OmnityUniqueHash.uniqueHash;

   /// <summary>
   /// Internal function for reading XML config file
   /// </summary>
   /// <exclude/>
   public void ReadXML(System.Xml.XPath.XPathNavigator nav)
   {
      enabled = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//enabled", enabled);
      width = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//width", width);
      height = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//height", height);
      mipmap = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//mipmap", mipmap);
      antiAliasing = OmnityHelperFunctions.ReadElementEnumDefault<AntiAliasingSampleCount>(nav, ".//antiAliasing", antiAliasing);
      depth = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//depth", depth);
      anisoLevel = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//anisoLevel", anisoLevel);
      mipMapBias = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//mipMapBias", mipMapBias);
      myRenderTextureFormat = OmnityHelperFunctions.ReadElementEnumDefault<RenderTextureFormat>(nav, ".//myRenderTextureFormat", myRenderTextureFormat);
      wrapMode = OmnityHelperFunctions.ReadElementEnumDefault<TextureWrapMode>(nav, ".//wrapMode", wrapMode);
      filterMode = OmnityHelperFunctions.ReadElementEnumDefault<FilterMode>(nav, ".//filterMode", filterMode);
      guiExpanded = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//guiExpanded", guiExpanded);
   }

   public RenderTextureSettings Clone()
   {
      return new RenderTextureSettings(this.enabled)
      {
         enabled = this.enabled,
         width = this.width,
         height = this.height,
         mipmap = this.mipmap,
         antiAliasing = this.antiAliasing,
         depth = this.depth,
         anisoLevel = this.anisoLevel,
         mipMapBias = this.mipMapBias,
         wrapMode = this.wrapMode,
         filterMode = this.filterMode,
         guiExpanded = this.guiExpanded,
         myRenderTextureFormat = this.myRenderTextureFormat
      };
   }

   /// <summary>
   /// internal component used by the GUI
   /// </summary>
   /// <exclude/>
   private bool guiExpanded = false;

   /// <summary>
   /// internal component used by the GUI
   /// </summary>
   /// <exclude/>
   public bool OnGUI(bool drawMain, float previewSize, bool isButton = false)
   {
      if (drawMain)
      {
         OmnityHelperFunctions.BeginExpander(ref guiExpanded, "RenderTextureSettings");
         if (guiExpanded)
         {
            enabled = OmnityHelperFunctions.BoolInput("enabled", enabled);
            if (enabled)
            {
               width = OmnityHelperFunctions.IntInputReset("width", width, 2048);
               height = OmnityHelperFunctions.IntInputReset("height", height, 2048);
               depth = OmnityHelperFunctions.IntInputReset("depth", depth, 24);
               myRenderTextureFormat = OmnityHelperFunctions.EnumInputReset<RenderTextureFormat>(expanderHash, "renderTextureFormat", myRenderTextureFormat, RenderTextureFormat.Default, 1);
               wrapMode = OmnityHelperFunctions.EnumInputReset<TextureWrapMode>(expanderHash, "wrapMode", wrapMode, TextureWrapMode.Clamp, 1);

               antiAliasing = OmnityHelperFunctions.EnumInputReset<AntiAliasingSampleCount>(expanderHash, "antiAliasing", antiAliasing, AntiAliasingSampleCount.AASampleCount_8, 0);
               filterMode = OmnityHelperFunctions.EnumInputReset<FilterMode>(expanderHash, "filterMode", filterMode, FilterMode.Trilinear, 1);
               anisoLevel = OmnityHelperFunctions.IntInputReset("anisoLevel", anisoLevel, 2);
               mipmap = OmnityHelperFunctions.BoolInputReset("mipmap", mipmap, true);
               mipMapBias = OmnityHelperFunctions.FloatInputReset("mipMapBias", mipMapBias, 0);
               GUILayout.Label("Settings file must be saved and reloaded for changes to take place.  Fastest option is 1X ");
            }
         }
         OmnityHelperFunctions.EndExpander();
      }

      bool returnval = false;
      if (enabled && null != rt)
      {
         if (width > 0 && height > 0)
         {
            if (isButton)
            {
               returnval = (GUILayout.Button(rt, GUILayout.Width(previewSize), GUILayout.Height(previewSize * height / width)));
            }
            else
            {
               GUILayout.Box(rt, GUILayout.Width(previewSize), GUILayout.Height(previewSize * height / width));
            }
            //GUILayout.Box(rt, GUILayout.Width(previewSize), GUILayout.Height(previewSize*height/width));
         }
      }
      return returnval;
   }
}

/// <summary>
/// The camera array
/// This is a PerspectiveCamera array.  Each PerspectiveCamera element is the definition of a camera that generates part of the perspective, on load a Unity3D camera is generated that uses the parameters defined in the array element.
/// Inside of the OmnityUpdatePhase() function, the each PerspectiveCamera captures the scene into a RenderTexture.
/// This is considered the first pass of the Perspective Projection Mapping Algorithm as described in: http://www.clementshimizu.com/0009-Omnimap/Shimizu_ProjectivePerspectiveMapping.pdf
/// </summary>
[System.Serializable]
public class PerspectiveCamera
{

   /// <summary>
   /// The name of camera.  This is a convenience property. used to disambiguate between various perspective cameras
   /// </summary>
   public string name = "Perspective Camera";

   /// <summary>
   /// Flag to alert omnity to serialize the camera when saving/loading
   /// needed when using plugins that generate cameras dynamically that should not be serialized
   /// </summary>
   public bool serializeCamera = true;

   /// <summary>
   /// Flag to alert omnity to automatically connect the camera texture to the final pass camera
   /// needed when using plugins that generate cameras that follow other conventions
   /// </summary>
   public bool automaticallyConnectTextures = true;

   /// <summary>
   /// Gets the default_ theater simple.
   /// </summary>
   /// <returns>PerspectiveCamera[][].</returns>
   static public PerspectiveCamera[] getDefault_TheaterSimple()
   {
      PerspectiveCamera[] portalDefaults = new PerspectiveCamera[3];
      for (int i = 0; i < portalDefaults.Length; i++)
      {
         portalDefaults[i] = new PerspectiveCamera();
      }

      portalDefaults[0].name = "Front";
      portalDefaults[1].name = "Right";
      portalDefaults[2].name = "Left";

      portalDefaults[0].localEulerAngles = new Vector3(0, 0, 45);
      portalDefaults[1].localEulerAngles = new Vector3(-45, 90, 0);
      portalDefaults[2].localEulerAngles = new Vector3(-45, -90, 0);

      return portalDefaults;
   }

   /// <summary>
   /// The local euler angles relative of the perspective camera
   /// </summary>
   public Vector3 localEulerAngles = new Vector3();

   /// <summary>
   /// The local position of the perspective camera.
   /// </summary>
   public Vector3 localPosition = new Vector3();

   /// <summary>
   /// a partial component of the field of view of the perspective camera.  This is used when computing the total view of this camera.  A total view uses the Left, Right, Top, and Bottom.
   /// </summary>
   public OmnityPerspectiveMatrix omnityPerspectiveMatrix = new OmnityPerspectiveMatrix();

   /// <summary>
   /// The render texture settings
   /// </summary>
   public RenderTextureSettings renderTextureSettings = new RenderTextureSettings(true);

   /// <summary>
   /// The culling mask of this camera.  This is used by Unity3D to determine what layers are seen by this camera
   /// http://docs.unity3d.com/Documentation/ScriptReference/Camera-cullingMask.html
   /// </summary>
   public LayerMask cullingMask = ~(1 << 30 | 1 << 31);

   /// <summary>
   /// the transform of the Camera.  This will be null at the start of loading, and connected the camera's transform after load.
   /// </summary>
   public Transform myCameraTrans;

   /// <summary>
   /// the Unity3D Camera that generates the view.
   /// </summary>
   public Camera myCamera;

   /// <summary>
   /// Elementwise Copy function
   /// </summary>
   public PerspectiveCamera Clone()
   {
      PerspectiveCamera p = this;
      PerspectiveCamera s = new PerspectiveCamera();
      s.name = p.name + " (copy)";
      s.localEulerAngles = p.localEulerAngles;
      s.localPosition = p.localPosition;
      s.omnityPerspectiveMatrix.fovL = p.omnityPerspectiveMatrix.fovL;
      s.omnityPerspectiveMatrix.fovR = p.omnityPerspectiveMatrix.fovR;
      s.omnityPerspectiveMatrix.fovT = p.omnityPerspectiveMatrix.fovT;
      s.omnityPerspectiveMatrix.fovB = p.omnityPerspectiveMatrix.fovB;
      s.omnityPerspectiveMatrix.near = p.omnityPerspectiveMatrix.near;
      s.omnityPerspectiveMatrix.far = p.omnityPerspectiveMatrix.far;
      s.omnityPerspectiveMatrix.nearOrtho = p.omnityPerspectiveMatrix.nearOrtho;
      s.omnityPerspectiveMatrix.farOrtho = p.omnityPerspectiveMatrix.farOrtho;
      s.serializeCamera = p.serializeCamera;
      s.renderTextureSettings = p.renderTextureSettings.Clone();
      s.cullingMask = p.cullingMask;
      return s;
   }

   /// <summary>
   /// Internal Function: Writes the XML.
   /// </summary>
   /// <param name="xmlWriter">The XML writer.</param>
   /// <exclude/>
   public void WriteXML(System.Xml.XmlTextWriter xmlWriter)
   {
      if (serializeCamera)
      {
         xmlWriter.WriteStartElement("PerspectiveCamera");
         xmlWriter.WriteElementString("name", name);
         xmlWriter.WriteElementString("localEulerAngles", localEulerAngles.ToString("F4"));
         xmlWriter.WriteElementString("localPosition", localPosition.ToString("F4"));
         omnityPerspectiveMatrix.WriteXMLInline(xmlWriter);
         xmlWriter.WriteElementString("cullingMask", cullingMask.value.ToString());

         xmlWriter.WriteElementString("guiExpanded", guiExpanded.ToString());
         xmlWriter.WriteElementString("automaticallyConnectTextures", automaticallyConnectTextures.ToString());

         renderTextureSettings.WriteXML(xmlWriter);
         xmlWriter.WriteEndElement();
      }
   }

   /// <summary>
   /// Internal Function: Reads the XML.
   /// </summary>
   /// <param name="nav">The nav.</param>
   /// <exclude/>
   public void ReadXML(System.Xml.XPath.XPathNavigator nav)
   {
      name = OmnityHelperFunctions.ReadElementStringDefault(nav, ".//name", name);

      omnityPerspectiveMatrix.ReadXMLInline(nav);
      cullingMask = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//cullingMask", cullingMask.value);
      localEulerAngles = OmnityHelperFunctions.ReadElementVector3Default(nav, ".//localEulerAngles", localEulerAngles);
      localPosition = OmnityHelperFunctions.ReadElementVector3Default(nav, ".//localPosition", localPosition);

      guiExpanded = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//guiExpanded", guiExpanded);
      automaticallyConnectTextures = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//automaticallyConnectTextures", automaticallyConnectTextures);
  

      renderTextureSettings = new RenderTextureSettings(true);
      renderTextureSettings.ReadXML(nav);
   }

   /// <summary>
   /// </summary>
   /// <exclude/>
   private bool guiExpanded = false;

   /// <summary>
   /// Internal component for GUI handling
   /// </summary>
   /// <exclude/>
   public void OnGUI()
   {
      if (!serializeCamera)
      {
         OmnityHelperFunctions.BeginExpander(ref guiExpanded, "AutoGeneratedCamera", "AutoGeneratedCamera");
      }
      else
      {
         OmnityHelperFunctions.BeginExpander(ref guiExpanded, name, name);
      }

      if (guiExpanded)
      {
         name = OmnityHelperFunctions.StringInputReset("name", name, "Perspective Camera");
         localPosition = OmnityHelperFunctions.Vector3InputReset("localPosition", localPosition, Vector3.zero);
         localEulerAngles = OmnityHelperFunctions.Vector3InputReset("localEulerAngles", localEulerAngles, Vector3.zero);

         cullingMask = OmnityHelperFunctions.LayerMaskInputReset("cullingMask" + name, "cullingMask", cullingMask, 1 << 30, true);
         automaticallyConnectTextures = OmnityHelperFunctions.BoolInputReset("automaticallyConnectTextures", automaticallyConnectTextures, true);
         omnityPerspectiveMatrix.DrawWidget(OmnityProjectorType.Rectilinear);
         renderTextureSettings.OnGUI(true, 256);
      }
      else
      {
         renderTextureSettings.OnGUI(false, 64);
      }
      OmnityHelperFunctions.EndExpander();
   }

   /// <summary>
   /// Updates the Unity3D camera's parameters with the parameters specified by the this class.
   /// </summary>
   public void UpdateCamera()
   {
      if (myCamera == null)
      {
         if (Omnity.anOmnity.debugLevel >= DebugLevel.High)
         {
            Debug.Log("Camera invalid during reset (this is normal)");
         }
         return;
      }

      /// ASSUME RECTILINEAR
      myCamera.nearClipPlane = omnityPerspectiveMatrix.near;
      myCamera.farClipPlane = omnityPerspectiveMatrix.far;
      myCameraTrans.localPosition = localPosition;
      myCameraTrans.localEulerAngles = localEulerAngles;
      myCamera.orthographic = false;
      myCamera.projectionMatrix = omnityPerspectiveMatrix.GetMatrix(OmnityProjectorType.Rectilinear);
   }
		

   /// <summary>
   /// Internal function that generates a new perspective cameras around an existing camera.  If the D is null than it will function will still work.
   /// </summary>
   /// <param name="d">The source camera.</param>
   public void SpawnCameraAround(Omnity anOmnity)
   {
      Camera flatCamera = anOmnity.GetComponent<Camera>();// may be null;

      myCameraTrans = (new GameObject(name)).transform;
      myCameraTrans.parent = anOmnity.transformRenderChannels;
      myCamera = myCameraTrans.gameObject.AddComponent<Camera>();
      if (Omnity.DisableCameraEventsMask)
      {
         myCamera.eventMask = 0;
      }
      myCamera.enabled = false;
      myCamera.fieldOfView = 90;
      if (flatCamera != null)
      {
         Skybox sb = flatCamera.GetComponent<Skybox>();
         if (sb != null)
         {
            myCameraTrans.gameObject.AddComponent<Skybox>().material = sb.material;
         }
         myCamera.depth = flatCamera.depth;
         myCamera.backgroundColor = flatCamera.backgroundColor;
         myCamera.clearFlags = flatCamera.clearFlags;
      }
      myCamera.cullingMask = cullingMask;

      if (renderTextureSettings.enabled)
      {
         myCamera.targetTexture = renderTextureSettings.GenerateRenderTexture();
      }

      myCamera.gameObject.AddComponent<PerspectiveCameraProxy>().myPerspectiveCamera = this;
      // render layers
      UpdateCamera();
   }

   public void RenderUsingCameraSettings(Camera sourceCamera, int? sourceCullingMask = null, Omnity.PerspectiveCameraDelegate preAction = null, Omnity.PerspectiveCameraDelegate postAction = null, float nearClipScaleToMakeSmaller = 1.0f)
   {
      // ASSUME THAT WE ARE RECTILINEAR
      if (sourceCamera == null || myCamera == null)
      {
         return;
      }

      OmnityPlatformDefines.CopyClipPlanes(omnityPerspectiveMatrix, sourceCamera, nearClipScaleToMakeSmaller);

      myCamera.backgroundColor = sourceCamera.backgroundColor;
      if (myCamera != null)
      {
         myCamera.clearFlags = sourceCamera.clearFlags;
         myCamera.cullingMask = cullingMask = (sourceCullingMask != null ? sourceCullingMask.GetValueOrDefault() : sourceCamera.cullingMask);
         if (preAction != null)
         {
            preAction(this);
         }
         UpdateCamera();
         bool wasEnabled = myCamera.enabled;
         myCamera.enabled = true;
         myCamera.Render();
         myCamera.enabled = wasEnabled;
         if (postAction != null)
         {
            postAction(this);
         }
      }
   }
}

/// <summary>
/// This class helps remind the user remember to upgrade Omnity when needed.
/// </summary>
/// <exclude/>
public class RegistrationInfo
{
   // this function
   // checks for latest update to omnity with Elumenati's update server

   /// <summary>
   /// The behavior of this function is dependent on if the app is running in the editor or not. In the editor, if a new version exists then it will alert the developer.  If running in the build, it will output Omnity's version number to the log.
   /// </summary>
   /// <returns>IEnumerator.</returns>
   public IEnumerator InitCoroutine()
   {
      if (UnityEngine.Application.isEditor)
      {
         WWW www;
         try
         {
            www = new WWW(Omnity.omnitySoftwareURL + "updates/updates.php");
         }
         catch
         {
            yield break;
         }

         yield return www;
         try
         {
            if (www.error == null)
            {
               foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(www.text, @"[,]*([^=]+)=([^,]*)"))
               {
                  if (m.Groups[1].Value.Contains("DownloadLocation"))
                  {
                     Omnity.omnitySoftwareURL = m.Groups[2].Value.Trim();
                  }
                  else if (m.Groups[1].Value.Contains("LatestVersion"))
                  {
                     if (Omnity.omnityVersion < int.Parse(m.Groups[2].Value.Trim()))
                     {
                        Debug.LogWarning("Latest Omnity Version is " + m.Groups[2].Value + " currently using " + Omnity.omnityVersion);
                        Debug.LogWarning("Download a new version at " + Omnity.omnitySoftwareURL);
                     }
                  }
               }
            }
         }
         catch
         {
         }
      }
      else
      {
         Debug.Log("Find out about Elumenati Omnity projection mapping software at " + Omnity.omnitySoftwareURL + "\r\nInitializing Omnity:" + Omnity.omnityVersion);
         yield return null;
      }
   }
}

[System.Serializable]
public class OmnityTransformInfo
{

   /// <summary>
   /// </summary>
   public Vector3 position;

   /// <summary>
   /// </summary>
   public Vector3 localEulerAngles;

   public OmnityTransformInfo Clone()
   {
      return new OmnityTransformInfo
      {
         position = this.position,
         localEulerAngles = this.localEulerAngles
      };
   }
}

/// <summary>
/// This defines the final pass camera.  Although this is represented as a Camera inside of Unity3D, it really is the definition of the Fisheye Video Projector and the parameters should mimic the projection system hardware/physical layout.
/// </summary>
[System.Serializable]
public class FinalPassCamera
{

   /// <summary>
   /// The name
   /// </summary>
   public string name = "Final Pass Camera";

   /// <summary>
   /// hint to save when saving.Needed to prevent automatically spawned cameras, from saving
   /// </summary>
   public bool serialize = true;

   /// <summary>
   /// Internal Function: Gets an array that represents the default final pass cameras.  This is used when initializing and resetting the class in the inspector.
   /// </summary>
   /// <returns>FinalPassCamera array</returns>
   static public FinalPassCamera[] GetDefaultFinalPassCameras()
   {
      FinalPassCamera[] cameras = new FinalPassCamera[1];
      cameras[0] = new FinalPassCamera();
      cameras[0].transformInfo.localEulerAngles = new Vector3(-90, 0, 0);
      return cameras;
   }

   public enum OmniGUIType
   {
      appDefault = -2,
      noGUI = -1,
      GUI0 = 0,
      GUI1 = 1,
      GUI2 = 2,
      GUI3 = 3,
   }

   public OmniGUIType guiType = OmniGUIType.appDefault;

   public bool showWeDrawGUI
   {
      get
      {
         switch (guiType)
         {
            case OmniGUIType.appDefault:
               return name.ContainsCaseInsensitiveSimple("flat") || Omnity.anOmnity.finalPassCameras.Length <= 1;

            case OmniGUIType.noGUI:
               return false;

            case OmniGUIType.GUI0:
               return true;

            case OmniGUIType.GUI1:
               return true;

            case OmniGUIType.GUI2:
               return true;

            case OmniGUIType.GUI3:
               return true;

            default:
               Debug.LogError("unkown gui type");
               return false;
         }
      }
   }

   /// <summary>
   /// A class that represents Position and LocalEulerAngles as Vector3.  This is represented as a Class instead of a struct.  If you have two final pass cameras and you set finalPassCameraA.transformInfo = finalPassCameraB.transformInfo, then the two cameras will track each other, and changes to either will change both.
   /// Situations where this would be useful: active stereo projection,  Multiple pass scenes.
   /// </summary>
   public bool startEnabled = true;

   /// <summary>
   /// A class that represents the camera's Position and LocalEulerAngles as Vector3.  This is represented as a Class instead of a struct.  If you have two final pass cameras and you set finalPassCameraA.transformInfo = finalPassCameraB.transformInfo, then the two cameras will track each other, and changes to either will change both.
   /// Situations where this would be useful: active stereo projection,  Multiple pass scenes.
   /// </summary>
   public OmnityTransformInfo transformInfo = new OmnityTransformInfo();

   /// <summary>
   /// The scale, If isOrtho is true adjusting this will scale the image circle larger or smaller.
   /// Warning: If isOrtho is false, then scale will be interpreted as FOV and the behavior is not guaranteed.
   /// </summary>
   public float scale = 1;

   /// <summary>
   /// The normalized viewport rect. of the final pass camera
   /// this will adjust where the image will land on the screen.
   /// Rect(0, 0, 1, 1) is fullscreen
   /// In multi monitor setups,  where the image spans across two displays, this can be used to target which display the spherical rendering lands on, leaving the primary monitor for flat screen rendering and gui elements.
   ///  For example if you had a unity window spanning across two displays [FlatRendering][DomeRendering].
   /// 	 Use Rect(0, 0, .5, 1) for the Flat Window and Rect(.5, 0, .5, 1) for the dome rendering window
   ///
   /// </summary>
   public Rect normalizedViewportRect = new Rect(0, 0, 1, 1);

   /// <summary>
   /// The background color
   /// </summary>
   public Color backgroundColor = new Color(0, 0, 0, 0);

   /// <summary>
   /// The clear flags
   /// </summary>
   public CameraClearFlags clearFlags = CameraClearFlags.SolidColor;

   /// <summary>
   /// The culling mask of this camera.  This is used by UNITY3D to determine what layers are seen by this camera.
   /// http://docs.unity3d.com/Documentation/ScriptReference/Camera-cullingMask.html
   /// </summary>
   public LayerMask cullingMask = 1 << 30;

   /// <summary>
   /// My camera
   /// </summary>
   public Camera myCamera;

   /// <summary>
   /// My camera transform
   /// </summary>
   public Transform myCameraTransform;

   /// <summary>
   /// The normalized lens offset
   /// </summary>
   public bool normalizedLensOffset = true; // use with lensOffsetY : +/- .25 is for 4/3 screen   +/- 0.4375 is for 16/9   +/- 0.375 is for 16/10

   /// <summary>
   /// The lens offset Y
   /// </summary>
   public float lensOffsetY = -1;// use with lensOffsetY :  0 is for center, 1 is for pin to top, -1  for pin to bottom

   public float lensScaleX = 1;
   public float lensScaleY = 1;
   public OmnityProjectorType projectorType = OmnityProjectorType.FisheyeTruncated;

   /// <summary>
   /// The add GUI layer
   /// </summary>
   public bool addGUILayer = false;

   /// <summary>
   /// My omnity GUI
   /// </summary>
   public OmnityGUI myOmnityGUI = null;

   public Omnity myOmnity
   {
      get
      {
         if (myOmnityGUI != null)
         {
            return myOmnityGUI.myOmnity;
         }
         else
         {
            return null;
         }
      }
   }

   /// <summary>
   /// Internal Function: Writes the XML.
   /// </summary>
   /// <param name="xmlWriter">The XML writer.</param>
   public void WriteXML(System.Xml.XmlTextWriter xmlWriter)
   {
      if (serialize)
      {
         xmlWriter.WriteStartElement("FinalPassCamera");
         xmlWriter.WriteElementString("name", name);
         xmlWriter.WriteElementString("position", transformInfo.position.ToString("F4"));
         xmlWriter.WriteElementString("localEulerAngles", transformInfo.localEulerAngles.ToString("F4"));
         xmlWriter.WriteElementString("scale", scale.ToString("F4"));

         xmlWriter.WriteElementString("normalizedLensOffset", normalizedLensOffset.ToString());
         xmlWriter.WriteElementString("lensOffsetY", lensOffsetY.ToString("F4"));
         xmlWriter.WriteElementString("lensScaleX", lensScaleX.ToString("F4"));
         xmlWriter.WriteElementString("lensScaleY", lensScaleY.ToString("F4"));
         xmlWriter.WriteElementString("GUIViewportScaleHint", GUIViewportScaleHint.ToString("F4"));

         xmlWriter.WriteElementString("projectorType", projectorType.ToString());
         omnityPerspectiveMatrix.WriteXMLInline(xmlWriter);

         xmlWriter.WriteElementString("startEnabled", startEnabled.ToString());

         xmlWriter.WriteElementString("normalizedViewportRect", normalizedViewportRect.ToString("R"));
         xmlWriter.WriteElementString("backgroundColor", backgroundColor.ToString("F4"));

         xmlWriter.WriteElementString("clearFlags", clearFlags.ToString());//OmnityHelperFunctions.ClearFlagsToString(clearFlags));
         xmlWriter.WriteElementString("cullingMask", cullingMask.value.ToString());
         xmlWriter.WriteElementString("guiExpanded", guiExpanded.ToString());
         xmlWriter.WriteElementString("guiType", guiType.ToString());

         renderTextureSettings.WriteXML(xmlWriter);

         xmlWriter.WriteEndElement();
      }
   }

   /// <summary>
   /// The GUI expanded
   /// </summary>
   private bool guiExpanded = false;

   private string expanderHash = OmnityHelperFunctions.OmnityUniqueHash.uniqueHash;

   /// <summary>
   /// Called when [GUI].
   /// </summary>
   public void OnGUI()
   {
      OmnityHelperFunctions.BeginExpander(ref guiExpanded, "FinalPassCamera", "FinalPassCamera : " + name);
      if (guiExpanded)
      {
         name = OmnityHelperFunctions.StringInputReset("name", name, "Final Pass Camera");
         startEnabled = OmnityHelperFunctions.BoolInputReset("startEnabled", startEnabled, true);
         transformInfo.position = OmnityHelperFunctions.Vector3InputReset("position", transformInfo.position, new Vector3(0, 0, 0));
         transformInfo.localEulerAngles = OmnityHelperFunctions.Vector3InputReset("localEulerAngles", transformInfo.localEulerAngles, new Vector3(0, 0, 0));
         backgroundColor = OmnityHelperFunctions.ColorInputReset("backgroundColor", backgroundColor, new Color(0, 0, 0, 0));
         clearFlags = OmnityHelperFunctions.EnumInputReset<CameraClearFlags>(expanderHash, "clearFlags", clearFlags, CameraClearFlags.Color, 1);
         cullingMask = OmnityHelperFunctions.LayerMaskInputReset("FinalPassCamera.cullingMask" + name, "cullingMask", cullingMask, 1 << 30, true);

         projectorType = OmnityHelperFunctions.EnumInputReset<OmnityProjectorType>(expanderHash, "projectorType", projectorType, OmnityProjectorType.FisheyeTruncated, 1);
         omnityPerspectiveMatrix.DrawWidget(projectorType);
         lensOffsetY = OmnityHelperFunctions.FloatInputReset("lensOffsetY", lensOffsetY, projectorType == OmnityProjectorType.FisheyeTruncated ? (normalizedLensOffset ? -1.0f : -.25f) : 0.0f);
         lensScaleX = OmnityHelperFunctions.FloatInputReset("lensScaleX", lensScaleX, 1.0f);
         lensScaleY = OmnityHelperFunctions.FloatInputReset("lensScaleY", lensScaleY, 1.0f);
         normalizedLensOffset = OmnityHelperFunctions.BoolInputReset("normalizedLensOffset", normalizedLensOffset, true);
         scale = OmnityHelperFunctions.FloatInputReset("scale", scale, 1);
         GUIViewportScaleHint = OmnityHelperFunctions.FloatInputReset("GUIViewportScaleHint", GUIViewportScaleHint, 1);

         guiType = OmnityHelperFunctions.EnumInputReset<OmniGUIType>("OMNIGUITYPE", "GUI Type", guiType, OmniGUIType.appDefault, 1);

         GUILayout.Label("(For truncated projection set: projectorType=FisheyeTruncated, normalizedLensOffset=true, lensOffsetY = 1 (Portal style) or -1 (Theater Style)\nFor Fulldome set: projectorType = FisheyeFullDome, normalizedLensOffset=true,lensOffsetY = 0");

         if (renderTextureSettings == null)
         {
            Debug.LogError("renderTextureSettings == null");
         }
         else if (myOmnityGUI == null)
         {
            Debug.LogError("myOmnityGUI == null");
         }
         else if (myOmnityGUI.myOmnity == null)
         {
            Debug.LogError("myOmnityGUI.myOmnity == null");
         }
         else if (!renderTextureSettings.enabled)
         {
            normalizedViewportRect = OmnityHelperFunctions.RectInputReset("normalizedViewportRect", normalizedViewportRect, new Rect(0, 0, 1, 1), ref myOmnityGUI.myOmnity.usePixel, ref viewportRectMouseState, myOmnity);
         }
         if (null != myCamera)
         {
            myCamera.rect = normalizedViewportRect;
         }
         renderTextureSettings.OnGUI(true, 256);
      }
      else
      {
         renderTextureSettings.OnGUI(false, 64);
      }
      OmnityHelperFunctions.EndExpander();
   }

   public RenderTextureSettings renderTextureSettings = new RenderTextureSettings(false);

   private int? viewportRectMouseState = null;

   /// <summary>
   /// Internal Function: Reads the XML.
   /// </summary>
   /// <param name="nav">The nav.</param>
   public void ReadXML(System.Xml.XPath.XPathNavigator nav)
   {
      name = OmnityHelperFunctions.ReadElementStringDefault(nav, ".//name", name);
      transformInfo.position = OmnityHelperFunctions.ReadElementVector3Default(nav, ".//position", transformInfo.position);
      transformInfo.localEulerAngles = OmnityHelperFunctions.ReadElementVector3Default(nav, ".//localEulerAngles", transformInfo.localEulerAngles);
      scale = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//scale", scale);
      omnityPerspectiveMatrix.ReadXMLInline(nav);
      normalizedViewportRect = OmnityHelperFunctions.ReadElementRectDefault(nav, ".//normalizedViewportRect", normalizedViewportRect);
      backgroundColor = OmnityHelperFunctions.ReadElementColorDefault(nav, ".//backgroundColor", backgroundColor);

      cullingMask = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//cullingMask", cullingMask.value);
      clearFlags = OmnityHelperFunctions.ReadElementEnumDefault<CameraClearFlags>(nav, ".//clearFlags", clearFlags);

      projectorType = OmnityHelperFunctions.ReadElementEnumDefault<OmnityProjectorType>(nav, ".//projectorType", projectorType);
      guiType = OmnityHelperFunctions.ReadElementEnumDefault<OmniGUIType>(nav, ".//guiType", OmniGUIType.appDefault);

      startEnabled = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//startEnabled", startEnabled);

      lensOffsetY = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//lensOffsetY", lensOffsetY);
      lensScaleX = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//lensScaleX", lensScaleX);
      lensScaleY = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//lensScaleY", lensScaleY);
      GUIViewportScaleHint = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//GUIViewportScaleHint", GUIViewportScaleHint);
      normalizedLensOffset = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//normalizedLensOffset", normalizedLensOffset);

      guiExpanded = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//guiExpanded", guiExpanded);

      renderTextureSettings = new RenderTextureSettings(false);

      renderTextureSettings.ReadXML(nav);
   }

   public FinalPassCamera Clone(bool _copyRenderTextureSettings)
   {
      return new FinalPassCamera
      {
         name = this.name,
         transformInfo = transformInfo.Clone(),
         normalizedViewportRect = this.normalizedViewportRect,
         backgroundColor = this.backgroundColor,
         scale = this.scale,
         cullingMask = this.cullingMask,
         clearFlags = this.clearFlags,
         projectorType = this.projectorType,
         startEnabled = this.startEnabled,
         lensOffsetY = this.lensOffsetY,
         lensScaleX = this.lensScaleX,
         lensScaleY = this.lensScaleY,
         normalizedLensOffset = this.normalizedLensOffset,
         guiExpanded = this.guiExpanded,
         omnityPerspectiveMatrix = this.omnityPerspectiveMatrix.Clone(),
         GUIViewportScaleHint = this.GUIViewportScaleHint,
         serialize = this.serialize,

         renderTextureSettings = (_copyRenderTextureSettings ? this.renderTextureSettings.Clone() : new RenderTextureSettings(false)),
      };
   }

   /// <summary>
   /// Flag for displays that folow the tilt of the bracket hotkeys
   /// </summary>


   /// <summary>
   /// Generates the specified parent.
   /// </summary>
   /// <param name="parent">The parent.</param>
   public void SpawnCameraAround(Omnity anOmnity)
   {
      Transform myCameraTransformParent = anOmnity.transformFinalPassCameras;

      if (startEnabled)
      {
         myCameraTransform = (new GameObject(name)).transform;
         myCameraTransform.parent = myCameraTransformParent;
         myCameraTransform.localScale = new Vector3(1, 1, 1);
         myCamera = myCameraTransform.gameObject.AddComponent<Camera>();

         OmnityPlatformDefines.SetOcclusionCulling(myCamera, false);

         myCamera.backgroundColor = backgroundColor;
         myCamera.clearFlags = clearFlags;
         myCamera.cullingMask = cullingMask;
         myCamera.rect = normalizedViewportRect;
         if (addGUILayer)
         {
            myCameraTransform.gameObject.AddComponent<GUILayer>();
         }

         if (renderTextureSettings.enabled)
         {
            myCamera.targetTexture = renderTextureSettings.GenerateRenderTexture();
         }
         UpdateCamera();

         myCameraTransform.gameObject.AddComponent<FinalPassCameraProxy>().myFinalPassCamera = this;

         myOmnityGUI = anOmnity.myOmnityGUI;
      }
   }

   // this really only needs to be updated if the camera transform changes size...  like if you start growing..  ONLY Works correct if you grow uniformly
   /// <summary>
   /// Updates the camera.
   /// </summary>
   public void UpdateCamera()
   {
      if (startEnabled)
      {
         if (myCamera == null)
         {
            if (Omnity.anOmnity.debugLevel >= DebugLevel.High)
            {
               Debug.Log("Camera invalid during reset (this is normal)");
            }
            return;
         }

         myCamera.orthographicSize = scale * myCameraTransform.lossyScale.y;

         if (omnityPerspectiveMatrix.isOrthographic(projectorType))
         {
            myCamera.nearClipPlane = omnityPerspectiveMatrix.nearOrtho * myCameraTransform.lossyScale.y;
            myCamera.farClipPlane = omnityPerspectiveMatrix.farOrtho * myCameraTransform.lossyScale.y;
         }
         else
         {
            myCamera.nearClipPlane = omnityPerspectiveMatrix.near * myCameraTransform.lossyScale.y;
            myCamera.farClipPlane = omnityPerspectiveMatrix.far * myCameraTransform.lossyScale.y;
         }

         myCameraTransform.localPosition = transformInfo.position;
         myCameraTransform.localEulerAngles = transformInfo.localEulerAngles;

         switch (projectorType)
         {
            case OmnityProjectorType.Rectilinear:
               if (omnityPerspectiveMatrix.matrixMode == OmnityPerspectiveMatrix.MatrixMode.Orthographic)
               {
                  myCamera.orthographic = true;
                  myCamera.orthographicSize = omnityPerspectiveMatrix.orthographicSize;
               }
               else
               {
                  myCamera.orthographic = false;
                  if (omnityPerspectiveMatrix.matrixMode == OmnityPerspectiveMatrix.MatrixMode.HorizontalFOV)
                  {
                     myCamera.fieldOfView = omnityPerspectiveMatrix.fovL * 2.0f;
                  }
                  else
                  {
                     myCamera.projectionMatrix = omnityPerspectiveMatrix.GetMatrix(projectorType);
                  }
               }
               break;

            case OmnityProjectorType.FisheyeFullDome:
               myCamera.orthographic = true;
               myCamera.projectionMatrix = omnityPerspectiveMatrix.GetMatrix(projectorType);
               break;

            case OmnityProjectorType.FisheyeTruncated:
               myCamera.orthographic = true;
               myCamera.projectionMatrix = omnityPerspectiveMatrix.GetMatrix(projectorType);
               break;

            default:
               Debug.LogError("Unhandled projector type " + projectorType);
               myCamera.orthographic = true;
               myCamera.projectionMatrix = omnityPerspectiveMatrix.GetMatrix(projectorType);
               break;
         }
      }
   }

   public OmnityPerspectiveMatrix omnityPerspectiveMatrix = new OmnityPerspectiveMatrix();

   public float GUIViewportScaleHint = 1.0f;

   public Matrix4x4 GUISCALEMATRIX
   {
      get
      {
         return OmnityGUIHelper.GUISCALEMATRIX(normalizedViewportRect, GUIViewportScaleHint);
      }
   }

   public float subwindowHeightPixels
   {
      get
      {
         return OmnityGUIHelper.subwindowHeightPixels(normalizedViewportRect, GUIViewportScaleHint);
      }
   }

   public float subwindowWidthPixels
   {
      get
      {
         return OmnityGUIHelper.subwindowWidthPixels(normalizedViewportRect, GUIViewportScaleHint);
      }
   }
}

/// <summary>
/// This is a parametric representation of a float screen surface.  The parameters can be set through the XML file or the shift-f12 menu.  This could be used to make a wall
/// </summary>
[System.Serializable]
public class OmnityPlaneParams
{
   public float left = -.5f * 1.6f;
   public float right = .5f * 1.6f;
   public float top = .5f;
   public float bottom = -0.5f;

   /// <summary>
   /// The number of slices to generate
   /// </summary>
   public int slices = 60;

   /// <summary>
   /// The number of stacks to generate
   /// </summary>
   public int stacks = 60;

   /// <summary>
   /// normally we destroy the factory object that creates the mesh after it is loaded.  Set this to false if you need to change the dome during runtime.
   /// </summary>
   public bool destroyOnLoad = true;

   /// <summary>
   /// set this to true to regenerate the screen surface.  Used by the gui menu when the user changes the settings
   /// </summary>
   public bool flagRegenerateScreen = true;

   public static string xmlTag
   {
      get
      {
         return "PlaneParams";
      }
   }

   /// <summary>
   /// Internal Function: Writes the XML.
   /// </summary>
   /// <param name="xmlWriter">The XML writer.</param>
   public void WriteXML(System.Xml.XmlTextWriter xmlWriter)
   {
      xmlWriter.WriteStartElement(xmlTag);
      xmlWriter.WriteElementString("left", left.ToString());
      xmlWriter.WriteElementString("right", right.ToString());
      xmlWriter.WriteElementString("top", top.ToString());
      xmlWriter.WriteElementString("bottom", bottom.ToString());
      xmlWriter.WriteElementString("slices", slices.ToString());
      xmlWriter.WriteElementString("stacks", stacks.ToString());
      xmlWriter.WriteElementString("destroyOnLoad", destroyOnLoad.ToString());
      xmlWriter.WriteEndElement();
   }

   /// <summary>
   /// Internal Function: Reads the XML.
   /// </summary>
   /// <param name="nav">The nav.</param>
   public void ReadXML(System.Xml.XPath.XPathNavigator nav)
   {
      left = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//left", left);
      right = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//right", right);
      bottom = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//bottom", bottom);
      top = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//top", top);
      slices = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//slices", slices);
      stacks = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//stacks", stacks);
      destroyOnLoad = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//destroyOnLoad", destroyOnLoad);
      guiExpanded = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//guiExpanded", guiExpanded);
   }

   /// <summary>
   /// The GUI expanded
   /// </summary>
   private bool guiExpanded = false;

   /// <summary>
   /// Called when [GUI].
   /// </summary>
   public void OnGUI()
   {
      OmnityHelperFunctions.BeginExpander(ref guiExpanded, "PlaneParams");
      if (guiExpanded)
      {
         left = OmnityHelperFunctions.FloatInputReset("left", left, -.5f * 1.6f);
         right = OmnityHelperFunctions.FloatInputReset("right", right, .5f * 1.6f);
         top = OmnityHelperFunctions.FloatInputReset("top", top, .5f);
         bottom = OmnityHelperFunctions.FloatInputReset("bottom", bottom, -0.5f);

         slices = OmnityHelperFunctions.IntInputReset("slices", slices, 60);
         stacks = OmnityHelperFunctions.IntInputReset("stacks", stacks, 60);

         destroyOnLoad = OmnityHelperFunctions.BoolInputReset("destroyOnLoad", destroyOnLoad, true);

         flagRegenerateScreen = true;
      }
      OmnityHelperFunctions.EndExpander();
   }

   public OmnityPlaneParams Clone()
   {
      return new OmnityPlaneParams
      {
         left = this.left,
         right = this.right,
         top = this.top,
         bottom = this.bottom,
         slices = this.slices,
         stacks = this.stacks,
         destroyOnLoad = this.destroyOnLoad
      };
   }
}

/// <summary>
/// This is a parametric representation of a sphere section.  The parameters can be set through the XML file or the shift-f12 menu.  With this class many "Dome" surfaces can be created.
/// </summary>
///     <remarks>
/// Example usage:
/// /// A full sphere <br/>
/// thetaStart = -180<br/>
/// thetaEnd = 180<br/>
/// phiStart = -90<br/>
/// phiEnd = 90<br/>
/// <br/>
/// FullDome Screen <br/>
/// thetaStart = -180<br/>
/// thetaEnd = 180<br/>
/// phiStart = 0<br/>
/// phiEnd = 90<br/>
/// <br/>
/// Elumenati Panorama Screen<br/>
/// thetaStart = -135<br/>
/// thetaEnd = 135<br/>
/// phiStart = -35<br/>
/// phiEnd = 35<br/>
/// <br/>
/// Elumenati Portal Screen<br/>
/// thetaStart = -90<br/>
/// thetaEnd = 90<br/>
/// phiStart = -33<br/>
/// phiEnd = 90<br/>
/// <br/>
/// <br/>
/// Theater shaped truncation can be obtained by using the portal screen shape and rotating it <br/>
/// screenshape.localEulerAngle would be -90,180,0 instead of 0,0,0<br/>
/// </remarks>
[System.Serializable]
public class SphereParams
{

   /// <summary>
   /// The radius.  If you leave this as 1 then it allows you to specify transform's in units relative to dome radius.  For example, if radius is 1 and the final pass camera is halfway between the center of the dome and the rear edge its position would be (0,0,-.5)
   /// </summary>
   public float radius = 1.0f;

   /// <summary>
   /// thetaStart and thetaEnd represent the horizontal sweep of the sphere section.
   /// Imagine looking down at a clock and sweeping starting at 6 o-clock as thetaStart= -180, and sweeping through the clock until back at 6 o-clock at +180.
   /// phiStart and phiEnd represent the vertical sweep of the sphere section.
   /// </summary>
   public float thetaStart = -180;

   /// <summary>
   /// thetaStart and thetaEnd represent the horizontal sweep of the sphere section.
   /// Imagine looking down at a clock and sweeping starting at 6 o-clock as thetaStart= -180, and sweeping through the clock until back at 6 o-clock at +180.
   /// phiStart and phiEnd represent the vertical sweep of the sphere section.
   /// </summary>
   public float thetaEnd = 180;

   /// <summary>
   /// thetaStart and thetaEnd represent the horizontal sweep of the sphere section.
   /// Imagine looking down at a clock and sweeping starting at 6 o-clock as thetaStart= -180, and sweeping through the clock until back at 6 o-clock at +180.
   /// phiStart and phiEnd represent the vertical sweep of the sphere section.
   /// </summary>
   public float phiStart = -90;

   /// <summary>
   /// thetaStart and thetaEnd represent the horizontal sweep of the sphere section.
   /// Imagine looking down at a clock and sweeping starting at 6 o-clock as thetaStart= -180, and sweeping through the clock until back at 6 o-clock at +180.
   /// phiStart and phiEnd represent the vertical sweep of the sphere section.
   /// </summary>
   public float phiEnd = 90;

   /// <summary>
   /// The number of slices to generate the sphere section.  The more slices the higher quality, but too many will slow down the system.
   /// </summary>
   public int slices = 60;

   /// <summary>
   /// The number of stacks to generate the sphere section.  The more slices the higher quality, but too many will slow down the system.
   /// </summary>
   public int stacks = 60;

   /// <summary>
   /// invert the dome?
   /// </summary>
   public bool invert = false;

   /// <summary>
   /// internal surface?  Most domes are internally projected and viewed from the inside.  internalSurface is true in this situation
   /// </summary>
   public bool internalSurface = true;

   /// <summary>
   /// normally we destroy the factory object that creates the mesh after it is loaded.  Set this to false if you need to change the dome during runtime.
   /// </summary>
   public bool destroyOnLoad = true;

   /// <summary>
   /// set this to true to regenerate the screen surface.  Used by the gui menu when the user changes the settings
   /// </summary>
   public bool flagRegenerateScreen = true;

   /// <summary>
   /// Internal Function: Writes the XML.
   /// </summary>
   /// <param name="xmlWriter">The XML writer.</param>
   public void WriteXML(System.Xml.XmlTextWriter xmlWriter)
   {
      xmlWriter.WriteStartElement("SphereParams");
      xmlWriter.WriteElementString("radius", radius.ToString());
      xmlWriter.WriteElementString("thetaStart", thetaStart.ToString());
      xmlWriter.WriteElementString("thetaEnd", thetaEnd.ToString());
      xmlWriter.WriteElementString("phiStart", phiStart.ToString());
      xmlWriter.WriteElementString("phiEnd", phiEnd.ToString());
      xmlWriter.WriteElementString("slices", slices.ToString());
      xmlWriter.WriteElementString("stacks", stacks.ToString());
      xmlWriter.WriteElementString("invert", invert.ToString());
      xmlWriter.WriteElementString("internalSurface", internalSurface.ToString());
      xmlWriter.WriteElementString("destroyOnLoad", destroyOnLoad.ToString());
      xmlWriter.WriteEndElement();
   }

   /// <summary>
   /// Internal Function: Reads the XML.
   /// </summary>
   /// <param name="nav">The nav.</param>
   public void ReadXML(System.Xml.XPath.XPathNavigator nav)
   {
      radius = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//radius", radius);
      thetaStart = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//thetaStart", thetaStart);
      thetaEnd = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//thetaEnd", thetaEnd);
      phiStart = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//phiStart", phiStart);
      phiEnd = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//phiEnd", phiEnd);

      slices = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//slices", slices);
      stacks = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//stacks", stacks);

      invert = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//invert", invert);
      internalSurface = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//internalSurface", internalSurface);
      destroyOnLoad = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//destroyOnLoad", destroyOnLoad);

      guiExpanded = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//guiExpanded", guiExpanded);
   }

   /// <summary>
   /// The GUI expanded
   /// </summary>
   private bool guiExpanded = false;

   /// <summary>
   /// Called when [GUI].
   /// </summary>
   public void OnGUI()
   {
      OmnityHelperFunctions.BeginExpander(ref guiExpanded, "SphereParams");
      if (guiExpanded)
      {
         radius = OmnityHelperFunctions.FloatInputReset("radius", radius, 1);
         thetaStart = OmnityHelperFunctions.FloatInputReset("thetaStart", thetaStart, -180);
         thetaEnd = OmnityHelperFunctions.FloatInputReset("thetaEnd", thetaEnd, 180);
         phiStart = OmnityHelperFunctions.FloatInputReset("phiStart", phiStart, -90);
         phiEnd = OmnityHelperFunctions.FloatInputReset("phiEnd", phiEnd, 90);

         slices = OmnityHelperFunctions.IntInputReset("slices", slices, 60);
         stacks = OmnityHelperFunctions.IntInputReset("stacks", stacks, 60);

         invert = OmnityHelperFunctions.BoolInputReset("invert", invert, false);
         internalSurface = OmnityHelperFunctions.BoolInputReset("internalSurface", internalSurface, true);
         destroyOnLoad = OmnityHelperFunctions.BoolInputReset("destroyOnLoad", destroyOnLoad, true);

         flagRegenerateScreen = true;
      }
      OmnityHelperFunctions.EndExpander();
   }

   public SphereParams Clone()
   {
      return new SphereParams
      {
         radius = this.radius,
         thetaStart = this.thetaStart,
         thetaEnd = this.thetaEnd,
         phiStart = this.phiStart,
         phiEnd = this.phiEnd,
         slices = this.slices,
         stacks = this.stacks,
         invert = this.invert,
         internalSurface = this.internalSurface,
         destroyOnLoad = this.destroyOnLoad
      };
   }
}

/// <summary>
/// This is a parametric representation of a cylindrical section.  The parameters can be set through the XML file or the shift-f12 menu.  With this class many "cylindrical" surfaces can be created.
/// </summary>
[System.Serializable]
public class CylinderParams
{

   /// <summary>
   /// The radius.  If you leave this as 1 then it allows you to specify transform's in units relative to dome radius.  For example, if radius is 1 and the final pass camera is halfway between the center of the dome and the rear edge its position would be (0,0,-.5)
   /// </summary>
   public float radius = 1.0f;

   /// <summary>
   /// thetaStart and thetaEnd represent the horizontal sweep of the sphere section.
   /// Imagine looking down at a clock and sweeping starting at 6 o-clock as thetaStart= -180, and sweeping through the clock until back at 6 o-clock at +180.
   /// phiStart and phiEnd represent the vertical sweep of the sphere section.
   /// </summary>
   public float thetaStart = -180;

   /// <summary>
   /// thetaStart and thetaEnd represent the horizontal sweep of the sphere section.
   /// Imagine looking down at a clock and sweeping starting at 6 o-clock as thetaStart= -180, and sweeping through the clock until back at 6 o-clock at +180.
   /// phiStart and phiEnd represent the vertical sweep of the sphere section.
   /// </summary>
   public float thetaEnd = 180;

   /// <summary>
   /// thetaStart and thetaEnd represent the horizontal sweep of the sphere section.
   /// Imagine looking down at a clock and sweeping starting at 6 o-clock as thetaStart= -180, and sweeping through the clock until back at 6 o-clock at +180.
   /// phiStart and phiEnd represent the vertical sweep of the sphere section.
   /// </summary>
   public float yTop = 1;

   /// <summary>
   /// thetaStart and thetaEnd represent the horizontal sweep of the sphere section.
   /// Imagine looking down at a clock and sweeping starting at 6 o-clock as thetaStart= -180, and sweeping through the clock until back at 6 o-clock at +180.
   /// phiStart and phiEnd represent the vertical sweep of the sphere section.
   /// </summary>
   public float yBottom = -1;

   /// <summary>
   /// The number of slices to generate the sphere section.  The more slices the higher quality, but too many will slow down the system.
   /// </summary>
   public int slices = 60;

   /// <summary>
   /// The number of stacks to generate the sphere section.  The more slices the higher quality, but too many will slow down the system.
   /// </summary>
   public int stacks = 60;

   /// <summary>
   /// invert the dome?
   /// </summary>
   public bool invert = false;

   /// <summary>
   /// internal surface?  Most domes are internally projected and viewed from the inside.  internalSurface is true in this situation
   /// </summary>
   public bool internalSurface = true;

   /// <summary>
   /// normally we destroy the factory object that creates the mesh after it is loaded.  Set this to false if you need to change the dome during runtime.
   /// </summary>
   public bool destroyOnLoad = true;

   /// <summary>
   /// set this to true to regenerate the screen surface.  Used by the gui menu when the user changes the settings
   /// </summary>
   public bool flagRegenerateScreen = true;

   /// <summary>
   /// Internal Function: Writes the XML.
   /// </summary>
   /// <param name="xmlWriter">The XML writer.</param>
   public void WriteXML(System.Xml.XmlTextWriter xmlWriter)
   {
      xmlWriter.WriteStartElement("CylinderParams");
      xmlWriter.WriteElementString("radius", radius.ToString());
      xmlWriter.WriteElementString("thetaStart", thetaStart.ToString());
      xmlWriter.WriteElementString("thetaEnd", thetaEnd.ToString());
      xmlWriter.WriteElementString("yTop", yTop.ToString());
      xmlWriter.WriteElementString("yBottom", yBottom.ToString());
      xmlWriter.WriteElementString("slices", slices.ToString());
      xmlWriter.WriteElementString("stacks", stacks.ToString());
      xmlWriter.WriteElementString("invert", invert.ToString());
      xmlWriter.WriteElementString("internalSurface", internalSurface.ToString());
      xmlWriter.WriteElementString("destroyOnLoad", destroyOnLoad.ToString());
      xmlWriter.WriteEndElement();
   }

   /// <summary>
   /// Internal Function: Reads the XML.
   /// </summary>
   /// <param name="nav">The nav.</param>
   public void ReadXML(System.Xml.XPath.XPathNavigator nav)
   {
      radius = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//radius", radius);
      thetaStart = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//thetaStart", thetaStart);
      thetaEnd = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//thetaEnd", thetaEnd);
      yTop = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//yTop", yTop);
      yBottom = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//yBottom", yBottom);

      slices = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//slices", slices);
      stacks = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//stacks", stacks);

      invert = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//invert", invert);
      internalSurface = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//internalSurface", internalSurface);
      destroyOnLoad = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//destroyOnLoad", destroyOnLoad);

      guiExpanded = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//guiExpanded", guiExpanded);
   }

   /// <summary>
   /// The GUI expanded
   /// </summary>
   private bool guiExpanded = false;

   /// <summary>
   /// Called when [GUI].
   /// </summary>
   public void OnGUI()
   {
      OmnityHelperFunctions.BeginExpander(ref guiExpanded, "CylinderParams");
      if (guiExpanded)
      {
         radius = OmnityHelperFunctions.FloatInputReset("radius", radius, 1);
         thetaStart = OmnityHelperFunctions.FloatInputReset("thetaStart", thetaStart, -180);
         thetaEnd = OmnityHelperFunctions.FloatInputReset("thetaEnd", thetaEnd, 180);
         yTop = OmnityHelperFunctions.FloatInputReset("yTop", yTop, 1);
         yBottom = OmnityHelperFunctions.FloatInputReset("yBottom", yBottom, -1);

         slices = OmnityHelperFunctions.IntInputReset("slices", slices, 60);
         stacks = OmnityHelperFunctions.IntInputReset("stacks", stacks, 60);

         invert = OmnityHelperFunctions.BoolInputReset("invert", invert, false);
         internalSurface = OmnityHelperFunctions.BoolInputReset("internalSurface", internalSurface, true);
         destroyOnLoad = OmnityHelperFunctions.BoolInputReset("destroyOnLoad", destroyOnLoad, true);

         flagRegenerateScreen = true;
      }
      OmnityHelperFunctions.EndExpander();
   }

   public CylinderParams Clone()
   {
      return new CylinderParams
      {
         radius = this.radius,
         thetaStart = this.thetaStart,
         thetaEnd = this.thetaEnd,
         yTop = this.yTop,
         yBottom = this.yBottom,
         slices = this.slices,
         stacks = this.stacks,
         invert = this.invert,
         internalSurface = this.internalSurface,
         destroyOnLoad = this.destroyOnLoad
      };
   }
}

/// <summary>
/// For enumerating the bestiary of screen shape types
/// </summary>
public enum OmnityScreenShapeType
{
   SphereSection = 0,
   Plane = 1,
   Custom = 2,
   CustomApplicationLoaded = 3,
   Cylinder = 4,
   CustomFile = 5,
   CustomFileFlipNormals = 6
}

/// <summary>
/// The a single screen shape. Typically this is a dome, but its possible to use other shapes. The parameters should mimic the projection surface's physical layout.
/// To use a dome shape or any spherical section, set the parameters of it in screenShape.sphereParams.
/// To Use a non-dome shape:
/// Inside Unity3D's editor, put a mesh file (for example funkyDome.obj representing the screen surface) in a folder within the project assests folder named "resources".
/// Inside the XML file or F12 Menu: Set screenShape.meshFileName to be the file name of the mesh resource without extension (for example funkyDome)
/// Omnity will use the call:
/// Mesh m = (Mesh)Resources.Load(screenShape.meshFileName, typeof(Mesh));
/// to load the screen shape.
/// If the call fails, it will return to using the sphere section rendering.
/// </summary>
[System.Serializable]
public class ScreenShape
{

   /// <summary>
   /// The name
   /// </summary>
   public string name = "Screen";

   /// <summary>
   /// Does screenshape follow tilt of bracket keys
   /// </summary>
 

   /// <summary>
   /// Hint to save this screen shape when saving
   /// </summary>
   public bool serialize = true;

   public bool startEnabled = true;

   /// <summary>
   /// set false if you dont want to connect the textures on creation
   /// </summary>
   public bool automaticallyConnectTextures = true;

   public OmnityScreenShapeType screenShapeType = OmnityScreenShapeType.SphereSection;

   /// <summary>
   /// This is a parametric representation of a sphere section.  The parameters can be set through the XML file or the shift-f12 menu.  With this class many "Dome" surfaces can be created.  See the SphereParams class for more info.
   /// If a non-spherical screen shape is used, this variable will be ignored.
   /// </summary>
   public SphereParams sphereParams = new SphereParams();

   /// <summary>
   /// The cylinder parameters, this should be converted to a single abstract
   /// </summary>
   public CylinderParams cylinderParams = new CylinderParams();

   /// <summary>
   /// The plane parameters, this should be converted to a single abstract
   /// </summary>
   public OmnityPlaneParams planeParams = new OmnityPlaneParams();

   /// <summary>
   /// Gets the default screen shapes.
   /// </summary>
   /// <returns>ScreenShape[][].</returns>
   static public ScreenShape[] GetDefaultScreenShapes()
   {
      ScreenShape[] screens = new ScreenShape[1];
      screens[0] = new ScreenShape();
      return screens;
   }

   /// <summary>
   /// The scale
   /// </summary>
   public Vector3 scale = new Vector3(1, 1, 1);

   /// <summary>
   /// cached link to the omnity object
   /// </summary>
   public OmnityTransformInfo transformInfo = new OmnityTransformInfo();

   /// <summary>
   /// The layer
   /// </summary>
   public int layer = 30;

   /// <summary>
   /// The mesh file name
   /// </summary>
   public string meshFileName = "OmnitySphere";

   /// <summary>
   /// The shader file name for custom final pass shader.  Only is used if finalPassShaderType is Custom.  Can be filename loaded at runtime, or name of shader loaded from resources directory.
   /// </summary>
   public string shaderFileName = "Elumenati/Omnity/FinalPass4/Compiled";

   public enum FinalPassShaderMultiSample
   {
      OMNIMAP_MULTISAMPLE_OFF = 0,
      OMNIMAP_MULTISAMPLE_5X = 5,
      OMNIMAP_MULTISAMPLE_9X = 9,
   }

   /// <summary>
   /// The variable to inform the system on the final pass shader to use
   /// </summary>
   public FinalPassShaderType finalPassShaderType = FinalPassShaderType.OmnityDefault;

   /// <summary>
   /// The variable to inform the system on the final pass shader to use
   /// </summary>
   public FinalPassShaderMultiSample finalPassShaderMultiSample = FinalPassShaderMultiSample.OMNIMAP_MULTISAMPLE_OFF;

   /// <summary>
   /// The renderer
   /// </summary>
   public MeshRenderer renderer;

   /// <summary>
   /// The trans
   /// </summary>
   public Transform trans;

   /// <summary>
   /// Internal Function: Writes the XML.
   /// </summary>
   /// <param name="xmlWriter">The XML writer.</param>
   public void WriteXML(System.Xml.XmlTextWriter xmlWriter)
   {
      if (serialize)
      {
         xmlWriter.WriteStartElement("ScreenShape");
         xmlWriter.WriteElementString("name", name);
         xmlWriter.WriteElementString("scale", scale.ToString("F4"));
         xmlWriter.WriteElementString("offset", transformInfo.position.ToString("F4"));
         xmlWriter.WriteElementString("localEulerAngles", transformInfo.localEulerAngles.ToString("F4"));
         xmlWriter.WriteElementString("layer", layer.ToString());
         xmlWriter.WriteElementString("meshFileName", meshFileName);
         xmlWriter.WriteElementString("shaderFileName", shaderFileName);
         xmlWriter.WriteElementString("guiExpanded", guiExpanded.ToString());
         xmlWriter.WriteElementString("startEnabled", startEnabled.ToString());
         xmlWriter.WriteElementString("automaticallyConnectTextures", automaticallyConnectTextures.ToString());
         //xmlWriter.WriteElementString("useDefaultShader", useDefaultShader.ToString());
         //xmlWriter.WriteElementString("useInternal6ChannelShader", useInternal6ChannelShader.ToString());

         xmlWriter.WriteElementString("screenShapeType", screenShapeType.ToString());

         xmlWriter.WriteElementString("finalPassShaderType", finalPassShaderType.ToString());
         xmlWriter.WriteElementString("finalPassShaderMultiSample", finalPassShaderMultiSample.ToString());

         sphereParams.WriteXML(xmlWriter);
         cylinderParams.WriteXML(xmlWriter);
         planeParams.WriteXML(xmlWriter);

         xmlWriter.WriteEndElement();
      }
   }

   public ScreenShape Clone()
   {
      ScreenShape s = new ScreenShape();
      s.name = name;
      s.scale = scale;
      s.transformInfo = transformInfo.Clone();
      s.layer = layer;
      s.meshFileName = meshFileName;
      s.shaderFileName = shaderFileName;
      s.guiExpanded = guiExpanded;
      s.startEnabled = startEnabled;

      s.finalPassShaderType = finalPassShaderType;
      s.finalPassShaderMultiSample = finalPassShaderMultiSample;
      s.sphereParams = sphereParams.Clone();
      s.planeParams = planeParams.Clone();
      s.cylinderParams = cylinderParams.Clone();
      s.screenShapeType = screenShapeType;
      s.serialize = serialize;
      s.automaticallyConnectTextures = automaticallyConnectTextures;
      return s;
   }

   /// <summary>
   /// The GUI expanded
   /// </summary>
   private bool guiExpanded = false;

   private string expanderHash = OmnityHelperFunctions.OmnityUniqueHash.uniqueHash;

   private bool ShouldWeShowShaderFileName()
   {
      switch (finalPassShaderType)
      {
         case FinalPassShaderType.OmnityDefault:
            return false;

         case FinalPassShaderType.Omnity4Channel:
            return false;

         case FinalPassShaderType.Omnity4ChannelAlpha:
            return false;

         case FinalPassShaderType.Omnity6Channel:
            return false;

         case FinalPassShaderType.OmnityPosterWarp:
            return false;

         case FinalPassShaderType.Cobra_TrueDimensionOff:
            return false;

         case FinalPassShaderType.Cobra_TrueDimensionOffFlat:
            return false;

         case FinalPassShaderType.Cobra_TrueDimensionOn:
            return false;

         case FinalPassShaderType.Omnity6ChannelWarpPreview:
            return false;

         case FinalPassShaderType.Omnity6ChannelWarpPreviewUniformity:
            return false;

         case FinalPassShaderType.Equirectangular3:
            return false;

         case FinalPassShaderType.Equirectangular6:
            return false;

         case FinalPassShaderType.Custom:
            return true;

         case FinalPassShaderType.Custom_ApplicationLoaded:
            return true;

         default:
            Debug.Log("Unknown type " + finalPassShaderType);
            return true;
      }
   }

   /// <summary>
   /// Call to show the gui.
   /// </summary>
   public void OnGUI()
   {
      OmnityHelperFunctions.BeginExpander(ref guiExpanded, "ScreenShape", "ScreenShape : " + name);
      if (guiExpanded)
      {
         name = OmnityHelperFunctions.StringInputReset("name", name, "Screen");
         startEnabled = OmnityHelperFunctions.BoolInputReset("startEnabled", startEnabled, true);
         scale = OmnityHelperFunctions.Vector3InputReset("scale", scale, new Vector3(1, 1, 1));
         transformInfo.position = OmnityHelperFunctions.Vector3InputReset("offset", transformInfo.position, new Vector3(0, 0, 0));
         transformInfo.localEulerAngles = OmnityHelperFunctions.Vector3InputReset("localEulerAngles", transformInfo.localEulerAngles, new Vector3(0, 0, 0));
         layer = (int)OmnityHelperFunctions.LayerMaskInputReset(expanderHash + "Layer", "Layer", (LayerMask)layer, (LayerMask)31, false);
         if (ShouldWeShowShaderFileName())
         {
            shaderFileName = OmnityHelperFunctions.StringInputReset("shaderFileName", shaderFileName, "OmniMap/FinalPass6");
         }

         finalPassShaderType = OmnityHelperFunctions.EnumInputReset<FinalPassShaderType>(expanderHash, "finalPassShaderType", finalPassShaderType, FinalPassShaderType.OmnityDefault, 1);
         finalPassShaderMultiSample = OmnityHelperFunctions.EnumInputReset<FinalPassShaderMultiSample>(expanderHash, "finalPassShaderMultiSample", finalPassShaderMultiSample, FinalPassShaderMultiSample.OMNIMAP_MULTISAMPLE_OFF, 1);

         screenShapeType = OmnityHelperFunctions.EnumInputReset<OmnityScreenShapeType>(expanderHash + "sst", "screenShapeType", screenShapeType, OmnityScreenShapeType.SphereSection, 1);

         switch (screenShapeType)
         {
            case OmnityScreenShapeType.SphereSection:
               sphereParams.OnGUI();
               break;

            case OmnityScreenShapeType.Cylinder:
               cylinderParams.OnGUI();
               break;

            case OmnityScreenShapeType.Plane:
               planeParams.OnGUI();
               break;

            case OmnityScreenShapeType.Custom:
               meshFileName = OmnityHelperFunctions.StringInputReset("meshFileName", meshFileName, "OmnitySphere");
               break;

            case OmnityScreenShapeType.CustomApplicationLoaded:
               meshFileName = OmnityHelperFunctions.StringInputReset("Application Loaded Mesh", meshFileName, "OmnitySphere");
               break;

            case OmnityScreenShapeType.CustomFile:
               meshFileName = OmnityHelperFunctions.StringInputReset("Custom Mesh File", meshFileName, "elumenati/screen.obj");
               break;

            default:
               Debug.Log("unknown screenShapeType type " + screenShapeType.ToString());
               sphereParams.OnGUI();
               break;
         }
      }
      OmnityHelperFunctions.EndExpander();
   }

   /// <summary>
   /// Internal Function: Reads the XML.
   /// </summary>
   /// <param name="nav">The nav.</param>
   public void ReadXML(System.Xml.XPath.XPathNavigator nav)
   {
      name = OmnityHelperFunctions.ReadElementStringDefault(nav, ".//name", name);
      scale = OmnityHelperFunctions.ReadElementVector3Default(nav, ".//scale", scale);
      transformInfo.position = OmnityHelperFunctions.ReadElementVector3Default(nav, ".//offset", transformInfo.position);
      transformInfo.localEulerAngles = OmnityHelperFunctions.ReadElementVector3Default(nav, ".//localEulerAngles", transformInfo.localEulerAngles);

      layer = OmnityHelperFunctions.ReadElementIntDefault(nav, ".//layer", layer);

      guiExpanded = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//guiExpanded", guiExpanded);
      meshFileName = OmnityHelperFunctions.ReadElementStringDefault(nav, ".//meshFileName", meshFileName);
      startEnabled = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//startEnabled", startEnabled);
      automaticallyConnectTextures = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//automaticallyConnectTextures", automaticallyConnectTextures);

      screenShapeType = OmnityHelperFunctions.ReadElementEnumDefault<OmnityScreenShapeType>(nav, ".//screenShapeType", screenShapeType);

      //useDefaultShader = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//useDefaultShader", useDefaultShader);
      //useInternal6ChannelShader = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//useInternal6ChannelShader", useInternal6ChannelShader);

      finalPassShaderType = OmnityHelperFunctions.ReadElementEnumDefault<FinalPassShaderType>(nav, ".//finalPassShaderType", finalPassShaderType);
      finalPassShaderMultiSample = OmnityHelperFunctions.ReadElementEnumDefault<FinalPassShaderMultiSample>(nav, ".//finalPassShaderMultiSample", finalPassShaderMultiSample);

      shaderFileName = OmnityHelperFunctions.ReadElementStringDefault(nav, ".//shaderFileName", shaderFileName);

      try
      {
         sphereParams.ReadXML(nav);
      }
      catch (System.Exception e)
      {
         Debug.LogError(e.Message);
      }
      try
      {
         cylinderParams.ReadXML(nav);
      }
      catch (System.Exception e)
      {
         Debug.LogError(e.Message);
      }
      try
      {
         planeParams.ReadXML(nav);
      }
      catch (System.Exception e)
      {
         Debug.LogError(e.Message);
      }
   }

   /// <summary>
   /// Updates the screen.
   /// </summary>
   public void UpdateScreen()
   {
      if (startEnabled)
      {
         if (trans == null)
         {
            if (Omnity.anOmnity.debugLevel >= DebugLevel.High)
            {
               Debug.Log("Screen invalid during reset");
            }
            return;
         }

         trans.localScale = scale;
         trans.localPosition = transformInfo.position;
         trans.localEulerAngles = transformInfo.localEulerAngles;
      }
   }

   /// <summary>
   /// Generates the screen shape.
   /// </summary>
   /// <param name="_parent">The _parent.</param>
   /// <exception cref="System.Exception">Not found</exception>
   public void SpawnScreenShape(Omnity anOmnity)
   {
      if (startEnabled)
      {
         GameObject newgo = new GameObject(name);
         trans = newgo.transform;
         trans.parent = anOmnity.transformScreenShapes;

         trans.localScale = scale;
         trans.localPosition = transformInfo.position;
         trans.localEulerAngles = transformInfo.localEulerAngles;

         newgo.AddComponent<MeshFilter>();

         SpawnOrUpdateScreen(newgo, true);

         // This is important to set the renderer bounds so that it will not get clipped....
         //mf.sharedMesh.bounds = new Bounds(new Vector3(0,0,0),new Vector3(100000,100000,100000));

         renderer = newgo.AddComponent<MeshRenderer>();

         OmnityPlatformDefines.TurnOffShadows(renderer);
         renderer.receiveShadows = false;
         ConnectFinalPassShader(finalPassShaderType);
         newgo.layer = layer;
         newgo.AddComponent<ScreenShapeProxy>().myScreenShape = this;
         SetJunkMatrices();
         if (finalPassShaderType == FinalPassShaderType.Custom_ApplicationLoaded)
         {
         }
         else if (renderer.sharedMaterial)
         {
            if (OmnityGraphicsInfo.bDirectX)
            {
               renderer.sharedMaterial.SetFloat("_zScale", 2);
               renderer.sharedMaterial.SetFloat("_zShift", 1);
            }
            else
            {
               renderer.sharedMaterial.SetFloat("_zScale", 1);
               renderer.sharedMaterial.SetFloat("_zShift", 0);
            }
         }
         else
         {
            Debug.LogError("SpawnScreenShape Failed");
         }
      }
   }

   public bool ConnectFinalPassShader(FinalPassShaderType finalPassShaderType)
   {
      bool success = false;
      if (startEnabled)
      {
         if (Omnity.anOmnity.debugLevel >= DebugLevel.High)
         {
            Debug.Log("loading shader " + finalPassShaderType.ToString());
         }

         switch (finalPassShaderType)
         {
            case FinalPassShaderType.OmnityDefault:
               success = FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               break;

            case FinalPassShaderType.Omnity4Channel:
               success = FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               break;

            case FinalPassShaderType.Omnity4ChannelAlpha:
               success = FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               break;

            case FinalPassShaderType.Omnity6Channel:
               success = FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               break;

            case FinalPassShaderType.OmnityPosterWarp:
               success = FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               break;

            case FinalPassShaderType.Cobra_TrueDimensionOff:
               success = FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               break;

            case FinalPassShaderType.Cobra_TrueDimensionOffFlat:
               success = FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               break;

            case FinalPassShaderType.Cobra_TrueDimensionOn:
               success = FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               break;

            case FinalPassShaderType.Omnity6ChannelWarpPreview:
               success = FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               break;

            case FinalPassShaderType.Omnity6ChannelWarpPreviewUniformity:
               success = FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               break;

            case FinalPassShaderType.Equirectangular3:
               success = FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               break;

            case FinalPassShaderType.Equirectangular6:
               success = FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               break;

            case FinalPassShaderType.Custom:
               bool IsFile = "" != System.IO.Path.GetExtension(shaderFileName);
               if (!IsFile)
               {
                  try
                  {
                     if (Shader.Find(shaderFileName) == null)
                     {
                        throw new System.Exception("Not found");
                     }
                     renderer.sharedMaterial = new Material(Shader.Find(shaderFileName));
                     success = true;
                  }
                  catch (System.Exception e)
                  {
                     Debug.Log("Could not find/load shader in project resources" + shaderFileName + " ERROR CODE FOLLOWS\r\n" + e.ToString() + "\r\n using default 6 chanel:");
                     FinalPassSetup.ApplySettings(FinalPassShaderType.Omnity4Channel, renderer);
                     success = false;
                     break; // use return if entering this same function again.
                  }
               }
               else
               {
                  if (OmnityPlatformDefines.CanReadShader())
                  {
                     success = OmnityPlatformDefines.TryReadShader(shaderFileName, renderer);
                  }
                  if (!success)
                  {
                     Debug.Log("COULD NOT LOAD SHADER FILE by file " + shaderFileName + "\r\nUSING FALLBACK 6 channel");
                     success = FinalPassSetup.ApplySettings(FinalPassShaderType.Omnity4Channel, renderer);
                     success = false;
                     break; // use return if entering this same function again.
                  }
               }
               break;

            case FinalPassShaderType.LineShader:
               return FinalPassSetup.ApplySettings(finalPassShaderType, renderer);

            case FinalPassShaderType.Custom_ApplicationLoaded:
               //                    Debug.Log("Asking for application loaded shader...");
               success = true;
               break;

            default:
               Debug.Log("Unknown Shader type " + finalPassShaderType.ToString());
               FinalPassSetup.ApplySettings(finalPassShaderType, renderer);
               success = false;
               break;
         }
         if (renderer && renderer.sharedMaterial && renderer.sharedMaterial.HasProperty("_LensFOVDegrees"))
         {
            renderer.sharedMaterial.SetFloat("_LensFOVDegrees", 180.0f);
         }
         // finally add the shader keywords
         SetShaderKeywords();
      }
      else
      {
         success = true;
         Debug.Log("Screen shape not enabled.");
      }
      if (!success)
      {
         Debug.LogError("Shader Not Connected");
      }
      return success;
   }

   public System.Collections.Generic.List<string> GetBaseShaderKeywords()
   {
      string newKeyword = finalPassShaderMultiSample.ToString();
      System.Collections.Generic.List<string> l = new System.Collections.Generic.List<string>(shaderKeywordsAdditional);
      l.Add(newKeyword);
      return l;
   }

   public System.Collections.Generic.List<string> shaderKeywordsAdditional = new System.Collections.Generic.List<string>();

   public void EasyToggleShaderKeyword(string keyword, bool updateMaterial)
   {
      if (keyword.Contains("_ON"))
      {
         shaderKeywordsAdditional.Remove(keyword);
         shaderKeywordsAdditional.Remove(keyword.Replace("_ON", "_OFF"));
         shaderKeywordsAdditional.Add(keyword);
      }
      else if (keyword.Contains("_OFF"))
      {
         shaderKeywordsAdditional.Remove(keyword);
         shaderKeywordsAdditional.Remove(keyword.Replace("_OFF", "_ON"));
         shaderKeywordsAdditional.Add(keyword);
      }
      else
      {
         Debug.LogError("Couldn't find inverse of " + keyword);
         shaderKeywordsAdditional.Add(keyword);
      }
      if (updateMaterial)
      {
         SetShaderKeywords();
      }
   }

   public void SetShaderKeywords()
   {
      if (renderer && renderer.sharedMaterial)
      {
         renderer.sharedMaterial.shaderKeywords = GetBaseShaderKeywords().ToArray();
      }
   }

   /// <summary>
   /// Sets the junk matrices.
   /// </summary>
   public void SetJunkMatrices(int iStart = 0)
   {
      Matrix4x4 junkMatrix = new Matrix4x4();
      junkMatrix[0, 3] = -10;
      junkMatrix[1, 3] = -10;
      junkMatrix[2, 3] = -10;
      junkMatrix[3, 3] = -10;

      for (int i = iStart; i < 6; i++)
      {
         if (renderer.sharedMaterial != null)
            renderer.sharedMaterial.SetMatrix(Omnity.GetMatrixStringFast(i), junkMatrix);
      }
   }

   private Mesh TryAnyTypeOfLoadFromFile(string file, bool flip)
   {
      Mesh m = ModelLoaderClass.LoadModel(meshFileName, flip);
      if (m == null)
      {
         m = ModelLoaderClass.LoadModel(meshFileName + ".obj", flip);
      }
      return m;
   }

   private Mesh TryAnyTypeOfLoadFromResources(string file, bool flip)
   {
      Mesh m = (Mesh)Resources.Load(meshFileName, typeof(Mesh));
      if (m == null)
      {
         m = (Mesh)Resources.Load(meshFileName.Replace(".obj", ""), typeof(Mesh));
      }
      return m;
   }

   private Mesh TryLoadFileWithBackup(string file, bool flip)
   {
      Mesh m = TryAnyTypeOfLoadFromFile(file, flip);
      if (m == null)
      {
         m = TryAnyTypeOfLoadFromResources(file, flip);
      }
      return m;
   }

   private Mesh TryLoadResourceWithBackup(string file, bool flip)
   {
      Mesh m = TryAnyTypeOfLoadFromResources(file, flip);
      if (m == null)
      {
         m = TryAnyTypeOfLoadFromFile(file, flip);
      }
      return m;
   }

   /// <summary>
   /// Spawns or updates the screen.
   /// </summary>
   internal void SpawnOrUpdateScreen(GameObject newgo, bool spawn)
   {
      try
      {
         switch (screenShapeType)
         {
            case OmnityScreenShapeType.SphereSection:
               if (sphereParams.flagRegenerateScreen || spawn)
               {
                  SpawnOrGenerateSphere(newgo);
               }
               break;

            case OmnityScreenShapeType.Cylinder:
               if (cylinderParams.flagRegenerateScreen || spawn)
               {
                  SpawnOrGenerateCylinder(newgo);
               }
               break;

            case OmnityScreenShapeType.CustomApplicationLoaded:
               //  Application loaded screen mesh:" + meshFileName
               break;

            case OmnityScreenShapeType.CustomFile:
               if (spawn)
               {
                  Mesh m = TryLoadFileWithBackup(meshFileName, false);
                  if (m != null)
                  {
                     newgo.GetComponent<MeshFilter>().sharedMesh = m;
                  }
                  else
                  {
                     throw new System.Exception("Screen shape model not loaded:" + meshFileName);
                  }
               }
               break;

            case OmnityScreenShapeType.CustomFileFlipNormals:
               if (spawn)
               {
                  Mesh m = TryLoadFileWithBackup(meshFileName, true);
                  if (m != null)
                  {
                     newgo.GetComponent<MeshFilter>().sharedMesh = m;
                  }
                  else
                  {
                     throw new System.Exception("Screen shape model not loaded:" + meshFileName);
                  }
               }
               break;

            case OmnityScreenShapeType.Custom:
               if (spawn)
               {
                  Mesh m = TryLoadResourceWithBackup(meshFileName, false);
                  if (m != null)
                  {
                     newgo.GetComponent<MeshFilter>().sharedMesh = m;
                  }
                  else
                  {
                     throw new System.Exception("Screen shape model not loaded:" + meshFileName);
                  }
               }
               break;

            case OmnityScreenShapeType.Plane:
               if (planeParams.flagRegenerateScreen || spawn)
               {
                  OmnityPlaneGenerator pp = newgo.GetComponent<OmnityPlaneGenerator>();
                  if (pp == null)
                  {
                     newgo.AddComponent<OmnityPlaneGenerator>().Generate(planeParams);
                  }
                  else
                  {
                     pp.Generate();
                  }
               }
               break;

            default:
               throw new System.Exception("unknown screenShapeType type " + screenShapeType.ToString());
         }
      }
      catch (System.Exception e)
      {
         Debug.Log(e);
         SpawnOrGenerateSphere(newgo);
      }
   }

   /// <summary>
   /// Spawns or updates the screen.
   /// </summary>
   private void SpawnOrGenerateSphere(GameObject newgo)
   {
      if (sphereParams.flagRegenerateScreen)
      {
         OmnitySphereSectionGenerator ss = newgo.GetComponent<OmnitySphereSectionGenerator>();
         if (ss == null)
         {
            newgo.AddComponent<OmnitySphereSectionGenerator>().Generate(sphereParams);
         }
         else
         {
            ss.Generate();
         }
      }
   }

   /// <summary>
   /// Spawns or updates the screen.
   /// </summary>
   private void SpawnOrGenerateCylinder(GameObject newgo)
   {
      if (cylinderParams.flagRegenerateScreen)
      {
         OmnityCylinderSectionGenerator cs = newgo.GetComponent<OmnityCylinderSectionGenerator>();
         if (cs == null)
         {
            newgo.AddComponent<OmnityCylinderSectionGenerator>().Generate(cylinderParams);
         }
         else
         {
            cs.Generate();
         }
      }
   }
}

/// <summary>
/// This class builds a plane mesh from the sphereParams definition.
/// </summary>
public class OmnityPlaneGenerator : MonoBehaviour
{

   /// <summary>
   /// The plane params
   /// </summary>
   public OmnityPlaneParams planeParams = new OmnityPlaneParams();

   /// <summary>
   /// Starts this instance.
   /// </summary>
   private void Start()
   {
      Generate();
   }

   //void Update()
   //{
   //	Generate();
   //	Debug.Log("Generate");
   //}
   /// <summary>
   /// Generates the specified params.
   /// </summary>
   /// <param name="_planeParams">The _sphere params.</param>
   public void Generate(OmnityPlaneParams _planeParams)
   {
      planeParams = _planeParams;
   }

   /// <summary>
   /// Generates this instance.
   /// </summary>
   public void Generate()
   {
      planeParams.flagRegenerateScreen = false;
      System.Collections.Generic.List<Vector3> myVerts = new System.Collections.Generic.List<Vector3>();
      System.Collections.Generic.List<int> myTris = new System.Collections.Generic.List<int>();
      System.Collections.Generic.List<Vector2> myUVs = new System.Collections.Generic.List<Vector2>();
      System.Collections.Generic.List<Vector3> myNorms = new System.Collections.Generic.List<Vector3>();
      System.Collections.Generic.List<Vector4> myTangents = new System.Collections.Generic.List<Vector4>();
      float xDelta = (planeParams.right - planeParams.left) / (float)planeParams.stacks;
      float yDelta = (planeParams.top - planeParams.bottom) / (float)planeParams.slices;

      Vector3 normal = new Vector3(0, 0, -1f);
      Vector4 tangent = new Vector4(-1, 0, 0, 1);
      //        Vector3 position = Vector3.zero;
      float xCurrent = planeParams.left;
      float ycurrent = planeParams.bottom;

      for (int aa = 0; aa <= planeParams.stacks; ++aa)
      {
         float V = ((float)aa / (float)(planeParams.stacks));
         for (int bb = 0; bb <= planeParams.slices; ++bb)
         {
            myVerts.Add(new Vector3(xCurrent, ycurrent, 0));
            myNorms.Add(normal);
            myTangents.Add(tangent);
            xCurrent += xDelta;

            float U = (float)bb / (float)(planeParams.slices);
            Vector2 newUV = new Vector2(U, V);
            myUVs.Add(newUV);
         }
         xCurrent = planeParams.left;
         ycurrent += yDelta;
      }

      for (int j = 0; j < planeParams.stacks; ++j)
      {
         for (int i = 0; i < planeParams.slices; ++i)
         {
            int square00 = (j) * (planeParams.slices + 1) + i;
            int square10 = (j) * (planeParams.slices + 1) + i + 1;
            int square01 = (j + 1) * (planeParams.slices + 1) + i;
            int square11 = (j + 1) * (planeParams.slices + 1) + i + 1;

            myTris.Add(square00);
            myTris.Add(square11);
            myTris.Add(square01);

            myTris.Add(square00);
            myTris.Add(square10);
            myTris.Add(square11);
         }
      }

      MeshFilter meshFilter = GetComponent<MeshFilter>();
      Mesh mesh = meshFilter.sharedMesh;
      if (mesh == null)
      {
         mesh = new Mesh();
      }
      mesh.Clear();
      mesh.vertices = myVerts.ToArray();
      mesh.uv = myUVs.ToArray();
      mesh.normals = myNorms.ToArray();
      mesh.triangles = myTris.ToArray();
      mesh.tangents = myTangents.ToArray();
      mesh.Optimize();
      meshFilter.sharedMesh = mesh;

      if (planeParams.destroyOnLoad)
      {
         Destroy(this);
      }
   }
}

/// <summary>
/// This class builds a sphere mesh from the SphereParams definition.
/// </summary>
public class OmnitySphereSectionGenerator : MonoBehaviour
{

   /// <summary>
   /// The sphere params
   /// </summary>
   public SphereParams sphereParams = new SphereParams();

   /// <summary>
   /// Starts this instance.
   /// </summary>
   private void Start()
   {
      Generate();
   }

   //void Update()
   //{
   //	Generate();
   //	Debug.Log("Generate");
   //}
   /// <summary>
   /// Generates the specified _sphere params.
   /// </summary>
   /// <param name="_sphereParams">The _sphere params.</param>
   public void Generate(SphereParams _sphereParams)
   {
      sphereParams = _sphereParams;
   }

   /// <summary>
   /// Generates this instance.
   /// </summary>
   public void Generate()
   {
      sphereParams.flagRegenerateScreen = false;
      System.Collections.Generic.List<Vector3> myVerts = new System.Collections.Generic.List<Vector3>();
      System.Collections.Generic.List<int> myTris = new System.Collections.Generic.List<int>();
      System.Collections.Generic.List<Vector2> myUVs = new System.Collections.Generic.List<Vector2>();
      System.Collections.Generic.List<Vector3> myNorms = new System.Collections.Generic.List<Vector3>();
      System.Collections.Generic.List<Vector4> myTangents = new System.Collections.Generic.List<Vector4>();
      float PhiDelta = (sphereParams.phiEnd - sphereParams.phiStart) / (float)sphereParams.stacks;
      float ThetaDelta = (sphereParams.thetaEnd - sphereParams.thetaStart) / (float)sphereParams.slices;

      Vector3 normal;

      float thetaCurrent = sphereParams.thetaStart;
      float phiCurrent = ((!sphereParams.invert) ? sphereParams.phiStart : sphereParams.phiEnd);

      //        float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(sphereParams.thetaEnd, sphereParams.thetaStart));
      //        bool connectedTheta = (deltaAngle < 1);

      //      connectedTheta = false;
      //	bool connectedPhi = ((sphereParams.phiEnd - sphereParams.phiStart) < 179);
      //Debug.LogError("connectedTheta " + deltaAngle + "=="+sphereParams.thetaStart + "-"+sphereParams.thetaEnd+ ": " +connectedTheta);
      for (int aa = 0; aa <= sphereParams.stacks; ++aa)
      {
         for (int bb = 0; bb <= sphereParams.slices; ++bb)
         {
            ///////////// Compute normals
            normal.x = Mathf.Cos(phiCurrent * Mathf.Deg2Rad) * Mathf.Cos(-thetaCurrent * Mathf.Deg2Rad + Mathf.PI / 2.0f);
            normal.y = Mathf.Sin(phiCurrent * Mathf.Deg2Rad);
            normal.z = Mathf.Cos(phiCurrent * Mathf.Deg2Rad) * Mathf.Sin(-thetaCurrent * Mathf.Deg2Rad + Mathf.PI / 2.0f);

            myVerts.Add(normal * sphereParams.radius);
            myNorms.Add(normal * (sphereParams.internalSurface ? -1.0f : 1.0f));

            /////////// Compute Tangents
            Vector3 normalPrime;
            normalPrime.x = Mathf.Cos(phiCurrent * Mathf.Deg2Rad) * Mathf.Cos(-(thetaCurrent + .01f) * Mathf.Deg2Rad + Mathf.PI / 2.0f);
            normalPrime.y = Mathf.Sin(phiCurrent * Mathf.Deg2Rad);
            normalPrime.z = Mathf.Cos(phiCurrent * Mathf.Deg2Rad) * Mathf.Sin(-(thetaCurrent + .01f) * Mathf.Deg2Rad + Mathf.PI / 2.0f);
            Vector4 tangent = Vector3.Normalize(normal - normalPrime);
            tangent.w = 1;
            myTangents.Add(tangent);
            /////////////////////////////

            thetaCurrent += ThetaDelta;
         }

         thetaCurrent = sphereParams.thetaStart;
         phiCurrent += ((!sphereParams.invert) ? PhiDelta : -PhiDelta);
      }

      for (int j = 0; j <= sphereParams.stacks; ++j)
      {
         for (int i = 0; i <= sphereParams.slices; ++i)
         {
            float U = (float)i / (float)(sphereParams.slices);
            float V = ((float)j / (float)(sphereParams.stacks));
            Vector2 newUV = new Vector2(U, V);
            myUVs.Add(newUV);
         }
      }

      for (int j = 0; j < sphereParams.stacks; ++j)
      {
         for (int i = 0; i < sphereParams.slices; ++i)
         {
            int square00 = (j) * (sphereParams.slices + 1) + i;
            int square10 = (j) * (sphereParams.slices + 1) + i + 1;
            int square01 = (j + 1) * (sphereParams.slices + 1) + i;
            int square11 = (j + 1) * (sphereParams.slices + 1) + i + 1;

            // 01  11
            // 00  10

            if ((!sphereParams.invert && !sphereParams.internalSurface) || (sphereParams.invert && sphereParams.internalSurface))
            {
               myTris.Add(square00);
               myTris.Add(square11);
               myTris.Add(square01);

               myTris.Add(square00);
               myTris.Add(square10);
               myTris.Add(square11);
            }
            else
            {
               myTris.Add(square00);
               myTris.Add(square01);
               myTris.Add(square11);

               myTris.Add(square00);
               myTris.Add(square11);
               myTris.Add(square10);
            }
         }
      }

      MeshFilter meshFilter = GetComponent<MeshFilter>();
      Mesh mesh = meshFilter.sharedMesh;
      if (mesh == null)
      {
         mesh = new Mesh();
      }

      mesh.Clear();
      mesh.vertices = myVerts.ToArray();
      mesh.uv = myUVs.ToArray();
      mesh.normals = myNorms.ToArray();
      mesh.triangles = myTris.ToArray();
      mesh.tangents = myTangents.ToArray();
      mesh.Optimize();
      meshFilter.sharedMesh = mesh;

      if (sphereParams.destroyOnLoad)
      {
         Destroy(this);
      }
   }
}

/// <summary>
/// This class builds a  mesh from the CylinderParams definition.
/// </summary>
public class OmnityCylinderSectionGenerator : MonoBehaviour
{

   /// <summary>
   /// The sphere params
   /// </summary>
   public CylinderParams cylinderParams = new CylinderParams();

   /// <summary>
   /// Starts this instance.
   /// </summary>
   private void Start()
   {
      Generate();
   }

   /// <summary>
   /// Generates the specified CylinderParams.
   /// </summary>
   /// <param name="_cylinderParams">The CylinderParams.</param>
   public void Generate(CylinderParams _cylinderParams)
   {
      cylinderParams = _cylinderParams;
   }

   /// <summary>
   /// Generates this instance.
   /// </summary>
   public void Generate()
   {
      cylinderParams.flagRegenerateScreen = false;
      System.Collections.Generic.List<Vector3> myVerts = new System.Collections.Generic.List<Vector3>();
      System.Collections.Generic.List<int> myTris = new System.Collections.Generic.List<int>();
      System.Collections.Generic.List<Vector2> myUVs = new System.Collections.Generic.List<Vector2>();
      System.Collections.Generic.List<Vector3> myNorms = new System.Collections.Generic.List<Vector3>();
      System.Collections.Generic.List<Vector4> myTangents = new System.Collections.Generic.List<Vector4>();
      float yDelta = (cylinderParams.yBottom - cylinderParams.yTop) / (float)cylinderParams.stacks;
      float ThetaDelta = (cylinderParams.thetaEnd - cylinderParams.thetaStart) / (float)cylinderParams.slices;

      Vector3 normal;

      float thetaCurrent = cylinderParams.thetaStart;
      float yCurrent = ((!cylinderParams.invert) ? cylinderParams.yTop : cylinderParams.yBottom);

      //        float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(sphereParams.thetaEnd, sphereParams.thetaStart));
      //        bool connectedTheta = (deltaAngle < 1);

      //      connectedTheta = false;
      //	bool connectedPhi = ((sphereParams.phiEnd - sphereParams.phiStart) < 179);
      //Debug.LogError("connectedTheta " + deltaAngle + "=="+sphereParams.thetaStart + "-"+sphereParams.thetaEnd+ ": " +connectedTheta);
      for (int aa = 0; aa <= cylinderParams.stacks; ++aa)
      {
         for (int bb = 0; bb <= cylinderParams.slices; ++bb)
         {
            ///////////// Compute normals
            normal.x = Mathf.Cos(-thetaCurrent * Mathf.Deg2Rad + Mathf.PI / 2.0f);
            normal.y = yCurrent;
            normal.z = Mathf.Sin(-thetaCurrent * Mathf.Deg2Rad + Mathf.PI / 2.0f);

            myVerts.Add(Vector3.Scale(normal, new Vector3(cylinderParams.radius, 1, cylinderParams.radius)));

            normal.y = 0;
            myNorms.Add(normal * (cylinderParams.internalSurface ? -1.0f : 1.0f));

            /////////// Compute Tangents
            Vector3 normalPrime;
            normalPrime.x = Mathf.Cos(-(thetaCurrent + .01f) * Mathf.Deg2Rad + Mathf.PI / 2.0f);
            normalPrime.y = 0;
            normalPrime.z = Mathf.Sin(-(thetaCurrent + .01f) * Mathf.Deg2Rad + Mathf.PI / 2.0f);
            Vector4 tangent = Vector3.Normalize(normal - normalPrime);
            tangent.w = 1;
            myTangents.Add(tangent);
            /////////////////////////////

            thetaCurrent += ThetaDelta;
         }

         thetaCurrent = cylinderParams.thetaStart;
         yCurrent += ((!cylinderParams.invert) ? yDelta : -yDelta);
      }

      for (int j = 0; j <= cylinderParams.stacks; ++j)
      {
         for (int i = 0; i <= cylinderParams.slices; ++i)
         {
            float U = (float)i / (float)(cylinderParams.slices);
            float V = ((float)j / (float)(cylinderParams.stacks));
            if (!cylinderParams.internalSurface)
            {
               U = 1 - U;
            }
            Vector2 newUV = new Vector2(U, 1 - V);
            myUVs.Add(newUV);
         }
      }

      for (int j = 0; j < cylinderParams.stacks; ++j)
      {
         for (int i = 0; i < cylinderParams.slices; ++i)
         {
            int square00 = (j) * (cylinderParams.slices + 1) + i;
            int square10 = (j) * (cylinderParams.slices + 1) + i + 1;
            int square01 = (j + 1) * (cylinderParams.slices + 1) + i;
            int square11 = (j + 1) * (cylinderParams.slices + 1) + i + 1;

            // 01  11
            // 00  10

            if ((!cylinderParams.invert && !cylinderParams.internalSurface) || (cylinderParams.invert && cylinderParams.internalSurface))
            {
               myTris.Add(square00);
               myTris.Add(square01);
               myTris.Add(square11);

               myTris.Add(square00);
               myTris.Add(square11);
               myTris.Add(square10);
            }
            else
            {
               myTris.Add(square00);
               myTris.Add(square11);
               myTris.Add(square01);

               myTris.Add(square00);
               myTris.Add(square10);
               myTris.Add(square11);
            }
         }
      }

      MeshFilter meshFilter = GetComponent<MeshFilter>();
      Mesh mesh = meshFilter.sharedMesh;
      if (mesh == null)
      {
         mesh = new Mesh();
      }
      mesh.Clear();
      mesh.vertices = myVerts.ToArray();
      mesh.uv = myUVs.ToArray();
      mesh.normals = myNorms.ToArray();
      mesh.triangles = myTris.ToArray();
      mesh.tangents = myTangents.ToArray();
      mesh.Optimize();
      meshFilter.sharedMesh = mesh;

      if (cylinderParams.destroyOnLoad)
      {
         Destroy(this);
      }
   }
}

/// <summary>
/// Provides a link between final pass ScreenShape's inspector(in the scene) and the ScreenShape component in Omnity.
/// </summary>
public class ScreenShapeProxy : MonoBehaviour
{

   /// <summary>
   /// My screen shape
   /// </summary>
   public ScreenShape myScreenShape;

   // Because each screen has the potential to be rendered my more that one camera (in the case of biclops)
   // the shader variables for the screen shapes need to be set for each camera separately...
   // it would be possible to optimize this (duplicate the screen shapes one for each camera so you don't need to set the shader variables every frame, or have multiple materials that swap in and out..
   // for one projector this doesn't need to be called every frame...

   /// <summary>
   /// Starts this instance.
   /// </summary>
   public void Start()
   {
   }

   /// <summary>
   /// Updates this instance.
   /// </summary>
   public void Update()
   {
      myScreenShape.SpawnOrUpdateScreen(gameObject, false);
   }

   /// <summary>
   /// The cached material holding the omnity shader
   /// </summary>
   private Material m;

   /// <summary>
   /// The proxy dictionary used to speed up access to finding the correct final camera
   /// </summary>
   private System.Collections.Generic.Dictionary<Camera, FinalPassCameraProxy> proxyDict = new System.Collections.Generic.Dictionary<Camera, FinalPassCameraProxy>();

   /// <summary>
   /// Called when the screen needs to be rendered
   /// </summary>
   public void OnWillRenderObject()
   {
      Camera current = Camera.current;
      FinalPassCameraProxy myFinalPassCameraProxy = null;
      if (proxyDict.ContainsKey(current))
      {
         myFinalPassCameraProxy = proxyDict[current];
      }
      else
      {
         myFinalPassCameraProxy = current.GetComponent<FinalPassCameraProxy>();
         proxyDict.Add(current, myFinalPassCameraProxy);
      }

      if (m == null)
      {
         m = GetComponent<Renderer>().sharedMaterial;
         if (m == null)
         {
            //Debug.Log("no material on omnity object "+gameObject.name);
            return;
         }
      }

      if (myFinalPassCameraProxy != null)
      {
         FinalPassCamera myFinalPassCamera = myFinalPassCameraProxy.myFinalPassCamera;
         if (OmnityLoader.doBUGFIX)
         {
            Transform parenttemp = transform.parent;
            transform.parent = null;
            Vector3 temp = transform.localScale;
            transform.localScale = Vector3.one;
            Matrix4x4 MVP = transform.localToWorldMatrix * current.worldToCameraMatrix * current.projectionMatrix;
            transform.localScale = temp;
            transform.parent = parenttemp;
            m.SetMatrix("_MVPNoScaleMatrix", MVP);
         }
         m.SetFloat("_AspectRatio", current.pixelRect.width / current.pixelRect.height);

         float invertOffset = 1;

         //  if (OmnityGraphicsInfo.bDirectX) {
         //  if (!warnedOnce) {
         //          warnedOnce = true;
         //QualitySettings.antiAliasing = 0;
         //Debug.Log("Setting app antialiasing settings... not tested");
         //     }
         //invertOffset *= -1;
         //	if(QualitySettings.antiAliasing == 0) {
         //	invertOffset *= -1;
         //	}
         //   }

         if (myFinalPassCameraProxy.myFinalPassCamera.normalizedLensOffset)
         {
            float AspectViewport = myFinalPassCamera.normalizedViewportRect.height / myFinalPassCameraProxy.myFinalPassCamera.normalizedViewportRect.width;
            float height = 1;
            float width = 1;
            if (myFinalPassCameraProxy.GetComponent<Camera>().targetTexture != null)
            {
               height = myFinalPassCameraProxy.GetComponent<Camera>().targetTexture.height;
               width = myFinalPassCameraProxy.GetComponent<Camera>().targetTexture.width;
            }
            else
            {
               OmnityPlatformDefines.GetCameraWidthHeight(ref width, ref height);
            }

            if (Application.platform == RuntimePlatform.OSXPlayer)
            {
               invertOffset *= -1;
            }

            m.SetFloat("_yOffset", invertOffset * myFinalPassCameraProxy.myFinalPassCamera.lensOffsetY * -1.0f * (1.0f - AspectViewport * height / width));
         }
         else
         {
            m.SetFloat("_yOffset", invertOffset * myFinalPassCameraProxy.myFinalPassCamera.lensOffsetY);
         }

         if (myFinalPassCamera.projectorType == OmnityProjectorType.FisheyeFullDome)
         {
            m.SetFloat("_LensZoomToHeight", 1);
         }
         else if (myFinalPassCamera.projectorType == OmnityProjectorType.FisheyeTruncated)
         {
            m.SetFloat("_LensZoomToHeight", 0);
         }
         else if (myFinalPassCamera.projectorType == OmnityProjectorType.Rectilinear)
         {
            m.SetFloat("_LensZoomToHeight", -1);
         }
         else
         {
            Debug.LogError("Unknown projector type " + myFinalPassCamera.projectorType);
            m.SetFloat("_LensZoomToHeight", 1);
         }
         m.SetFloat("_xScale", myFinalPassCamera.lensScaleX);
         m.SetFloat("_yScale", myFinalPassCamera.lensScaleY);
      }
      else
      {
         m.SetFloat("_LensZoomToHeight", -1);            // i am being rendered by a non omnimap camera! if its not in the editor its probably a mistake..
      }
   }
}

/// <summary>
/// OmnityGUI class.  When Shift-F12 is pressed a gui menu pops up and shows the settings related to Omnity Rendering.  This class controls the menu.
/// </summary>
public class OmnityGUI : MonoBehaviour
{

   /// <summary>
   /// </summary>
   public Omnity myOmnity;

   /// <summary>
   /// </summary>
   static public OmnityGUI anOmnityGUI;

   /// <summary>
   /// This list holds all of the tab available in the omnity menu
   /// </summary>
   static public System.Collections.Generic.List<TAB> omnityGUITabs = new System.Collections.Generic.List<TAB>();

   public class TAB
   {
      public string name = "Tab";
      public Omnity.OmnityEventDelegate callback = null;
      public Omnity.OmnityEventDelegate closeGUICallback = null;
   }

   /// <summary>
   /// </summary>
   /// <param name="_myOmnity">The _my omnity.</param>
   public void Init(Omnity _myOmnity)
   {
      anOmnityGUI = this;
      myOmnity = _myOmnity;
      this.enabled = false;
   }

   /// <summary>
   /// </summary>
   private static Vector2 scrollPosition = Vector2.zero;

   /// <summary>

   /// </summary>
   private static System.Collections.Generic.List<OmnityGUI> myOmnityGUIs = new System.Collections.Generic.List<OmnityGUI>();

   /// <summary>
   /// </summary>
   private void OnEnable()
   {
      myOmnityGUIs.Add(this);
      OmnityHelperFunctions.CallDelegate(myOmnity, onGuiEnable);
   }

   /// <summary>
   /// </summary>
   private void OnDisable()
   {
      myOmnityGUIs.Remove(this);
      OmnityHelperFunctions.CallDelegate(myOmnity, onGuiDisable);
   }

   static public Omnity.OmnityEventDelegate onGuiEnable = null, onGuiDisable = null;

   /// <summary>
   /// The position of the window.
   /// </summary>
   [System.NonSerialized]
   public Rect windowRect = new Rect(0, 0, 800, 600);

   /// <summary>
   /// </summary>
   private void Start()
   {
      if (Application.isEditor)
      {
         windowRect = new Rect(0, 0, Screen.width, Screen.height);
      }
      else
      {
         windowRect = new Rect(myOmnity.guiPosition.x * Screen.width, Screen.height - myOmnity.guiPosition.height * Screen.height - myOmnity.guiPosition.y * Screen.height, myOmnity.guiPosition.width * Screen.width, myOmnity.guiPosition.height * Screen.height);
      }
   }

   /// <summary>
   /// </summary>
   private void OnGUI()
   {
      windowRect = GUI.Window(0, windowRect, DoMyWindow, "My Window");
   }

   /// <summary>
   /// variable for keeping track of gui resizing
   /// </summary>
   private bool resizingX = false;

   /// <summary>
   /// variable for keeping track of gui resizing
   /// </summary>
   private bool resizingY = false;

   // This is used to combine all of the omnimap menus into one long menu.
   /// <summary>
   /// </summary>
   private static bool firstRender = true;

   /// <summary>
   /// </summary>
   private bool iamGoingToDrawTheSecondPass = false;

   /// <summary>
   /// </summary>
   public void OnRenderObject()
   {
      firstRender = true;
      iamGoingToDrawTheSecondPass = false;
   }

   /// <summary>
   /// GUI HELPER FUNCTION
   /// </summary>
   static public void ExpandButton(ref bool guiExpanded)
   {
      if (GUILayout.Button(guiExpanded ? "-" : "+"))
      {
         guiExpanded = !guiExpanded;
      }
   }

   /// <summary>
   /// GUI HELPER FUNCTION
   /// </summary>
   /// <param name="label">The label.</param>
   static public void LeftJustifiedLabel(string label)
   {
      GUILayout.BeginHorizontal();
      GUILayout.Label(label);
      GUILayout.FlexibleSpace();
      GUILayout.EndHorizontal();
   }

   private TAB currentTab = null;

   /// <summary>
   /// GUI HELPER FUNCTION
   /// </summary>
   /// <param name="windowID">The window ID.</param>
   private void DoMyWindow(int windowID)
   {
      // Because there may be multiple omnimap objects in the scene, we have to elect one to render all of the GUIs.
      // ALSO since OnGui is a two pass process, it needs to keep track of that too...
      if (!iamGoingToDrawTheSecondPass)
      {
         if (!firstRender)
         {
            return;
         }
      }

      firstRender = false;
      iamGoingToDrawTheSecondPass = true;
      float tabrowheight = 20;

      GUILayout.BeginHorizontal();
      GUILayout.BeginVertical();

      #region TITLE

      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Omnity"))
      {
         if (currentTab != null)
         {
            OmnityHelperFunctions.CallDelegate(myOmnity, currentTab.closeGUICallback);
         }
         currentTab = null;
      }

      for (int i = 0; i < omnityGUITabs.Count; i++)
      {
         if (GUILayout.Button(omnityGUITabs[i].name, GUILayout.Height(tabrowheight)))
         {
            if (currentTab != omnityGUITabs[i] && currentTab != null)
            {
               OmnityHelperFunctions.CallDelegate(myOmnity, currentTab.closeGUICallback);
            }
            currentTab = omnityGUITabs[i];
         }
      }
      GUILayout.FlexibleSpace();
      if (GUILayout.Button("[X]", GUILayout.Width(20), GUILayout.Height(tabrowheight)))
      {
         myOmnity.myOmnityGUI.GUIEnabled = !myOmnity.myOmnityGUI.GUIEnabled;
      }
      GUILayout.EndHorizontal();

      #endregion TITLE

      GUILayout.BeginHorizontal();

      #region scrollview

      scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(windowRect.width - OmnityHelperFunctions.rightPadding * 2), GUILayout.Height(windowRect.height - OmnityHelperFunctions.rightPadding * 3 - tabrowheight));

      if (currentTab == null)
      {
         for (int i = 0; i < myOmnityGUIs.Count; i++)
         {
            myOmnityGUIs[i].myOmnity.DrawGUI();
         }
      }
      else
      {
         currentTab.callback(myOmnity);
      }

      GUILayout.EndScrollView();

      #endregion scrollview

      GUILayout.EndHorizontal();
      OmnityHelperFunctions.WinResize(ref windowRect, ref resizingX, ref resizingY);
      GUILayout.EndVertical();
      GUILayout.EndHorizontal();

      if (!(resizingX || resizingY))
      {
         GUI.DragWindow();
      }
   }

   public bool GUIEnabled
   {
      set
      {
         enabled = value;
         if (enabled == false)
         {
            if (currentTab != null)
            {
               OmnityHelperFunctions.CallDelegate(myOmnity, currentTab.closeGUICallback);
            }
         }
      }

      get
      {
         return enabled;
      }
   }

   /* // todo make sure you know what camera/ finalpass or gui
  static public void ScaleGUIToWindowInfo(System.Action a) {
     Matrix4x4 matNormal = GUI.matrix; //push matrix
     if (!Application.isEditor) {
        GUI.matrix = Omnity.anOmnity.windowInfo.GUISCALEMATRIX;
     }
     a();
     if (!Application.isEditor) {
        GUI.matrix = matNormal; // POP MATRIX
     }
  }*/

   public static void ShowTabNow(TAB tab)
   {
      Omnity.anOmnity.myOmnityGUI.GUIEnabled = true;
      Omnity.anOmnity.myOmnityGUI.currentTab = tab;
   }
}

/// <summary>
/// Class OmnityGraphicsInfo, used internally to determine the capabilities of the graphics hardware.
/// </summary>
public class OmnityGraphicsInfo
{

   /// <summary>
   /// keeps track if we are using Direct X
   /// </summary>
   private static int _bDirectX = -1;

   /// <summary>
   /// Gets a value indicating whether the graphics subsystem is direct X
   /// </summary>
   /// <value><c>true</c> if [b direct X]; otherwise, <c>false</c>.</value>
   public static bool bDirectX
   {
      get
      {
         if (_bDirectX < 0)
         {
            _bDirectX = SystemInfo.graphicsDeviceVersion.Contains("Direct") ? 1 : 0;
         }
         return _bDirectX > 0;
      }
   }
}

/// <summary>
/// Class FinalPassCameraProxy, provides a link between final pass camera's inspector(in the scene) and the FinalPassCamera component in Omnity.
/// </summary>
public class FinalPassCameraProxy : MonoBehaviour
{

   /// <summary>
   /// My final pass camera
   /// </summary>
   public FinalPassCamera myFinalPassCamera;

   private Color c;

   public void Awake()
   {
      c = new Color(UnityEngine.Random.Range(.75f, 1.0f), UnityEngine.Random.Range(.0f, .25f), UnityEngine.Random.Range(.75f, 1f));
   }

   private void OnDrawGizmosSelected()
   {
      if (myFinalPassCamera != null && myFinalPassCamera.projectorType == OmnityProjectorType.Rectilinear)
      {
         if (myFinalPassCamera.omnityPerspectiveMatrix.matrixMode == OmnityPerspectiveMatrix.MatrixMode.Orthographic)
         {
         }
         else
         {
            Gizmos.color = c;
            Vector3 TL_near = new Vector3(-Mathf.Tan(myFinalPassCamera.omnityPerspectiveMatrix.fovL * Mathf.Deg2Rad), Mathf.Tan(myFinalPassCamera.omnityPerspectiveMatrix.fovT * Mathf.Deg2Rad), 1);
            Vector3 TR_near = new Vector3(Mathf.Tan(myFinalPassCamera.omnityPerspectiveMatrix.fovR * Mathf.Deg2Rad), Mathf.Tan(myFinalPassCamera.omnityPerspectiveMatrix.fovT * Mathf.Deg2Rad), 1);
            Vector3 BL_near = new Vector3(-Mathf.Tan(myFinalPassCamera.omnityPerspectiveMatrix.fovL * Mathf.Deg2Rad), -Mathf.Tan(myFinalPassCamera.omnityPerspectiveMatrix.fovB * Mathf.Deg2Rad), 1);
            Vector3 BR_near = new Vector3(Mathf.Tan(myFinalPassCamera.omnityPerspectiveMatrix.fovR * Mathf.Deg2Rad), -Mathf.Tan(myFinalPassCamera.omnityPerspectiveMatrix.fovB * Mathf.Deg2Rad), 1);
            Matrix4x4 m = Matrix4x4.TRS(myFinalPassCamera.myCameraTransform.position, myFinalPassCamera.myCameraTransform.rotation, Vector3.one);
            Vector3 TL_far = TL_near;
            Vector3 TR_far = TR_near;
            Vector3 BL_far = BL_near;
            Vector3 BR_far = BR_near;

            Vector3 nearVec = (TL_near + TR_near + BR_near + BL_near).normalized;
            Vector3 farVec = (TL_far + TR_far + BR_far + BL_far).normalized;

            VecMathFunctions.LinePlaneIntersection(out BR_far, Vector3.zero, BR_far, farVec, BR_far);
            VecMathFunctions.LinePlaneIntersection(out TR_far, Vector3.zero, TR_far, farVec, TR_far);
            VecMathFunctions.LinePlaneIntersection(out BL_far, Vector3.zero, BL_far, farVec, BL_far);
            VecMathFunctions.LinePlaneIntersection(out TL_far, Vector3.zero, TL_far, farVec, TL_far);

            VecMathFunctions.LinePlaneIntersection(out BR_near, Vector3.zero, BR_near, nearVec, BR_near);
            VecMathFunctions.LinePlaneIntersection(out TR_near, Vector3.zero, TR_near, nearVec, TR_near);
            VecMathFunctions.LinePlaneIntersection(out BL_near, Vector3.zero, BL_near, nearVec, BL_near);
            VecMathFunctions.LinePlaneIntersection(out TL_near, Vector3.zero, TL_near, nearVec, TL_near);

            Gizmos.DrawLine(m.MultiplyPoint3x4(TL_near * myFinalPassCamera.omnityPerspectiveMatrix.near), m.MultiplyPoint3x4(TL_far * myFinalPassCamera.omnityPerspectiveMatrix.far));
            Gizmos.DrawLine(m.MultiplyPoint3x4(TR_near * myFinalPassCamera.omnityPerspectiveMatrix.near), m.MultiplyPoint3x4(TR_far * myFinalPassCamera.omnityPerspectiveMatrix.far));
            Gizmos.DrawLine(m.MultiplyPoint3x4(TL_near * myFinalPassCamera.omnityPerspectiveMatrix.near), m.MultiplyPoint3x4(TR_near * myFinalPassCamera.omnityPerspectiveMatrix.near));
            Gizmos.DrawLine(m.MultiplyPoint3x4(TL_far * myFinalPassCamera.omnityPerspectiveMatrix.far), m.MultiplyPoint3x4(TR_far * myFinalPassCamera.omnityPerspectiveMatrix.far));

            Gizmos.DrawLine(m.MultiplyPoint3x4(BL_near * myFinalPassCamera.omnityPerspectiveMatrix.near), m.MultiplyPoint3x4(BL_far * myFinalPassCamera.omnityPerspectiveMatrix.far));
            Gizmos.DrawLine(m.MultiplyPoint3x4(BR_near * myFinalPassCamera.omnityPerspectiveMatrix.near), m.MultiplyPoint3x4(BR_far * myFinalPassCamera.omnityPerspectiveMatrix.far));
            Gizmos.DrawLine(m.MultiplyPoint3x4(BL_far * myFinalPassCamera.omnityPerspectiveMatrix.far), m.MultiplyPoint3x4(BR_far * myFinalPassCamera.omnityPerspectiveMatrix.far));
            Gizmos.DrawLine(m.MultiplyPoint3x4(BL_near * myFinalPassCamera.omnityPerspectiveMatrix.near), m.MultiplyPoint3x4(BR_near * myFinalPassCamera.omnityPerspectiveMatrix.near));

            Gizmos.DrawLine(m.MultiplyPoint3x4(TL_near * myFinalPassCamera.omnityPerspectiveMatrix.near), m.MultiplyPoint3x4(BL_near * myFinalPassCamera.omnityPerspectiveMatrix.near));
            Gizmos.DrawLine(m.MultiplyPoint3x4(TR_near * myFinalPassCamera.omnityPerspectiveMatrix.near), m.MultiplyPoint3x4(BR_near * myFinalPassCamera.omnityPerspectiveMatrix.near));
            Gizmos.DrawLine(m.MultiplyPoint3x4(TL_far * myFinalPassCamera.omnityPerspectiveMatrix.far), m.MultiplyPoint3x4(BL_far * myFinalPassCamera.omnityPerspectiveMatrix.far));
            Gizmos.DrawLine(m.MultiplyPoint3x4(TR_far * myFinalPassCamera.omnityPerspectiveMatrix.far), m.MultiplyPoint3x4(BR_far * myFinalPassCamera.omnityPerspectiveMatrix.far));
         }
      }
   }
}

/// <summary>
/// Class PerspectiveCameraProxy, provides a link between perspective camera's inspector(in the scene) and the PerspectiveCamera component in Omnity.
/// </summary>
public class PerspectiveCameraProxy : MonoBehaviour
{

   /// <summary>
   /// My perspective camera
   /// </summary>
   public PerspectiveCamera myPerspectiveCamera;
}

/// <summary>
/// internal class Most of these functions are for dealing with the XML serialization and GUI handling
/// This class is excluded from the documenation because it is used internally only.
/// </summary>
/// <exclude/>
static public partial class OmnityHelperFunctions
{

   static public class OmnityUniqueHash
   {
      static private int uniqueHashIndex = 0;

      public static string uniqueHash
      {
         get
         {
            uniqueHashIndex++;
            return uniqueHashIndex.ToString();
         }
      }
   }

   static public void CallDelegate(Omnity anOmnity, Omnity.OmnityEventDelegate omnityDelegate)
   {
      if (anOmnity != null && omnityDelegate != null)
      {
         omnityDelegate(anOmnity);
      }
   }

   /// <summary>
   /// Delegate WriteXMLDelegate
   /// </summary>
   /// <param name="xmlWriter">The XML writer.</param>
   public delegate void WriteXMLDelegate(System.Xml.XmlTextWriter xmlWriter);

   /// <summary>
   /// Delegate ReadXMLDelegate
   /// </summary>
   /// <param name="nav">The nav.</param>
   public delegate void ReadXMLDelegate(System.Xml.XPath.XPathNavigator nav);

   /// <summary>
   /// Internal function : Save XML helper
   /// </summary>
   /// <param name="filename">The filename.</param>
   /// <param name="writeXMLDelegate">This function should write the individual tags.</param>
   static public void SaveXML(string filename, OmnityHelperFunctions.WriteXMLDelegate writeXMLDelegate)
   {
      System.Xml.XmlTextWriter xmlWriter = new System.Xml.XmlTextWriter(filename, new System.Text.UTF8Encoding(false));
      xmlWriter.Formatting = System.Xml.Formatting.Indented;
      xmlWriter.IndentChar = '\t';
      xmlWriter.Indentation = 1;
      xmlWriter.WriteStartDocument();
      writeXMLDelegate(xmlWriter);
      xmlWriter.WriteEndDocument();
      xmlWriter.Close();
   }

   /// <summary>
   /// Internal function : Load XML helper
   /// </summary>
   /// <param name="filename">The filename.</param>
   /// <param name="reader">The ReadXMLDelegate, pass a function that deals with reading xml System.Xml.XPath.XPathNavigator </param>
   static public void LoadXML(string filename, ReadXMLDelegate reader)
   {
      System.Xml.XPath.XPathNavigator nav = OmnityHelperFunctions.LoadXML(filename);
      reader(nav);
   }

   /// <summary>
   /// GUI HELPER FUNCTION
   /// </summary>
   /// <param name="isExpanded">if set to <c>true</c> [is expanded].</param>
   /// <param name="title">The title.</param>
   static public void BeginExpander(ref bool isExpanded, string title)
   {
      BeginExpander(ref isExpanded, title, title);
   }

   /// <summary>
   /// GUI HELPER FUNCTION
   /// </summary>
   /// <param name="isExpanded">if set to <c>true</c> [is expanded].</param>
   /// <param name="titleIfClosed">The titleIfClosed.</param>
   /// <param name="titleIfOpened">The titleIfOpened.</param>
   static public void BeginExpander(ref bool isExpanded, string titleIfClosed, string titleIfOpened)
   {
      GUILayout.BeginHorizontal();
      OmnityGUI.ExpandButton(ref isExpanded);
      GUILayout.BeginVertical();
      OmnityGUI.LeftJustifiedLabel(isExpanded ? titleIfClosed : titleIfOpened);
   }

   /// <summary>
   /// GUI HELPER FUNCTION
   /// </summary>
   static public void EndExpander()
   {
      GUILayout.EndVertical();
      GUILayout.EndHorizontal();
   }

   static public void DoGarbageCollectNow()
   {
      System.GC.Collect();
      Resources.UnloadUnusedAssets();
   }

   ////////////////////////////////////////////////////////////////
   // simple Expander
   static private System.Collections.Generic.HashSet<string> isExpandedSet = new System.Collections.Generic.HashSet<string>();

   static public bool BeginExpanderSimple(string stack, string titleIfClosed, string titleIfOpened, bool startClosed = true)
   {
      bool isExpanded = isExpandedSet.Contains(stack) ^ !startClosed;
      bool wasExpanded = isExpanded;

      BeginExpander(ref isExpanded, titleIfClosed, titleIfOpened);
      if (isExpanded && !wasExpanded)
      {
         if (startClosed)
         {
            isExpandedSet.Add(stack);
         }
         else
         {
            isExpandedSet.Remove(stack);
         }
      }
      else if (!isExpanded && wasExpanded)
      {
         if (startClosed)
         {
            isExpandedSet.Remove(stack);
         }
         else
         {
            isExpandedSet.Add(stack);
         }
      }
      return isExpanded;
   }

   static public void EndExpanderSimple()
   {
      EndExpander();
   }

   static public void ExpanderSimple(string stack, string titleIfClosed, string titleIfOpened, System.Action fn, bool startClosed = true)
   {
      if (BeginExpanderSimple(stack, titleIfClosed, titleIfOpened, startClosed))
      {
         fn();
      }
      EndExpanderSimple();
   }

   //////////////////////////////////////////////////////////////////

   private static Vector2 startClickPosition = Vector2.zero;
   private static Vector2 startSize = Vector2.zero;

   static public void WinResize(ref Rect WindowRect, ref bool resize_enabledX, ref bool resize_enabledY)
   {
      float radius = 7;
      bool resize_enable = resize_enabledX || resize_enabledY;
      if (Input.GetMouseButtonDown(0) || resize_enable)
      {
         if (!Input.GetMouseButton(0))
         {
            resize_enabledX = resize_enabledY = resize_enable = false;
            return;
         }

         if (resize_enable != true)
         {
            if (Mathf.Abs(Input.mousePosition.x - (WindowRect.x + WindowRect.width + radius / 4.0f)) < radius)
            {
               resize_enabledX = true;
               startClickPosition = Input.mousePosition;
               startSize = new Vector2(WindowRect.width, WindowRect.height);
            }
            if (Mathf.Abs(Input.mousePosition.y - (Screen.height - WindowRect.y - WindowRect.height - radius / 4.0f)) < radius)
            {
               resize_enabledY = true;
               startClickPosition = Input.mousePosition;
               startSize = new Vector2(WindowRect.width, WindowRect.height);
            }
            resize_enable = resize_enabledX || resize_enabledY;
         }
      }
      else
      {
         resize_enabledX = resize_enabledY = resize_enable = false;
      }

      if (resize_enabledX)
      {
         WindowRect.width = Input.mousePosition.x - startClickPosition.x + startSize.x;
      }
      if (resize_enabledY)
      {
         WindowRect.height = -(Input.mousePosition.y - startClickPosition.y) + startSize.y;
      }
   }

   /// <summary>
   /// Internal Function: helper function
   /// </summary>
   static public void BR()
   {
      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal();
   }

   /// <summary>
   /// Internal Function: helper function
   /// </summary>
   static public void HR()
   {
      GUILayout.EndHorizontal();
      LINE();
      GUILayout.BeginHorizontal();
   }

   /// <summary>
   /// Internal Function: helper function
   /// </summary>
   static public void LINE()
   {
      GUILayout.Label("-----------------------------------------------------------------------------------------------------------");
   }

   /// <summary>
   /// Internal Function: helper function
   /// </summary>
   static public void P()
   {
      GUILayout.EndHorizontal();
      GUILayout.Label(" ");
      GUILayout.BeginHorizontal();
   }

   /// <summary>
   /// Internal Function: helper function
   /// </summary>
   /// <returns>System.String.</returns>
   static public string ParseXPathNavigatorAsStringDefault(System.Xml.XPath.XPathNavigator s, string DefaultVal)
   {
      if (s == null)
      {
         return DefaultVal;
      }
      if (s.InnerXml == null)
      {
         return DefaultVal;
      }
      return s.InnerXml;
   }

   /// <summary>
   /// Internal Function: helper function
   /// </summary>
   /// <param name="s"></param>
   /// <param name="DefaultVal"></param>
   /// <returns></returns>
   static public bool ParseXPathNavigatorAsBooleanDefault(System.Xml.XPath.XPathNavigator s, bool DefaultVal)
   {
      if (s == null)
      {
         return DefaultVal;
      }
      if (s.InnerXml == null)
      {
         return DefaultVal;
      }

      string tidy = s.InnerXml.ToLower().Trim();
      if (tidy == "true" || tidy == "1" || tidy == "yes" || tidy == "enabled")
      {
         return true;
      }
      else if (tidy == "false" || tidy == "0" || tidy == "no" || tidy == "disabled")
      {
         return false;
      }
      else
      {
         return DefaultVal;
      }
   }

   /// <summary>
   /// Internal Function: helper function
   /// reads a file and returns a string.
   /// </summary>
   /// <param name="filename">The filename.</param>
   /// <returns>System.String of the files contents</returns>
   /// <exception cref="System.Exception">Could not open  + filename +  possibly a bad network link or the original file was moved from its home with out taking the dependencies with it.</exception>
   public static string LoadFile(string filename)
   {
      // TCW MOD: try to load the given filename via the resources
      string resFilename = filename;
      if (resFilename.EndsWith(".xml"))
         resFilename = resFilename.Substring(0, filename.Length - 4);
      TextAsset xmlTextAsset = (TextAsset)Resources.Load(resFilename, typeof(TextAsset));
      if (xmlTextAsset != null)
         return xmlTextAsset.text;

      if (!System.IO.File.Exists(filename))
      {
         try
         {
            return new System.Net.WebClient().DownloadString(filename);
         }
         catch (System.Exception e)
         {
            Debug.Log(e.ToString() + "Could not open " + filename + " possibly a bad network link or the original file was moved from its home with out taking the dependencies with it.");
            throw new System.Exception("Could not open " + filename + " possibly a bad network link or the original file was moved from its home with out taking the dependencies with it.");
         }
      }
      else
      {
         System.IO.StreamReader sr = System.IO.File.OpenText(filename);
         string s = sr.ReadToEnd();
         sr.Close();
         return s;
      }
   }

   /// <summary>
   /// Internal Function: helper function
   /// removes invalid characters
   /// </summary>
   /// <param name="inString">The input.</param>
   /// <returns>System.String.</returns>
   public static string FixString(string inString)
   {
      if (inString == null)
      {
         return null;
      }
      System.Text.StringBuilder newString = new System.Text.StringBuilder();
      char ch;

      for (int i = 0; i < inString.Length; i++)
      {
         ch = inString[i];
         // remove any characters outside the valid UTF-8 range as well as all control characters
         // except tabs and new lines
         if ((ch < 0x00FD && ch > 0x001F) || ch == '\t' || ch == '\n' || ch == '\r')
         {
            newString.Append(ch);
         }
      }
      return newString.ToString();
   }

   /// <summary>
   /// Internal Function: helper function
   /// </summary>
   /// <param name="s">The s.</param>
   /// <returns>System.IO.StringReader.</returns>
   public static System.IO.StringReader stringToStream(string s)
   {
      return new System.IO.StringReader(s);
   }

   /// <summary>
   /// Internal Function: helper function
   /// Loads the XML.
   /// </summary>
   /// <param name="filename">The filename.</param>
   /// <returns>System.Xml.XPath.XPathNavigator.</returns>
   static public System.Xml.XPath.XPathNavigator LoadXML(string filename)
   {
      try
      {
         System.Xml.XPath.XPathDocument doc = new System.Xml.XPath.XPathDocument(stringToStream(FixString(LoadFile(filename))));
         System.Xml.XPath.XPathNavigator nav = doc.CreateNavigator();
         return nav;
      }
      catch (System.Exception e)
      {
         Debug.LogError(e.Message + " in file " + filename);
         return null;
      }
   }

   /// <summary>
   /// Internal Function: helper function
   /// Reads the element returns a default value if it can't parse it.
   /// </summary>
   /// <param name="nav">The nav.</param>
   /// <param name="path">The path.</param>
   /// <param name="def">The def.</param>
   /// <returns>System.String.</returns>
   static public string ReadElementStringDefault(System.Xml.XPath.XPathNavigator nav, string path, string def)
   {
      System.Xml.XPath.XPathNavigator s = nav.SelectSingleNode(path);
      if (s == null)
      {
         return def;
      }
      if (s.InnerXml == null)
      {
         return def;
      }
      return s.InnerXml;
   }

   /// <summary>
   /// Internal Function: helper function
   /// Reads the element as an ENUM and returns a default value if it can't parse it.
   /// Make sure to call this function with the templated type that is a enum
   /// </summary>
   static public T ReadElementEnumDefault<T>(System.Xml.XPath.XPathNavigator nav, string path, T def)
   {
      try
      {
         System.Xml.XPath.XPathNavigator s = nav.SelectSingleNode(path);
         if (s == null)
         {
            return def;
         }
         if (s.InnerXml == null)
         {
            return def;
         }
         return (T)System.Enum.Parse(typeof(T), s.InnerXml, true);
      }
      catch
      {
         return def;
      }
   }

   /// <summary>
   /// Internal Function: helper function
   /// Reads the element returns a default value if it can't parse it.
   /// </summary>
   /// <param name="nav">The nav.</param>
   /// <param name="path">The path.</param>
   /// <param name="def">The def.</param>
   /// <returns>System.Single.</returns>
   static public float ReadElementFloatDefault(System.Xml.XPath.XPathNavigator nav, string path, float def)
   {
      System.Xml.XPath.XPathNavigator s = nav.SelectSingleNode(path);
      if (s == null)
      {
         return def;
      }
      if (s.InnerXml == null)
      {
         return def;
      }
      try
      {
         return float.Parse(s.InnerXml);
      }
      catch
      {
         return def;
      }
   }

   /// <summary>
   /// Internal Function: helper function
   /// Reads the element returns a default value if it can't parse it.
   /// </summary>
   /// <param name="nav">The nav.</param>
   /// <param name="path">The path.</param>
   /// <param name="def">The def.</param>
   /// <returns>System.Int32.</returns>
   static public int ReadElementIntDefault(System.Xml.XPath.XPathNavigator nav, string path, int def)
   {
      try
      {
         System.Xml.XPath.XPathNavigator s = nav.SelectSingleNode(path);
         if (s == null)
         {
            return def;
         }
         if (s.InnerXml == null)
         {
            return def;
         }
         return int.Parse(s.InnerXml);
      }
      catch
      {
         return def;
      }
   }

   /// <summary>
   /// Internal Function: helper function
   /// Reads the element returns a default value if it can't parse it.
   /// </summary>
   /// <param name="nav">The nav.</param>
   /// <param name="path">The path.</param>
   /// <param name="def">if set to <c>true</c> [def].</param>
   /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
   static public bool ReadElementBoolDefault(System.Xml.XPath.XPathNavigator nav, string path, bool def)
   {
      try
      {
         System.Xml.XPath.XPathNavigator s = nav.SelectSingleNode(path);
         if (s == null)
         {
            return def;
         }
         if (s.InnerXml == null)
         {
            return def;
         }

         string tidy = s.InnerXml.ToLower().Trim();
         if (tidy == "true" || tidy == "1" || tidy == "yes" || tidy == "enabled")
         {
            return true;
         }
         else if (tidy == "false" || tidy == "0" || tidy == "no" || tidy == "disabled")
         {
            return false;
         }
         else
         {
            return def;
         }
      }
      catch
      {
         return def;
      }
   }

   /// <summary>
   /// Internal Function: helper function
   /// Reads the element returns a default value if it can't parse it.
   /// </summary>
   /// <param name="nav">The nav.</param>
   /// <param name="path">The path.</param>
   /// <param name="def">The def.</param>
   /// <returns>Vector3.</returns>
   static public Vector3 ReadElementVector2Default(System.Xml.XPath.XPathNavigator nav, string path, Vector2 def)
   {
      try
      {
         System.Xml.XPath.XPathNavigator s = nav.SelectSingleNode(path);
         if (s == null)
         {
            return def;
         }
         if (s.InnerXml == null)
         {
            return def;
         }
         string tidy = s.InnerXml.ToLower().Trim().Replace("(", "").Replace(")", "");
         string[] vectorNumbers = tidy.Split(","[0]);
         Vector2 vec = new Vector3(float.Parse(vectorNumbers[0]), float.Parse(vectorNumbers[1]));
         return vec;
      }
      catch (System.Exception e)
      {
         Debug.Log(e.ToString());
         return def;
      }
   }

   /// <summary>
   /// Internal Function: helper function
   /// Reads the element returns a default value if it can't parse it.
   /// </summary>
   /// <param name="nav">The nav.</param>
   /// <param name="path">The path.</param>
   /// <param name="def">The def.</param>
   /// <returns>Vector3.</returns>
   static public Vector3 ReadElementVector3Default(System.Xml.XPath.XPathNavigator nav, string path, Vector3 def)
   {
      try
      {
         System.Xml.XPath.XPathNavigator s = nav.SelectSingleNode(path);
         if (s == null)
         {
            return def;
         }
         if (s.InnerXml == null)
         {
            return def;
         }
         string tidy = s.InnerXml.ToLower().Trim().Replace("(", "").Replace(")", "");
         string[] vectorNumbers = tidy.Split(","[0]);
         Vector3 vec = new Vector3(
                                 float.Parse(vectorNumbers[0]),
                                 float.Parse(vectorNumbers[1]),
                                 float.Parse(vectorNumbers[2]));
         return vec;
      }
      catch (System.Exception e)
      {
         Debug.Log(e.ToString());
         return def;
      }
   }

   /// <summary>
   /// Internal Function: helper function
   /// Reads the element returns a default value if it can't parse it.
   /// </summary>
   /// <param name="nav">The nav.</param>
   /// <param name="path">The path.</param>
   /// <param name="def">The def.</param>
   /// <returns>Vector4.</returns>
   static public Vector4 ReadElementVector4Default(System.Xml.XPath.XPathNavigator nav, string path, Vector4 def)
   {
      try
      {
         System.Xml.XPath.XPathNavigator s = nav.SelectSingleNode(path);
         if (s == null)
         {
            return def;
         }
         if (s.InnerXml == null)
         {
            return def;
         }
         string tidy = s.InnerXml.ToLower().Trim().Replace("(", "").Replace(")", "");
         string[] vectorNumbers = tidy.Split(","[0]);
         Vector4 vec = new Vector4(
                                 float.Parse(vectorNumbers[0]),
                                 float.Parse(vectorNumbers[1]),
                                 float.Parse(vectorNumbers[2]),
                                 float.Parse(vectorNumbers[3]));
         return vec;
      }
      catch (System.Exception e)
      {
         Debug.Log(e.ToString());
         return def;
      }
   }

   /// <summary>
   /// Internal Function: helper function
   /// Reads the element returns a default value if it can't parse it.
   /// </summary>
   /// <param name="nav">The nav.</param>
   /// <param name="path">The path.</param>
   /// <param name="def">The def.</param>
   /// <returns>Color.</returns>
   static public Color ReadElementColorDefault(System.Xml.XPath.XPathNavigator nav, string path, Color def)
   {
      try
      {
         System.Xml.XPath.XPathNavigator s = nav.SelectSingleNode(path);
         if (s == null)
         {
            return def;
         }
         if (s.InnerXml == null)
         {
            return def;
         }
         string tidy = s.InnerXml.ToLower().Trim().Replace("rgb", "").Replace("a", "").Replace("(", "").Replace(")", "");
         string[] vectorNumbers = tidy.Split(","[0]);
         Color vec = new Color(float.Parse(vectorNumbers[0]), float.Parse(vectorNumbers[1]), float.Parse(vectorNumbers[2]));
         return vec;
      }
      catch (System.Exception e)
      {
         Debug.Log(e.ToString());
      }
      return def;
   }

   /// <summary>
   /// Internal Function: helper function
   /// Reads the element returns a default value if it can't parse it.
   /// </summary>
   /// <param name="nav">The nav.</param>
   /// <param name="path">The path.</param>
   /// <param name="def">The def.</param>
   /// <returns>Rect.</returns>
   static public Rect ReadElementRectDefault(System.Xml.XPath.XPathNavigator nav, string path, Rect def)
   {
      try
      {
         System.Xml.XPath.XPathNavigator s = nav.SelectSingleNode(path);
         if (s == null)
         {
            return def;
         }
         if (s.InnerXml == null)
         {
            return def;
         }

         string tidy = s.InnerXml.ToLower().Trim().Replace("(", "").Replace(")", "").Replace("x:", "").Replace("y:", "").Replace("left:", "").Replace("top:", "").Replace("width:", "").Replace("height:", "");
         string[] vectorNumbers = tidy.Split(","[0]);
         Rect vec = new Rect(
                                     float.Parse(vectorNumbers[0]),
                                     float.Parse(vectorNumbers[1]),
                                     float.Parse(vectorNumbers[2]),
                                     float.Parse(vectorNumbers[3]));
         return vec;
      }
      catch (System.Exception e)
      {
         Debug.Log(e.ToString());
         return def;
      }
   }

   static public bool DrawFOVWidget(string expanderHash, string title, ref float fovL, ref float fovR, ref float fovT, ref float fovB,
      Vector3 leftRange, Vector3 rightRange, Vector3 topRange, Vector3 botRange
      )
   {
      float oldFovT = fovT;
      float oldFovB = fovB;
      float oldFovL = fovL;
      float oldFovR = fovR;
      if (OmnityHelperFunctions.BeginExpanderSimple(expanderHash, title, title))
      {
         GUILayout.BeginHorizontal();
         GUILayout.FlexibleSpace();

         fovT = OmnityHelperFunctions.FloatInputResetNoSpace("top", fovT, topRange.z);
         GUILayout.FlexibleSpace();
         GUILayout.EndHorizontal();
         GUILayout.BeginHorizontal();
         OmnityHelperFunctions.BR();
         GUILayout.FlexibleSpace();
         fovT = GUILayout.VerticalSlider(fovT, topRange.y, topRange.x);
         GUILayout.FlexibleSpace();
         OmnityHelperFunctions.BR();
         fovL = OmnityHelperFunctions.FloatInputResetNoSpace("left", fovL, leftRange.z);
         fovL = GUILayout.HorizontalSlider(fovL, leftRange.y, leftRange.x);
         fovR = GUILayout.HorizontalSlider(fovR, rightRange.x, rightRange.y);
         fovR = OmnityHelperFunctions.FloatInputResetNoSpace("right", fovR, rightRange.z);
         OmnityHelperFunctions.BR();
         GUILayout.FlexibleSpace();
         fovB = GUILayout.VerticalSlider(fovB, botRange.x, botRange.y);
         GUILayout.FlexibleSpace();
         OmnityHelperFunctions.BR();
         GUILayout.FlexibleSpace();
         fovB = OmnityHelperFunctions.FloatInputResetNoSpace("bottom", fovB, botRange.z);
         GUILayout.FlexibleSpace();
         OmnityHelperFunctions.BR();
         GUILayout.FlexibleSpace();
         GUILayout.EndHorizontal();
      }
      OmnityHelperFunctions.EndExpanderSimple();

      return (fovT != oldFovT || fovR != oldFovR || fovB != oldFovB || fovL != oldFovL);
   }

   /// <summary>
   /// Internal Function, GUITODO , will show a reminder that the variable must be edited through XML.
   /// </summary>
   static public void GUITODO(string s1)
   {
      GUILayout.BeginHorizontal();
      if (Application.isEditor)
      {
         GUILayout.Label("EDIT WITH XML/EDITOR INSPECTOR (may require save and reload): " + s1);
      }
      else
      {
         GUILayout.Label("EDIT WITH XML : " + s1);
      }

      GUILayout.EndHorizontal();
   }

   /// <summary>
   /// Internal Function: GUI helper function
   /// provides a gui widget with a reset button that returns the value back to default
   /// </summary>
   /// <param name="s">The s.</param>
   /// <param name="f">The f.</param>
   /// <param name="def">The def.</param>
   /// <returns>System.Single.</returns>
   static public float FloatInputReset(string s, float f, float def)
   {
      float returnVal = f;
      GUILayout.BeginHorizontal();
      try
      {
         GUILayout.Label(s);
         string floatString = f.ToString();
         if (!floatString.Contains("."))
         {
            floatString += ".0";
         }
         returnVal = float.Parse(GUILayout.TextField(floatString, GUILayout.Width(60)));
         if (ResetButton())
         {
            returnVal = def;
         }
      }
      catch
      {
         returnVal = f;
      }
      GUILayout.EndHorizontal();
      return returnVal;
   }

   static public float FloatInputResetNoSpace(string s, float f, float def)
   {
      float returnVal = f;
      try
      {
         GUILayout.Label(s, GUILayout.Width(60));
         string floatString = f.ToString();
         if (!floatString.Contains("."))
         {
            floatString += ".0";
         }
         returnVal = float.Parse(GUILayout.TextField(floatString, GUILayout.Width(60)));
         if (ResetButton())
         {
            returnVal = def;
         }
      }
      catch
      {
         returnVal = f;
      }
      return returnVal;
   }

   static public float FloatInputResetSlider(string s, float f, float min, float def, float max, float stringWidth = 60, float sliderWidth = 200, float okWidth = 60)
   {
      float returnVal = f;
      GUILayout.BeginHorizontal();
      GUILayout.Label(s);
      GUILayout.FlexibleSpace();
      returnVal = GUILayout.HorizontalSlider(returnVal, min, max, GUILayout.Width(sliderWidth));
      try
      {
         string floatString = returnVal.ToString();
         if (!floatString.Contains("."))
         {
            floatString += ".0";
         }
         string floatStringBefore = floatString;
         floatString = GUILayout.TextField(floatString, GUILayout.Width(stringWidth));
         if (floatString != floatStringBefore)
         {
            returnVal = float.Parse(floatString);
         }
      }
      catch
      {
      }

      if (Button("Reset", okWidth))
      {
         returnVal = def;
      }
      GUILayout.EndHorizontal();
      return returnVal;
   }

   /// <summary>
   /// Reset button gui
   /// </summary>
   /// <param name="text">The text.</param>
   /// <param name="_width">The _width.</param>
   /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
   static public bool Button(string text, float _width = 60)
   {// default is 60
      return GUILayout.Button(text, GUILayout.Width(_width));
   }

   /// <summary>
   /// Reset button
   /// </summary>
   /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
   static public bool ResetButton()
   {
      return Button("Reset");
   }

   /// <summary>
   /// Internal Function: GUI helper function
   /// provides a gui widget with a reset button that returns the value back to default
   /// </summary>
   /// <returns>System.Int32.</returns>
   static public int IntInputReset(string s, int f, int def)
   {
      int returnVal = f;
      GUILayout.BeginHorizontal();
      try
      {
         GUILayout.Label(s);
         string floatString = f.ToString();
         returnVal = int.Parse(GUILayout.TextField(floatString, GUILayout.Width(60)));
         if (ResetButton())
         {
            returnVal = def;
         }
      }
      catch
      {
         returnVal = f;
      }
      GUILayout.EndHorizontal();
      return returnVal;
   }

   /// <summary>
   /// Internal Function: GUI helper function
   /// provides a gui widget with a reset button that returns the value back to default
   /// </summary>
   static public bool BoolInputReset(string s, bool f, bool def)
   {
      GUILayout.BeginHorizontal();
      bool returnVal = GUILayout.Toggle(f, s);
      if (ResetButton())
      {
         returnVal = def;
      }
      GUILayout.EndHorizontal();
      return returnVal;
   }

   static public LayerMask LayerMaskInputReset(string stack, string s, LayerMask f, LayerMask def, bool layerMaskMultiCheckIfTrue_intLayerIfFalse)
   {
      if (layerMaskMultiCheckIfTrue_intLayerIfFalse == false)
      {
         f = 1 << (int)f;
         def = 1 << (int)def;
      }

      int thiswidth = 250;
      if (BeginExpanderSimple(stack, s, s))
      {
         GUILayout.BeginHorizontal();
         GUILayout.FlexibleSpace();
         if (layerMaskMultiCheckIfTrue_intLayerIfFalse)
         {
            if (GUILayout.Toggle(false, "all", GUILayout.Width(thiswidth)))
            {
               f = 0;
               for (int i = 0; i < 32; i++)
               {
                  f |= 1 << i;
               }
            }

            BR();
            GUILayout.FlexibleSpace();
            if (GUILayout.Toggle(false, "none", GUILayout.Width(thiswidth)))
            {
               f = 0;
            }
            BR();
            GUILayout.FlexibleSpace();
         }

         for (int i = 0; i < 32; i++)
         {
            //  int field = 2 ^ i;
            int fieldMask = 1 << i;
            bool isChecked = (fieldMask & f) != 0;
            string fieldName = i.ToString() + " : " + LayerMask.LayerToName(i) + (i == 30 ? "(default final pass)" : "");
            isChecked = GUILayout.Toggle(isChecked, fieldName, GUILayout.Width(thiswidth));
            if (layerMaskMultiCheckIfTrue_intLayerIfFalse)
            {
               if (isChecked)
               {
                  f = f | fieldMask;
               }
               else
               {
                  f = f & (~fieldMask);
               }
            }
            else
            {
               if (isChecked)
               {
                  f = fieldMask;
               }
            }
            BR();
            GUILayout.FlexibleSpace();
         }
         if (Button("Reset", thiswidth))
         {
            f = def;
         }
         GUILayout.EndHorizontal();
      }
      EndExpanderSimple();
      if (layerMaskMultiCheckIfTrue_intLayerIfFalse == false)
      {
         for (int i = 0; i < 32; i++)
         {
            int fieldMask = 1 << i;
            if ((fieldMask & f) != 0)
            {
               return (LayerMask)i;
            }
         }
      }
      return f;
   }

   private static T EnumGetNextOrFirst<T>(T e) where T : struct, System.IConvertible
   {
      if (!typeof(T).IsEnum)
      {
         throw new System.ArgumentException("T must be an enumerated type");
      }
      T[] all = (T[])System.Enum.GetValues(typeof(T));
      return all[NegModInt(System.Array.IndexOf(all, e) + 1, all.Length)];
   }

   static public T EnumInputReset<T>(string hash, string s, T f, T def, int numberPerColumn, int width = 300) where T : struct, System.IConvertible
   {
      if (!typeof(T).IsEnum)
      {
         throw new System.ArgumentException("T must be an enumerated type");
      }

      if (numberPerColumn == -1)
      {
         GUILayout.BeginHorizontal();
         var array = System.Enum.GetValues(typeof(T));
         for (int i = 0; i < array.Length; i++)
         {
            T m = (T)array.GetValue(i);
            GUILayout.FlexibleSpace();
            string s2 = m.ToString();
            if (m.ToString() == f.ToString())
            {
               s2 = "[" + s2 + "]";
            }
            if (GUILayout.Button(s2, GUILayout.Width(width)))
            {
               f = m;
            }
            BR();
         }
         GUILayout.FlexibleSpace();
         if (ResetButton())
         {
            f = def;
         }
         GUILayout.EndHorizontal();
      }
      else if (numberPerColumn == 0)
      {
         if (GUILayout.Button(f.ToString(), GUILayout.Width(width)))
         {
            f = EnumGetNextOrFirst<T>(f);
         }
      }
      else
      {
         if (BeginExpanderSimple(hash + s, s, s + ":" + f.ToString()))
         {
            GUILayout.BeginHorizontal();
            GUILayout.Label(s);
            GUILayout.FlexibleSpace();
            var array = System.Enum.GetValues(typeof(T));
            for (int i = 0; i < array.Length; i++)
            {
               T m = (T)array.GetValue(i);
               if (i != 0 && (i % numberPerColumn) == 0)
               {
                  BR();
                  GUILayout.FlexibleSpace();
               }

               string s2 = m.ToString();
               if (m.ToString() == f.ToString())
               {
                  s2 = "[" + s2 + "]";
               }
               if (GUILayout.Button(s2, GUILayout.Width(width)))
               {
                  f = m;
               }
            }
            BR();
            GUILayout.FlexibleSpace();

            if (ResetButton())
            {
               f = def;
            }
            GUILayout.EndHorizontal();
         }
         EndExpanderSimple();
      }
      return f;
   }

   static public string ListInputReset(string hash, string s, string f, System.Collections.Generic.List<string> marray, string def, int numberPerColumn = 1, int width = 300)
   {
      if (BeginExpanderSimple(hash + s, s, s + ":" + f.ToString()))
      {
         GUILayout.BeginHorizontal();
         GUILayout.Label(s);
         GUILayout.FlexibleSpace();
         for (int i = 0; i < marray.Count; i++)
         {
            var m = marray[i];
            if (i != 0 && (i % numberPerColumn) == 0)
            {
               BR();
               GUILayout.FlexibleSpace();
            }

            string s2 = m.ToString();
            if (m.ToString() == f.ToString())
            {
               s2 = "[" + s2 + "]";
            }
            if (GUILayout.Button(s2, GUILayout.Width(width)))
            {
               f = m;
            }
         }
         BR();
         GUILayout.FlexibleSpace();

         if (ResetButton())
         {
            f = def;
         }
         GUILayout.EndHorizontal();
      }
      EndExpanderSimple();

      return f;
   }

   /// <summary>
   /// Internal Function: GUI helper function
   /// provides a gui widget with a reset button that returns the value back to default
   /// </summary>
   /// <param name="s">The s.</param>
   /// <param name="f">if set to <c>true</c> [f].</param>
   /// <returns>bool</returns>
   static public bool BoolInput(string s, bool f)
   {
      GUILayout.BeginHorizontal();
      bool returnVal = GUILayout.Toggle(f, s);
      GUILayout.EndHorizontal();
      return returnVal;
   }

   public static bool BoolInput(string s, bool f, float width)
   {
      GUILayout.BeginHorizontal();
      bool returnVal = GUILayout.Toggle(f, s, GUILayout.Width(width));
      GUILayout.EndHorizontal();
      return returnVal;
   }

   /// <summary>
   /// Internal Function: GUI helper function
   /// provides a gui widget with a reset button that returns the value back to default
   /// </summary>
   static public string StringInputReset(string s, string f, string def, float? maxWidth = null)
   {
      if (maxWidth != null)
      {
         GUILayout.BeginHorizontal(GUILayout.MaxWidth(maxWidth.GetValueOrDefault()));
      }
      else
      {
         GUILayout.BeginHorizontal();
      }
      GUILayout.Label(s);
      f = GUILayout.TextField(f);
      if (ResetButton())
      {
         f = def;
      }
      GUILayout.EndHorizontal();
      return f;
   }

   static public string TextBoxInputReset(string s, string oldF, string def, float? width = null, float? height = null)
   {
      GUILayout.Label(s);
      string f = oldF;
      if (height != null & width != null)
      {
         f = GUILayout.TextField(oldF, GUILayout.Width(width.GetValueOrDefault()), GUILayout.Height(height.GetValueOrDefault()));
      }
      else if (width != null)
      {
         f = GUILayout.TextField(oldF, GUILayout.Width(width.GetValueOrDefault()));
      }
      else if (height != null)
      {
         f = GUILayout.TextField(oldF, GUILayout.Height(height.GetValueOrDefault()));
      }
      else
      {
         f = GUILayout.TextField(oldF);
      }
      if (ResetButton())
      {
         f = def;
      }
      if (f != oldF)
      {
         f = f.Replace("\r\n", "\n").Replace("\r", "\n");
      }
      return f;
   }

   /// <summary>
   /// Internal Function: GUI helper function
   /// provides a gui widget with a reset button that returns the value back to default
   /// </summary>
   static public bool StringInputResetWasChanged(string s, ref string f, string def, float? maxWidth = null)
   {
      string old = f;
      f = StringInputReset(s, f, def, maxWidth);
      return (f != old);
   }

   /// <summary>
   /// Internal Function: GUI helper function
   /// </summary>
   static public string StringInput(string s, string f, float? maxWidth = null)
   {
      if (maxWidth != null)
      {
         GUILayout.BeginHorizontal(GUILayout.MaxWidth(maxWidth.GetValueOrDefault()));
      }
      else
      {
         GUILayout.BeginHorizontal();
      }
      GUILayout.Label(s);
      f = GUILayout.TextField(f);
      GUILayout.EndHorizontal();
      return f;
   }

   /// <summary>
   /// Internal Function: GUI helper function
   /// provides a gui widget with a reset button that returns the value back to default
   /// </summary>
   /// <param name="s">The s.</param>
   /// <param name="f">The f.</param>
   /// <param name="def">The def.</param>
   /// <returns>Vector3.</returns>
   static public Vector3 Vector3InputReset(string s, Vector3 f, Vector3 def)
   {
      Vector3 returnVal = f;
      GUILayout.BeginHorizontal();
      try
      {
         GUILayout.Label(s);
         string floatStringX = f.x.ToString();
         string floatStringY = f.y.ToString();
         string floatStringZ = f.z.ToString();

         if (!floatStringX.Contains("."))
         {
            floatStringX += ".0";
         }
         if (!floatStringY.Contains("."))
         {
            floatStringY += ".0";
         }
         if (!floatStringZ.Contains("."))
         {
            floatStringZ += ".0";
         }

         string floatStringXold = floatStringX;
         string floatStringYold = floatStringY;
         string floatStringZold = floatStringZ;

         floatStringX = GUILayout.TextField(floatStringX, GUILayout.Width(60));
         floatStringY = GUILayout.TextField(floatStringY, GUILayout.Width(60));
         floatStringZ = GUILayout.TextField(floatStringZ, GUILayout.Width(60));

         if (floatStringX != floatStringXold)
         {
            returnVal.x = float.Parse(floatStringX);
         }

         if (floatStringY != floatStringYold)
         {
            returnVal.y = float.Parse(floatStringY);
         }
         if (floatStringZ != floatStringZold)
         {
            returnVal.z = float.Parse(floatStringZ);
         }

         if (ResetButton())
         {
            returnVal = def;
         }
      }
      catch
      {
         returnVal = f;
      }
      GUILayout.EndHorizontal();
      return returnVal;
   }

   static public Vector4 Vector4InputReset(string s, Vector4 f, Vector4 def)
   {
      Vector4 returnVal = f;
      GUILayout.BeginHorizontal();
      try
      {
         GUILayout.Label(s);
         string floatStringX = f.x.ToString();
         string floatStringY = f.y.ToString();
         string floatStringZ = f.z.ToString();
         string floatStringW = f.w.ToString();

         if (!floatStringX.Contains("."))
         {
            floatStringX += ".0";
         }
         if (!floatStringY.Contains("."))
         {
            floatStringY += ".0";
         }
         if (!floatStringZ.Contains("."))
         {
            floatStringZ += ".0";
         }
         if (!floatStringW.Contains("."))
         {
            floatStringW += ".0";
         }
         returnVal.x = float.Parse(GUILayout.TextField(floatStringX, GUILayout.Width(60)));
         returnVal.y = float.Parse(GUILayout.TextField(floatStringY, GUILayout.Width(60)));
         returnVal.z = float.Parse(GUILayout.TextField(floatStringZ, GUILayout.Width(60)));
         returnVal.w = float.Parse(GUILayout.TextField(floatStringW, GUILayout.Width(60)));

         if (ResetButton())
         {
            returnVal = def;
         }
      }
      catch
      {
         returnVal = f;
      }
      GUILayout.EndHorizontal();
      return returnVal;
   }

   static public bool RectInputReset(string s, ref Rect f, Rect def, ref bool? usePixel, ref int? mouseButtonDown, Omnity anOmnity,
                                     float? OverriddenScreenwidth = null,
                                     float? OverriddenScreenheight = null
                                     )
   {
      Rect oldRect = f;

      f = RectInputReset(s, f, def, ref usePixel, ref mouseButtonDown, anOmnity,
                  OverriddenScreenwidth,
                  OverriddenScreenheight);
      return (oldRect != f);
   }

   static public bool BoolInputResetWasChanged(string s, ref bool f, bool def)
   {
      GUILayout.BeginHorizontal();
      bool oldVal = f;

      f = GUILayout.Toggle(f, s);
      if (ResetButton())
      {
         f = def;
      }

      bool wasChanged = (oldVal != f);

      GUILayout.EndHorizontal();
      return wasChanged;
   }

   static public bool FloatInputResetWasChanged(string s, ref float f, float def)
   {
      float oldF = f;
      GUILayout.BeginHorizontal();
      try
      {
         GUILayout.Label(s);
         string floatString = f.ToString();
         if (!floatString.Contains("."))
         {
            floatString += ".0";
         }
         f = float.Parse(GUILayout.TextField(floatString, GUILayout.Width(60)));
         if (ResetButton())
         {
            f = def;
         }
      }
      catch
      {
      }

      GUILayout.EndHorizontal();
      return f != oldF;
   }

   static public Rect RectInputReset(string s, Rect f, Rect def, ref bool usePixel, ref int? mouseButtonDown, Omnity anOmnity, float? OverriddenScreenWidth = null, float? OverriddenScreenHeight = null)
   {
      bool? myUsePixel = usePixel;
      Rect r = RectInputReset(s, f, def, ref myUsePixel, ref mouseButtonDown, anOmnity, OverriddenScreenWidth, OverriddenScreenHeight);
      usePixel = myUsePixel.GetValueOrDefault();
      return r;
   }

   static public Rect RectInputReset(string s, Rect f, Rect def, ref bool? usePixel, ref int? mouseButtonDown, Omnity anOmnity,
       float? OverriddenScreenWidth = null,
       float? OverriddenScreenHeight = null
       )
   {
      Rect returnVal = f;
      GUILayout.BeginHorizontal();
      try
      {
         if (usePixel != null)
         {
            if (Screen.height > 0 && Screen.width > 0)
            {
            }
            else
            {
               usePixel = false;
            }
         }
         GUILayout.Label(s);
         GUILayout.FlexibleSpace();

         float Screenwidth = anOmnity.windowInfo.fullscreenInfo.goalWindowPosAndRes.width;
         float Screenheight = anOmnity.windowInfo.fullscreenInfo.goalWindowPosAndRes.height;
         if (OverriddenScreenWidth != null)
         {
            Screenwidth = OverriddenScreenWidth.GetValueOrDefault();
         }
         if (OverriddenScreenHeight != null)
         {
            Screenheight = OverriddenScreenHeight.GetValueOrDefault();
         }

         bool myUsePixel = false;
         if (usePixel != null)
         {
            myUsePixel = usePixel.GetValueOrDefault();
         }
         string floatStringX = (f.x * (myUsePixel ? Screenwidth : 1)).ToString();
         string floatStringY = (f.y * (myUsePixel ? Screenheight : 1)).ToString();
         string floatStringW = (f.width * (myUsePixel ? Screenwidth : 1)).ToString();
         string floatStringH = (f.height * (myUsePixel ? Screenheight : 1)).ToString();

         if (!myUsePixel)
         {
            if (!floatStringX.Contains("."))
            {
               floatStringX += ".0";
            }
            if (!floatStringY.Contains("."))
            {
               floatStringY += ".0";
            }

            if (!floatStringW.Contains("."))
            {
               floatStringW += ".0";
            }
            if (!floatStringH.Contains("."))
            {
               floatStringH += ".0";
            }
         }

         GUILayout.Label("x");
         returnVal.x = float.Parse(GUILayout.TextField(floatStringX, GUILayout.Width(60)));
         GUILayout.Label("y");
         returnVal.y = float.Parse(GUILayout.TextField(floatStringY, GUILayout.Width(60)));
         GUILayout.Label("w");
         returnVal.width = float.Parse(GUILayout.TextField(floatStringW, GUILayout.Width(60)));
         GUILayout.Label("h");
         returnVal.height = float.Parse(GUILayout.TextField(floatStringH, GUILayout.Width(60)));
         if (myUsePixel)
         {
            returnVal.height = ((float)returnVal.height / (float)Screenheight);
            returnVal.width = ((float)returnVal.width / (float)Screenwidth);
            returnVal.x = ((float)returnVal.x / (float)Screenwidth);
            returnVal.y = ((float)returnVal.y / (float)Screenheight);
         }
         returnVal.x = Mathf.Clamp01(returnVal.x);
         returnVal.y = Mathf.Clamp01(returnVal.y);
         returnVal.width = Mathf.Clamp01(returnVal.width);
         returnVal.height = Mathf.Clamp01(returnVal.height);

         if (ResetButton())
         {
            returnVal = def;
         }
         if (usePixel != null)
         {
            BR();
            GUILayout.FlexibleSpace();
            usePixel = GUILayout.Toggle(usePixel.GetValueOrDefault(), "units in pixels");
            BR();
            GUILayout.Label(usePixel.GetValueOrDefault() ? "Units are in pixels" : "Units are normalized");
         }
      }
      catch
      {
         returnVal = f;
      }
      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal();
      GUILayout.Label("Preview");
      GUILayout.FlexibleSpace();
      DrawRect(ref returnVal, ref mouseButtonDown);
      GUILayout.EndHorizontal();
      return returnVal;
   }

   /// <summary>
   /// Internal function: draws a rectangle used for viewport preview
   /// </summary>
   /// <param name="returnVal">The return value.</param>
   /// <returns>Rect.</returns>
   private static void DrawRect(ref Rect returnVal, ref int? mouseButtonDown)
   {
      try
      {
         // size of the preview
         float totalWidth = 300.0f;
         float totalHeight = 300.0f;

         // set to aspect ratio of window
         if (Screen.height > 0 && Screen.width > 0)
         {
            if (Screen.height > Screen.width)
            {
               totalWidth *= (float)Screen.width / (float)Screen.height;
            }
            else
            {
               totalHeight *= (float)Screen.height / (float)Screen.width;
            }
         }

         #region calculatesize

         // size of each row and collumn
         float leftWidth = returnVal.x * totalWidth;
         float middleWidth = returnVal.width * totalWidth;
         float rightWidth = (1.0f - returnVal.x - returnVal.width) * totalWidth;
         float topHeight = (1.0f - returnVal.y - returnVal.height) * totalHeight;
         float middleHeight = returnVal.height * totalHeight;
         float bottomHeight = (returnVal.y) * totalHeight;

         #endregion calculatesize

         GUILayout.BeginVertical();

         #region TopRow

         GUILayout.BeginHorizontal();
         GUILayout.FlexibleSpace();
         GUILayout.Button("", GUILayout.Height(topHeight), GUILayout.Width(leftWidth));
         GUILayout.Button("", GUILayout.Height(topHeight), GUILayout.Width(middleWidth));
         GUILayout.Button("", GUILayout.Height(topHeight), GUILayout.Width(rightWidth));
         GUILayout.Space(rightPadding);
         GUILayout.EndHorizontal();

         #endregion TopRow

         #region MiddleRow

         GUILayout.BeginHorizontal();
         GUILayout.FlexibleSpace();
         GUILayout.Button("", GUILayout.Height(middleHeight), GUILayout.Width(leftWidth));

         bool buttonDown = GUILayout.RepeatButton("Viewport", GUILayout.Height(middleHeight), GUILayout.Width(middleWidth));
         if (buttonDown && null == mouseButtonDown)
         {
            if (Input.GetMouseButton(0))
            {
               mouseButtonDown = 0;
            }
            else if (Input.GetMouseButton(1))
            {
               mouseButtonDown = 1;
            }
         }
         else if (mouseButtonDown != null)
         {
            if (!(Input.GetMouseButton(0) || Input.GetMouseButton(1)))
            {
               mouseButtonDown = null;
            }
         }

         if (mouseButtonDown != null)
         {
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");

            float normalizedDx = dx * totalWidth / (float)Screen.width;
            float normalizedDy = dy * totalHeight / (float)Screen.height;

            normalizedDx *= .125f;
            normalizedDy *= .125f;

            switch (mouseButtonDown.GetValueOrDefault())
            {
               case 0:
                  returnVal.x += normalizedDx;
                  returnVal.y += normalizedDy;
                  returnVal.x = Mathf.Clamp(returnVal.x, 0, 1 - returnVal.width);
                  returnVal.y = Mathf.Clamp(returnVal.y, 0, 1 - returnVal.height);
                  break;

               case 1:
                  returnVal.width += normalizedDx;
                  returnVal.height += normalizedDy;
                  returnVal.width = Mathf.Clamp(returnVal.width, 0.15f, 1 - returnVal.x);
                  returnVal.height = Mathf.Clamp(returnVal.height, 0.15f, 1 - returnVal.y);
                  break;

               default:
                  break;
            }
         }
         GUILayout.Button("", GUILayout.Height(middleHeight), GUILayout.Width(rightWidth));
         GUILayout.Space(rightPadding);
         GUILayout.EndHorizontal();

         #endregion MiddleRow

         #region BottomRow

         GUILayout.BeginHorizontal();
         GUILayout.FlexibleSpace();
         GUILayout.Button("", GUILayout.Height(bottomHeight), GUILayout.Width(leftWidth));
         GUILayout.Button("", GUILayout.Height(bottomHeight), GUILayout.Width(middleWidth));
         GUILayout.Button("", GUILayout.Height(bottomHeight), GUILayout.Width(rightWidth));
         GUILayout.Space(rightPadding);
         GUILayout.EndHorizontal();

         #endregion BottomRow

         GUILayout.EndVertical();
      }
      catch
      {
         Debug.LogError("Error something happened with the gui here");
      }
   }

   static public float rightPadding = 10;

   /// <summary>
   /// Internal Function: GUI helper function
   /// provides a gui widget with a reset button that returns the value back to default
   /// </summary>
   /// <param name="s">The s.</param>
   /// <param name="f">The f.</param>
   /// <param name="def">The def.</param>
   /// <returns>Vector2.</returns>
   static public Vector2 Vector2InputReset(string s, Vector2 f, Vector2 def)
   {
      Vector2 returnVal = f;
      GUILayout.BeginHorizontal();
      try
      {
         GUILayout.Label(s);
         string floatStringX = f.x.ToString();
         string floatStringY = f.y.ToString();

         if (!floatStringX.Contains("."))
         {
            floatStringX += ".0";
         }
         if (!floatStringY.Contains("."))
         {
            floatStringY += ".0";
         }
         returnVal.x = float.Parse(GUILayout.TextField(floatStringX, GUILayout.Width(60)));
         returnVal.y = float.Parse(GUILayout.TextField(floatStringY, GUILayout.Width(60)));

         if (ResetButton())
         {
            returnVal = def;
         }
      }
      catch
      {
         returnVal = f;
      }
      GUILayout.EndHorizontal();
      return returnVal;
   }

   /// <summary>
   /// Internal Function: GUI helper function
   /// provides a gui widget with a reset button that returns the value back to default
   /// </summary>
   /// <param name="s">The s.</param>
   /// <param name="f">The f.</param>
   /// <param name="def">The def.</param>
   /// <returns>Color.</returns>
   static public Color ColorInputReset(string s, Color f, Color def)
   {
      Color returnVal = f;
      GUILayout.BeginHorizontal();
      try
      {
         GUILayout.Label(s);
         string floatStringX = f.r.ToString();
         string floatStringY = f.g.ToString();
         string floatStringZ = f.b.ToString();
         string floatStringA = f.a.ToString();

         if (!floatStringX.Contains("."))
         {
            floatStringX += ".0";
         }
         if (!floatStringY.Contains("."))
         {
            floatStringY += ".0";
         }
         if (!floatStringZ.Contains("."))
         {
            floatStringZ += ".0";
         }
         if (!floatStringA.Contains("."))
         {
            floatStringA += ".0";
         }
         returnVal.r = float.Parse(GUILayout.TextField(floatStringX, GUILayout.Width(60)));
         returnVal.g = float.Parse(GUILayout.TextField(floatStringY, GUILayout.Width(60)));
         returnVal.b = float.Parse(GUILayout.TextField(floatStringZ, GUILayout.Width(60)));
         returnVal.a = float.Parse(GUILayout.TextField(floatStringA, GUILayout.Width(60)));

         if (ResetButton())
         {
            returnVal = def;
         }
      }
      catch
      {
         returnVal = f;
      }
      GUILayout.EndHorizontal();
      return returnVal;
   }

   /// <summary>
   /// Internal Function: GUI helper function
   /// provides a gui widget with a reset button that returns the value back to default
   /// </summary>
   /// <param name="s">The s.</param>
   /// <param name="current">The current.</param>
   /// <param name="def">The def.</param>
   /// <returns>System.Int32.</returns>
   static public int NumericUpDownReset(string s, int current, int def)
   {
      int returnVal = current;
      GUILayout.BeginHorizontal();
      try
      {
         GUILayout.Label(s + "[" + current.ToString() + "]");
         if (GUILayout.Button("+", GUILayout.Width(30)))
         {
            returnVal++;
         }
         if (GUILayout.Button("-", GUILayout.Width(30)))
         {
            returnVal--;
         }
         if (ResetButton())
         {
            returnVal = def;
         }
      }
      catch
      {
         returnVal = current;
      }
      GUILayout.EndHorizontal();
      return returnVal;
   }

   /// <summary>
   /// Negmods : Modulus function that allows negative numbers.
   /// </summary>
   /// <param name="x">The x.</param>
   /// <param name="m">The m.</param>
   /// <returns>System.Int32.</returns>
   public static int NegModInt(int x, int m)
   {
      return (x % m + m) % m;
   }

   static public void MultiEnumInputClear<T>(string hash, string s, System.Collections.Generic.List<int> currentlySelectedOptions) where T : struct, System.IConvertible
   {
      if (!typeof(T).IsEnum)
      {
         throw new System.ArgumentException("T must be an enumerated type");
      }
      if (BeginExpanderSimple(hash + s, s, s))
      {
         int width = 300;
         GUILayout.BeginHorizontal();
         int? removeIt = null;
         int? addIt = null;
         for (int j = 0; j < currentlySelectedOptions.Count; j++)
         {
            int i = currentlySelectedOptions[j];
            string str = System.Enum.GetName(typeof(T), i);
            if (str == null || str == "")
            {
               str = "Unnamed plugin id " + i.ToString();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("[" + str + "]", GUILayout.Width(width)))
            {
               removeIt = i;
            }
            BR();
         }
         System.Array array = System.Enum.GetValues(typeof(T));
         for (int i = 0; i < array.Length; i++)
         {
            T m = (T)array.GetValue(i);
            System.Enum test = System.Enum.Parse(typeof(T), m.ToString()) as System.Enum;
            int x = System.Convert.ToInt32(test); // x is the integer value of enum
            if (currentlySelectedOptions.Contains(x))
            {
               continue;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(m.ToString(), GUILayout.Width(width)))
            {
               addIt = x;
            }
            BR();
         }
         if (addIt != null)
         {
            currentlySelectedOptions.Add(addIt.GetValueOrDefault());
         }
         if (removeIt != null)
         {
            currentlySelectedOptions.Remove(removeIt.GetValueOrDefault());
         }

         GUILayout.FlexibleSpace();
         if (Button("Clear", width))
         {
            currentlySelectedOptions.Clear();
         }
         GUILayout.EndHorizontal();
      }
      EndExpanderSimple();
   }

   static public void WriteList<T>(System.Xml.XmlTextWriter xmlWriter, string title, System.Collections.Generic.List<T> pluginIDs)
   {
      System.Text.StringBuilder sb = new System.Text.StringBuilder();
      for (int i = 0; i < pluginIDs.Count; i++)
      {
         if (sb.Length > 0)
         {
            sb.Append(",");
         }
         sb.Append(pluginIDs[i].ToString());
      }
      xmlWriter.WriteElementString(title, sb.ToString());
   }

   static public System.Collections.Generic.List<int> ReadElementIntListDefault(System.Xml.XPath.XPathNavigator nav, string path, System.Collections.Generic.List<int> def)
   {
      try
      {
         System.Xml.XPath.XPathNavigator s = nav.SelectSingleNode(path);
         if (s == null)
         {
            return def;
         }
         if (s.InnerXml == null)
         {
            return def;
         }

         string[] listOfString = s.InnerXml.Split(","[0]);
         System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
         foreach (string ss in listOfString)
         {
            try
            {
               int i = int.Parse(ss);
               list.Add(i);
            }
            catch
            {
            }
         }

         return list;
      }
      catch (System.Exception e)
      {
         Debug.Log(e.ToString());
         return def;
      }
   }

   public static bool FloatInputResetSliderWasChanged(string p1, ref float w, float _min, float _def, float _max, float stringWidth = 60, float sliderWidth = 200, float okWidth = 60)
   {
      float oldW = w;
      w = FloatInputResetSlider(p1, w, _min, _def, _max, stringWidth, sliderWidth, okWidth);
      return (w != oldW);
   }

   internal static bool Vector3InputResetWasChanged(string p, ref Vector3 val, Vector3 def)
   {
      Vector3 old = val;
      val = Vector3InputReset(p, val, def);
      return old != val;
   }

   internal static bool Vector2InputResetWasChanged(string p, ref Vector2 val, Vector2 def)
   {
      Vector2 old = val;
      val = Vector2InputReset(p, val, def);
      return old != val;
   }

   internal static bool IntInputResetWasChanged(string name, ref int v, int def)
   {
      int oldVal = v;
      v = IntInputReset(name, v, def);
      return (oldVal != v);
   }

   static public bool EnumInputResetWasChanged<T>(string hash, string s, ref T f, T def, int numberPerColumn, int width = 300) where T : struct, System.IConvertible
   {
      T original = f;
      f = EnumInputReset<T>(hash, s, f, def, numberPerColumn, width);
      // this is a work around because i could not get System.IConvertible, System.IComparable,  System.IFormattable to work
      return (System.Convert.ToInt64(f) != System.Convert.ToInt64(original));
   }

   public static bool ContainsCaseInsensitiveSimple(this string source, string toCheck)
   {
      if (source == null)
      {
         return false;
      }
      return source.IndexOf(toCheck, System.StringComparison.OrdinalIgnoreCase) >= 0;
   }
}

/// <summary>
/// Enum DebugLevel
/// debug level verbosity options
/// </summary>
public enum DebugLevel
{

   /// <summary>
   /// Low verbosity
   /// </summary>
   Low = 0,

   /// <summary>
   /// Hight verbosity
   /// </summary>
   High = 1,
}

/// <summary>
/// Class OmnityWindowInfo.  This holds info about how to place the unity window to span across the desktop/projector.  it holds hints about gui positioning and viewports.
/// this will not do as much if easymultidisplay is not added to the scene.  In that case the application developers must find a strategy for getting the fisheye image on the projector.
/// </summary>
[System.Serializable]
public class OmnityWindowInfo
{

   /// <summary>
   /// A hint to the application on wear to put the flat screen display on the screen if using a dome + flatscreen.
   /// the application author is required to enable a camera with this normalizedViewport
   /// It is suggested to look into
   /// http://docs.unity3d.com/Documentation/ScriptReference/GUI-matrix.html
   /// to ensure that gui elements end up on the correct screen and work right
   /// </summary>
   public Rect GUIViewportPositionHint = new Rect(0, 0, 1, 1);

   /// <summary>
   ///  Helper function for serializing the XML.
   /// </summary>
   /// <param name="xmlWriter">The XML writer.</param>
   public void WriteXML(System.Xml.XmlTextWriter xmlWriter)
   {
      xmlWriter.WriteStartElement("windowInfo");
      xmlWriter.WriteElementString("GUIViewportPositionHint", GUIViewportPositionHint.ToString("R"));
      xmlWriter.WriteElementString("GUIViewportScaleHint", GUIViewportScaleHint.ToString());
      fullscreenInfo.WriteXML("fullscreenInfo", xmlWriter);
      xmlWriter.WriteEndElement();
   }

   /// <summary>
   /// Reads the xml for this class
   /// </summary>
   /// <param name="nav">The nav.</param>
   public void ReadXML(System.Xml.XPath.XPathNavigator nav)
   {
      GUIViewportPositionHint = OmnityHelperFunctions.ReadElementRectDefault(nav, ".//GUIViewportPositionHint", new Rect(0, 0, 1, 1));
      GUIViewportScaleHint = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//GUIViewportScaleHint", GUIViewportScaleHint);
      fullscreenInfo.ReadXML("fullscreenInfo", nav);
   }

   private string expanderHash = OmnityHelperFunctions.OmnityUniqueHash.uniqueHash;

   public void OnGUI(Omnity parentOmnity)
   {
      if (OmnityHelperFunctions.BeginExpanderSimple(expanderHash + "MultipleWindowInterface (Hint Only)", "MultipleWindowInterface (Hint Only)", "MultipleWindowInterface (Hint Only)"))
      {
         if (OmnityHelperFunctions.BeginExpanderSimple(expanderHash + "GUI", "GUI", "GUI"))
         {
            GUIViewportPositionHint = OmnityHelperFunctions.RectInputReset("GUIViewportPositionHint", GUIViewportPositionHint, new Rect(0, 0, 1, 1), ref parentOmnity.usePixel, ref flatScreenViewportResizeHint, parentOmnity);
            GUIViewportScaleHint = OmnityHelperFunctions.FloatInputReset("GUIViewportScaleHint", GUIViewportScaleHint, 1.0f);
            OmnityHelperFunctions.GUITODO("ADD checkbox for Auto Apply enable camera/gui.matrix/viewport (Currently you must make these connections in the source code.)");
         }
         OmnityHelperFunctions.EndExpanderSimple();

         if (OmnityHelperFunctions.BeginExpanderSimple(expanderHash + "WindowInfo", "WindowInfo (Spans across multiple monitors)", "WindowInfo (Spans across multiple monitors)"))
         {
            fullscreenInfo.OnGUI(parentOmnity);
            OmnityHelperFunctions.GUITODO("ADD checkbox for Auto Apply enable camera/gui.matrix/viewport (Currently you must make these connections in the source code.)");
         }
         OmnityHelperFunctions.EndExpanderSimple();
      }
      OmnityHelperFunctions.EndExpanderSimple();
   }

   /// <summary>
   /// keeps track if viewport is being resized...
   /// </summary>
   private int? flatScreenViewportResizeHint = null;

   public OmnityFullScreenHelper fullscreenInfo = new OmnityFullScreenHelper();

   public float GUIViewportScaleHint = 1.0f;

   public void OnFinishLoadInMemoryConfig(Omnity parentOmnity)
   {
      if (fullscreenInfo.applyOnLoad)
      {
         if (Omnity.onResizeWindowFunctionCallback != null)
         {
            if (!Input.GetKey(KeyCode.LeftShift))
            {
               Omnity.onResizeWindowFunctionCallback(parentOmnity);
            }
         }
      }
   }

   public Matrix4x4 GUISCALEMATRIX
   {
      get
      {
         return OmnityGUIHelper.GUISCALEMATRIX(GUIViewportPositionHint, GUIViewportScaleHint);
      }
   }

   public float subwindowHeightPixels
   {
      get
      {
         return OmnityGUIHelper.subwindowHeightPixels(GUIViewportPositionHint, GUIViewportScaleHint);
      }
   }

   public float subwindowWidthPixels
   {
      get
      {
         return OmnityGUIHelper.subwindowWidthPixels(GUIViewportPositionHint, GUIViewportScaleHint);
      }
   }

   public float subwindowLeftPixels
   {
      get
      {
         return OmnityGUIHelper.subwindowLeftPixels(GUIViewportPositionHint, GUIViewportScaleHint);
      }
   }

   public float subwindowTopPixels
   {
      get
      {
         return OmnityGUIHelper.subwindowTopPixels(GUIViewportPositionHint, GUIViewportScaleHint);
      }
   }

   public float subWindowPixelsFromTopNotScaled
   {
      get
      {
         return (1 - (GUIViewportPositionHint.y + GUIViewportPositionHint.height)) * (float)Screen.height;
      }
   }

   public float subWindowLeftPixelActualPixelsNotScaled
   {
      get
      {
         return GUIViewportPositionHint.x * (float)Screen.width;
      }
   }

   public float subWindowHeightActualPixelsNotScaled
   {
      get
      {
         return GUIViewportPositionHint.height * (float)Screen.height;
      }
   }

   public float subWindowWidthActualPixelsNotScaled
   {
      get
      {
         return GUIViewportPositionHint.width * (float)Screen.width;
      }
   }

   public float subwindowWidthRatioOfApp
   {
      get
      {
         return GUIViewportPositionHint.width;
      }
   }

   public float subwindowHeightRatioOfApp
   {
      get
      {
         return GUIViewportPositionHint.height;
      }
   }
}

/// <summary>
/// The omnity update phase. You may need to change this if your camera is jittering every frame.
/// </summary>
/// <remarks>
/// This specifies the timing of when the Unity3D perspective cameras render, as well as any other updates.
/// Please see http://docs.unity3d.com/Documentation/Manual/ExecutionOrder.html
/// for more information.
/// </remarks>
public enum OmnityUpdatePhase
{

   /// <summary>
   /// NONE allows you to take complete control of the OmnityUpdate call.  If you set NONE, you will need call omnityClass.OmnityUpdate() from your own script, or at least make sure all of the cameras are enabled.
   /// </summary>
   NONE = 0,

   /// <summary>
   /// omnityClass.OmnityUpdate will be called inside of the the Update() Loop
   /// </summary>
   Update = 1,

   /// <summary>
   /// omnityClass.OmnityUpdate will be called inside of the the LateUpdate() Loop (this is a good choice)
   /// </summary>
   LateUpdate = 2,

   /// <summary>
   /// omnityClass.OmnityUpdate will be called via a coroutine that waits for the EndOfFrame to happen.
   /// </summary>
   EndOfFrame = 3,
}

/// <summary>
/// a partial component of the field of view of the perspective camera.  This is used when computing the total view of this camera.  A total view uses the Left, Right, Top, and Bottom.
/// </summary>
[System.Serializable]
public class OmnityPerspectiveMatrix
{
   public float fovL = 45.005f;
   public float fovR = 45.005f;
   public float fovT = 45.005f;
   public float fovB = 45.005f;

   private string expanderHash = OmnityHelperFunctions.OmnityUniqueHash.uniqueHash;

   internal void DrawWidget(OmnityProjectorType projectorType)
   {
      if (projectorType == OmnityProjectorType.Rectilinear)
      {
         if (OmnityHelperFunctions.BeginExpanderSimple(expanderHash, "omnityPerspectiveMatrix", "omnityPerspectiveMatrix"))
         {
            GUILayout.BeginHorizontal();
            if (matrixMode == MatrixMode.Orthographic)
            {
               GUILayout.FlexibleSpace();
               orthographicSize = OmnityHelperFunctions.FloatInputReset("orthographicSize", orthographicSize, 1);
               OmnityHelperFunctions.BR();
               GUILayout.FlexibleSpace();
               nearOrtho = OmnityHelperFunctions.FloatInputReset("near", nearOrtho, -1);
               OmnityHelperFunctions.BR();
               GUILayout.FlexibleSpace();
               farOrtho = OmnityHelperFunctions.FloatInputReset("far", farOrtho, 1);
               OmnityHelperFunctions.BR();
            }
            else if (matrixMode == MatrixMode.HorizontalFOV)
            {
               GUILayout.FlexibleSpace();
               fovR = fovT = fovB = fovL = OmnityHelperFunctions.FloatInputResetSlider("fov", fovL * 2.0f, 0, 90, 140) * .5f;
               OmnityHelperFunctions.BR();
               GUILayout.FlexibleSpace();
               near = OmnityHelperFunctions.FloatInputReset("near", near, .1f);
               far = OmnityHelperFunctions.FloatInputReset("far", far, 10f);
               OmnityHelperFunctions.BR();
            }
            else
            {
               GUILayout.FlexibleSpace();
               fovT = OmnityHelperFunctions.FloatInputResetNoSpace("fovT", fovT, 45);
               GUILayout.FlexibleSpace();
               GUILayout.EndHorizontal();
               GUILayout.BeginHorizontal();
               OmnityHelperFunctions.BR();
               GUILayout.FlexibleSpace();
               fovT = GUILayout.VerticalSlider(fovT, 90, 0);
               GUILayout.FlexibleSpace();
               OmnityHelperFunctions.BR();
               fovL = OmnityHelperFunctions.FloatInputResetNoSpace("fovL", fovL, 45);
               fovL = GUILayout.HorizontalSlider(fovL, 90, 0);
               fovR = GUILayout.HorizontalSlider(fovR, 0, 90);
               fovR = OmnityHelperFunctions.FloatInputResetNoSpace("fovR", fovR, 45);
               OmnityHelperFunctions.BR();
               GUILayout.FlexibleSpace();
               fovB = GUILayout.VerticalSlider(fovB, 0, 90);
               GUILayout.FlexibleSpace();
               OmnityHelperFunctions.BR();
               GUILayout.FlexibleSpace();
               fovB = OmnityHelperFunctions.FloatInputResetNoSpace("fovB", fovB, 45);
               GUILayout.FlexibleSpace();
               OmnityHelperFunctions.BR();
               GUILayout.FlexibleSpace();
               near = OmnityHelperFunctions.FloatInputReset("near", near, .1f);
               far = OmnityHelperFunctions.FloatInputReset("far", far, 10f);

               OmnityHelperFunctions.BR();
            }

            matrixMode = OmnityHelperFunctions.EnumInputReset<MatrixMode>(expanderHash, "matrixMode", matrixMode, MatrixMode.Default, 1);
            GUILayout.EndHorizontal();
         }
         OmnityHelperFunctions.EndExpanderSimple();
      }
      else
      {
         flipHorizontal = OmnityHelperFunctions.BoolInputReset("FlipHorizontal", flipHorizontal, false);
      }

      preRotation = OmnityHelperFunctions.Vector3InputReset("Pre Rotation (leave 0,0,0 in most situations)", preRotation, Vector3.zero);
   }

   /// The near clip plane of this camera
   /// </summary>
   public float near = 0.1f;

   public float nearOrtho = -1.0f;

   /// <summary>
   /// The far clip plane of this camera
   /// </summary>
   public float far = 2000.0f;

   public float farOrtho = 1.0f;

   public bool isOrthographic(OmnityProjectorType projectorType)
   {
      switch (projectorType)
      {
         case OmnityProjectorType.Rectilinear:
            return false;

         case OmnityProjectorType.FisheyeFullDome:
            return true;

         case OmnityProjectorType.FisheyeTruncated:
            return true;

         default:
            Debug.LogError("Unknown projector type mode " + projectorType);
            return true;
      }
   }

   /// <summary>
   /// Gets the matrix.  Use this to build the Camera's matrix based on the component's FOV and clip planes.  It will return an asymmetric projection matrix if needed.
   /// </summary>
   /// <returns>Matrix4x4.</returns>
   public Matrix4x4 GetMatrix(OmnityProjectorType projectorType)
   {
      Matrix4x4 preMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(preRotation), Vector3.one);
      if (isOrthographic(projectorType))
      {
         return GetOrthographicMatrix() * preMatrix;
      }
      else
      {
         return AsymmetricalViewFrustum(fovL, fovR, fovB, fovT, near, far, matrixMode) * preMatrix;
      }
   }

   public Vector3 preRotation = Vector3.zero;

   /// <summary>
   /// Generate a Symmetrical view matrix, if needed
   /// </summary>
   /// <param name="fovH">The fov H.</param>
   /// <param name="fovV">The fov V.</param>
   /// <param name="near">The near.</param>
   /// <param name="far">The far.</param>
   /// <returns>Matrix4x4.</returns>
   private static Matrix4x4 SymmetricalViewFrustum(float fovH, float fovV, float near, float far, MatrixMode matrixMode)
   {
      return AsymmetricalViewFrustum(fovH / 2.0f, fovH / 2.0f, fovV / 2.0f, fovV / 2.0f, near, far, matrixMode);
   }

   /// <summary>
   /// Asymmetrical view frustum generation.  Some hardware setups can optimized by using fewer render channel camera.  If the dome is not able to be fully covered with 3 cameras, sometimes, an asymmetrical view camera can help cover the dome, with minimal quality loss.  If the image becomes too distorted, or low res, or camera effects break apart then using 4 cameras with symmetrical FOVs is the solution.
   /// </summary>
   /// <param name="fovLeft">The fov left.</param>
   /// <param name="fovRight">The fov right.</param>
   /// <param name="fovDown">The fov down.</param>
   /// <param name="fovUp">The fov up.</param>
   /// <param name="near">The near.</param>
   /// <param name="far">The far.</param>
   /// <returns>Matrix4x4.</returns>
   static internal Matrix4x4 AsymmetricalViewFrustum(float fovLeft, float fovRight, float fovDown, float fovUp, float near, float far, MatrixMode matrixMode)
   {
      float top = Mathf.Tan(fovUp * Mathf.Deg2Rad) * near;
      float bottom = -Mathf.Tan(fovDown * Mathf.Deg2Rad) * near;
      float left = -Mathf.Tan(fovLeft * Mathf.Deg2Rad) * near;
      float right = Mathf.Tan(fovRight * Mathf.Deg2Rad) * near;
      return PerspectiveOffCenter(left, right, bottom, top, near, far, matrixMode);
   }

   /// <summary>
   /// Asymmetrical view frustum generation helper function.  Some hardware setups can optimized by using fewer render channel camera.  If the dome is not able to be fully covered with 3 cameras, sometimes, an asymmetrical view camera can help cover the dome, with minimal quality loss.  If the image becomes too distorted, or low res, or camera effects break apart then using 4 cameras with symmetrical FOVs is the solution.
   /// </summary>
   /// <param name="left">The left fov in degrees</param>
   /// <param name="right">The right fov in degrees</param>
   /// <param name="bottom">The bottom fov in degrees</param>
   /// <param name="top">The top fov in degrees</param>
   /// <param name="near">The near clip plane</param>
   /// <param name="far">The far clip plane</param>
   /// <param name="matrixMode">normally not used, but is passed to the customMatrixFunction to give a hint on how to override the matrix generation</param>
   /// <returns>Matrix4x4</returns>
   internal delegate Matrix4x4 CustomMatrixFunction(float left, float right, float bottom, float top, float near, float far, MatrixMode matrixMode);

   static internal CustomMatrixFunction customMatrixFunction = null;

   private static Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far, MatrixMode matrixMode)
   {
      if (customMatrixFunction != null)
      {
         return customMatrixFunction(left, right, bottom, top, near, far, matrixMode);
      }
      else
      {
         float x = (2.0f * near) / (right - left);
         float y = (2.0f * near) / (top - bottom);
         float a = (right + left) / (right - left);
         float b = (top + bottom) / (top - bottom);
         float c = -(far + near) / (far - near);
         float d = -(2.0f * far * near) / (far - near);
         float e = -1.0f;

         Matrix4x4 m = new Matrix4x4();
         m[0, 0] = x;
         m[0, 1] = 0;
         m[0, 2] = a;
         m[0, 3] = 0;
         m[1, 0] = 0;
         m[1, 1] = y;
         m[1, 2] = b;
         m[1, 3] = 0;
         m[2, 0] = 0;
         m[2, 1] = 0;
         m[2, 2] = c;
         m[2, 3] = d;
         m[3, 0] = 0;
         m[3, 1] = 0;
         m[3, 2] = e;
         m[3, 3] = 0;
         return m;
      }
   }

   /// <summary>
   /// Makes a orthographic matrix.
   /// </summary>
   /// <returns>Matrix4x4.</returns>
   private Matrix4x4 MakeMatrixOrthographic()
   {
      if (!OmnityGraphicsInfo.bDirectX)
      {
         if (flipHorizontal)
         {
            return Matrix4x4.Ortho(1, -1, -1, 1, -1.0f, 1.0f);
         }
         else
         {
            return Matrix4x4.Ortho(-1, 1, -1, 1, -1.0f, 1.0f);
         }
      }

      float f = 1.0f;
      float n = 0.0f;
      float l = -1;
      float r = 1;
      float t = 1;
      float b = -1;
      Vector4 row1 = new Vector4((flipHorizontal ? -1.0f : 1.0f) * (2.0f / (r - l)), 0.0f, 0.0f, 0.0f);
      Vector4 row2 = new Vector4(0.0f, 2.0f / (t - b), 0.0f, 0.0f);
      Vector4 row3 = new Vector4(0.0f, 0.0f, 1.0f / (n - f), 0.0f);
      Vector4 row4 = new Vector4(0.0f, 0.0f, -(n) / (n - f), 1.0f);

      Matrix4x4 orthoMat = new Matrix4x4();
      orthoMat.SetRow(0, row1);
      orthoMat.SetRow(1, row2);
      orthoMat.SetRow(2, row3);
      orthoMat.SetRow(3, row4);
      return (orthoMat);
   }

   /// <summary>
   /// The orthographic projection matrix
   /// </summary>
   private Matrix4x4 orthographicProjectionMatrix = Matrix4x4.identity;

   private bool? lastComputedWasHorizontal = null; // this bool is a nullable type so if it is null that means orthographicProjectionMatrix needs to be initalized

   private Matrix4x4 GetOrthographicMatrix()
   {
      if (lastComputedWasHorizontal == null || lastComputedWasHorizontal.GetValueOrDefault() != flipHorizontal)
      {
         lastComputedWasHorizontal = flipHorizontal;
         orthographicProjectionMatrix = MakeMatrixOrthographic();
      }
      return orthographicProjectionMatrix;
   }

   /// <summary>
   /// Enum MatrixMode to give hint to matrix generation routine, use custom and overload the matrix function to design your own.
   /// </summary>
   public MatrixMode matrixMode = MatrixMode.Default;

   /// <summary>
   /// Enum MatrixMode to give hint to matrix generation routine, use custom and overload the matrix function to design your own.
   /// </summary>
   public enum MatrixMode
   {
      Default = 0,
      Inverted = 1,
      Orthographic = 2,
      HorizontalFOV = 3,

      Special_0 = 10,
      Special_1 = 11,
      Special_2 = 12,
      Special_3 = 13,
      Special_4 = 14,
      Special_5 = 15,
      Special_6 = 16,
      Special_7 = 17,
      Special_8 = 18,
      Special_9 = 19,
      Special_10 = 20,
      Special_11 = 11,
      Special_12 = 12,
   }

   /// <summary>
   /// flip horizontal (orthographic camera/fisheye camera only)
   /// </summary>
   public bool flipHorizontal = false;

   public float orthographicSize = .5f;

   /// <summary>
   /// Internal helper function
   /// </summary>
   internal void WriteXMLInline(System.Xml.XmlTextWriter xmlWriter)
   {
      xmlWriter.WriteElementString("fovL", fovL.ToString("F4"));
      xmlWriter.WriteElementString("fovR", fovR.ToString("F4"));
      xmlWriter.WriteElementString("fovT", fovT.ToString("F4"));
      xmlWriter.WriteElementString("fovB", fovB.ToString("F4"));
      xmlWriter.WriteElementString("near", near.ToString("F4"));
      xmlWriter.WriteElementString("far", far.ToString("F4"));
      xmlWriter.WriteElementString("nearOrtho", nearOrtho.ToString("F4"));
      xmlWriter.WriteElementString("farOrtho", farOrtho.ToString("F4"));
      xmlWriter.WriteElementString("matrixMode", matrixMode.ToString());
      xmlWriter.WriteElementString("orthographicSize", orthographicSize.ToString());
      xmlWriter.WriteElementString("FlipHorizontal", flipHorizontal.ToString());
      xmlWriter.WriteElementString("preRotation", preRotation.ToString("F4"));
   }

   /// <summary>
   /// Internal helper function
   /// </summary>
   internal void ReadXMLInline(System.Xml.XPath.XPathNavigator nav)
   {
      fovL = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//fovL", fovL);
      fovR = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//fovR", fovR);
      fovT = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//fovT", fovT);
      fovB = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//fovB", fovB);
      near = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//near", near);
      far = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//far", far);
      nearOrtho = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//nearOrtho", nearOrtho);
      farOrtho = OmnityHelperFunctions.ReadElementFloatDefault(nav, ".//farOrtho", farOrtho);
      orthographicSize = OmnityHelperFunctions.ReadElementFloatDefault(nav, "..//orthographicSize", orthographicSize);
      matrixMode = OmnityHelperFunctions.ReadElementEnumDefault<MatrixMode>(nav, ".//matrixMode", matrixMode);
      flipHorizontal = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//FlipHorizontal", flipHorizontal);
      preRotation = OmnityHelperFunctions.ReadElementVector3Default(nav, ".//preRotation", preRotation);
   }

   /// <summary>
   /// Clones this instance.
   /// </summary>
   /// <returns>OmnityPerspectiveMatrix.</returns>
   public OmnityPerspectiveMatrix Clone()
   {
      return new OmnityPerspectiveMatrix
      {
         fovL = this.fovL,
         fovR = this.fovR,
         fovT = this.fovT,
         fovB = this.fovB,
         near = this.near,
         far = this.far,
         nearOrtho = this.nearOrtho,
         farOrtho = this.farOrtho,
         matrixMode = this.matrixMode,
         flipHorizontal = this.flipHorizontal,
         orthographicSize = this.orthographicSize
      };
   }
}

/// <summary>
/// OmnityProjectorType Specifies what type of projector is connected.  Most common options are FisheyeFullDome or FisheyeTruncated
/// </summary>
public enum OmnityProjectorType
{
   FisheyeFullDome = 0,
   FisheyeTruncated = 1,
   Rectilinear = 3,
}

/// <summary>
/// OmnityFullScreenMode. This helps the EasyMultidisplay class know how to maximize the window.  Easy multidisplay script must be added for this to work.  Its important to note that Borderless and BorderlessTopmost will only work in windows, and you are better off if you also use the -popupwindow command line flag because otherwise the mouse may be off by a few pixels.
/// </summary>
public enum OmnityFullScreenMode
{
   ApplicationDefault = 0,
   FullScreenUnity = 1,
   Borderless = 2,
   BorderlessTopMost = 3,
   WindowedWithBorder = 4,
}

/// <summary>
/// Internal class for helping with GUIs for selecting viewports.
/// </summary>
[System.Serializable]
public class OmnityFullScreenHelper
{
   static public readonly Rect defaultRes = new Rect(0, 0, 1024, 768);
   public bool applyOnLoad = false;
   public Rect goalWindowPosAndRes = defaultRes;
   public OmnityFullScreenMode fullScreenMode = OmnityFullScreenMode.ApplicationDefault;

   internal static Rect actualWindowPosAndRes
   {
      get
      {
         return new Rect(0, 0, Screen.width, Screen.height);
      }
   }

   private static Omnity doResize = null;

   internal void DoUpdate()
   {
      if (doResize)
      {
         Omnity.onResizeWindowFunctionCallback(doResize);
         doResize = null;
      }
   }

   private string expanderHash = OmnityHelperFunctions.OmnityUniqueHash.uniqueHash;

   internal void OnGUI(Omnity parentOmnity)
   {
      GUILayout.BeginHorizontal();
      GUILayout.Label("Window position and Resolution");
      GUILayout.FlexibleSpace();
      GUILayout.Label("x");
      goalWindowPosAndRes.x = int.Parse(GUILayout.TextField(goalWindowPosAndRes.x.ToString(), GUILayout.Width(60)));
      GUILayout.Label("y");
      goalWindowPosAndRes.y = int.Parse(GUILayout.TextField(goalWindowPosAndRes.y.ToString(), GUILayout.Width(60)));
      GUILayout.Label("w");
      goalWindowPosAndRes.width = int.Parse(GUILayout.TextField(goalWindowPosAndRes.width.ToString(), GUILayout.Width(60)));
      GUILayout.Label("h");
      goalWindowPosAndRes.height = int.Parse(GUILayout.TextField(goalWindowPosAndRes.height.ToString(), GUILayout.Width(60)));
      if (GUILayout.Button("Reset", GUILayout.Width(150)))
      {
         goalWindowPosAndRes = defaultRes;
      }
      OmnityHelperFunctions.BR();
      fullScreenMode = OmnityHelperFunctions.EnumInputReset<OmnityFullScreenMode>(expanderHash + "fullscreenmode", "fullscreenMode", fullScreenMode, OmnityFullScreenMode.ApplicationDefault, 1);
      OmnityHelperFunctions.BR();
      GUILayout.FlexibleSpace();
      applyOnLoad = GUILayout.Toggle(applyOnLoad, "applyOnLoad");
      OmnityHelperFunctions.BR();
      if (Omnity.onResizeWindowFunctionCallback != null)
      {
         GUILayout.FlexibleSpace();
         if (GUILayout.Button("Apply Now", GUILayout.Width(150)))
         {
            Omnity.onResizeWindowFunctionCallback(parentOmnity);
         }
         OmnityHelperFunctions.BR();
         GUILayout.Label(applyOnLoad ? "Hold Left Shift on load to prevent resize.  In some situations, you may need to use resolution -1 pixel." : "In some situations, you may need to use resolution -1 pixel.");
      }
      else
      {
         GUILayout.Label("Override the delegate Omnity.onResizeWindowFunctionCallback to utilize this feature.");
      }
      GUILayout.EndHorizontal();
   }

   internal void WriteXML(string classname, System.Xml.XmlTextWriter xmlWriter)
   {
      xmlWriter.WriteStartElement(classname);
      xmlWriter.WriteElementString("applyOnLoad", applyOnLoad.ToString());
      xmlWriter.WriteElementString("goalWindowPosAndRes", goalWindowPosAndRes.ToString("R"));
      xmlWriter.WriteElementString("fullScreenMode", fullScreenMode.ToString(""));
      xmlWriter.WriteEndElement();
   }

   internal void ReadXML(string className, System.Xml.XPath.XPathNavigator nav)
   {
      System.Xml.XPath.XPathNodeIterator fullscreenInfoIterator = nav.Select("(//" + className + ")");
      while (fullscreenInfoIterator.MoveNext())
      {
         applyOnLoad = OmnityHelperFunctions.ReadElementBoolDefault(nav, ".//applyOnLoad", false);
         goalWindowPosAndRes = OmnityHelperFunctions.ReadElementRectDefault(nav, ".//goalWindowPosAndRes", goalWindowPosAndRes);
         fullScreenMode = OmnityHelperFunctions.ReadElementEnumDefault<OmnityFullScreenMode>(nav, ".//fullScreenMode", fullScreenMode);
      }
   }
}

/// <summary>
/// Abstract Class OmnityTabbedFeature.  create a Class derive from this and add it to your scene.  By overloading MyGuiCallback with a GuiLayout gui to add a gui panel to omnity's shift - f12 menu
/// </summary>
abstract public class OmnityTabbedFeature : OmnityAutoAddPlugin
{
   private OmnityGUI.TAB tab = null;

   virtual public void OnEnable()
   {
      tab = new OmnityGUI.TAB
      {
         name = MyTabName,
         callback = MyGuiCallback,
         closeGUICallback = OnCloseGUI
      };
      OmnityGUI.omnityGUITabs.Add(tab);
   }

   public bool VerifySingleton<T>(ref T _singleton) where T : OmnityTabbedFeature
   {
      if (_singleton == null)
      {
         _singleton = (T)this;
         return true;
      }
      else if (_singleton != this)
      {
         Debug.LogError("ERROR THERE SHOULD ONLY BE ONE of " + this.GetType().ToString() + " per scene");
         enabled = false;
         return false;
      }
      else
      {
         return true;
      }
   }

   virtual public void OnDisable()
   {
      if (tab != null)
      {
         OmnityGUI.omnityGUITabs.Remove(tab);
      }
   }

   virtual public void Update()
   {
   }

   /// <summary>
   /// Overload this with the gui layout calls.
   /// </summary>
   abstract public void MyGuiCallback(Omnity anOmnity);

   /// <summary>
   /// Called when the GUI closes
   /// </summary>
   public virtual void OnCloseGUI(Omnity anOmnity) { }

   public void ShowTabNow()
   {
      OmnityGUI.ShowTabNow(tab);
   }

   public string configPath(Omnity anOmnity)
   {
      return OmnityLoader.AddSpecialConfigPath(anOmnity, filename);
   }

   virtual public IEnumerator Load(Omnity anOmnity)
   {
      Unload();
      //  try {
      string path = configPath(anOmnity);
      if (System.IO.File.Exists(path))
      {
         OmnityHelperFunctions.LoadXML(path, ReadXMLDelegate);
      }
      //        } catch (System.Exception e) {
      //          Debug.LogError(e.Message);
      //    }
      OtherLoaders();
      yield return anOmnity.StartCoroutine(PostLoad());
   }

   virtual public void Unload()
   {
   }

   public virtual System.Collections.IEnumerator PostLoad()
   {
      yield return null;
   }

   virtual public void OtherLoaders()
   {
   }

   public virtual void OtherSavers()
   {
   }

   virtual public void Save(Omnity anOmnity)
   {
      if (anOmnity.PluginEnabled(myOmnityPluginsID))
      {
         Debug.Log("Saving " + configPath(anOmnity));
         OmnityHelperFunctions.SaveXML(configPath(anOmnity), (System.Xml.XmlTextWriter xmlWriter) =>
         {
            xmlWriter.WriteStartElement(XMLHeader);
            WriteXMLDelegate(xmlWriter);
            xmlWriter.WriteEndElement();
         });
         OtherSavers();
      }
   }

   public void SaveLoadGUIButtons(Omnity anOmnity)
   {
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Save"))
      {
         Save(anOmnity);
      }
      if (GUILayout.Button("Reload"))
      {
         anOmnity.StartCoroutine(Load(anOmnity));
      }
      GUILayout.EndHorizontal();
   }

   public System.Collections.IEnumerator CoroutineLoader(Omnity anOmnity)
   {
      if (!anOmnity.PluginEnabled(myOmnityPluginsID))
      {
         this.enabled = false;
         yield break;
      }
      else
      {
         this.enabled = true;
      }
      yield return anOmnity.StartCoroutine(Load(anOmnity));
   }

   abstract public void ReadXMLDelegate(System.Xml.XPath.XPathNavigator nav);

   abstract public void WriteXMLDelegate(System.Xml.XmlTextWriter xmlWriter);

   public string filename
   {
      get
      {
         return BaseName + ".xml";
      }
   }

   public string XMLHeader
   {
      get
      {
         return BaseName;
      }
   }

   abstract public string BaseName { get; }

   /// <summary>
   /// Overload this with the name of the tab.
   /// </summary>
   /// <value>The name of my tab.</value>
   public string MyTabName
   {
      get { return BaseName; }
   }
}

public class OmnityFileChooser
{
   public string configXMLFilename2 = "cdp_current.xml";  // file to load
   public string configXMLFilename_Default = "cdp_current.xml";  // default file to load if main file is missing

   public string iniFile = "config.ini";  // ini file saying which to load

   // TCW
   //public string defaultConfigPath_relativeTo_Bundle2 = "./SubDirectory/";
   public string searchFilter = "*.xml";

   public string fileExtension
   {
      get
      {
         return searchFilter.Replace("*", "");
      }
   }

   public string title = "Config";

   private bool fileBoxExpanded = false;
   private System.Collections.Generic.List<string> allConfigs = new System.Collections.Generic.List<string>();

   private void MakeSureDirectoryExists()
   {
      /* TCW
      try
      {
         if (!System.IO.Directory.Exists(defaultConfigPath_relativeTo_Bundle2))
         {
            Debug.Log("creating " + defaultConfigPath_relativeTo_Bundle2);
            System.IO.Directory.CreateDirectory(defaultConfigPath_relativeTo_Bundle2);
         }
         else
         {
            if (DebugLevel.High == Omnity.anOmnity.debugLevel)
            {
               Debug.Log("testing " + defaultConfigPath_relativeTo_Bundle2);
            }
         }
      }
      catch
      {
      }
      */
   }

   public void UpdateAllConfigs()
   {
      try
      {
         MakeSureDirectoryExists();
         // TCW
         allConfigs.Add(configXMLFilename2);

         /*
         string[] potential = System.IO.Directory.GetFiles(defaultConfigPath_relativeTo_Bundle2, searchFilter);
         allConfigs.Clear();
         foreach (string filename in potential)
         {
            allConfigs.Add(System.IO.Path.GetFileName(filename));
         }
         */
      }
      catch
      {
      }
   }

   public void OnGUI(Omnity anOmnity, Omnity.OmnityEventDelegate onSave, Omnity.OmnityEventDelegate onLoad, Omnity.OmnityEventDelegate onChange)
   {
      GUILayout.Label(title);
      GUILayout.FlexibleSpace();
      if (GUILayout.Button(fileBoxExpanded ? "-" : "+"))
      {
         fileBoxExpanded = !fileBoxExpanded;
      }
      string newxml = GUILayout.TextField(configXMLFilename2, GUILayout.Width(300));

      if (newxml != configXMLFilename2)
      {
         configXMLFilename2 = newxml;
         onChange(anOmnity);
      }

      if (GUILayout.Button("Save", GUILayout.Width(75)))
      {
         onSave(anOmnity);
         UpdateAllConfigs();
      }
      if (GUILayout.Button("Load", GUILayout.Width(75)))
      {
         onLoad(anOmnity);
         UpdateAllConfigs();
      }
      OmnityHelperFunctions.BR();
      if (fileBoxExpanded)
      {
         bool ToLoad = false;
         bool? isTHis = null;
         foreach (string type in allConfigs)
         {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(type == configXMLFilename2 ? ("[" + type + "]") : type, GUILayout.Width(300)))
            {
               configXMLFilename2 = type;
               ToLoad = true;
            }

            bool _isTHis = type == configXMLFilename_Default;
            if (GUILayout.Toggle(_isTHis, _isTHis ? "default" : "", GUILayout.Width(150)) != _isTHis)
            {
               _isTHis = !_isTHis;
               isTHis = _isTHis;

               if (_isTHis)
               {
                  configXMLFilename2 = type;
               }
               ToLoad = true;
            }
            OmnityHelperFunctions.BR();
         }

         if (isTHis == true)
         {
            SetCurrentConfig(true, anOmnity);
         }
         else if (isTHis == false)
         {
            SetCurrentConfig(false, anOmnity);
         }

         if (ToLoad)
         {
            onLoad(anOmnity);
         }
      }
   }

   private string AddConfigPath(string file)
   {
      // TCW
      //return System.IO.Path.Combine(defaultConfigPath_relativeTo_Bundle2, file);
      //Debug.Log("AddConfigPath: " + file + " = " + System.IO.Path.Combine(Application.dataPath + "/StreamingAssets", file));
      return System.IO.Path.Combine(Application.dataPath + "/StreamingAssets", file);
   }

   public void SetCurrentConfig(bool p, Omnity anOmnity)
   {
#if LOADSAVE_NOTSUPORTED
#else
      if (OmnityPlatformDefines.LoadSaveSupported())
      {
         try
         {
            if (p)
            {
               configXMLFilename_Default = configXMLFilename2;

               System.IO.File.WriteAllLines(AddConfigPath(iniFile), new string[] { configXMLFilename2 });
            }
            else
            {
               configXMLFilename_Default = null;
               System.IO.File.Delete(AddConfigPath(iniFile));
            }
         }
         catch (System.Exception e)
         {
            Debug.LogError(e.Message);
         }
      }
#endif
   }

   public string configXMLFilename_FullPath
   {
      get
      {
         return AddConfigPath(configXMLFilename2);
      }
   }

   public void TryToUpdateConfigFilenameWithCurrentConfigINI(System.Action oncomplete)
   {
      MakeSureDirectoryExists();

      try
      {
         if (System.IO.File.Exists(AddConfigPath(iniFile)))
         {
            var lines = System.IO.File.ReadAllLines(AddConfigPath(iniFile));
            foreach (var line in lines)
            {
               if (line.Trim().Contains(fileExtension))
               {
                  string filenamePotential = line.Trim();
                  if (System.IO.File.Exists(AddConfigPath(filenamePotential)))
                  {
                     configXMLFilename_Default = configXMLFilename2 = filenamePotential;

                     break;
                  }
               }
            }
         }
      }
      catch
      {
      }

      if (oncomplete != null)
      {
         oncomplete();
      }
   }
}

/// <summary>
/// Enumeration of Plugins that Omnity Knows about.  These plugins are availible a seperate purchace from Elumenati and must be installed at build time.
/// </summary>
public enum OmnityPluginsIDs
{

   // calibration starts at 1000
   PaintMorph = 1000,

   BiclopsBlender = 1001,
   EdgeBlender = 1002,
   GlobeUniformity = 1003,
   SDMCalibration = 1004,
   SDMCalibrationOrthographic = 1005,

   FOVNormalization = 1010,
   OmniSpinner = 1011,

   CustomObjLoader = 1020,

   // UI starts at 2000
   OmniMouse = 2000,

   // Omnity Helpers
   LinkComponents = 3000,

   StereoCombiner = 3001,

   OmniCameraHelper = 3010,

   OmniSpout = 3500,

   // Omniplayer Helpers
   OmniPlayerClipMapper = 4001,

   ClipMapperSDM = 4002,

   // Cobra Helper
   CobraHelper = 5000,

   // Stuff below this doesn't need to show up in the plugin selector

   EasyMultiDisplay = 100000,
   HeartRateMonitor = 100010,
   SphereRenderingExtensions = 100020,

   // app specific extensions
};

public static class OmniPluginPack
{

   static public System.Collections.Generic.List<string> BasicStarterPack
   {
      get
      {
         return new System.Collections.Generic.List<string>() {
                OmnityPluginsIDs.OmniSpinner.ToString(),
                OmnityPluginsIDs.OmniMouse.ToString(),
                OmnityPluginsIDs.OmniCameraHelper .ToString(),
                OmnityPluginsIDs.EasyMultiDisplay .ToString(),
                OmnityPluginsIDs.HeartRateMonitor.ToString(),
            };
      }
   }
}

/// <summary>
/// Helper functions for Unity GUI.
/// </summary>
static internal class OmnityGUIHelper
{

   static internal float subwindowHeightPixels(Rect GUIViewportPositionHint, float GUIViewportScaleHint)
   {
      try
      {
         return Screen.height * GUIViewportPositionHint.height;
      }
      catch
      {
         return Screen.height;
      }
   }

   static internal float subwindowWidthPixels(Rect GUIViewportPositionHint, float GUIViewportScaleHint)
   {
      try
      {
         return Screen.width * GUIViewportPositionHint.width;
      }
      catch
      {
         return Screen.width;
      }
   }

   static internal float subwindowTopPixels(Rect GUIViewportPositionHint, float GUIViewportScaleHint)
   {
      try
      {
         return (1 - GUIViewportPositionHint.height - GUIViewportPositionHint.yMin) * Screen.height;
      }
      catch
      {
         return 0;
      }
   }

   static internal float subwindowLeftPixels(Rect GUIViewportPositionHint, float GUIViewportScaleHint)
   {
      try
      {
         return (GUIViewportPositionHint.xMin) * Screen.width;
      }
      catch
      {
         return 0;
      }
   }

   static internal Matrix4x4 GUISCALEMATRIX(Rect GUIViewportPositionHint, float GUIViewportScaleHint)
   {
      Matrix4x4 matResized = Matrix4x4.identity;
      if (GUIViewportScaleHint == 0)
      {
         matResized[0, 0] = matResized[1, 1] = .001f;
      }
      else
      {
         matResized[0, 0] = GUIViewportScaleHint;
         matResized[1, 1] = GUIViewportScaleHint;
      }

      if (Screen.width > 0 && Screen.height > 0)
      {
         matResized[0, 3] = GUIViewportPositionHint.x * Screen.width;
         matResized[1, 3] = (1 - GUIViewportPositionHint.y - GUIViewportPositionHint.height) * Screen.height;
      }
      return matResized;
   }
}

static internal partial class OmnityHelperFunctionsGUI
{

   static public void ScaleGUI(this OmnityWindowInfo owi, System.Action a)
   {
      Matrix4x4 matNormal = GUI.matrix; //push matrix
      GUI.matrix = owi.GUISCALEMATRIX;
      a();
      GUI.matrix = matNormal; // POP MATRIX
   }

   static public void ResetGUIScale(System.Action a)
   {
      Matrix4x4 matNormal = GUI.matrix; //push matrix
      GUI.matrix = Matrix4x4.identity;
      a();
      GUI.matrix = matNormal; // POP MATRIX
   }

   static public void ScaleGUI(this FinalPassCamera fpc, System.Action a)
   {
      Matrix4x4 matNormal = GUI.matrix; //push matrix
      GUI.matrix = fpc.GUISCALEMATRIX;
      a();
      GUI.matrix = matNormal; // POP MATRIX
   }

   static public void HorizontalLayout(this GUIStyle s, System.Action a, params GUILayoutOption[] options)
   {
      GUILayout.BeginHorizontal(s, options);
      a();
      GUILayout.EndHorizontal();
   }

   static public void VerticalLayout(this GUIStyle s, System.Action a, params GUILayoutOption[] options)
   {
      GUILayout.BeginVertical(s, options);
      a();
      GUILayout.EndVertical();
   }
}

/// <summary>
/// Class PriorityEventHandler helps omnity load plugins in a a desired order.
/// </summary>
public class PriorityEventHandler
{

   /// <summary>
   /// ctor
   /// </summary>
   public PriorityEventHandler()
   {
      AddLoaderFunction(Ordering.Order_Default, CallDefaultEvents);
   }

   ~PriorityEventHandler()
   {
      RemoveLoaderFunction(Ordering.Order_Default, CallDefaultEvents);
   }

   /// <summary>
   /// Calls the default loading events as specified by the Omnity.onReloadEndCallback handler.
   /// </summary>
   private IEnumerator CallDefaultEvents(Omnity o)
   {
      OmnityHelperFunctions.CallDelegate(o, Omnity.onReloadEndCallback);
      yield return null;
   }

   /// <summary>
   /// The is a priority queue for sorting the order of events that happen on a reload.  Specifically the order in which things like file loading must happen.
   /// </summary>
   public System.Collections.Generic.SortedDictionary<int, System.Collections.Generic.List<System.Func<Omnity, IEnumerator>>> eventHandlerPriorityQueue = new System.Collections.Generic.SortedDictionary<int, System.Collections.Generic.List<System.Func<Omnity, IEnumerator>>>();

   /// <summary>
   /// Register a event in terms of its priority and loader function callback.  The loader functions are coroutines so they will happen completely before moving to the next
   /// </summary>
   public void AddLoaderFunction(int priority, System.Func<Omnity, System.Collections.IEnumerator> loader)
   {
      if (!eventHandlerPriorityQueue.ContainsKey(priority))
      {
         eventHandlerPriorityQueue.Add(priority, new System.Collections.Generic.List<System.Func<Omnity, IEnumerator>>());
      }
      eventHandlerPriorityQueue[priority].Add(loader);
   }

   public void RemoveLoaderFunction(int priority, System.Func<Omnity, System.Collections.IEnumerator> loader)
   {
      if (eventHandlerPriorityQueue.ContainsKey(priority))
      {
         eventHandlerPriorityQueue[priority].Remove(loader);
      }
   }

   /// <summary>
   /// Calls all loader function callbacks.  Since the loader functions are coroutines so they will happen completely before moving to the next
   /// </summary>
   public IEnumerator CoroutineCallPriorityEventHandler(Omnity anOmnity)
   {
      foreach (var kvp in eventHandlerPriorityQueue)
      {
         foreach (var loader in kvp.Value)
         {
            yield return anOmnity.StartCoroutine(loader(anOmnity));
         }
      }
      if (Omnity.garbageCollectAfterLoad)
      {
         OmnityHelperFunctions.DoGarbageCollectNow();
      }
   }

   /// <summary>
   /// list of default orderings
   /// </summary>
   static public class Ordering
   {
      public const int Order_OmniInit = 10;
      public const int StereoCombinerManager = 400;

      public const int Order_Cobra = 450;

      public const int SDMManager = 500;
      public const int SDMManagerOrthographic = 501;
      public const int SDMClipMapper = 510;

      public const int Order_OmniplayerClipmapper = 600;

      public const int Order_OmnilinkClipMapper = 601;

      public const int Order_FOVNormalization = 700;
      public const int Order_GlobeUniformity = 701;
      public const int Order_OmniSpinner = 702;

      public const int Order_PaintMorphManager = 1500;
      public const int Order_BiclopsBlender = 1501;
      public const int Order_Edgeblender = 1502;

      public const int Order_GUIPositioner = 1800;
      public const int Order_OmniMouse = 1900;
      public const int Order_OmniCameraHelper = 1901;

      public const int Order_OmniSpout = 1910;

      public const int Order_Default = 2000;
   }
}

static public partial class OmnityRendererExtensions
{

   static public void DisableShadowsAndLightProbes(Renderer renderer)
   {
      OmnityPlatformDefines.DisableShadowsAndLightProbes(renderer);
   }
}

/// <summary>
/// Helper functions for some math functions
/// </summary>
internal static class VecMathFunctions
{

   //Get the intersection between a line and a plane.
   //If the line and plane are not parallel, the function outputs true, otherwise false.
   public static bool LinePlaneIntersection(out Vector3 intersection, Vector3 linePoint, Vector3 lineVec, Vector3 planeNormal, Vector3 planePoint)
   {
      float length;
      float dotNumerator;
      float dotDenominator;
      Vector3 vector;
      intersection = Vector3.zero;

      //calculate the distance between the linePoint and the line-plane intersection point
      dotNumerator = Vector3.Dot((planePoint - linePoint), planeNormal);
      dotDenominator = Vector3.Dot(lineVec, planeNormal);

      //line and plane are not parallel
      if (dotDenominator != 0.0f)
      {
         length = dotNumerator / dotDenominator;

         //create a vector from the linePoint to the intersection point
         vector = lineVec.normalized * length;

         //get the coordinates of the line-plane intersection point
         intersection = linePoint + vector;

         return true;
      }
      else
      {
         return false; //output not valid
      }
   }
}

internal static class ModelLoaderClass
{
   static public System.Func<string, bool, Mesh> modelLoaderDelegate = null;

   static public Mesh LoadModel(string name, bool flipnormals)
   {
      try
      {
         if (modelLoaderDelegate == null)
         {
            Debug.LogError("Error ModelLoaderClass.modelLoaderDelegate is null, the program author must set this varible to a function callback that takes as input a string, for example a filename, and returns a mesh, for example a loaded mesh from the obj file.");
            return null;
         }
         else
         {
            return modelLoaderDelegate(name, flipnormals);
         }
      }
      catch (System.Exception e)
      {
         Debug.LogError("Error in ModelLoaderClass.modelLoaderDelegate " + e.Message);
         return null;
      }
   }
}

static public class OmniJoystick
{

   public enum JoystickTypes
   {
      LogitechF710 = 0,
      XBOX360 = 1,
      PS4 = 2,

      DisableJoystick = 100
   }

   private const JoystickTypes defaultType = JoystickTypes.PS4;
   static private JoystickTypes? _type = null;

   static public JoystickTypes type
   {
      get
      {
         if (_type == null)
         {
            string s = PlayerPrefs.GetString("OmniJoystick.type", defaultType.ToString());

            try
            {
               type = (JoystickTypes)System.Enum.Parse(typeof(JoystickTypes), s, true);
            }
            catch
            {
               type = defaultType;
            }
         }
         return _type.GetValueOrDefault();
      }
      set
      {
         if (_type != value)
         {
            _type = value;
            PlayerPrefs.SetString("OmniJoystick.type", _type.GetValueOrDefault().ToString());
         }
      }
   }

   static public string Name
   {
      get
      {
         switch (type)
         {
            case JoystickTypes.LogitechF710:
               return ("LogitechF710");

            case JoystickTypes.XBOX360:
               return ("XBOX360");

            case JoystickTypes.PS4:
               return ("PS4");

            case JoystickTypes.DisableJoystick:
               return ("No Joystick");

            default:
               Debug.LogError("Unknown joystick type");
               return ("Unknown joystick type");
         }
      }
   }

   static public void OnGUI()
   {
      type = OmnityHelperFunctions.EnumInputReset<JoystickTypes>("JoystickTypes", "JoystickType", type, defaultType, 1);
   }

   static public bool ShoulderLeftDown()
   {
      return Input.GetKeyDown("joystick button 4");
   }

   static public bool ShoulderRightDown()
   {
      return Input.GetKeyDown("joystick button 5");
   }

   static public bool Button1
   {
      get
      {
         return Input.GetKey("joystick button 1");
      }
   }

   static public bool Button2
   {
      get
      {
         return Input.GetKey("joystick button 2");
      }
   }

   static public bool Button3
   {
      get
      {
         return Input.GetKey("joystick button 3");
      }
   }

   static public string Button1str
   {
      get
      {
         switch (type)
         {
            case JoystickTypes.LogitechF710:
               return ("joystick button 1");

            case JoystickTypes.XBOX360:
               return ("joystick button 1");

            case JoystickTypes.PS4:
               return ("joystick button X");

            case JoystickTypes.DisableJoystick:
               return ("joystick button 1");

            default:
               Debug.LogError("Unknown joystick type");
               return ("joystick button 1");
         }
      }
   }

   static public string Button2str
   {
      get
      {
         switch (type)
         {
            case JoystickTypes.LogitechF710:
               return ("joystick button 2");

            case JoystickTypes.XBOX360:
               return ("joystick button 2");

            case JoystickTypes.PS4:
               return ("joystick button O");

            case JoystickTypes.DisableJoystick:
               return ("joystick button 2");

            default:
               Debug.LogError("Unknown joystick type");
               return ("joystick button 2");
         }
      }
   }

   static public string Button3str
   {
      get
      {
         switch (type)
         {
            case JoystickTypes.LogitechF710:
               return ("joystick button 3");

            case JoystickTypes.XBOX360:
               return ("joystick button 3");

            case JoystickTypes.PS4:
               return ("joystick button /\\");

            case JoystickTypes.DisableJoystick:
               return ("joystick button 3");

            default:
               Debug.LogError("Unknown joystick type");
               return ("joystick button 3");
         }
      }
   }

   private const string JoyAll0 = "JoyAll0";// LEFT ANALOG x
   private const string JoyAll1 = "JoyAll1";  // LEFT ANALOG Y
   private const string JoyAll2 = "JoyAll2";
   private const string JoyAll3 = "JoyAll3"; // RIGHT ANALOG X
   private const string JoyAll4 = "JoyAll4"; // RIGHT ANALOG Y
   private const string JoyAll5 = "JoyAll5"; // DIGITAL PAD X
   private const string JoyAll6 = "JoyAll6"; // DIGITAL PAD Y
   private const string JoyAll7 = "JoyAll7";   //UNKNOWN
   private const string JoyAll8 = "JoyAll8";   // LEFT ANALOG TRIGGER BOTTOM
   private const string JoyAll9 = "JoyAll9";   // Right ANALOG TRIGGER BOTTOM

   static public float VerticalMain
   {
      get
      {
         switch (type)
         {
            case JoystickTypes.LogitechF710:
               return -Input.GetAxis(JoyAll1);

            case JoystickTypes.XBOX360:
               return -Input.GetAxis(JoyAll1);

            case JoystickTypes.PS4:
               return Input.GetAxis(JoyAll2);

            case JoystickTypes.DisableJoystick:
               return 0;

            default:
               Debug.LogError("Unknown joystick type");
               return -Input.GetAxis(JoyAll1);
         }
      }
   }

   static public float HorizontallMain
   {
      get
      {
         return Input.GetAxis(JoyAll0);
      }
   }
}

abstract public class OmnityAutoAddPlugin : MonoBehaviour
{
   abstract public OmnityPluginsIDs myOmnityPluginsID { get; }

   static public T GetSingleton<T>(ref T _singleton, GameObject go) where T : OmnityAutoAddPlugin
   {
      if (_singleton == null)
      {
         // Debug.Log("O " + o.name + " VS "+typeof(T).ToString());
         // Debug.Log("FINDING " + typeof(T).ToString());

         System.Type typet = typeof(T);

         UnityEngine.Object o = OmnityPlatformDefines.FindObjectOfType(typet);
         _singleton = (T)o;
         if (_singleton == null)
         {
            _singleton = go.AddComponent<T>();
         }
      }
      return _singleton;
   }
}

abstract public class OmnityAutoInitPlugin
{
   abstract public OmnityPluginsIDs myOmnityPluginsID { get; }
   // your function must have
   // static public void OmnityInit();  // unfortunately we can not enforce this using inhertience because it is imposible to inherit static functions
}

static public class OmnityPluginManager
{

   static public void ApplyPlugins(GameObject gameObject, bool forceConnect = false)
   {
      bool haveWeFoundAtLeastOnePlugin = false;
      var types = System.Reflection.Assembly.GetExecutingAssembly().GetTypes();
      System.Collections.Generic.List<string> requirements = new System.Collections.Generic.List<string>();
      foreach (var type in types)
      {
         if (type.IsAbstract)
         {
            continue;
         }
         if (type.IsSubclassOf(typeof(OmnityTabbedFeature)) ||
             type.IsSubclassOf(typeof(OmnityAutoAddPlugin)) ||
             type.IsSubclassOf(typeof(OmnityAutoInitPlugin)))
         {
            var properties = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            bool foundID = false;
            foreach (var property in properties)
            {
               var propertyName = property.ToString();
               if (propertyName.Contains(" _myOmnityPluginsID"))
               { // this used to be type.ToString() + " myOmnityPluginsID" but it doesn't work in certian situations
                  foundID = true;
                  OmnityPluginsIDs omniPluginId = (OmnityPluginsIDs)property.GetValue(null);
                  if (OmniDrivers.IncompatibleDrivers(new System.Collections.Generic.List<string> { omniPluginId.ToString() }))
                  {
                     if (forceConnect && Omnity.anOmnity.PluginEnabled(omniPluginId))
                     {
                        requirements.Add(omniPluginId.ToString());
                     }
                     continue;
                  }
                  else
                  {
                     haveWeFoundAtLeastOnePlugin = true;
                     var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                     foreach (var method in methods)
                     {
                        var methodName = method.ToString();
                        if (methodName.Contains(type.ToString() + " OmniCreateSingleton(UnityEngine.GameObject)"))
                        {
                           //                        Debug.Log(method.ToString());
                           MonoBehaviour mo = (MonoBehaviour)method.Invoke(null, new object[] { gameObject });
                           if (mo != null)
                           {
                              if (type.ToString().Contains("HeartRateMonitor"))
                              {
                                 mo.enabled = true;
                              }
                              else
                              {
                                 mo.enabled = false;
                              }
                           }
                        }
                        else if (type.IsSubclassOf(typeof(OmnityAutoInitPlugin)))
                        {
                           if (methodName.Contains("OmnityInit()"))
                           {
                              method.Invoke(null, new object[] { });
                           }
                        }
                     }
                  }
               }
            }
            if (!foundID)
            {
               Debug.LogWarning("Didn't find \"public static OmnityPluginsID _myOmnityPluginsID\" in plugin " + type.Name);
            }
         }
      }
      OmniDrivers.ResetDrivers();
      if ((!haveWeFoundAtLeastOnePlugin) || requirements.Count > 0)
      {
         if (Omnity.anOmnity != null && Omnity.anOmnity.myOmnityGUI != null)
         {
            Omnity.anOmnity.myOmnityGUI.GUIEnabled = false;
         }
         requirements.AddRange(OmniPluginPack.BasicStarterPack);
         OmniDrivers.CheckDriversAsynchronous(requirements, () => { }, () => { }, () => { });
      }
   }
}