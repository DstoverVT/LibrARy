using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Threading.Tasks;
using UnityEditor.XR;
using Microsoft.MixedReality.Toolkit;
using UnityEngine.Windows.WebCam;
using System.Linq;

#if ENABLE_WINMD_SUPPORT
using HL2UnityPlugin;
#endif
using System;

public class SensorStreamController : MonoBehaviour
{
#if ENABLE_WINMD_SUPPORT
    HL2ResearchMode researchMode;    
#endif

    /** <summary> True to log active brightness images as well, else False. </summary> */
    private bool logAB = false;
    private bool logEyeGaze = false;
    private bool useDepth = true;

    [SerializeField]
    private Transform playerCamera;

#if ENABLE_WINMD_SUPPORT
    Windows.Perception.Spatial.SpatialCoordinateSystem unityWorldOrigin;
#endif

    /** <summary> Variables for saving depth, eye gaze, and velocity </summary> */
    public float depth_FPS = 1f;
    private float waitTime;
    private bool startStream = false;
    private float streamTime = 0.0f;
    private Vector3 lastPlayerPos;

    /** <summary> Directories to save sensor data. </summary> */
    private string depthPath;
    private string abPath;
    private string gazePath;
    private string videoPath;

    /** <summary> Keeps track of when depth writing is complete is async task. </summary> */
    private bool lastDepthWriteComplete = true;

#if ENABLE_WINMD_SUPPORT
    VideoCapture m_VideoCapture = null;
#endif

    /** <summary> Initializes research mode devices. </summary> */
    void Start()
    {
#if ENABLE_WINMD_SUPPORT
        researchMode = new HL2ResearchMode();

        unityWorldOrigin = Microsoft.MixedReality.OpenXR.PerceptionInterop.GetSceneCoordinateSystem(UnityEngine.Pose.identity) as Windows.Perception.Spatial.SpatialCoordinateSystem;
        researchMode.SetReferenceCoordinateSystem(unityWorldOrigin);

        // Depth sensor should be initialized in only one mode
        if (useDepth)
        {
            researchMode.InitializeDepthSensor();
        }
#endif
        waitTime = 1 / depth_FPS;
    }

    /** <summary> Controls saving depth maps throughout process. </summary> */
    private void Update()
    {
        if (startStream)
        {
            streamTime += Time.deltaTime;

            if (streamTime > waitTime)
            {
#if ENABLE_WINMD_SUPPORT
                /* Log depth camera data. */
                if (lastDepthWriteComplete)               
                {
                    lastDepthWriteComplete = false;

                    //AppController.debugAndLog("Logging depth stream.");
                    if (useDepth)
                    {
                        SaveShortDepthSensor();
                    }
                }                  
#endif
                /* Log eye gaze and velocity data. */
                if(logEyeGaze)
                {
                    WriteSensorData();
                }

                streamTime -= waitTime;
            }
        }
    }

    private string createSensorDirectory(string path, string subdirectory)
    {
        string fullPath = Application.persistentDataPath + path;
        if(!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }
        //int dirNum = Directory.GetDirectories(fullPath).Length;
        //fullPath += subdirectory + dirNum;

        /* Append date to file. Ex: 09-20_10-30 */
        DateTime now = DateTime.Now;
        string fileTime = string.Format("{0:MM-dd_HH-mm-ss}", now);
        fullPath += subdirectory + fileTime;

        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        AppController.debugAndLog("Saving files to " + fullPath);

        return fullPath;
    }

