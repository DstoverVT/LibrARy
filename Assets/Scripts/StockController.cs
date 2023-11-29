using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;


/** <summary> Stock app logic controller. </summary> */
public class StockController : MonoBehaviour
{
    /** <summary> Keep track of total accumulated stock price. </summary> */
    private float totalPrice = 0f;

    /** <summary> Private data from Unity scene </summary> */
    [SerializeField]
    private GameObject appleLabel;
    [SerializeField]
    private GameObject msftLabel;
    [SerializeField]
    private GameObject totalPriceLabel;

    private AudioSource stockAudio;

    /** <summary> Stock data </summary> */
    private List<float> AAPLprices;
    private List<float> MSFTprices;
    private string appleText;
    private string msftText;
    private int priceCount = 0;
    private int numPrices;

    /** <summary> Timing data </summary> */
    private float waitTime;
    private float time = 0.0f;
    private int lowChangeThreshold = 40;  // Seconds
    private int highChangeThreshold = 60; // Seconds

    /** <summary> Button action logging data </summary> */
    private bool appleIncrease = false;
    private bool msftIncrease = false;
    private bool appleCorrect = false;
    private bool msftCorrect = false;
    private float valuesChangeTime = -1f;
    private string videoChangeTime;
    private float appleClickTime = -1f;
    private float msftClickTime = -1f;

    public string logFile { get; set; }

    /** <summary> Class used to store stock prices when parsing JSON files. </summary> */
    [System.Serializable]
    public class StockData
    {
        public List<float> prices1 { get; set; }
    }

    /** <summary> Logic on startup. </summary> */
    private void Start()
    {
        string AAPLpath = "Data/aapl_stock_dec2019";
        string MSFTpath = "Data/msft_stock_dec2019";
        var AAPLjson = Resources.Load<TextAsset>(AAPLpath).ToString();
        var MSFTjson = Resources.Load<TextAsset>(MSFTpath).ToString();

        if (AAPLjson == null || MSFTjson == null)
        {
            AppController.debugAndLog("Stock prices could not be loaded from Resources");
            return;
        }

        // Between 30 and 45 seconds
        waitTime = UnityEngine.Random.Range(lowChangeThreshold, highChangeThreshold);

        AAPLprices = JsonConvert.DeserializeObject<StockData>(AAPLjson).prices1;
        MSFTprices = JsonConvert.DeserializeObject<StockData>(MSFTjson).prices1;
        numPrices = AAPLprices.Count;
        appleText = AAPLprices[priceCount].ToString();
        msftText = MSFTprices[priceCount].ToString();
        priceCount = ++priceCount % numPrices;

        // Update UI with data
        appleLabel.GetComponent<TextMeshPro>().SetText(appleText);
        msftLabel.GetComponent<TextMeshPro>().SetText(msftText);

        stockAudio = GetComponents<AudioSource>()[1];
    }


