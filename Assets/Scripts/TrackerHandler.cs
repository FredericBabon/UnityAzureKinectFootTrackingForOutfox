using System.Collections.Generic;
using UnityEngine;
using Microsoft.Azure.Kinect.BodyTracking;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using System;
using System.Collections;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEditor;
using UnityEngine.EventSystems;
using TMPro;
using System.Diagnostics;
using UnityEngine.UIElements;

public class TrackerHandler : MonoBehaviour
{

    [DllImport("user32.dll")]
    private static extern void DisableProcessWindowsGhosting();


    [DllImport("user32.dll")]
    public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    static extern int SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopWidth;
        public int cyBottomWidth;
    }

    [DllImport("Dwmapi.dll")]
    private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    const int GWL_EXSTYLE = -20;

    const uint WS_EX_LAYERED = 0x00080000;
    const uint WS_EX_TRANSPARENT = 0x00000020;

    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    const uint LWA_COLORKEY = 0x00000001;



    enum calibrationState
    {
        NothingInFrontOfCamera,
        NotCalibrated,
        CalibratingLeftRight,
        WaitingForCalibUpDown,
        CalibratingUpDown,
        CalibrationDone
    }

    calibrationState currentCalibrationState = calibrationState.NothingInFrontOfCamera;
    calibrationState previousCalibrationState = calibrationState.NothingInFrontOfCamera;   

    

    bool isTrackingIgnored = false;

    public GameObject pad;
    public GameObject mycamera;

    public detectArrowCollision detectArrowCollisionLeft;
    public detectArrowCollision detectArrowCollisionRight;
    public detectArrowCollision detectArrowCollisionUp;
    public detectArrowCollision detectArrowCollisionDown;

    Stopwatch stopWatch;

    public RawImage monImageRGB;
    Texture2D outputTexture = null;

    public SliderChange sliderLeft;
    public SliderChange sliderRight;
    public SliderChange sliderTop;
    public SliderChange sliderBack;

    public UnityEngine.UI.Button calibLeftRightButton;
    public UnityEngine.UI.Button calibUpDownButton;
    public UnityEngine.UI.Button closeButton;
    public UnityEngine.UI.Button resetButton;
    public TextMeshProUGUI statusText;

    FixedSizedQueue stored_rotations_queue = new FixedSizedQueue(30);
    FixedSizedQueueVector3 stored_vector3_queue = new FixedSizedQueueVector3(30);




    public Dictionary<JointId, JointId> parentJointMap;
    Dictionary<JointId, Quaternion> basisJointMap;
    public Quaternion[] absoluteJointRotations = new Quaternion[(int)JointId.Count];
    public bool drawSkeletons = true;
    Quaternion Y_180_FLIP = new Quaternion(0.0f, 1.0f, 0.0f, 0.0f);

    void setCalibrationStatus(calibrationState newState)
    {
        previousCalibrationState = currentCalibrationState;
        currentCalibrationState = newState;

        if(currentCalibrationState == calibrationState.NothingInFrontOfCamera) 
        {
            statusText.text = "Nobody in front of the camera !";
            calibLeftRightButton.gameObject.SetActive(false);
            calibUpDownButton.gameObject.SetActive(false);


            transform.GetChild(0).GetChild((int)JointId.FootLeft).gameObject.GetComponent<Rigidbody>().detectCollisions = false;
            transform.GetChild(0).GetChild((int)JointId.FootLeft).gameObject.SetActive(false);
            transform.GetChild(0).GetChild((int)JointId.FootLeft).GetChild(0).gameObject.GetComponent<Rigidbody>().detectCollisions = false;
            transform.GetChild(0).GetChild((int)JointId.FootLeft).GetChild(0).gameObject.SetActive(false);
            transform.GetChild(0).GetChild((int)JointId.FootRight).gameObject.GetComponent<Rigidbody>().detectCollisions = false;
            transform.GetChild(0).GetChild((int)JointId.FootRight).gameObject.SetActive(false);
            transform.GetChild(0).GetChild((int)JointId.FootRight).GetChild(0).gameObject.GetComponent<Rigidbody>().detectCollisions = false;
            transform.GetChild(0).GetChild((int)JointId.FootRight).GetChild(0).gameObject.SetActive(false);

            detectArrowCollisionLeft.triggerOffArrowIfCurrentCollisionExist();
            detectArrowCollisionRight.triggerOffArrowIfCurrentCollisionExist();
            detectArrowCollisionUp.triggerOffArrowIfCurrentCollisionExist();
            detectArrowCollisionDown.triggerOffArrowIfCurrentCollisionExist();
        }
        else if (currentCalibrationState == calibrationState.NotCalibrated)
        {
            statusText.text = "Please calibrate left/right !";
            calibLeftRightButton.gameObject.SetActive(true);
            calibUpDownButton.gameObject.SetActive(false);
        }
        else if (currentCalibrationState == calibrationState.CalibratingLeftRight)
        {
            statusText.text = "Calibrating Left/Right...";
            calibLeftRightButton.gameObject.SetActive(false);
            calibUpDownButton.gameObject.SetActive(false);
            stored_rotations_queue.Clear();
            stored_vector3_queue.Clear();
        }
        else if (currentCalibrationState == calibrationState.WaitingForCalibUpDown)
        {
            statusText.text = "Please calibrate forward/backward !";
            calibLeftRightButton.gameObject.SetActive(false);
            calibUpDownButton.gameObject.SetActive(true);
        }
        else if (currentCalibrationState == calibrationState.CalibratingUpDown)
        {
            statusText.text = "Calibrating forward/backward...";
            calibLeftRightButton.gameObject.SetActive(false);
            calibUpDownButton.gameObject.SetActive(false);
            stored_rotations_queue.Clear();
        }
        else if (currentCalibrationState == calibrationState.CalibrationDone)
        {
            statusText.text = "Ready, feel free to recalibrate if necessary";
            calibLeftRightButton.gameObject.SetActive(true);
            calibUpDownButton.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
#if !UNITY_EDITOR
        IntPtr hWnd = GetActiveWindow();

        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);

        //SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED|WS_EX_TRANSPARENT);
        SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED);
        SetLayeredWindowAttributes(hWnd, 0, 0, LWA_COLORKEY);

        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, 0);

        DisableProcessWindowsGhosting();