    private void createLogFiles()
    {
        string streamRoot = "/Logs/Streams";
        DateTime now = DateTime.Now;
        string fileTime = string.Format("{0:MM-dd_HH-mm-ss}", now);

#if ENABLE_WINMD_SUPPORT
        depthPath = createSensorDirectory(streamRoot + "/Depth", "/depth");

        if (logAB)
        {
            abPath = createSensorDirectory(streamRoot + "/AB", "/ab");
        }

        /* Create log for video recording. */
        videoPath = Application.persistentDataPath + streamRoot + "/Videos";
        if (!Directory.Exists(videoPath))
        {
            Directory.CreateDirectory(videoPath);
        }
        //int videoNum = Directory.GetFiles(videoPath, "*.mp4").Length;
        videoPath += "/hololens_video" + fileTime + ".mp4";
        AppController.debugAndLog("Saving videos to " + videoPath);
#endif

        if (logEyeGaze)
        {
            gazePath = Application.persistentDataPath + streamRoot + "/EyeGaze";
            if(!Directory.Exists(gazePath))
            {
                Directory.CreateDirectory(gazePath);
            }

            //int fileNum = Directory.GetFiles(gazePath, "*.csv").Length;
            gazePath += "/sensor_data" + fileTime + ".csv";

            AppController.debugAndLog("Saving files to " + gazePath);

            try
            {
                FileStream fs = File.Create(gazePath);

                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.WriteLine("Timestamp (MM:SS), 3D Gaze (x, y, z), Gaze in Viewport (x, y), Player Velocity (m/s)");
                }

                fs.Close();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                AppController.debugAndLog(e.Message);
            }
        }
    }

    /** <summary> Starts research mode device loops and creates log files. Runs when user exits setup mode. </summary> */
    public void StartSensorStreams()
    {
        logEyeGaze = CoreServices.InputSystem.EyeGazeProvider.IsEyeTrackingEnabledAndValid;
        AppController.debugAndLog("Log eye gaze: " + logEyeGaze);

        createLogFiles();

#if ENABLE_WINMD_SUPPORT
        if (useDepth)
        {
            researchMode.StartDepthSensorLoop(false);
        }
#endif
        lastPlayerPos = playerCamera.position;
        startStream = true;
        AppController.debugAndLog("Starting sensor stream");

        /* Start video recording */
#if ENABLE_WINMD_SUPPORT
        StartVideoCapture();
#endif
    }


    /** <summary> Stops research mode device loops. </summary> */
    public void StopSensorsEvent()
    {
        startStream = false;
        AppController.debugAndLog("Stopping sensor stream");

#if ENABLE_WINMD_SUPPORT
        m_VideoCapture.StopRecordingAsync(OnStoppedRecordingVideo);
#endif
    }

    /** <summary> 
     * Writes bytes to file called "image_[time].dat" asynchronously.
     * </summary>
     * 
     * <param name="data"> Data to write to file. </param>
     * <param name="filepath"> Path to create file in. </param>
     */
    private async Task WriteDepthMapData(byte[] data, string filepath)
    {
        /* File log name format: image-<seconds>_<milliseconds>.dat */
        string timeString = string.Format("{0:N2}", AppController.FullTimer);
        timeString = timeString.Replace('.', '_');
        
        /* Save depth sensor to folder on HoloLens */
        try
        {
            using (FileStream fs = File.Create(filepath + "/image-" + timeString + ".dat"))
            {
                await fs.WriteAsync(data, 0, data.Length);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            AppController.debugAndLog(e.Message);
        }
    }
    
    
    /** <summary> Converts ushort data to byte array. </summary> */
    private byte[] UshortToByteArray(ushort[] data)
    {
        byte[] byteArray = new byte[data.Length * 2];
        Buffer.BlockCopy(data, 0, byteArray, 0, byteArray.Length);
        return byteArray;
    }

    /** <summary> Saves short throw depth sensor data asynchronously. Sets lastDepthWriteComplete to true when done. </summary> */
    private async Task SaveShortDepthSensor()
    {
#if ENABLE_WINMD_SUPPORT
        //AppController.debugAndLog("Save short depth");
        ushort[] depthMap = researchMode.GetDepthMapBuffer();
        ushort[] AbImage = researchMode.GetShortAbImageBuffer();

        byte[] depthBytes = UshortToByteArray(depthMap);
        //AppController.debugAndLog("Before depth write.");
        await WriteDepthMapData(depthBytes, depthPath);

        /* Save active brightness sensor to folder on HoloLens */
        if(logAB) 
        {
            byte[] abBytes = UshortToByteArray(AbImage);
            await WriteDepthMapData(abBytes, abPath);
        }
        //AppController.debugAndLog("After depth write.");

        lastDepthWriteComplete = true;

        /* Zip folder into a .tar compressed file */
#endif
    }

    private void WriteSensorData()
    {
        /* Gaze in world coordinates. */
        Vector3 direction = CoreServices.InputSystem.EyeGazeProvider.GazeOrigin + CoreServices.InputSystem.EyeGazeProvider.GazeDirection;
        
        /* Gaze (x, y) on 2D screen .*/
        Vector3 screenDirection = playerCamera.GetComponent<Camera>().WorldToViewportPoint(direction);
        Vector2 gaze2D = new Vector2(screenDirection.x, screenDirection.y);

        /* Transform coordinates from world to head-space. */
        direction = playerCamera.InverseTransformDirection(direction);

        /* Get user's velocity from last 1 second. */
        Vector3 playerVelocity = (playerCamera.position - lastPlayerPos) / waitTime;
        playerVelocity = new Vector3(playerVelocity.x, 0, playerVelocity.z);
        float velocityMag = playerVelocity.magnitude;

        string line = AppController.getVideoTime() + ", " + direction.ToString() + ", " + gaze2D.ToString() + ", " + velocityMag.ToString();
        try
        {
            using (StreamWriter sw = new StreamWriter(gazePath, true))
            {
                sw.WriteLine(line);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            AppController.debugAndLog(e.Message);
        }

        lastPlayerPos = playerCamera.position;
    }

    void StartVideoCapture()
    {
#if ENABLE_WINMD_SUPPORT
        int resWidth = 896;
        int resHeight = 504;
        int desiredResolution = resWidth * resHeight;
        float desiredFPS = 15f;

        foreach (var r in VideoCapture.SupportedResolutions)
        {
            AppController.debugAndLog("Resolution: " + r.ToString());
        }

        //Resolution cameraResolution = VideoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
        Resolution cameraResolution = UnityEngine.Windows.WebCam.VideoCapture.SupportedResolutions.First((res) => (res.width * res.height) == desiredResolution);
        AppController.debugAndLog(cameraResolution.ToString());

        float cameraFramerate = UnityEngine.Windows.WebCam.VideoCapture.GetSupportedFrameRatesForResolution(cameraResolution).First((fps) => Mathf.Approximately(fps, desiredFPS));
        AppController.debugAndLog(cameraFramerate.ToString());

        UnityEngine.Windows.WebCam.VideoCapture.CreateAsync(true, delegate (UnityEngine.Windows.WebCam.VideoCapture videoCapture)
        {
            if (videoCapture != null)
            {
                m_VideoCapture = videoCapture;
                AppController.debugAndLog("Created VideoCapture Instance!");

                UnityEngine.Windows.WebCam.CameraParameters cameraParameters = new UnityEngine.Windows.WebCam.CameraParameters();
                cameraParameters.hologramOpacity = 1f;
                //cameraParameters.frameRate = fps;
                //cameraParameters.cameraResolutionWidth = resWidth;
                //cameraParameters.cameraResolutionHeight = resHeight;
                cameraParameters.frameRate = cameraFramerate;
                cameraParameters.cameraResolutionWidth = cameraResolution.width;
                cameraParameters.cameraResolutionHeight = cameraResolution.height;
                cameraParameters.pixelFormat = UnityEngine.Windows.WebCam.CapturePixelFormat.BGRA32;

                m_VideoCapture.StartVideoModeAsync(cameraParameters,
                                                   UnityEngine.Windows.WebCam.VideoCapture.AudioState.ApplicationAndMicAudio,
                                                   OnStartedVideoCaptureMode);
            }
            else
            {
                Debug.LogError("Failed to create VideoCapture Instance!");
                AppController.debugAndLog("Failed to create VideoCapture Instance!");
                
            }
        });
#endif
    }

    void OnStartedVideoCaptureMode(UnityEngine.Windows.WebCam.VideoCapture.VideoCaptureResult result)
    {
#if ENABLE_WINMD_SUPPORT
        AppController.debugAndLog("Started Video Capture Mode!");
        m_VideoCapture.StartRecordingAsync(videoPath, OnStartedRecordingVideo);
#endif
    }

    void OnStartedRecordingVideo(VideoCapture.VideoCaptureResult result)
    {
        AppController.debugAndLog("Started Recording Video!");
    }

    void OnStoppedRecordingVideo(UnityEngine.Windows.WebCam.VideoCapture.VideoCaptureResult result)
    {
#if ENABLE_WINMD_SUPPORT
        AppController.debugAndLog("Stopped Recording Video!");
        m_VideoCapture.StopVideoModeAsync(OnStoppedVideoCaptureMode);
#endif
    }

    void OnStoppedVideoCaptureMode(VideoCapture.VideoCaptureResult result)
    {
        AppController.debugAndLog("Stopped Video Capture Mode!");
    }
}