    public string createLogFile()
    {
        // USING HOLOLENS
        string path = Application.persistentDataPath + "/Logs/Stocks";

        // USING LAPTOP
        //string path = "Assets/Resources/Logs/Stocks";

        // Create directories
        if(!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        AppController.debugAndLog("Saving files to " + path);
        // Uncomment on actual study
        /* Use current number of CSV files in directory as number to use in file name. */
        //int numLogs = Directory.GetFiles(path, "*.csv").Length;
        //string fileName = "stock_log" + numLogs + ".csv";
        //string fileName = "stock_log0.csv";

        DateTime now = DateTime.Now;
        string fileTime = string.Format("{0:MM-dd_HH-mm-ss}", now);
        string filePath = path + "/stock_log" + fileTime + ".csv";

        try
        {
            FileStream fs = File.Create(filePath);
            //FileStream fs = File.OpenWrite(filePath);

            // Uncomment on actual study
            using (StreamWriter sw = new StreamWriter(fs))
            {
                sw.WriteLine("Video time changed (MM:SS), Time changed (s), Apple correct (T/F), Apple time clicked (s), MSFT correct (T/F), MSFT time clicked (s)");
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


    /**
     * <summary> 
     * Changes color of price label based on price change. 
     * Sets either Apple or MSFT price label green (if increased) or red (if decreased)
     * </summary>
     */
    public void changeColor(bool isApple, bool increase)
    {
        GameObject label = isApple ? appleLabel : msftLabel;

        if(increase)
        {
            // Custom green
            label.GetComponent<TextMeshPro>().color = new Color(.16f, .53f, .26f, 1f);
        }
        else
        {
            // Red
            label.GetComponent<TextMeshPro>().color = Color.red;
        }
    }


    /** <summary> 
     *  Logic called in a loop during execution. 
     *  Updates stock prices when timer expires and checks user's correctness.
     *  </summary> */
    void Update()
    { 
        // Only update time if not in edit mode
        if (!AppController.inEditMode)
        {
            time += Time.deltaTime;
        }

        // Timer expired
        if(time > waitTime)
        {
            // User did not click stock app
            if(appleClickTime < 0f)
            {
                appleClickTime = AppController.TaskTimer;
            }
            if(msftClickTime < 0f)
            {
                msftClickTime = AppController.TaskTimer;
            }

            time = time - waitTime;
            // Reset timer with new wait time
            waitTime = UnityEngine.Random.Range(lowChangeThreshold, highChangeThreshold);

            appleText = AAPLprices[priceCount].ToString();
            msftText = MSFTprices[priceCount].ToString();

            // Check correct variables from last iteration
            if (priceCount > 1)
            {
                // Log
                string logTest = videoChangeTime + ", " + valuesChangeTime.ToString() + ", " + appleCorrect + ", " + appleClickTime + ", " + 
                                 msftCorrect + ", " + msftClickTime;

                using (StreamWriter sw = new StreamWriter(logFile, true))
                {
                    sw.WriteLine(logTest);
                }

                // Reset stock click time
                appleClickTime = -1f;
                msftClickTime = -1f;

                if(!appleCorrect)
                {
                    totalPrice += -AAPLprices[priceCount];
                }
                if(!msftCorrect)
                {
                    totalPrice += -MSFTprices[priceCount];
                }
                totalPriceLabel.GetComponent<TextMeshPro>().SetText($"<b>${totalPrice.ToString()}</b>");
                AppController.debugAndLog("Total: " + totalPrice.ToString());
            }

            // Update values for next iteration
            // Determine increase or decrease
            appleIncrease = (AAPLprices[priceCount] > AAPLprices[priceCount - 1]);
            msftIncrease = (MSFTprices[priceCount] > MSFTprices[priceCount - 1]);

            // Reset test
            appleCorrect = false;
            msftCorrect = false;
            valuesChangeTime = AppController.TaskTimer;
            videoChangeTime = AppController.getVideoTime();

            changeColor(true, appleIncrease);
            changeColor(false, msftIncrease);
            appleLabel.GetComponent<TextMeshPro>().SetText($"<b>{appleText}</b>");
            msftLabel.GetComponent<TextMeshPro>().SetText($"<b>{msftText}</b>");

            /* Sound effect plays to tell user that stocks are changing. */
            stockAudio.Play();

            // Update iteration
            priceCount = ++priceCount % numPrices;
        }
    }

    
    /** <summary> Buy button is clicked (Attached as Unity event). </summary> */
    public void buyClicked(bool isApple)
    {
        // Apple
        if(isApple)
        {
            if (!appleCorrect)
            {
                appleClickTime = AppController.TaskTimer;
                appleCorrect = !appleIncrease;
                /* When user makes decision, price text goes black for feedback if they are correct. */
                if (appleCorrect)
                {
                    TextMeshPro appleTMP = appleLabel.GetComponent<TextMeshPro>();
                    appleTMP.color = Color.black;
                    totalPrice += AAPLprices[priceCount];
                }
            }
        }
        // MSFT
        else
        {
            if (!msftCorrect)
            {
                msftClickTime = AppController.TaskTimer;
                msftCorrect = !msftIncrease;
                /* When user makes decision, price text goes black for feedback if they are correct. */
                if (msftCorrect)
                {
                    TextMeshPro msftTMP = msftLabel.GetComponent<TextMeshPro>();
                    msftTMP.color = Color.black;
                    totalPrice += MSFTprices[priceCount];
                }
            }
        }
        totalPriceLabel.GetComponent<TextMeshPro>().SetText($"<b>${totalPrice.ToString()}</b>");
    }


    /** <summary> Sell button is clicked (Attached as Unity event). </summary> */
    public void sellClicked(bool isApple)
    {
        // Apple
        if (isApple)
        {
            if (!appleCorrect)
            {
                appleClickTime = AppController.TaskTimer;
                appleCorrect = appleIncrease;
                /* When user makes decision, price text goes black for feedback if they are correct. */
                if (appleCorrect)
                {
                    TextMeshPro appleTMP = appleLabel.GetComponent<TextMeshPro>();
                    appleTMP.color = Color.black;
                    totalPrice += AAPLprices[priceCount];
                }
            }
        }
        // MSFT
        else
        {
            if (!msftCorrect)
            {
                msftClickTime = AppController.TaskTimer;
                msftCorrect = msftIncrease;
                /* When user makes decision, price text goes black for feedback if they are correct. */
                if (msftCorrect)
                {
                    TextMeshPro msftTMP = msftLabel.GetComponent<TextMeshPro>();
                    msftTMP.color = Color.black;
                    totalPrice += MSFTprices[priceCount];
                }
            }
        }
        totalPriceLabel.GetComponent<TextMeshPro>().SetText($"<b>${totalPrice.ToString()}</b>");
    }
}
