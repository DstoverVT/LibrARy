using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_WINMD_SUPPORT
using Windows.Foundation.Diagnostics;
#endif

/**
 * <summary>
 * Overall control logic for application.
 * </summary>
 */
public class AppController : MonoBehaviour
{
#if ENABLE_WINMD_SUPPORT
    public static LoggingChannel lc;
#endif

    /**
     * <summary>
     * Enum to keep track of app frame of reference options. 
     * Unopened is for apps that have not been opened yet.
     * None is for apps that been opened but reference has not been selected.
     * Head, Body, and World are user-selected references.
     * </summary>
     */
    public enum Reference
    {
        Unopened,
        None,
        Head,
        Body,
        World
    }
    /** <summary> Currently includes: book 1, book 2, stocks, library map, and text messages. </summary> */
    private const int APPS = 5;

    /** <summary> Last player position and forward direction for body fixed calculation. </summary> */
    private Vector3 lastPlayerPos;
    private Vector3 currPlayerForward;
    /** <summary> Threshold for when a change of direction is detected. </summary> */
    private const float DIRECTION_THRESHOLD = 0.15f;
    private const float UPDATE_TIME = 0.2f;
    private float directionTime = 0f;

    /** <summary> Array holding all apps. </summary> */
    private GameObject[] apps;

    /** <summary> Key is app name, Value is app settings object containing app's adaptations to be logged. </summary> */
    Dictionary<string, AppSettings> initialSettings = new Dictionary<string, AppSettings>();

    /** <summary> Variables for app logic. </summary> */
    private string filePath;
    public static bool inEditMode;
    private bool setupMode;

    /** <summary> Game objects that will be stored by this class. </summary> */
    private GameObject handMenu;
    private GameObject minimizeMenu;
    private GameObject stockButtons;
    private GameObject bodyDialog;
    private BookController bookController;

    /** <summary> Game objects that need to be populated by Unity editor. </summary> */
    [SerializeField]
    private GameObject playerCamera;

    /** <summary> Task timer pauses when in edit mode, full timer is synced with video. </summary> */
    public static float TaskTimer { get; set; }
    public static float FullTimer { get; set; }

