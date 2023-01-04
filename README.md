
# Kawasaki-Rockwell RS007N/MagMo Robotics Demo - ROS Container Repo
 

<p align="center"><img src="img/SimTrayToRail.gif" alt-"Robot sorting small boxes from tray to track" width="800"/></p>




This is a Unity3D app that simulates the Kawasaki RS007N Robot and Rockwell MagnaMotion track table demo for the HoloLens, that is featured in the Microsoft Build 2022 Keynote Demo (at the end of Satya Nadella's portion).

A link to the video (1:31): (https://www.youtube.com/watch?v=kFIM6vPLO9Y) - the HoloLens portion is from 1:00.

Note much of this code derived from the Unity Robotics Hub Pick-and-Place tutorial.

# Architecture
The overall demo logical architecture is as follows:

<p align="center"><img src="img/DemoArch01.png" alt-"Logical Architecture of demo" width="800"/></p>

## Components 
The following components and their repos can be found here:

### Consumers
- Physical Robot and Table - the complete robot with the MagnaMotion table only exists in its complete form in Microsofts Houston Lab (ex-Marsden)
- Pick-and-place Playback Tool - a Windows 11 App, code can be found in following repo:()
- KhiSimDemo App - (this app) A Unity3D App for Windows, code can be found in following repo:()

### Middleware
- ROS Container - up-to-date code can be found in tihs repo:()
- Cloud version - and older version of the code with instructions for installing in various places (on an Azure VM, as an ACI) can be found here:()

### Consumers
- Hololens App - Robotics Metaverse Demo, app can be found in Windows Store, code can be found in following repo:()
- KhiSimDemo App - (this app) A Unity3D App for Windows, code can be found in following repo:()




      
# Usage Notes
The application has different modes:
  - Simulation mode in which it can run in 
       - moving boxes from rail to rail
       - moving boxes from tray to rail
       - moving boxes from rail to tray
  - Echo mode
       - echos the box and robot motions that are occuring somewhere else, either on a physical robot or on a virtual one (for example another instance of this appoication)


## Compiling the Unity Project
- The Unity Project root is in `KhiPickAndPlaceProject` Subdirectory in this repo, that is where you have to point Unity Hub
- The scene you should use for building is `Assets/Scenes/build1.unity`
- The packages needed should install themselves, I beleive - not sure about this
- One small change needs to be made after the UnityRoboticsHub package is installed to make this compile:
   - the `KhiDemoSIm\Library\PackageCache\com.unity.robotics.ros-tcp-connector@c27f00c6cf\Runtime\TcpConnector\ROSConnection.cs` file has a method (`InitializeHUD`) that needs to be made public like this on line 1016:
   - `public void InitializeHUD()` 
   - I will see if I can find another workaround for it later
   
   
## Unity Robotics Hub ROS Messages
- The Unity Robotics Hub provided a ROS message generation utility that needs to be configured
- Configuration settings are under the menu selection "Robotics/Generate ROS Message" - this brings up a dialog box.
  - ROS Message Path: `d:\ros\KhiDemoRos1\ROS\src\rs007_control`
  - Built Message Path: `RosMessages`

## Building an exe
- We want this app to run in its own window, the setting for this is kind of buried deep in Unity
- Go the the dialog box opened with the menu command `Edit/Build Settings`
    - Press the `Player Settings` button in the lower left of that window
    - Select the `Player` settings from the list of about 20 settings on the left 
    - Expand the `Resolution and Presentation` tab that you can find on the right of the same window then
    - Select "Windowed" from the `Fullscreen Mode` setting
- We build in the "Build" subdirectory (which is standard in Unity and excluded in their usual .gitignore).
- We build to the name `KhiDemoSim.exe`
   
# Connecting to ROS
- While using the Unity editor I prefer to edit the ROS and ZMQ server fields in the Magmo properties dialog. These will be used if there are no overriding command line parameters.
- To use to a local docker container use the server "localhost" or something eqiivelent (127.0.0.1)
- 


### Keyboard Commands:

   - Ctrl-E Echo Mode
   - Ctrl-P Publish Mode
   - Ctrl-L RailToRail Mode
   - Ctrl-T TrayToRail Mode
   - Ctrl-R Reverse TrayRail
            
   - Ctrl-F Speed up
   - Ctrl-S Slow down
            
   - Ctrl-N Toggle Enclosure
   - Ctrl-D Toggle Stop Simulation
   - Ctrl-G Toggle Log Screen
            
   - Ctrl-V Ctrl-F View from Front
   - Ctrl-V Ctrl-B View from Back
   - Ctrl-V Ctrl-T View from Top
   - Ctrl-V Ctrl-S View from Top (rotated)
   - Ctrl-V Ctrl-R View from Right
   - Ctrl-V Ctrl-L View from Left
            
   - Ctrl-H Toggle Help Screen
   - Ctrl-Q Ctrl-Q Quit Application

### Parameters:
   --roshost localhost
   --zmqhost localhost
   --rosport 10004
   --zmqport 10006
   --mode echo
   --mode rail2rail
   --mode rail2rail
   --mode tray2rail
   --mode rail2tray

### Addresses as of 15 July 2022:
   Western Europe - 20.234.234.190
   USA -  20.225.161.122

 
 
 # Start two instances with one echoing the other
   - Make sure you have a ROS container running and know what port it is using for the ROS-Unity communication (below example localhost:10005)
   
   - Open a cmd window
   - Enter: `khidemosim --mode tray2rail --zmqhost localhost --zmqport 10006`
   - (note: If you are using a local host you should see output echoing the statusrunning in the container window)
   - Open a second cmd window
   - Enter: `khidemosim --mode echo --roshost localhost --rosport 10005`
   - The upper left communication HUD IP window should be showing green communication arrows

## Make a package out of this
 - Exported package with dependncies
   - Menu Assets/Export Package
     - MagneMotion
     - Scripts
     - Environment
     - Check ROS Messages
   
 - created new test project (KhiDemoSimPackageTest1)
 - imported package
 - added robotics urdf urls"https://github.com/Unity-Technologies/URDF-Importer.git?path=/com.unity.robotics.urdf-importer#v0.5.2"
 - added robotics urdf urls "https://github.com/Unity-Technologies/ROS-TCP-Connector.git?path=/com.unity.robotics.ros-tcp-connector#v0.7.0"
 - restarted to get Robotics entry in menu
 - added reference to ROS pointing to RS007 and built msgs and srv
 - added reference to ROS pointing to moveit and built msgs, srv, actions (everything)
 - manuall added "com.unity.nuget.newtonsoft-json": "2.0.0" to packages/manifist.json
 - manuall added nuget (https://github.com/GlitchEnzo/NuGetForUnity)
    - downloaded
    - made sure only one instance of Unity running
    - clicked on .unitypackage
    - restarted to get NuGet entry in menu


## To do 2023-01-04
- Document how OvConnector works
-- what it is derived from (the old sample)
-- how to install it (i.e. from a native directory)
-- how to test it (need a clean OV capable computer for this?)
- Make creation of JsonState files optional (add a switch - actually two switches for both modes)

- See how it differs from newer connector sample and maybe port to that
- See if we can install zmq from Python instructions on this https://docs.omniverse.nvidia.com/kit/docs/kit-manual/latest/api/pxr_index.html