#endif

        Application.runInBackground = true;
        calibLeftRightButton.onClick.AddListener(onCalibrateLeftRightButton);
        calibUpDownButton.onClick.AddListener(onCalibrateUpDownButton);
        closeButton.onClick.AddListener(onCloseButton);
        resetButton.onClick.AddListener(onResetButton);



        outputTexture = new Texture2D(1280, 720, TextureFormat.BGRA32, false);

        setCalibrationStatus(calibrationState.NothingInFrontOfCamera);

        stopWatch = new Stopwatch();

        applyLeftRightCalib();
        applyUpDownCalib();
    }

    // Start is called before the first frame update
    void Awake()
    {
        parentJointMap = new Dictionary<JointId, JointId>();

        // pelvis has no parent so set to count
        parentJointMap[JointId.Pelvis] = JointId.Count;
        parentJointMap[JointId.SpineNavel] = JointId.Pelvis;
        parentJointMap[JointId.SpineChest] = JointId.SpineNavel;
        parentJointMap[JointId.Neck] = JointId.SpineChest;
        parentJointMap[JointId.ClavicleLeft] = JointId.SpineChest;
        parentJointMap[JointId.ShoulderLeft] = JointId.ClavicleLeft;
        parentJointMap[JointId.ElbowLeft] = JointId.ShoulderLeft;
        parentJointMap[JointId.WristLeft] = JointId.ElbowLeft;
        parentJointMap[JointId.HandLeft] = JointId.WristLeft;
        parentJointMap[JointId.HandTipLeft] = JointId.HandLeft;
        parentJointMap[JointId.ThumbLeft] = JointId.HandLeft;
        parentJointMap[JointId.ClavicleRight] = JointId.SpineChest;
        parentJointMap[JointId.ShoulderRight] = JointId.ClavicleRight;
        parentJointMap[JointId.ElbowRight] = JointId.ShoulderRight;
        parentJointMap[JointId.WristRight] = JointId.ElbowRight;
        parentJointMap[JointId.HandRight] = JointId.WristRight;
        parentJointMap[JointId.HandTipRight] = JointId.HandRight;
        parentJointMap[JointId.ThumbRight] = JointId.HandRight;
        parentJointMap[JointId.HipLeft] = JointId.SpineNavel;
        parentJointMap[JointId.KneeLeft] = JointId.HipLeft;
        parentJointMap[JointId.AnkleLeft] = JointId.KneeLeft;
        parentJointMap[JointId.FootLeft] = JointId.AnkleLeft;
        parentJointMap[JointId.HipRight] = JointId.SpineNavel;
        parentJointMap[JointId.KneeRight] = JointId.HipRight;
        parentJointMap[JointId.AnkleRight] = JointId.KneeRight;
        parentJointMap[JointId.FootRight] = JointId.AnkleRight;
        parentJointMap[JointId.Head] = JointId.Pelvis;
        parentJointMap[JointId.Nose] = JointId.Head;
        parentJointMap[JointId.EyeLeft] = JointId.Head;
        parentJointMap[JointId.EarLeft] = JointId.Head;
        parentJointMap[JointId.EyeRight] = JointId.Head;
        parentJointMap[JointId.EarRight] = JointId.Head;

        Vector3 zpositive = Vector3.forward;
        Vector3 xpositive = Vector3.right;
        Vector3 ypositive = Vector3.up;
        // spine and left hip are the same
        Quaternion leftHipBasis = Quaternion.LookRotation(xpositive, -zpositive);
        Quaternion spineHipBasis = Quaternion.LookRotation(xpositive, -zpositive);
        Quaternion rightHipBasis = Quaternion.LookRotation(xpositive, zpositive);
        // arms and thumbs share the same basis
        Quaternion leftArmBasis = Quaternion.LookRotation(ypositive, -zpositive);
        Quaternion rightArmBasis = Quaternion.LookRotation(-ypositive, zpositive);
        Quaternion leftHandBasis = Quaternion.LookRotation(-zpositive, -ypositive);
        Quaternion rightHandBasis = Quaternion.identity;
        Quaternion leftFootBasis = Quaternion.LookRotation(xpositive, ypositive);
        Quaternion rightFootBasis = Quaternion.LookRotation(xpositive, -ypositive);

        basisJointMap = new Dictionary<JointId, Quaternion>();

        // pelvis has no parent so set to count
        basisJointMap[JointId.Pelvis] = spineHipBasis;
        basisJointMap[JointId.SpineNavel] = spineHipBasis;
        basisJointMap[JointId.SpineChest] = spineHipBasis;
        basisJointMap[JointId.Neck] = spineHipBasis;
        basisJointMap[JointId.ClavicleLeft] = leftArmBasis;
        basisJointMap[JointId.ShoulderLeft] = leftArmBasis;
        basisJointMap[JointId.ElbowLeft] = leftArmBasis;
        basisJointMap[JointId.WristLeft] = leftHandBasis;
        basisJointMap[JointId.HandLeft] = leftHandBasis;
        basisJointMap[JointId.HandTipLeft] = leftHandBasis;
        basisJointMap[JointId.ThumbLeft] = leftArmBasis;
        basisJointMap[JointId.ClavicleRight] = rightArmBasis;
        basisJointMap[JointId.ShoulderRight] = rightArmBasis;
        basisJointMap[JointId.ElbowRight] = rightArmBasis;
        basisJointMap[JointId.WristRight] = rightHandBasis;
        basisJointMap[JointId.HandRight] = rightHandBasis;
        basisJointMap[JointId.HandTipRight] = rightHandBasis;
        basisJointMap[JointId.ThumbRight] = rightArmBasis;
        basisJointMap[JointId.HipLeft] = leftHipBasis;
        basisJointMap[JointId.KneeLeft] = leftHipBasis;
        basisJointMap[JointId.AnkleLeft] = leftHipBasis;
        basisJointMap[JointId.FootLeft] = leftFootBasis;
        basisJointMap[JointId.HipRight] = rightHipBasis;
        basisJointMap[JointId.KneeRight] = rightHipBasis;
        basisJointMap[JointId.AnkleRight] = rightHipBasis;
        basisJointMap[JointId.FootRight] = rightFootBasis;
        basisJointMap[JointId.Head] = spineHipBasis;
        basisJointMap[JointId.Nose] = spineHipBasis;
        basisJointMap[JointId.EyeLeft] = spineHipBasis;
        basisJointMap[JointId.EarLeft] = spineHipBasis;
        basisJointMap[JointId.EyeRight] = spineHipBasis;
        basisJointMap[JointId.EarRight] = spineHipBasis;
    }

    //FixedSizedQueue diff_foots_queue = new FixedSizedQueue(30);

    void onCalibrateLeftRightButton()
    {
        setCalibrationStatus(calibrationState.CalibratingLeftRight);
        EventSystem.current.SetSelectedGameObject(null);
    }

    void onCalibrateUpDownButton()
    {
        setCalibrationStatus(calibrationState.CalibratingUpDown);
        EventSystem.current.SetSelectedGameObject(null);
    }

    void onCloseButton()
    {
        UnityEngine.Debug.Log("onCloseButton");
#if UNITY_EDITOR
        // Application.Quit() does not work in the editor so
        // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void onResetButton()
    {
        PlayerPrefs.DeleteKey("meanFootsVectorX");
        PlayerPrefs.DeleteKey("meanFootsVectorY");
        PlayerPrefs.DeleteKey("meanFootsVectorZ");
        PlayerPrefs.DeleteKey("meanEulerAnglesZ");
        PlayerPrefs.DeleteKey("meanEulerAnglesX");

        PlayerPrefs.DeleteKey("SliderLeft");
        PlayerPrefs.DeleteKey("SliderRight");
        PlayerPrefs.DeleteKey("SliderTop");
        PlayerPrefs.DeleteKey("SliderBack");

        sliderLeft.Reset();
        sliderRight.Reset();
        sliderTop.Reset();
        sliderBack.Reset();

        pad.transform.position = new Vector3(0f, 0.5f, 0f);
        pad.transform.rotation = Quaternion.identity;
        mycamera.transform.position = new Vector3(0f, 1.5f, 2.5f);        
        mycamera.transform.rotation = Quaternion.identity;

        Vector3 rot = mycamera.transform.rotation.eulerAngles;
        rot = new Vector3(rot.x, rot.y + 180, rot.z);
        mycamera.transform.rotation = Quaternion.Euler(rot);

        setCalibrationStatus(calibrationState.NothingInFrontOfCamera);
        previousCalibrationState = calibrationState.NothingInFrontOfCamera;
    }


    void Update()
    {        

        if (Input.GetKeyDown("i"))
            isTrackingIgnored = true;


        if (Input.GetKeyDown("r"))
            isTrackingIgnored = false;

        if(currentCalibrationState!= calibrationState.NothingInFrontOfCamera && stopWatch.Elapsed.Milliseconds > 200)
        {
            setCalibrationStatus(calibrationState.NothingInFrontOfCamera);
        }
    }
    
    void applyLeftRightCalib()
    {
        if (PlayerPrefs.HasKey("meanFootsVectorX") && PlayerPrefs.HasKey("meanFootsVectorY") && PlayerPrefs.HasKey("meanFootsVectorZ") && PlayerPrefs.HasKey("meanEulerAnglesZ"))
        {
            //Vector3 meanFootsVector3 = stored_vector3_queue.GetMean();

            float meanFootsVectorX = PlayerPrefs.GetFloat("meanFootsVectorX");
            float meanFootsVectorY = PlayerPrefs.GetFloat("meanFootsVectorY");
            float meanFootsVectorZ = PlayerPrefs.GetFloat("meanFootsVectorZ");

            pad.transform.position = new Vector3(meanFootsVectorX, meanFootsVectorY - 0.05f, meanFootsVectorZ);

            //float meanEulerAnglesZ = stored_rotations_queue.GetMean();

            float meanEulerAnglesZ = PlayerPrefs.GetFloat("meanEulerAnglesZ");

            Vector3 currentPadEulerAngles = pad.transform.rotation.eulerAngles;
            pad.transform.rotation = Quaternion.Euler(currentPadEulerAngles.x, currentPadEulerAngles.y, meanEulerAnglesZ);

            Vector3 currentCamEulerAngles = mycamera.transform.rotation.eulerAngles;
            mycamera.transform.rotation = Quaternion.Euler(-currentPadEulerAngles.x + 20, currentCamEulerAngles.y, -meanEulerAnglesZ);


            Vector3 padTransformPosition = pad.transform.position;
            mycamera.transform.position = new Vector3(padTransformPosition.x, padTransformPosition.y, padTransformPosition.z);
            mycamera.transform.Translate(new Vector3(0, 0.5f, -2));

            setCalibrationStatus(calibrationState.WaitingForCalibUpDown);
        }
            
    }

    void applyUpDownCalib()
    {        
        if(PlayerPrefs.HasKey("meanEulerAnglesX"))
        {
            //float meanEulerAnglesX = stored_rotations_queue.GetMean();
            float meanEulerAnglesX = PlayerPrefs.GetFloat("meanEulerAnglesX");

            Vector3 currentPadEulerAngles = pad.transform.rotation.eulerAngles;
            pad.transform.rotation = Quaternion.Euler(-meanEulerAnglesX, currentPadEulerAngles.y, currentPadEulerAngles.z);

            Vector3 currentCamEulerAngles = mycamera.transform.rotation.eulerAngles;
            mycamera.transform.rotation = Quaternion.Euler(meanEulerAnglesX + 20, currentCamEulerAngles.y, currentCamEulerAngles.z);


            Vector3 padTransformPosition = pad.transform.position;
            mycamera.transform.position = new Vector3(padTransformPosition.x, padTransformPosition.y, padTransformPosition.z);
            mycamera.transform.Translate(new Vector3(0, 0.5f, -2));
            //camera.transform.LookAt(pad.transform);
            setCalibrationStatus(calibrationState.CalibrationDone);
        }
        
    }

    public void updateTracker(BackgroundData trackerFrameData)
    {
        stopWatch.Restart();

        if(currentCalibrationState == calibrationState.NothingInFrontOfCamera)
        {
            transform.GetChild(0).GetChild((int)JointId.FootLeft).gameObject.GetComponent<Rigidbody>().detectCollisions = true;
            transform.GetChild(0).GetChild((int)JointId.FootLeft).gameObject.SetActive(true);
            transform.GetChild(0).GetChild((int)JointId.FootLeft).GetChild(0).gameObject.GetComponent<Rigidbody>().detectCollisions = true;
            transform.GetChild(0).GetChild((int)JointId.FootLeft).GetChild(0).gameObject.SetActive(true);
            transform.GetChild(0).GetChild((int)JointId.FootRight).gameObject.GetComponent<Rigidbody>().detectCollisions = true;
            transform.GetChild(0).GetChild((int)JointId.FootRight).gameObject.SetActive(true);
            transform.GetChild(0).GetChild((int)JointId.FootRight).GetChild(0).gameObject.GetComponent<Rigidbody>().detectCollisions = true;
            transform.GetChild(0).GetChild((int)JointId.FootRight).GetChild(0).gameObject.SetActive(true);


            if (previousCalibrationState != calibrationState.NothingInFrontOfCamera)
                setCalibrationStatus(previousCalibrationState);
            else
                setCalibrationStatus(calibrationState.NotCalibrated);
        }


        if (isTrackingIgnored)
            return;


        outputTexture.LoadRawTextureData(trackerFrameData.ColorImage);
        outputTexture.Apply();
        monImageRGB.texture = outputTexture;

        //this is an array in case you want to get the n closest bodies
        int closestBody = findClosestTrackedBody(trackerFrameData);

        // render the closest body
        Body skeleton = trackerFrameData.Bodies[closestBody];
        renderSkeleton(skeleton, 0);        
        

        if(currentCalibrationState == calibrationState.CalibratingLeftRight)
        {
            float meanXfoots = (skeleton.JointPositions3D[(int)JointId.FootLeft].X + skeleton.JointPositions3D[(int)JointId.FootRight].X) / 2.0f;
            float meanYfoots = (-skeleton.JointPositions3D[(int)JointId.FootLeft].Y - skeleton.JointPositions3D[(int)JointId.FootRight].Y) / 2.0f + 1.04f;
            float meanZfoots = (skeleton.JointPositions3D[(int)JointId.FootLeft].Z + skeleton.JointPositions3D[(int)JointId.FootRight].Z) / 2.0f;

            Vector3 currentFootsPosition = new Vector3(meanXfoots, meanYfoots, meanZfoots);
            //UnityEngine.Debug.Log("currentFootsPosition=" + currentFootsPosition);
            stored_vector3_queue.Enqueue(currentFootsPosition);

            Vector3 footLeftVector = new Vector3(skeleton.JointPositions3D[(int)JointId.FootLeft].X, -skeleton.JointPositions3D[(int)JointId.FootLeft].Y, skeleton.JointPositions3D[(int)JointId.FootLeft].Z);
            Vector3 footRightVector = new Vector3(skeleton.JointPositions3D[(int)JointId.FootRight].X, -skeleton.JointPositions3D[(int)JointId.FootRight].Y, skeleton.JointPositions3D[(int)JointId.FootRight].Z);

            Quaternion currentZRotation = Quaternion.FromToRotation(Vector3.right, footLeftVector - footRightVector);
            stored_rotations_queue.Enqueue(currentZRotation.eulerAngles.z);

            if (stored_rotations_queue.GetSize() == stored_rotations_queue.Capacity && stored_vector3_queue.GetSize() == stored_vector3_queue.Capacity)
            {
                Vector3 meanFootsVector3 = stored_vector3_queue.GetMean();
                PlayerPrefs.SetFloat("meanFootsVectorX", meanFootsVector3.x);
                PlayerPrefs.SetFloat("meanFootsVectorY", meanFootsVector3.y);
                PlayerPrefs.SetFloat("meanFootsVectorZ", meanFootsVector3.z);
                //pad.transform.position = new Vector3(meanFootsVector3.x, meanFootsVector3.y - 0.05f, meanFootsVector3.z);

                float meanEulerAnglesZ = stored_rotations_queue.GetMean();
                PlayerPrefs.SetFloat("meanEulerAnglesZ", meanEulerAnglesZ);
                /*Vector3 currentPadEulerAngles = pad.transform.rotation.eulerAngles;
                pad.transform.rotation = Quaternion.Euler(currentPadEulerAngles.x, currentPadEulerAngles.y, meanEulerAnglesZ);

                Vector3 currentCamEulerAngles = mycamera.transform.rotation.eulerAngles;
                mycamera.transform.rotation = Quaternion.Euler(-currentPadEulerAngles.x+20, currentCamEulerAngles.y, -meanEulerAnglesZ);
                

                Vector3 padTransformPosition = pad.transform.position;
                mycamera.transform.position = new Vector3(padTransformPosition.x, padTransformPosition.y, padTransformPosition.z);
                mycamera.transform.Translate(new Vector3(0, 0.5f, -2));*/
                applyLeftRightCalib();

                
            }

            
        }
        else if (currentCalibrationState == calibrationState.CalibratingUpDown)
        {
            Vector3 footLeftVector = new Vector3(skeleton.JointPositions3D[(int)JointId.FootLeft].X, -skeleton.JointPositions3D[(int)JointId.FootLeft].Y, skeleton.JointPositions3D[(int)JointId.FootLeft].Z);
            Vector3 footRightVector = new Vector3(skeleton.JointPositions3D[(int)JointId.FootRight].X, -skeleton.JointPositions3D[(int)JointId.FootRight].Y, skeleton.JointPositions3D[(int)JointId.FootRight].Z);
            Quaternion currentXRotation = Quaternion.FromToRotation(Vector3.forward, footLeftVector - footRightVector);


            stored_rotations_queue.Enqueue(currentXRotation.eulerAngles.x);

            if(stored_rotations_queue.GetSize() == stored_rotations_queue.Capacity)
            {
                float meanEulerAnglesX = stored_rotations_queue.GetMean();
                PlayerPrefs.SetFloat("meanEulerAnglesX", meanEulerAnglesX);
                /*Vector3 currentPadEulerAngles = pad.transform.rotation.eulerAngles;
                pad.transform.rotation = Quaternion.Euler(-meanEulerAnglesX, currentPadEulerAngles.y, currentPadEulerAngles.z);

                Vector3 currentCamEulerAngles = mycamera.transform.rotation.eulerAngles;
                mycamera.transform.rotation = Quaternion.Euler(meanEulerAnglesX + 20, currentCamEulerAngles.y, currentCamEulerAngles.z);


                Vector3 padTransformPosition = pad.transform.position;
                mycamera.transform.position = new Vector3(padTransformPosition.x, padTransformPosition.y, padTransformPosition.z);
                mycamera.transform.Translate(new Vector3(0, 0.5f, -2));
                //camera.transform.LookAt(pad.transform);
                setCalibrationStatus(calibrationState.CalibrationDone);*/
                applyUpDownCalib();
            }

            

        }



    }

    int findIndexFromId(BackgroundData frameData, int id)
    {
        int retIndex = -1;
        for (int i = 0; i < (int)frameData.NumOfBodies; i++)
        {
            if ((int)frameData.Bodies[i].Id == id)
            {
                retIndex = i;
                break;
            }
        }
        return retIndex;
    }

    private int findClosestTrackedBody(BackgroundData trackerFrameData)
    {
        int closestBody = -1;
        const float MAX_DISTANCE = 5000.0f;
        float minDistanceFromKinect = MAX_DISTANCE;
        for (int i = 0; i < (int)trackerFrameData.NumOfBodies; i++)
        {
            var pelvisPosition = trackerFrameData.Bodies[i].JointPositions3D[(int)JointId.Pelvis];
            Vector3 pelvisPos = new Vector3((float)pelvisPosition.X, (float)pelvisPosition.Y, (float)pelvisPosition.Z);
            if (pelvisPos.magnitude < minDistanceFromKinect)
            {
                closestBody = i;
                minDistanceFromKinect = pelvisPos.magnitude;
            }
        }
        return closestBody;
    }

    public void turnOnOffSkeletons()
    {
        drawSkeletons = !drawSkeletons;
        const int bodyRenderedNum = 0;
        for (int jointNum = 0; jointNum < (int)JointId.Count; jointNum++)
        {
            transform.GetChild(bodyRenderedNum).GetChild(jointNum).gameObject.GetComponent<MeshRenderer>().enabled = drawSkeletons;
            transform.GetChild(bodyRenderedNum).GetChild(jointNum).GetChild(0).GetComponent<MeshRenderer>().enabled = drawSkeletons;
        }
    }

    public void renderSkeleton(Body skeleton, int skeletonNumber)
    {
        for (int jointNum = 0; jointNum < (int)JointId.Count; jointNum++)
        {
            Vector3 jointPos = new Vector3(skeleton.JointPositions3D[jointNum].X, -skeleton.JointPositions3D[jointNum].Y, skeleton.JointPositions3D[jointNum].Z);
            Vector3 offsetPosition = transform.rotation * jointPos;
            Vector3 positionInTrackerRootSpace = transform.position + offsetPosition;
            Quaternion jointRot = Y_180_FLIP * new Quaternion(skeleton.JointRotations[jointNum].X, skeleton.JointRotations[jointNum].Y,
                skeleton.JointRotations[jointNum].Z, skeleton.JointRotations[jointNum].W) * Quaternion.Inverse(basisJointMap[(JointId)jointNum]);
            absoluteJointRotations[jointNum] = jointRot;
            // these are absolute body space because each joint has the body root for a parent in the scene graph
            transform.GetChild(skeletonNumber).GetChild(jointNum).localPosition = jointPos;
            transform.GetChild(skeletonNumber).GetChild(jointNum).localRotation = jointRot;

            const int boneChildNum = 0;
            if (parentJointMap[(JointId)jointNum] != JointId.Head && parentJointMap[(JointId)jointNum] != JointId.Count)
            {
                Vector3 parentTrackerSpacePosition = new Vector3(skeleton.JointPositions3D[(int)parentJointMap[(JointId)jointNum]].X,
                    -skeleton.JointPositions3D[(int)parentJointMap[(JointId)jointNum]].Y, skeleton.JointPositions3D[(int)parentJointMap[(JointId)jointNum]].Z);
                Vector3 boneDirectionTrackerSpace = jointPos - parentTrackerSpacePosition;
                Vector3 boneDirectionWorldSpace = transform.rotation * boneDirectionTrackerSpace;
                Vector3 boneDirectionLocalSpace = Quaternion.Inverse(transform.GetChild(skeletonNumber).GetChild(jointNum).rotation) * Vector3.Normalize(boneDirectionWorldSpace);
                transform.GetChild(skeletonNumber).GetChild(jointNum).GetChild(boneChildNum).localScale = new Vector3(1, 20.0f * 0.5f * boneDirectionWorldSpace.magnitude, 1);
                transform.GetChild(skeletonNumber).GetChild(jointNum).GetChild(boneChildNum).localRotation = Quaternion.FromToRotation(Vector3.up, boneDirectionLocalSpace);
                transform.GetChild(skeletonNumber).GetChild(jointNum).GetChild(boneChildNum).position = transform.GetChild(skeletonNumber).GetChild(jointNum).position - 0.5f * boneDirectionWorldSpace;
            }
            else
            {
                transform.GetChild(skeletonNumber).GetChild(jointNum).GetChild(boneChildNum).gameObject.SetActive(false);
            }
        }
    }

    public Quaternion GetRelativeJointRotation(JointId jointId)
    {
        JointId parent = parentJointMap[jointId];
        Quaternion parentJointRotationBodySpace = Quaternion.identity;
        if (parent == JointId.Count)
        {
            parentJointRotationBodySpace = Y_180_FLIP;
        }
        else
        {
            parentJointRotationBodySpace = absoluteJointRotations[(int)parent];
        }
        Quaternion jointRotationBodySpace = absoluteJointRotations[(int)jointId];
        Quaternion relativeRotation =  Quaternion.Inverse(parentJointRotationBodySpace) * jointRotationBodySpace;

        return relativeRotation;
    }

}