    public static string getVideoTime()
    {
        int minutes = Mathf.FloorToInt(FullTimer / 60f);
        int seconds = Mathf.FloorToInt(FullTimer - (minutes * 60f));

        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public static void debugAndLog(string msg)
    {
        Debug.Log(msg);
#if ENABLE_WINMD_SUPPORT
        lc.LogMessage(msg);
#endif
    }


    /**
     * <summary> Runs on startup of app, before any other logic. </summary>
     */
    void Start()
    {
        /* Add all apps to app array by using fact that all are children of "Apps" GameObject. */
        apps = new GameObject[APPS];
        Transform appParent = GameObject.Find("Apps").transform;
        int appNum = 0;
        foreach(Transform child in appParent)
        {
            apps[appNum] = child.gameObject;
            apps[appNum].GetComponent<AdaptationsController>().enabled = true;
            appNum++;
        }

        /* Initialize private Game Objects from Unity. */
        minimizeMenu = GameObject.Find("MinimizeMenu");
        handMenu = GameObject.Find("HandMenu");
        handMenu.SetActive(false);
        stockButtons = GameObject.Find("Stocks/Buttons");
        bookController = GameObject.Find("AppController").GetComponent<BookController>();
        bodyDialog = GameObject.Find("BodyDialog");
        bodyDialog.SetActive(false);

#if ENABLE_WINMD_SUPPORT
        lc = new LoggingChannel("UnityLogger", null, new Guid("2df964bb-cd29-4ac0-a462-59b4c484ae3d"));
#endif

        setupMode = true;
        appSetup();
    }


    private void createAllLogFiles()
    {
        filePath = createLogFile();

        StockController stock = GetComponent<StockController>();
        stock.logFile = stock.createLogFile();

        BookController book = GetComponent<BookController>();
        book.filePath = book.createLogFile();
    }


    /**
     * <summary> App setup. Disables all apps. </summary>
     */
    private void appSetup()
    {
        foreach (GameObject app in apps)
        {
            app.SetActive(false);
        }
        editClicked(true);
    }


    /** <summary> Disable setup mode. </summary> */
    public void finishSetup()
    {
        bookController.foundBook1Btn.SetActive(true);
        lastPlayerPos = playerCamera.transform.position;

        /* Start collecting depth and eye gaze data. */
        GetComponent<SensorStreamController>().StartSensorStreams();

        setupMode = false;
    }


    /** <summary> Update method runs in a loop during application. </summary> */
    private void Update()
    {
        if(!setupMode)
        {
            /* Update task timer when not in setup mode and edit mode */
            if (!inEditMode)
            {
                TaskTimer += Time.deltaTime;
            }
            /* Update full timer when not in setup mode */
            FullTimer += Time.deltaTime;
        }

        /* Calculate direction vector of player's movement. */
        if (!setupMode && !inEditMode)
        {
            directionTime += Time.deltaTime;

            /* Only check for change of direction every UPDATE_TIME seconds. */
            if(directionTime >= UPDATE_TIME)
            {
                directionTime -= UPDATE_TIME;

                Vector3 newDirection = this.playerCamera.transform.position - lastPlayerPos;
                /* This assumes world Y-axis points up (perpendicular to ground), let's hope so. */
                /* Only detect change in ground plane (XZ). */
                newDirection = new Vector3(newDirection.x, 0, newDirection.z);
                //Vector3 newDirectionXZ = newDirection;
                //newDirectionXZ.y = 0;

                /* Only check for body fixed apps if player is moving (not in Y-direction though). */
                const float WALKING_THRESHOLD = 0.8f;
                if (newDirection.magnitude > WALKING_THRESHOLD)
                {
                    handleDirectionChange(newDirection);
                }
            }
        }
    }


    /** <summary> 
     * Calculate similarity in direction of player's current direction and new change in position. 
     * If the similarity is below a threshold, then update the player's direction.
     * Uses dot product result to determine if the player has changed direction based on a threshold t.
     * (dot(v_last, v_new) < (1 - t) --> change in direction).
     * </summary> */
    private void handleDirectionChange(Vector3 newDirection)
    {
        /* Dot product of unit vectors (1: same direction, 0: perpendicular, -1: opposite direction). */
        float similarity = Vector3.Dot(newDirection.normalized, currPlayerForward.normalized);

        /* Dot product similarity with threshold. */
        if (similarity < (1 - DIRECTION_THRESHOLD))
        {
            currPlayerForward = newDirection.normalized;
            recenterBodyApps(currPlayerForward, Vector3.up);
        }

        lastPlayerPos = playerCamera.transform.position;
    }


    /**
     * <summary>
     * Opens app that is clicked. Uses app's current state to determine postition for startup.
     * This function is linked as a Unity event in the Unity editor upon button press in minimization menu.
     * </summary>
     * 
     * <param name="clickedApp"> Game Object of app that is clicked. </param>
     */
    public void minimizeAppClicked(GameObject clickedApp)
    {
        /* Place app in front of camera if it has already been open. World fixes since will break old location. */
        if (clickedApp.activeSelf)
        {
            changeWorldFixed(clickedApp);
            resetAppPlacement(clickedApp);
        }

        if (getFrameOfReference(clickedApp) == Reference.Unopened)
        {
            AdaptationsController ac = clickedApp.GetComponent<AdaptationsController>();
            ac.appOpened();
            resetAppPlacement(clickedApp);
            clickedApp.SetActive(true);
        }
    }


    /** <summary> 
     * Places an app directly in front of the camera.
     * Used for position of app on startup
     * Also called when an open app is clicked in the app menu (to summon a lost app).
     * </summary>
     */
    private void resetAppPlacement(GameObject app)
    {
        Vector3 camForward = playerCamera.transform.forward;
        app.transform.position = playerCamera.transform.position + camForward;
        app.transform.rotation = Quaternion.LookRotation(camForward, playerCamera.transform.up);
    }


    /**
     * <summary> Checks that all apps are have been placed on startup. </summary>
     * 
     * <returns> True if all apps have been placed, else False. </returns>
     */
    private bool checkAllAppsPlaced()
    {
        //return true;

        foreach (GameObject app in apps)
        {
            // App was not configured and is not minimized
            if(getFrameOfReference(app) == Reference.Unopened || 
               (app.activeSelf && getFrameOfReference(app) == Reference.None))
            {
                return false;
            }
        }

        return true;
    }


    /**
     * <summary> Logic for when closing minimization menu. </summary>
     */
    public void minimizeDoneClicked()
    {
        if (setupMode)
        {
            /* Ensures that all apps have been placed before starting. */
            if (checkAllAppsPlaced())
            {
                minimizeMenu.SetActive(false);
                bodyDialog.SetActive(true);     
            }
            else
            {
                debugAndLog("Place all apps!");
            }
        }
        else
        {
            minimizeMenu.SetActive(false);
            bodyDialog.SetActive(true);
            bookController.showFoundBookButtons();
        }
    }


    /** <summary> Run when body dialog confirm button is clicked, completes edit mode logic. </summary> */
    public void bodyDialogConfirmed()
    {
        if (setupMode)
        {
            createAllLogFiles();
        }

        editClicked(false);
        storeBodyAppsInitial();

        if (setupMode)
        {
            finishSetup();
        }

        bodyDialog.SetActive(false);

        debugAndLog("Body apps calculated.");
    }


    /**
     * <summary>
     * Logic to enable/disable edit mode.
     * </summary>
     */
    public void editClicked(bool enable)
    {
        inEditMode = enable;
        stockButtons.SetActive(!enable);

        bookController.setBookCovers(enable);

        foreach (GameObject app in apps)
        {
            AdaptationsController ac = app.GetComponent<AdaptationsController>();
            ac.enableMovement(enable);

            /* Enables edit menu around apps. */
            app.transform.Find("Adaptations").gameObject.SetActive(enable);
        }

        minimizeMenu.SetActive(enable);
        handMenu.SetActive(!enable);
        
        if(enable)
        {
            // Entering edit mode (or setup mode)
            storeInitialAppSettings();
        }
        else
        {
            // Leaving edit mode
            logSettingChanges();
        }
    }


    /**
     * <summary> 
     * Calculates app position to be logged. 
     * Returns coordinates relative to camera's axis.
     * </summary>
     * 
     * <param name="FoR"> App's frame of reference. </param>
     * <param name="app"> App object to calculate position for. </param>
     * 
     * <returns> Vector3 of app's position to log. </returns>
     */
    private Vector3 calculateAppPosition(Reference FoR, Transform app)
    {        
        //if(FoR == Reference.World || FoR == Reference.Unopened || FoR == Reference.None )
        //{
        //    // Position in world coordinates
        //    return app.position;
        //}
        // Position of app in camera coordinate system
        return playerCamera.transform.InverseTransformVector(app.position - playerCamera.transform.position);
    }


    /**
     * <summary> Maps angle to stay within -180 and 180 degrees. </summary>
     * 
     * <returns> Angle in degrees. </returns>
     */
    private float remapAngle(float anglePos)
    {
        if(anglePos > 180f)
        {
            return (anglePos - 360f);
        }
        return anglePos;
    }


    /**
     * <summary>
     * Calculates app's rotation to be logged.
     * Rotation is with respect to camera's axes.
     * </summary>
     * 
     * <param name="FoR"> App's frame of reference. </param>
     * <param name="app"> App to calculate rotation for. </param>
     * 
     * <returns> Vector3 of app's calculated rotation. </returns>
     */
    private Vector3 calculateAppRotation(Reference FoR, Transform app)
    {
        //if(FoR == Reference.World || FoR == Reference.Unopened || FoR == Reference.None )
        //{
        //    Vector3 appRot = app.rotation.eulerAngles;
        //    return (new Vector3(remapAngle(appRot.x), remapAngle(appRot.y), remapAngle(appRot.z)));
        //}
        /* Get world rotation of camera. */
        Vector3 camAngle = Quaternion.LookRotation(playerCamera.transform.forward, playerCamera.transform.up).eulerAngles;
        camAngle = new Vector3(remapAngle(camAngle.x), remapAngle(camAngle.y), remapAngle(camAngle.z));
        /* Get world rotation of app. */
        Vector3 appAngle = app.rotation.eulerAngles;
        appAngle = new Vector3(remapAngle(appAngle.x), remapAngle(appAngle.y), remapAngle(appAngle.z));
        /* Find app angle relative to camera's angle. */
        return appAngle - camAngle;
    }


    /**
     * <summary> Clears previous settings and stores initial app settings. </summary>
     */
    private void storeInitialAppSettings()
    {
        initialSettings.Clear();
        foreach (GameObject app in apps)
        {
            AppSettings appSettings = new AppSettings(
                calculateAppPosition(getFrameOfReference(app), app.transform),
                calculateAppRotation(getFrameOfReference(app), app.transform),
                app.transform.localScale,
                getTransparency(app),
                getFrameOfReference(app),
                app.activeSelf
            );
            initialSettings.Add(app.name, appSettings);
        }
    }


    /**
     * <summary> Log app settings that are different from initial settings. </summary>
     */
    private void logSettingChanges()
    {
        foreach(GameObject app in apps)
        {
            AppSettings beforeSettings = initialSettings[app.name];
            /* Store new app state after edit mode. */
            AppSettings finalSettings = new AppSettings(
                calculateAppPosition(getFrameOfReference(app), app.transform),
                calculateAppRotation(getFrameOfReference(app), app.transform),
                app.transform.localScale,
                getTransparency(app),
                getFrameOfReference(app),
                app.activeSelf
            );

            /* Make sure to log all apps during setup mode. */
            /* Otherwise, don't log app if nothing changed. */
            if(beforeSettings.Equals(finalSettings) && !setupMode)
            {
                continue;
            }
            else
            {
                /* Don't log adaptations if app is still not visibile unless in setup mode. */
                if (!finalSettings.Visibility && !beforeSettings.Visibility && !setupMode)
                {
                    continue;
                }
                /* Log only changed adaptations. */
                logAppSettings(app.name, beforeSettings, finalSettings);
            }
        }
    }


    /**
     * <returns> 
     * Log file name.
     * File name has a number that increases automatically to not overwrite previous files.
     * </returns>
     */
    private string createLogFile()
    {
        /*
         * Laptop persistent path: "C:\Users\<user>\AppData\LocalLow\DefaultCompany\LibrARy"
         * Hololens persistent path: "LocalAppData\LibrARy\LocalState"
         */
        string path = Application.persistentDataPath + "/Logs/Adaptations";

        // USING LAPTOP
        //string path = "Assets/Resources/Logs/Adaptations";

        // Create directories
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        debugAndLog("Saving files to " + path);
        /* Use current number of CSV files in directory as number to use in file name. */
        //int numLogs = Directory.GetFiles(path, "*.csv").Length;
        //string fileName = "adaptations_log" + numLogs + ".csv";
        //string fileName = "adaptations_log0.csv";

        DateTime now = DateTime.Now;
        string fileTime = string.Format("{0:MM-dd_HH-mm-ss}", now);
        string filePath = path + "/adaptations_log" + fileTime + ".csv";
        //FileStream fs = File.OpenWrite(filePath);

        try
        {
            FileStream fs = File.Create(filePath);
            //FileStream fs = File.OpenWrite(filePath);

            using (StreamWriter sw = new StreamWriter(fs))
            {
                sw.WriteLine("Video Timestamp (MM:SS), Task Timestamp (s), App Name, Frame of Reference, Position (x, y, z), Local Scale (x, y, z), Rotation (x, y, z), Transparency, Visible");
            }

            fs.Close();

            return filePath;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            debugAndLog(e.Message);
            return null;
        }
    }


    /**
     * <summary> Logs app settings that have changed to adaptations CSV file. </summary>
     * 
     * <param name="appName"> App name of app to log. </param>
     * <param name="initial"> Initial app settings to compare to. </param>
     * <param name="final"> Final app settings. </param>
     */
    private void logAppSettings(string appName, AppSettings initial, AppSettings final)
    {
        string logLine = "";
        logLine += getVideoTime(); 
        logLine += ", " + TaskTimer.ToString() + ", " + appName;

        /* Log only the visibility if app changes visibility from true -> false. */
        if(!final.Visibility)
        {
            final = initial;
            final.Visibility = false;
        }

        // Log changed adaptations for each app
        if(final.FrameOfRef != initial.FrameOfRef)
        {
            logLine += ", " + Enum.GetName(typeof(Reference), final.FrameOfRef);
            debugAndLog(appName + ": " + final.FrameOfRef.ToString());
        }
        else
        {
            logLine += ", ";
        }
        if(final.Position != initial.Position)
        {
            logLine += ", " + final.Position.ToString();
        }
        else
        {
            logLine += ", ";
        }
        /* Always log scale if app is configured for first time (frame of reference goes from None/Unopened -> other) */
        bool openedFirstTime = ((initial.FrameOfRef == Reference.None || initial.FrameOfRef == Reference.Unopened) &&
                                (final.FrameOfRef != Reference.Unopened && final.FrameOfRef != Reference.None));
        if (final.Scale != initial.Scale || openedFirstTime)
        {
            logLine += ", " + final.Scale.ToString();
        }
        else
        {
            logLine += ", ";
        }
        if (final.Rotation != initial.Rotation)
        {
            logLine += ", " + final.Rotation.ToString();
        }
        else
        {
            logLine += ", ";
        }
        if (final.Transparency != initial.Transparency)
        {
            logLine += ", " + final.Transparency.ToString();
        }
        else
        {
            logLine += ", ";
        }
        if (final.Visibility != initial.Visibility || !final.Visibility)
        {
            logLine += ", " + final.Visibility.ToString();
        }
        else
        {
            logLine += ", ";
        }

        using (StreamWriter sw = new StreamWriter(filePath, true))
        {
            sw.WriteLine(logLine);
        }
    }


    /** <returns> Transparency of app as a float, where 0 is transparenct and 1 is opaque. </returns> */
    private float getTransparency(GameObject app)
    {
        return app.GetComponent<AdaptationsController>().getTransparency();
    }


    /** <returns> Frame of reference of app. </returns> */
    private Reference getFrameOfReference(GameObject app)
    {
        return (Reference)app.GetComponent<AdaptationsController>().CurrentReference;
    }


    /** <summary> Set app to head-fixed. </summary> */
    public void changeHeadFixed(GameObject app)
    {
        AdaptationsController ac = app.GetComponent<AdaptationsController>();
        ac.headFixed();
    }


    /** <summary> Set app to body-fixed. </summary> */
    public void changeBodyFixed(GameObject app)
    {
        AdaptationsController ac = app.GetComponent<AdaptationsController>();
        ac.bodyFixed();
    }


    /** <summary> Set app to world-fixed. </summary> */
    public void changeWorldFixed(GameObject app)
    {
        AdaptationsController ac = app.GetComponent<AdaptationsController>();
        ac.worldFixed();
    }


    /**
     * <summary>
     * Settings of an app's adaptations, which includes the app's:
     * Position, rotation, scale, transparency, frame of reference, visiblity
     * </summary>
     */
    private class AppSettings
    {
        // App's position relative to the camera coordinate system
        public Vector3 Position { get; set; }
        // App's rotation relative to the camera coordinate system
        public Vector3 Rotation { get; set; }
        // App's scale in Unity units (meters)
        public Vector3 Scale { get; set; }
        // App's transparency (0 is transparent, 1 is opaque)
        public float Transparency { get; set; }
        // App's frame of reference (head, world, or body fixed)
        public Reference FrameOfRef { get; set; }
        // App's visibility (true if minimized)
        public bool Visibility { get; set; }


        public AppSettings(Vector3 position, Vector3 rotation, Vector3 scale, float transparency, Reference frameOfRef, bool visibility)
        {
            Position = position;
            Rotation = rotation; 
            Scale = scale;
            Transparency = transparency;
            FrameOfRef = frameOfRef;
            Visibility = visibility;
        }


        /** <returns> True if all settings are equal, else False. </returns> */
        public bool Equals(AppSettings a)
        {
            return ((Position == a.Position) &&
                    (Scale == a.Scale) &&
                    (Transparency == a.Transparency) &&
                    (FrameOfRef == a.FrameOfRef) &&
                    (Visibility == a.Visibility));
        }


        public void printAppSettings()
        {
            debugAndLog("Position: " + Position);
            debugAndLog("Rotation: " + Rotation);
            debugAndLog("Scale: " + Scale);
            debugAndLog("Transparency: " + Transparency);
            debugAndLog("Visibility: " + Position);
        }
    }


    /** <summary> 
     * Store initial position used for body-fixed calculation. 
     * Called when edit mode is closed.
     * </summary> 
     */
    public void storeBodyAppsInitial()
    {
        foreach (GameObject app in apps)
        {
            AdaptationsController ac = app.GetComponent<AdaptationsController>();
            if(getFrameOfReference(app) == Reference.Body)
            {
                ac.InitialBodyPos = calculateAppPosition(Reference.Body, app.transform);
                ac.InitialBodyRot = calculateAppRotation(Reference.Body, app.transform);
            }
        }
        currPlayerForward = playerCamera.transform.forward;
    }


    /**
     * <summary>
     * Recenters body-fixed apps based on calculated position.
     * </summary> 
     */
    private void recenterBodyApps(Vector3 newForward, Vector3 newUp)
    {
        if (!setupMode && !inEditMode)
        {
            foreach (GameObject app in apps)
            {
                AdaptationsController ac = app.GetComponent<AdaptationsController>();
                if (getFrameOfReference(app) == Reference.Body)
                {
                    ac.recenterBody(newForward, newUp);
                }
            }
        }
    }

    /** <summary> Called through Unity voice command "center" when not in edit mode. </summary> */
    public void recenterVoiceCommand()
    {
        recenterBodyApps(playerCamera.transform.forward, playerCamera.transform.up);
    }

    public void finishApp()
    {
        GetComponent<SensorStreamController>().StopSensorsEvent();
    }
}
