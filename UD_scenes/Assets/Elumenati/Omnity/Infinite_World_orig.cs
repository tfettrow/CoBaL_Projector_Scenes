using UnityEngine;
using System.Collections;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

// Options
// Depth of wall
// Walls Present (TopBottom and LeftRight)
// Length of Walls (TopBottom and LeftRight)
// 
public class Infinite_World_orig: MonoBehaviour{
	// // // // // // // INITIALIZE VARIABLES // // // // // // // // // // // // // // // // // // // // // // // // // // // // // // // // // // //
	//public Transform OmnityDomeTransform;
	// Number of Cubes in Scene
	private int num_Objects = 500;
	// for plane ground
    public Renderer rend;

    private Vector2 TextureOffset;
	private Vector2 TextureOffset_left;
	private Vector2 TextureOffset_right;

    // Should hard code this once determine appropriate values...
    private float Distance = 125f;
    private float LeftRightSideLength = 10f;
	private float TopWallLength = 4f;
    private float DepthofWall = 15f;
	private float HalfTopWall;
    private float HalfSideWall;
	// Receiving Thread
	Thread receiveThread;
	UdpClient client;
	// Define all game objects
	public GameObject SquarePrefab;
	public GameObject CameraObject;
	public GameObject OrigObject;
	public GameObject SceneObject;
	// Variables being read from UDP
	private float num_of_UDP_vals;
	private float camera_translation_x_labview;
	private float camera_translation_y_labview;
	private float camera_translation_z_labview;
	private float falling_rotation_y_labview;
	private float ground_translation_y_labview;
	private float camera_translation_x_unity;
	private float camera_translation_y_unity;
	private float camera_translation_z_unity;
	private float falling_rotation_z_unity;
	private float ground_translation_z_unity;
	private float falling_position_z;
	private float falling_position_x;
	// Initialize udp variables
	public string text = ""; //received text
	public string IP = "192.168.253.2";
	public int port; // Defined on "init 
	public string lastReceivedUDPPacket = "";
	// Variables used for updating object positions in unity environment
	Vector3 CameraPosition;
	Vector3 FallingRotation;
	Vector3 OrigObjectPosition;
	Vector3 this_pyramid_position;
	Vector3 DomePosition;
	Vector3 ChildObjectPlacement;
	/// //////////////////////////////////////////////////////////////////////////////////////////////	///
	//public float dt;
	/// 


    void Awake() {
        // Find the game ojbects
        OrigObject = GameObject.Find("OrigCube");
		ChildObjectPlacement = GameObject.Find ("Object Placement Origin").transform.position;
		SceneObject = GameObject.Find ("SceneOrigin");
    }

    public void Start(){
	 	////////// ******* Why doesn't this get assigned here??? ///////////////////////////
        // Find the Camera within omnity script (takes a while to load so may not grab it here)
		CameraObject = GameObject.Find ("Render Channel Cameras");
		////////////////////////////////////////////////////////////////////////////////////


		// // // // // INSTANTIATE SCENE OBJECTS // // // // // // // // // // // // // // // // // // // // //// // // // // // // // // // // // // //// // // // // // // // // // // // // // 
        HalfSideWall = .5f * LeftRightSideLength;
		HalfTopWall = .5f * TopWallLength;
            // Right Wall
			for (int i = 0; i < num_Objects / 2; i++){
				this_pyramid_position.x = (ChildObjectPlacement.x + UnityEngine.Random.Range(HalfTopWall, HalfTopWall + DepthofWall));
				this_pyramid_position.y = 2*Math.Abs(UnityEngine.Random.Range(-HalfSideWall, HalfSideWall));
				this_pyramid_position.z = (UnityEngine.Random.Range(ChildObjectPlacement.z, Distance));
				
			GameObject clone = Instantiate (OrigObject, this_pyramid_position, transform.rotation * Quaternion.Euler (UnityEngine.Random.Range (0f, 360f), UnityEngine.Random.Range (0f, 360f), UnityEngine.Random.Range (0f, 360f))) as GameObject;
				
            }
            // Left Wall
			for (int i = num_Objects / 2; i < num_Objects; i++){
				this_pyramid_position.x = (ChildObjectPlacement.x - UnityEngine.Random.Range(HalfTopWall, HalfTopWall + DepthofWall)); 
                this_pyramid_position.y = 2*Math.Abs(UnityEngine.Random.Range(-HalfSideWall, HalfSideWall));
				this_pyramid_position.z = (UnityEngine.Random.Range(ChildObjectPlacement.z, Distance));
				
					GameObject clone = Instantiate(SquarePrefab, this_pyramid_position, transform.rotation * Quaternion.Euler(UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f))) as GameObject;
				
            }
		///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


		// // // // // // // // UDP SETUP // // // // // // // //// // // // // // // //// // // // // // // //
            port = 6843;
            receiveThread = new Thread(
            new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();
		///////////////////////////////////////////////////////////////////////////////////////////////////////// 
    }

