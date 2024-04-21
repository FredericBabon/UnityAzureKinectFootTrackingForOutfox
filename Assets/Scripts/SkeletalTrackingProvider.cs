using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkeletalTrackingProvider : BackgroundDataProvider
{
    const uint K4ABT_BODY_INDEX_MAP_BACKGROUND = 255;




    bool readFirstFrame = false;
    TimeSpan initialTimestamp;

    TextMeshProUGUI statusText;

    public SkeletalTrackingProvider(int id, ref TextMeshProUGUI paramStatusText) : base(id) 
    {
        Debug.Log("in the skeleton provider constructor");
        statusText = paramStatusText;
    }

    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter binaryFormatter { get; set; } = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

    public Stream RawDataLoggingFile = null;

    protected override void RunBackgroundThreadAsync(int id, CancellationToken token)
    {
        try
        {
            UnityEngine.Debug.Log("Starting body tracker background thread.");

            // Buffer allocations.
            BackgroundData currentFrameData = new BackgroundData();
            // Open device.
            using (Device device = Device.Open(id))
            {
                device.StartCameras(new DeviceConfiguration()
                {
                    CameraFPS = FPS.FPS30,
                    //ColorResolution = ColorResolution.Off,
                    ColorResolution = ColorResolution.R720p,
                    ColorFormat = ImageFormat.ColorBGRA32,
                    //DepthMode = DepthMode.NFOV_Unbinned,
                    DepthMode = DepthMode.WFOV_2x2Binned,
                    //DepthMode = DepthMode.WFOV_Unbinned,
                    WiredSyncMode = WiredSyncMode.Standalone,
                }); ;

                UnityEngine.Debug.Log("Open K4A device successful. id " + id + "sn:" + device.SerialNum);

                var deviceCalibration = device.GetCalibration();
                Transformation myTestTransformation = deviceCalibration.CreateTransformation();

                using (Tracker tracker = Tracker.Create(deviceCalibration, new TrackerConfiguration() { ProcessingMode = TrackerProcessingMode.DirectML, SensorOrientation = SensorOrientation.Default }))
                {
                    UnityEngine.Debug.Log("Body tracker created.");
                    while (!token.IsCancellationRequested)
                    {
                        using (Capture sensorCapture = device.GetCapture())
                        {
                            //fred : try to get color image
                            Memory<byte> colorData = sensorCapture.Color.Memory;                            

                            currentFrameData.ColorImageWidth = sensorCapture.Color.WidthPixels;
                            currentFrameData.ColorImageHeight = sensorCapture.Color.HeightPixels;
                            currentFrameData.ColorImageSize = currentFrameData.ColorImageWidth * currentFrameData.ColorImageHeight * 4;
                            currentFrameData.ColorImage = colorData.ToArray();                       

                            // Queue latest frame from the sensor.
                            tracker.EnqueueCapture(sensorCapture);
                        }

                        // Try getting latest tracker frame.
                        using (Frame frame = tracker.PopResult(TimeSpan.Zero, throwOnTimeout: false))
                        {
                            if (frame == null)
                            {
                                UnityEngine.Debug.Log("Pop result from tracker timeout!");
                            }
                            else
                            {
                                IsRunning = true;
                                // Get number of bodies in the current frame.
                                currentFrameData.NumOfBodies = frame.NumberOfBodies;

                                // Copy bodies.
                                for (uint i = 0; i < currentFrameData.NumOfBodies; i++)
                                {
                                    currentFrameData.Bodies[i].CopyFromBodyTrackingSdk(frame.GetBody(i), deviceCalibration);
                                }                                

                                // Store depth image.
                                Capture bodyFrameCapture = frame.Capture;
                                Microsoft.Azure.Kinect.Sensor.Image depthImage = bodyFrameCapture.Depth;
                                if (!readFirstFrame)
                                {
                                    readFirstFrame = true;
                                    initialTimestamp = depthImage.DeviceTimestamp;
                                }
                                currentFrameData.TimestampInMs = (float)(depthImage.DeviceTimestamp - initialTimestamp).TotalMilliseconds;
                                currentFrameData.DepthImageWidth = depthImage.WidthPixels;
                                currentFrameData.DepthImageHeight = depthImage.HeightPixels;

                                //Fred : try to get body index map                                
                                var resultTransfo = myTestTransformation.DepthImageToColorCameraCustom(depthImage, frame.BodyIndexMap, TransformationInterpolationType.Nearest, K4ABT_BODY_INDEX_MAP_BACKGROUND);
                                Microsoft.Azure.Kinect.Sensor.Image transformedBodyMap = resultTransfo.transformedCustom;
                                Memory<byte> transformedBodyData = transformedBodyMap.Memory;
                                byte[] transformedBodyDataArray = transformedBodyData.ToArray();

                                //UnityEngine.Debug.Log("currentFrameData.ColorImage.Length="+ currentFrameData.ColorImage.Length);
                                //UnityEngine.Debug.Log("transformedBodyDataArray.Length=" + transformedBodyDataArray.Length);

                                int j = 0;                                
                                for (int i = 0; i < transformedBodyDataArray.Length; i++)
                                {                                    
                                    if (transformedBodyDataArray[i] == K4ABT_BODY_INDEX_MAP_BACKGROUND)
                                    {
                                        currentFrameData.ColorImage[j] = 0;
                                        currentFrameData.ColorImage[j+1] = 0;
                                        currentFrameData.ColorImage[j+2] = 0;
                                        currentFrameData.ColorImage[j+3] = 0;
                                    }
                                    j = j + 4;
                                }

                                // Read image data from the SDK.
                                var depthFrame = MemoryMarshal.Cast<byte, ushort>(depthImage.Memory.Span);

                                // Repack data and store image data.
                                int byteCounter = 0;
                                currentFrameData.DepthImageSize = currentFrameData.DepthImageWidth * currentFrameData.DepthImageHeight * 3;

                                for (int it = currentFrameData.DepthImageWidth * currentFrameData.DepthImageHeight - 1; it > 0; it--)
                                {
                                    byte b = (byte)(depthFrame[it] / (ConfigLoader.Instance.Configs.SkeletalTracking.MaximumDisplayedDepthInMillimeters) * 255);
                                    currentFrameData.DepthImage[byteCounter++] = b;
                                    currentFrameData.DepthImage[byteCounter++] = b;
                                    currentFrameData.DepthImage[byteCounter++] = b;
                                }

                                if (RawDataLoggingFile != null && RawDataLoggingFile.CanWrite)
                                {
                                    binaryFormatter.Serialize(RawDataLoggingFile, currentFrameData);
                                }

                                // Update data variable that is being read in the UI thread.
                                SetCurrentFrameData(ref currentFrameData);
                            }

                        }
                    }
                    Debug.Log("dispose of tracker now!!!!!");
                    tracker.Dispose();
                }
                device.Dispose();
            }
            if (RawDataLoggingFile != null)
            {
                RawDataLoggingFile.Close();
            }
        }
        catch (Exception e)
        {
            statusText.text = "No azure kinect camera detected !";
            Debug.Log($"catching exception for background thread {e.Message}");
            token.ThrowIfCancellationRequested();
        }
    }
}