using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;


/** <summary> Book logic controller. </summary> */
public class BookController : MonoBehaviour
{
    [HideInInspector]
    public string filePath;

    /** <summary> Public variables used in other scripts. </summary> */
    public GameObject foundBook1Btn;

    /** <summary> Task and video timestamps for start of each book task. </summary> */
    public float StartTaskTime { get; set; }
    public string StartVideoTime { get; set; }

    /** <summary> Private Game Objects obtained from Unity editor. </summary> */
    /* Some of these aren't being used anymore, but did not remove unecessary variables. */
    [SerializeField]
    private GameObject textApp;
    [SerializeField]
    private GameObject book1App;
    [SerializeField]
    private GameObject book2App;
    [SerializeField]
    private GameObject libraryApp;
    [SerializeField]
    private Material textMaterial;
    [SerializeField]
    private Material book1Material;
    [SerializeField]
    private Material book2Material;
    [SerializeField]
    private Material libraryMaterial;
    [SerializeField]
    private Material placeholderMaterial1;
    [SerializeField]
    private Material placeholderMaterial2;
    [SerializeField]
    private Material placeholderLibraryApp;
    [SerializeField]
    private GameObject foundBook2Btn;
    [SerializeField]
    private GameObject foundFriendBtn;
    [SerializeField]
    private GameObject finishAppBtn;

    /** <summary> Used to determine when to show each confirmation button in menu. </summary> */
    private bool showBook2 = false;
    private bool showFoundFriend = false;
    private bool showFinishApp = false;

    /** <summary> App start logic. Randomizes which books are selected. </summary> */
    private void Start()
    {
        /* Used to randomize 2 out of 4 books, not doing this anymore. */

        StartTaskTime = 0f;
        StartVideoTime = AppController.getVideoTime();
    }


    /** <returns> Book log file name. </returns> */
    public string createLogFile()
    {
        // USING HOLOLENS
        string path = Application.persistentDataPath + "/Logs/Books";

        // USING LAPTOP
        //string path = "Assets/Resources/Logs/Adaptations";

        // Create directories
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        AppController.debugAndLog("Saving files to " + path);
        // Uncomment on actual study
        /* Use current number of CSV files in directory as number to use in file name. */
        //int numLogs = Directory.GetFiles(path, "*.csv").Length;
        //string fileName = "books_log" + numLogs + ".csv";
        //string fileName = "adaptations_log0.csv";

        DateTime now = DateTime.Now;
        string fileTime = string.Format("{0:MM-dd_HH-mm-ss}", now);
        string filePath = path + "/books_log" + fileTime + ".csv";
        //FileStream fs = File.OpenWrite(filePath);

        try
        {
            FileStream fs = File.Create(filePath);
            //FileStream fs = File.OpenWrite(filePath);

            using (StreamWriter sw = new StreamWriter(fs))
            {
                sw.WriteLine("Video time changed (MM:SS), Time changed (s), Book/Task found, Video time found (MM:SS), Time found (s)");
            }

            fs.Close();

            return filePath;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            AppController.debugAndLog(e.Message);
            return null;
        }
    }


    /** <summary> Logs when book found buttons are clicked. Attached to found book buttons in Unity </summary> */
    public void foundBooks(bool first)
    {
        MeshRenderer bookMesh;

        if (first)
        {
            bookMesh = book1App.GetComponent<MeshRenderer>();
            AppController.debugAndLog("Found book 1");
        }
        else
        {
            bookMesh = book2App.GetComponent<MeshRenderer>();
            MeshRenderer mesh = textApp.GetComponent<MeshRenderer>();
            mesh.material = textMaterial;
            /* Play text message sound for next task. */
            GetComponents<AudioSource>()[0].Play();
            AppController.debugAndLog("Found book 2");
        }

        string bookName = bookMesh.material.name;
        string logLine = StartVideoTime + ", " + StartTaskTime.ToString() + ", " + bookName + ", " + AppController.getVideoTime() +
                         ", " + AppController.TaskTimer.ToString();
        using (StreamWriter sw = new StreamWriter(filePath, true))
        {
            sw.WriteLine(logLine);
        }
            
        StartVideoTime = AppController.getVideoTime();
        StartTaskTime = AppController.TaskTimer;
    }


