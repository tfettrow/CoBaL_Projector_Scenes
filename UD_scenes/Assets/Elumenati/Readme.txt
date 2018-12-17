This folder is the parts needed to use and deploy the Omnity dome projection code. The orignal .CS files have been modified to support
loading the xml files and etc from the StreamingAssessts folder, which simplifies deployment somewhat.

The main bit of code (and probably all you will need) is the CameraContainer.cs file. This file, when added to a "Camera Container" or "Player Stick"
will automaticaly attach the Omnity projection code and set up the needed bits. This can be controlled programmaticaly if desired,
and the dome projection can be toggled off (removed) if needed. Optionally, you can directly drag the Omnity.cs file into your camera and just run with that,
but you will have no direct control over turning the projection on or off or changing the tilt etc.