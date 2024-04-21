# Azure Kinect Foot Tracking Unity Application (for rhythm games like Outfox, Stepmania, DDR,...)
This project is based on the Microsoft Sample Unity Body Tracking Application
for info this sample is available here : https://github.com/microsoft/Azure-Kinect-Samples/tree/master/body-tracking-samples/sample_unity_bodytracking

One font used for that project : Assets\Fonts\LuckiestGuy-Regular
This Font is under Apache 2 license : https://fonts.google.com/specimen/Luckiest+Guy/about

Most of the steps below are part of the README from the "Microsoft Sample Unity Body Tracking Application"

### Directions for getting started:

#### 1) First get the nuget packages of azure kinect libraries:

Open the project with Unity 2019.1.2f1 (necessary to get correctly nuget package) .
Open the Visual Studio Solution associated with this project.
If there is no Visual Studio Solution yet you can make one by opening the Unity Editor
and selecting one of the csharp files in the project and opening it for editing.
You may also need to set the preferences->External Tools to Visual Studio

In Visual Studio:
Select Tools->NuGet Package Manager-> Package Manager Console

On the command line of the console at type the following command:

Update-Package -reinstall

The latest libraries will be put in the Packages folder under sample_unity_bodytracking

#### 2) Next add these libraries to the Assets/Plugins folder and sample_unity_bodytracking project root directory:

Just **run the batch file MoveLibraryFile.bat** in the sample_unity_bodytracking directory

#### 3) Copy System.Windows.Forms.dll

Copy the file System.Windows.Forms.dll located in the "FileToCopyInAssetsPlugins" directory to the folder "Assets/Plugins"

#### 4) Close the Unity project and reopen it with Unity 2023.2.3f1 now

Now that all packages for Azure Kinect have been correctly imported you can reimport the project with Unity 2023
Click continue when asked for reimporting (different unity version)...

#### 5) Open the Unity Project and under Scenes/  select the AzureKinectFootTrackingScene

You can now build and export the project