    public void foundFriend()
    {
        string logLine = StartVideoTime + ", " + StartTaskTime.ToString() + ", Found Friend, " + AppController.getVideoTime() +
                         ", " + AppController.TaskTimer.ToString();
        using (StreamWriter sw = new StreamWriter(filePath, true))
        {
            sw.WriteLine(logLine);
        }
        AppController.debugAndLog("Found friend");
    }


    /** <summary> Condition for when to show book 2 button, used in AppController. </summary> */
    public void showFoundBookButtons()
    {
        /* Book 1 was found, make book 2 button visible if not already found too. */
        if (!foundBook1Btn.activeSelf && !showBook2)
        {
            foundBook2Btn.SetActive(true);
            showBook2 = true;
        }

        /* Book 2 was found, make the found friend button visible if not already found too. */
        if(!foundBook1Btn.activeSelf && !foundBook2Btn.activeSelf && !showFoundFriend)
        {
            foundFriendBtn.SetActive(true);
            showFoundFriend = true;
        }

        /* Finished finding friend, show finished app button. */ 
        if(!foundBook1Btn.activeSelf && !foundBook2Btn.activeSelf && !foundFriendBtn.activeSelf && !showFinishApp)
        {
            finishAppBtn.SetActive(true);
            showFinishApp = true;
        }
    }


    /** <summary> Set books to placeholders if in edit, or real if not in edit mode. Called in AppController. </summary> */
    public void setBookCovers(bool inEditMode)
    {
        MeshRenderer mesh1 = book1App.GetComponent<MeshRenderer>();
        float alpha1 = mesh1.material.color.a; 
        MeshRenderer mesh2 = book2App.GetComponent<MeshRenderer>();
        float alpha2 = mesh2.material.color.a; 
        MeshRenderer libMesh = libraryApp.GetComponent<MeshRenderer>();
        float alphaMap = libMesh.material.color.a; 

        /* If in edit mode, use placeholder books. */
        if (inEditMode)
        {
            mesh1.material = placeholderMaterial1;
            mesh2.material = placeholderMaterial2;
            libMesh.material = placeholderLibraryApp;
        } 
        /* If not in edit mode, use real books. */
        else
        {
            mesh1.material = book1Material;
            mesh2.material = book2Material;
            libMesh.material = libraryMaterial;
        }

        /* Ensure transparency doesnt change when material changes. */
        Color c1 = mesh1.material.color;
        c1.a = alpha1;
        mesh1.material.color = c1;

        Color c2 = mesh2.material.color;
        c2.a = alpha2;
        mesh2.material.color = c2;

        Color c3 = libMesh.material.color;
        c3.a = alphaMap;
        libMesh.material.color = c3;
    }

    /** <summary> Set book app title from material name. </summary> */
    /* Not used anymore
    private void setBookLabels()
    {
        MeshRenderer mesh1 = book1App.GetComponent<MeshRenderer>();
        MeshRenderer mesh2 = book2App.GetComponent<MeshRenderer>();
        string book1Name = mesh1.material.name;
        string book2Name = mesh2.material.name;
        book1Label.SetText("Book1: " + book1Name);
        book2Label.SetText("Book2: " + book2Name);
    }
    */

    /** <summary> Randomize which books (chooses 2 of 4) are used. Ensures both books aren't same. </summary> */
    /* Not used anymore
    public void randomizeBooks()
    {
        int BOOKS = 4;
        var random = new System.Random();
        int num1 = random.Next(BOOKS);
        int num2 = random.Next(BOOKS);
        while(num2 == num1)
        {
            num2 = random.Next(BOOKS);
        }

        MeshRenderer mesh1 = book1App.GetComponent<MeshRenderer>();
        MeshRenderer mesh2 = book2App.GetComponent<MeshRenderer>();
        mesh1.material = books[num1];
        mesh2.material = books[num2];
        setBookLabels();
    }
    */
}
