Transform HUD Editor

This custom editor shows an additional Transform Inspector (position, rotation, scale) directly in the Scene View right next to the selected object. This allows you to conveniently edit an object’s position, rotation, and scale directly in the Scene View.


INSTALLATION

1. In your project, in the root Assets folder, use or create a new folder named Editor. 

2. Place the TransformHUDEditor.cs script within the Editor folder. So the file structure should look like this:

Assets
   Editor
     TransformHUDEditor.cs



INSPECTOR SETTINGS

The custom inspector shows in the Transform Inspector where the position, rotation, and scale are displayed.


Show/Hide HUD
Press the button to show or hide the HUD in the Scene View.

Show/Hide Options
Press the button to show or hide the options below.

Box Offset
The offset of the HUD box in pixels from the origin of the selected object in the Scene View.

Box Color
The color and transparency of the HUD box in the Scene View. Adjust the transparency by changing the Alpha value of the color.