	// // // // // // // // RECEIVE DATA FUNCTION for UDP SETUP // // // // // // // //// // // // // // // //
	private void ReceiveData(){
		client = new UdpClient(port);
		while (true){
			try{
				IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
				byte[] data = client.Receive(ref anyIP);			
				text = Encoding.UTF8.GetString(data);				
				lastReceivedUDPPacket = text;
			}
			catch (Exception err){
				print(err.ToString());
			}
		}
	}
	/// /////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void Update(){
		// // // // // // // // GRAB OMNITY OBJECTS // // // // // // // //// // // // // // // //// // // // // // // //
        // Figure out why this isn't grabbing during Start!!!
        // Only runs if CameraObject is empty
        if (CameraObject == null){
            CameraObject = GameObject.Find("Render Channel Cameras");
        }
		//if (OmnityDomeTransform == null) {
	//		OmnityDomeTransform = GameObject.Find("Dome Position").transform;
	//	}
		///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		// // // // // // // // READ UDP STRING // // // // // // // //// // // // // // // //// // // // // // // //// // 
		// reads string - splits the characters based on "," - assigns the characters to the variables based on the order within the string
		char[] delimiter1 = new char[] { ',' };
		var strvalues = lastReceivedUDPPacket.Split (delimiter1, StringSplitOptions.None);
		
		foreach (string word in strvalues) {
			num_of_UDP_vals++;
			if (num_of_UDP_vals == 1) {
				float.TryParse (word, out camera_translation_x_labview);
			}
			if (num_of_UDP_vals == 2) {
				float.TryParse (word, out camera_translation_y_labview);
			}
			if (num_of_UDP_vals == 3) {
				float.TryParse (word, out camera_translation_z_labview);
			}
			if (num_of_UDP_vals == 4) {
				float.TryParse (word, out ground_translation_y_labview);
			}
			if (num_of_UDP_vals == 5) {
				float.TryParse (word, out falling_rotation_y_labview);
			}
		}
		num_of_UDP_vals = 0;
		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		// // // // // // // // TRANSFORM COORDINATE FRAMES // // // // // // // //// // // // // // // //// // // // // // // //// // // // // // // //
		camera_translation_x_unity = camera_translation_x_labview;
		camera_translation_y_unity = camera_translation_z_labview;
		camera_translation_z_unity = camera_translation_y_labview;
		ground_translation_z_unity = ground_translation_y_labview;
		falling_rotation_z_unity = falling_rotation_y_labview;
		///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		// // // // // // MOVE THE SCENE OBJECTS // // // // // // // //// // // // // // // //// // // // // // // //// // // // // // // //
		// Moves the original/ parent object, thus moving all children objects in the scene assigned to the parent object
		OrigObjectPosition.z = - ground_translation_z_unity - 50f; // 50 = starting position Parent Object in scene
		OrigObject.transform.position = OrigObjectPosition;
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		// // // // // // Move THE SCENE GROUND // // // // // // // //// // // // // // // //// // // // // // // //// // // // // // // 
		// Move the texture with speed of treadmill
        TextureOffset[1] = - ground_translation_z_unity/4f; // offset update based on scale of plane being used... find a way to automate..
		TextureOffset_left[1] = -ground_translation_z_unity/2f;
		TextureOffset_right[1] = -ground_translation_z_unity/2f;
        rend.material.SetTextureOffset("_MainTex", TextureOffset); // Get rid of New Vector...

		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		// // // // // // MOVE PERSPECTIVE CAMERA // // // // // // // // // // // // // // // // // // // // // // // // // // // // 
        // Moves the "Render Chanel Camera" based on real world coordinates gathered from Nexus(motion capture)
		CameraPosition.x = camera_translation_x_unity;
		CameraPosition.y = camera_translation_y_unity - .03f;
		CameraPosition.z = camera_translation_z_unity; 
		CameraObject.transform.position = CameraPosition;
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		// // // // // // SET DOME/OMNITY POSITION // // // // // // // // // // // // // // // // // // // // // // // // // // // // // //  
		// the values are a manually measure distance from the world/nexus/forceplate origin to the middle of the screen/dome 
		// DomePosition.y = 1.8f;
		// DomePosition.z = 2f;
		// OmnityDomeTransform.position = DomePosition;
		/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		// // // // // // ROTATE THE WORLD // // // // // // // // // // // // // // // // // // // // // // // // // // // // // // // // 
		// rotate the world around SceneOrigin to allow for a proper perception of a fall
		FallingRotation.z = falling_rotation_z_unity;
		SceneObject.transform.localEulerAngles = FallingRotation;
		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		///  
		//dt = Time.deltaTime;
		/// 
	}

	// Necessary in order to properly close udp connection
	void OnDisable(){
		if (receiveThread != null)
			receiveThread.Abort();
		client.Close();
	}
}
