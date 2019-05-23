// Copyright RoSchmi 2019 License Apache 2.0
// Version 1.0.2 23.05.2019
// App to write sensor data to Azure Storage Table service
// For TinyCLR Board FEZ with SPWF04Sx Wifi module

// With #define UseTestValues you can select if data are read from sensors or if simulated data (sinus curves) are used

#define UseTestValues

#region Using directives
using System;
using System.Collections;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.Spi;
using GHIElectronics.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Devices.Adc;
using RoSchmi.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx;
using RoSchmi.Net.Azure.Storage;
using RoSchmi.DayLightSavingTime;
using GHIElectronics.TinyCLR.Native;
using PervasiveDigital.Utilities;
using AzureDataSender.Models;
using AzureDataSender;
#endregion


namespace AzureDataSender_FEZ
{
    class Program
    {
        #region Region Fields

        //****************  Settings to be changed by user   ********************************* 

        private static AzureStorageHelper.DebugMode _debug = AzureStorageHelper.DebugMode.StandardDebug;
        private static AzureStorageHelper.DebugLevel _debug_level = AzureStorageHelper.DebugLevel.DebugAll;
             
        //private static int timeZoneOffset = 60;      // Berlin offest in minutes of your timezone to Greenwich Mean Time (GMT)
        private static int timeZoneOffset = 120;      // Berlin offest in minutes of your timezone to Greenwich Mean Time (GMT)

        private static bool workWithWatchdog = false;    // with watchdog activated occasionally OutOfMemory exceptions are thrown, test if watchdog works for you

        // Set the name of the table for analog values (name must be conform to special rules: see Azure)
        private static string analogTableName = "AnalogTestValues";
       
        private static string analogTablePartPrefix = "Y2_";     // Your choice (name must be conform to special rules: see Azure)
        private static bool augmentPartitionKey = true;          // If true, the actual year is added as suffix to the Tablenames

        // Set the name of the table for On/Off values (name must be conform to special rules: see Azure)
        //private static string OnOffSensor01TableName = "OnOffValues01";
        private static string OnOffSensor01TableName = "OnOffValues02";

        static string onOffTablePartPrefix = "Y3_";             // Your choice (name must be conform to special rules: see Azure)

        // Set intervals (in seconds, invalidateInterval in minutes)
        static int readInterval = 4;                     // in this interval (seconds) analog sensors are read

        static int writeToCloudInterval = 45;        // in this interval (seconds) the analog data are stored to the cloud (600 = 10 min is recommended)
           
        static int invalidateIntervalMinutes = 15;      // if analog values ar not actualized in this interval, they are set to invalid (999.9)

        // Set your WiFi Credentials here or store them in the Resources
        static string wiFiSSID_1 = ResourcesSecret.GetString(ResourcesSecret.StringResources.SSID_1);
        //static string wiFiSSID_1 = "VirtualWiFi";

        static string wiFiKey_1 = ResourcesSecret.GetString(ResourcesSecret.StringResources.Key_1);
        //static string wiFiKey_1 = "MySecretWiFiKey";
        
        // Set your Azure Storage Account Credentials here or store them in the Resources      
        static string storageAccountName = ResourcesSecret.GetString(ResourcesSecret.StringResources.AzureAccountName);      
        //static string storageAccount = "your Accountname";

        static string storageKey = ResourcesSecret.GetString(ResourcesSecret.StringResources.AzureAccountKey);        
        //static string storageKey = "your key";

        //private static bool Azure_useHTTPS = true;
        private static bool Azure_useHTTPS = false;

        //****************  End of Settings to be changed by user   ********************************* 


        private static X509Certificate[] caCerts = Azure_useHTTPS ? new X509Certificate[] { new X509Certificate(Resources.GetBytes(Resources.BinaryResources.BaltimoreCyberTrustRoot)) } : null;

        //private static X509Certificate[] caCerts = new X509Certificate[] { new X509Certificate(Resources.GetBytes(Resources.BinaryResources.Google_Trust_Services___GlobalSign_Root_CA_R2)) };

        //DayLightSavingTimeSettings  //not used in this App
        // Europe       
        // private static int dstOffset = 60; // 1 hour (Europe 2016)
        // private static string dstStart = "Mar lastSun @2";
        // private static string dstEnd = "Oct lastSun @3";
        /*  USA
        private static int dstOffset = 60; // 1 hour (US 2013)
        private static string dstStart = "Mar Sun>=8"; // 2nd Sunday March (US 2013)
        private static string dstEnd = "Nov Sun>=1"; // 1st Sunday Nov (US 2013)
        */

        // Attaching fiddler for NETMF (not used in this App for SPWF04SA)
        // private static string fiddlerIPAddress = "0.0.0.0";
        // private static bool attachFiddler = false;
        // private static int fiddlerPort = 77;

        private static int AnalogCloudTableYear = 1900;   // preset with year in the past

        private static TableClient table;

        // Create Datacontainer for values of 4 analog channels, Data invalidate time = 15 min
        private static DataContainer dataContainer = new DataContainer(new TimeSpan(0, invalidateIntervalMinutes, 0));

        private static Timer getSensorDataTimer;
        private static Timer writeAnalogToCloudTimer;
        private static Timer readLastAnalogRowTimer;

        private static AutoResetEvent waitForWiFiReady = new AutoResetEvent(false);

        private static readonly object LockProgram = new object();
     
        private static GpioPin OnOffSensor01;

        // RoSchmi: must be tested
        //public static SPWF04SxInterface wifi;

        public static SPWF04SxInterfaceExtension wifi;

        private static WiFi_SPWF04S_Mgr wiFi_SPWF04S_Mgr;

        private static DateTime dateTimeNtpServerDelivery = DateTime.MinValue;
        
        private static bool dateTimeAndIpAddressAreSet = false;

        private static IPAddress ip4Address = IPAddress.Parse("0.0.0.0");
     
        private static CloudStorageAccount myCloudStorageAccount;      
        private static GpioPin mode;

        private static AdcController adc = AdcController.GetDefault();
        private static AdcChannel analog0 = adc.OpenChannel(FEZ.AdcChannel.A0);
        private static AdcChannel analog1 = adc.OpenChannel(FEZ.AdcChannel.A1);
        private static AdcChannel analog2 = adc.OpenChannel(FEZ.AdcChannel.A2);
        private static AdcChannel analog3 = adc.OpenChannel(FEZ.AdcChannel.A3);

        // For OnOffSensor01        
        static DateTime OnOffSensor01LastSendTime = DateTime.MinValue;
        static TimeSpan OnOffSensor01OnTimeDay = new TimeSpan(0, 0, 0);
        private static int OnOffTable01Year = 1900;

        private static Thread WatchdogResetThread;
        private static bool watchdogIsActive = false;
        private static bool lastRebootReason = false;

        #endregion

        #region Region Main
        static void Main()
        {
            lastRebootReason = Watchdog.LastReboot;
            if (workWithWatchdog)
            {
                Watchdog.Start(new TimeSpan(0, 0, 30));
                Watchdog.Reset();
                WatchdogResetThread = new Thread(new ThreadStart(watchdogReset));
                WatchdogResetThread.Start();
            }

            var cont = GpioController.GetDefault();

            OnOffSensor01 = cont.OpenPin(FEZ.GpioPin.Btn1);
            OnOffSensor01.SetDriveMode(GpioPinDriveMode.InputPullUp);
            OnOffSensor01.ValueChanged += OnOffSensor01_ValueChanged;

            GpioPin reset = cont.OpenPin(FEZ.GpioPin.WiFiReset);

            mode = cont.OpenPin(FEZCLR.GpioPin.PA0);
            mode.SetDriveMode(GpioPinDriveMode.InputPullDown);

            var irq = cont.OpenPin(FEZ.GpioPin.WiFiInterrupt);
            
            var scont = SpiController.FromName(FEZ.SpiBus.WiFi);
                       
            var spi = scont.GetDevice(SPWF04SxInterface.GetConnectionSettings(SpiChipSelectType.Gpio, FEZ.GpioPin.WiFiChipSelect));
           
            wifi = new SPWF04SxInterfaceExtension(spi, irq, reset);

            wiFi_SPWF04S_Mgr = new WiFi_SPWF04S_Mgr(wifi, wiFiSSID_1, wiFiKey_1);
            
            wiFi_SPWF04S_Mgr.PendingSocketData += WiFi_SPWF04S_Device_PendingSocketData;
            wiFi_SPWF04S_Mgr.SocketWasClosed += WiFi_SPWF04S_Device_SocketWasClosed;
            wiFi_SPWF04S_Mgr.Ip4AddressAssigned += WiFi_SPWF04S_Device_Ip4AddressAssigned;
            wiFi_SPWF04S_Mgr.DateTimeNtpServerDelivered += WiFi_SPWF04S_Device_DateTimeNtpServerDelivered;           
            wiFi_SPWF04S_Mgr.WiFiNetworkLost += WiFi_SPWF04S_Device_WiFiNetworkLost;

            wiFi_SPWF04S_Mgr.Initialize();
                      
            myCloudStorageAccount = new CloudStorageAccount(storageAccountName, storageKey, useHttps: Azure_useHTTPS);
          
            waitForWiFiReady.WaitOne(10000, true);  // ******** Wait 15 sec to scan for wlan devices   ********************
        
            for (int i = 0; i < 500; i++)    // Wait up to 50 sec for getting IP-Address and time
            {
                Thread.Sleep(100);
                if (dateTimeAndIpAddressAreSet)
                { break; }
            }

            string theTime = wifi.GetTime();

            int year = 0;
            try
            {
                int.TryParse(theTime.Substring(5, 2), out year);
            }
            catch { }

            if ((theTime == null) || (year < 17))
            {
                Debug.WriteLine("Reboot");
                GHIElectronics.TinyCLR.Native.Power.Reset(true);      // Reset Board if no time over internet     
            }

            if (DateTime.Now < new DateTime(2019,01,01))                  // Actualize TinyCLR Datetime if not up to date         
            {
                string[] splitDateTime = theTime.Replace("Time:", "#").Split(new char[] { '#' });
                DateTime nowDate = new DateTime(int.Parse("20" + splitDateTime[0].Substring(5, 2)), int.Parse(splitDateTime[0].Substring(8, 2)), int.Parse(splitDateTime[0].Substring(11, 2)), int.Parse(splitDateTime[1].Substring(0, 2)), int.Parse(splitDateTime[1].Substring(3, 2)), int.Parse(splitDateTime[1].Substring(6, 2)));
                SystemTime.SetTime(nowDate, timeZoneOffset);
            }

            // Tests with changing ntp-refresh time of SPWF04Sx module, didn't get it working
            // https://community.st.com/s/question/0D50X0000APZKXBSQ5/how-to-control-timing-of-spwf04sa-ntpserverdelivery-events
            // wifi.SetConfiguration("ip_ntp_refresh", "30");         // set refresh time
            // string readBack = wifi.GetConfiguration("ip_ntp_refresh");
            // readBack = wifi.GetConfiguration("console_wind_off_high");
            // wifi.SetConfiguration("ip_ntp_startup", "0");        // switch off ntp client
            // wifi.SaveConfiguration();


            wifi.ClearTlsServerRootCertificate();
                  
            #region Region: List of additional commands for file handling  *** only for demonstration  ***
            // do not delete, can be useful in future 
            /*
            theTime = wifi.GetTime();

            wifi.MountVolume(SPWF04SxVolume.Ram);

            wifi.SetConfiguration("ramdisk_memsize", "18");
           
            wifi.DeleteFile("mytestfile");


            wifi.CreateFile("mytestfile", Encoding.UTF8.GetBytes("Das hat geklappt, ganz wunderbar. ABCDEFGHIJKLMNOPQRSTUVWXYZ.ABCDEFGHIJKLMNOPQRSTUVWXYZ.ABCDEFGHIJKLMNOPQRSTUVWXYZ.ABCDEFGHIJKLMNOPQRSTUVWXYZ"));

            // Get the list of files in SPWF04Sx directory: First wifi.GetFileListing, then ReadResponseBody
            wifi.GetFileListing();

            StringBuilder stringBuilder = new StringBuilder("");
            byte[] readBuf = new byte[50];
            int len = readBuf.Length;

            while (len > 0)
            {
                len = readBuf.Length;
                len = wifi.ReadResponseBody(readBuf, 0, readBuf.Length);
                // make here what you want to do with the byte Array
                stringBuilder.Append(Encoding.UTF8.GetString(readBuf, 0, len));                
            }
            string fileList = stringBuilder.ToString();

            // ********************** This is an alternative way to get the fileListing
            // wifi.GetFileListing();
            // string fileListing = GetResponsebodyAsString(50);
            // ***********************

            // Now as we have the FileListing, we can get the properties of a special file

            FileEntity fileEntity = wifi.GetFileProperties(fileList, "mytestfile");   // Can be used as 'FileExists' equivalent 

            // Now as we know the length of the file, we can read the content
            byte[] fileContent = wifi.GetFileDataBinary("mytestfile", int.Parse(fileEntity.Length));
           
            //Debug.WriteLine(Encoding.UTF8.GetString(fileContent));
                     
            */        
            #endregion

            getSensorDataTimer = new System.Threading.Timer(new TimerCallback(getSensorDataTimer_tick), null, readInterval * 1000, readInterval * 1000);

            // start timer to write analog data to the Cloud
            writeAnalogToCloudTimer = new System.Threading.Timer(new TimerCallback(writeAnalogToCloudTimer_tick), null, 5 * 1000, Timeout.Infinite);

            // readLastAnalogRowTimer is started in writeAnalogToCloudTimer_tick event
            readLastAnalogRowTimer = new System.Threading.Timer(new TimerCallback(readLastAnalogRowTimer_tick), null, Timeout.Infinite, Timeout.Infinite);

           // Debug.WriteLine(lastRebootReason ? "RebootReason: Watchdog" : "RebootReason: Power/Reset");
           
            while (true)
            {
                Thread.Sleep(100);
            }              
        }
        #endregion

        #region Timer Event: writeAnalogToCloudTimer_tick  --- Entity with analog values is written to the Cloud
        private static void writeAnalogToCloudTimer_tick(object state)
        {        
            writeAnalogToCloudTimer.Change(10 * 60 * 1000, 10 * 60 * 1000);    // Set to a long interval, so will not fire again before completed

            lock (LockProgram)
            {              
                int yearOfSend = DateTime.Now.Year;

                #region Region Create analogTable if not exists
                HttpStatusCode resultTableCreate = HttpStatusCode.Ambiguous;
                if (AnalogCloudTableYear != yearOfSend)
                {
                    Debug.WriteLine("\r\nGoing to create analog Table");
                    watchdogIsActive = true;
                    resultTableCreate = createTable(myCloudStorageAccount, caCerts, analogTableName + DateTime.Today.Year.ToString());
                    watchdogIsActive = false;                   
                }

                #endregion

                if ((resultTableCreate == HttpStatusCode.Created) || (resultTableCreate == HttpStatusCode.NoContent) || (resultTableCreate == HttpStatusCode.Conflict))
                {
                    // Set flag to indicate that table already exists, avoid trying to create again
                    AnalogCloudTableYear = yearOfSend;

                    writeAnalogToCloudTimer.Change(1 * 1000, writeToCloudInterval * 1000);  // set the timer event to come again in 1 sec.
                }
                else
                {
                    wifi.SetConfiguration("console_wind_off_high", "0x100000");    // mask WIND NtpServerDelivery 

                    string partitionKey = makePartitionKey(analogTablePartPrefix, augmentPartitionKey);

                    DateTime actDate = DateTime.Now;

                    string rowKey = makeRowKey(actDate);

                    string sampleTime = actDate.Month.ToString("D2") + "/" + actDate.Day.ToString("D2") + "/" + actDate.Year + " " + actDate.Hour.ToString("D2") + ":" + actDate.Minute.ToString("D2") + ":" + actDate.Second.ToString("D2");


                    // Fill array with 4 analog values from datacontainer
                    double[] sampleValues = new double[4];
                    for (int i = 1; i < 5; i++)
                    {
                        double measuredValue = dataContainer.GetAnalogValueSet(i).MeasureValue;
                        // limit measured values to the allowed range of -40.0 to +140.0, exception: 999.9 (not valid value)
                        if ((measuredValue < 999.89) || (measuredValue > 999.91))  // want to be careful with decimal numbers
                        {
                            measuredValue = (measuredValue < -40.0) ? -40.0 : (measuredValue > 140.0 ? 140.0 : measuredValue);
                        }
                        else
                        {
                            measuredValue = 999.9;
                        }
                        sampleValues[i - 1] = measuredValue;
                    }

                    // Populate Analog Table with values from the array
                    ArrayList propertiesAL = AnalogTablePropertiesAL.AnalogPropertiesAL(sampleTime, sampleValues[0], sampleValues[1], sampleValues[2], sampleValues[3]);

                    AnalogTableEntity analogTableEntity = new AnalogTableEntity(partitionKey, rowKey, propertiesAL);

                    Debug.WriteLine("\r\nGoing to upload analog values.     SampleTime: " + sampleTime);
                    
                    string insertEtag = string.Empty;
                    HttpStatusCode insertResult = HttpStatusCode.BadRequest;

                    watchdogIsActive = true;

                    insertResult = insertTableEntity(myCloudStorageAccount, caCerts, analogTableName + yearOfSend.ToString(), analogTableEntity, out insertEtag);

                    watchdogIsActive = false;
                                                                                                                     
                    if ((insertResult == HttpStatusCode.NoContent) || (insertResult == HttpStatusCode.Conflict))
                    {
                        Debug.WriteLine("Succeeded to insert Entity\r\n");
                      
                        writeAnalogToCloudTimer.Change(writeToCloudInterval * 1000, writeToCloudInterval * 1000);

                        // trigger the timer to read the last row
                        readLastAnalogRowTimer.Change(1000, Timeout.Infinite);
                    }
                    else
                    {
                        Debug.WriteLine("Failed to insert Entity\r\n");                                              
                        writeAnalogToCloudTimer.Change(1000, writeToCloudInterval * 1000);
                    }
                    wifi.SetConfiguration("console_wind_off_high", "0x000000");   // unmask WIND NtpServerDelivery 
                }
            }           
        }
        #endregion

        #region Timer Event: readLastAnalogRowTimer_tick
        private static void readLastAnalogRowTimer_tick(object state)
        {           
            lock (LockProgram)
            {
                Debug.WriteLine("Going to read back last uploaded entity");

                wifi.SetConfiguration("console_wind_off_high", "0x100000");      // mask WIND NtpServerDelivery 

                ArrayList queryResult = new ArrayList();

                watchdogIsActive = true;

                HttpStatusCode resultQuery = queryTableEntities(myCloudStorageAccount, caCerts, analogTableName + DateTime.Now.Year.ToString(), "$top=1", out queryResult);

                watchdogIsActive = false;

                if (resultQuery == HttpStatusCode.OK)
                {
                    var entityHashtable = queryResult[0] as Hashtable;
                    var theRowKey = entityHashtable["RowKey"];
                    var SampleTime = entityHashtable["SampleTime"];
                    Debug.WriteLine("Successfully read back from Azure, SampleTime: " + SampleTime);
                }
                else
                {
                    Debug.WriteLine("Failed to read back last entity from Azure");
                }
                // the timer is set to a short time in 'writeAnalogToCloudTimer_tick'
                readLastAnalogRowTimer.Change(Timeout.Infinite, Timeout.Infinite);

                wifi.SetConfiguration("console_wind_off_high", "0x000000");     // unmask WIND NtpServerDelivery 
            }
        }
        #endregion

        #region Timer Event: OnOffSensor01_ValueChanged  -- Entity with OnOffSensorData is written to the Cloud

        private static void OnOffSensor01_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs e)
        {
            lock (LockProgram)
            {
                int yearOfSend = DateTime.Now.Year;

                // Create OnOffTable if not exists
                HttpStatusCode resultTableCreate = HttpStatusCode.Ambiguous;
                if (OnOffTable01Year != yearOfSend)
                {
                    Debug.WriteLine("Going to create On/Off Table");

                    watchdogIsActive = true;

                    resultTableCreate = createTable(myCloudStorageAccount, caCerts, OnOffSensor01TableName + DateTime.Today.Year.ToString());

                    watchdogIsActive = false;
                }

                if ((resultTableCreate == HttpStatusCode.Created) || (resultTableCreate == HttpStatusCode.NoContent) || (resultTableCreate == HttpStatusCode.Conflict))
                {
                    OnOffTable01Year = yearOfSend;
                }
                else
                {
                    string partitionKey = makePartitionKey(onOffTablePartPrefix, augmentPartitionKey);

                    DateTime actDate = DateTime.Now;
                    string rowKey = makeRowKey(actDate);

                    string sampleTime = actDate.Month.ToString("D2") + "/" + actDate.Day.ToString("D2") + "/" + actDate.Year + " " + actDate.Hour.ToString("D2") + ":" + actDate.Minute.ToString("D2") + ":" + actDate.Second.ToString("D2");

                    TimeSpan tflSend = OnOffSensor01LastSendTime == DateTime.MinValue ? new TimeSpan(0) : e.Timestamp - OnOffSensor01LastSendTime;

                    OnOffSensor01LastSendTime = e.Timestamp;

                    string timeFromLastSendAsString = tflSend.Days.ToString("D3") + "-" + tflSend.Hours.ToString("D2") + ":" + tflSend.Minutes.ToString("D2") + ":" + tflSend.Seconds.ToString("D2");

                    OnOffSensor01OnTimeDay = OnOffSensor01.Read() == GpioPinValue.High ? OnOffSensor01OnTimeDay + tflSend : OnOffSensor01OnTimeDay;

                    string onTimeDayAsString = OnOffSensor01OnTimeDay.Days.ToString("D3") + "-" + OnOffSensor01OnTimeDay.Hours.ToString("D2") + ":" + OnOffSensor01OnTimeDay.Minutes.ToString("D2") + ":" + OnOffSensor01OnTimeDay.Seconds.ToString("D2");

                    ArrayList propertiesAL = OnOffTablePropertiesAL.OnOffPropertiesAL(OnOffSensor01.Read() == GpioPinValue.Low ? "On" : "Off", OnOffSensor01.Read() == GpioPinValue.Low ? "Off" : "On", onTimeDayAsString, sampleTime, timeFromLastSendAsString);

                    OnOffTableEntity onOffTableEntity = new OnOffTableEntity(partitionKey, rowKey, propertiesAL);

                    HttpStatusCode insertResult = HttpStatusCode.BadRequest;

                    while (!((insertResult == HttpStatusCode.NoContent) || (insertResult == HttpStatusCode.Conflict)))
                    {
                        string insertEtag = string.Empty;
                        string state = OnOffSensor01.Read() == GpioPinValue.Low ? "On" : "Off";
                        Debug.WriteLine("Going to upload OnOff-Sensor State:" + state);
                        insertResult = insertTableEntity(myCloudStorageAccount, caCerts, OnOffSensor01TableName + yearOfSend.ToString(), onOffTableEntity, out insertEtag);

                        Debug.WriteLine(((insertResult == HttpStatusCode.NoContent) || (insertResult == HttpStatusCode.Conflict)) ? "Succeded to insert Entity\r\n" : "Failed to insert Entity *************\r\n");                        
                    }
                }
            }
        }


        #endregion

        #region Timer Event: getSensorDataTimer_tick
        private static void getSensorDataTimer_tick(object state)
        {
            lock (LockProgram)
            {
                DateTime actDateTime = DateTime.Now;

                dataContainer.SetNewAnalogValue(1, actDateTime, ReadAnalogSensor(0));
                dataContainer.SetNewAnalogValue(2, actDateTime, ReadAnalogSensor(1));
                dataContainer.SetNewAnalogValue(3, actDateTime, ReadAnalogSensor(2));
                dataContainer.SetNewAnalogValue(4, actDateTime, ReadAnalogSensor(3));
            }        
        }
        #endregion

        #region Region ReadAnalogSensors      
        private static double ReadAnalogSensor(int pAin)
        {

#if !UseTestValues
            // Use values read from the analogInput ports

            double theRead = 999.9;             
            switch (pAin)
            {
                case 0:
                    {                       
                        theRead = analog0.ReadRatio();
                    }
                    break;

                case 1:
                    {                       
                        theRead = analog1.ReadRatio();
                    }
                    break;
                case 2:
                    {
                        theRead = analog2.ReadRatio();                       
                    }
                    break;
                case 3:
                    {
                        theRead = analog3.ReadRatio();             
                    }
                    break;                          
            }
            
            return theRead * 10.0;

#else
            // Only as an example we here return values which draw a sinus curve
            // Console.WriteLine("entering Read analog sensor");
            int frequDeterminer = 4;
            int y_offset = 1;
            // different frequency and y_offset for aIn_0 to aIn_3
            if (pAin == 0)
            { frequDeterminer = 4; y_offset = 1; }
            if (pAin == 1)
            { frequDeterminer = 8; y_offset = 10; }
            if (pAin == 2)
            { frequDeterminer = 12; y_offset = 20; }
            if (pAin == 3)
            { frequDeterminer = 16; y_offset = 30; }

            int secondsOnDayElapsed = DateTime.Now.Second + DateTime.Now.Minute * 60 + DateTime.Now.Hour * 60 * 60;
            
            return Math.Round(25f * (double)Math.Sin(Math.PI / 2.0 + (secondsOnDayElapsed * ((frequDeterminer * Math.PI) / (double)86400)))) / 10  + y_offset;          
#endif
        }
        #endregion

        #region Region diverse Eventhandler
        private static void WiFi_SPWF04S_Device_WiFiNetworkLost(WiFi_SPWF04S_Mgr sender, WiFi_SPWF04S_Mgr.WiFiNetworkLostEventArgs e)
        {
            AzureStorageHelper.WiFiNetworkLost = e.WiFiNetworkLost;
        }

        private static void WiFi_SPWF04S_Device_PendingSocketData(WiFi_SPWF04S_Mgr sender, WiFi_SPWF04S_Mgr.PendingDataEventArgs e)
        {
            AzureStorageHelper.SocketDataPending = e.SocketDataPending;
        }

        private static void WiFi_SPWF04S_Device_SocketWasClosed(WiFi_SPWF04S_Mgr sender, WiFi_SPWF04S_Mgr.SocketClosedEventArgs e)
        {
            AzureStorageHelper.SocketWasClosed = e.SocketIsClosed;
        }

        private static void WiFi_SPWF04S_Device_Ip4AddressAssigned(WiFi_SPWF04S_Mgr sender, WiFi_SPWF04S_Mgr.Ip4AssignedEventArgs e)
        {
            lock (LockProgram)
            {
                ip4Address = e.Ip4Address;
                AzureStorageHelper.WiFiNetworkLost = false;
            }
        }

        private static void WiFi_SPWF04S_Device_DateTimeNtpServerDelivered(WiFi_SPWF04S_Mgr sender, WiFi_SPWF04S_Mgr.NTPServerDeliveryEventArgs e)
        {
            lock (LockProgram)
            {               
                dateTimeNtpServerDelivery = e.DateTimeNTPServer;           
                SystemTime.SetTime(dateTimeNtpServerDelivery, timeZoneOffset);
                dateTimeAndIpAddressAreSet = true;
            }
            waitForWiFiReady.Set();
        }
        #endregion

        #region Region Private methods 

        private static string makePartitionKey(string partitionKeyprefix, bool augmentWithYear)
        {
            // if wanted, augment with year and month (12 - month for right order)     
            return augmentWithYear == true ? partitionKeyprefix + DateTime.Today.Year + "-" + (12 - DateTime.Now.Month).ToString("D2") : partitionKeyprefix;
        }

        private static string makeRowKey(DateTime actDate)
        {
            // formatting the RowKey (= reverseDate) this way to have the tables sorted with last added row upmost
            return (10000 - actDate.Year).ToString("D4") + (12 - actDate.Month).ToString("D2") + (31 - actDate.Day).ToString("D2")
                       + (23 - actDate.Hour).ToString("D2") + (59 - actDate.Minute).ToString("D2") + (59 - actDate.Second).ToString("D2");
        }

        private static void watchdogReset()
        {
            while (true)
            {
                Thread.Sleep(5000);
                if (!watchdogIsActive)
                {
                    Watchdog.Reset();
                }
            }

        }

        /*
        private static int GetDlstOffset(DateTime pDateTime)
        {
            return DayLightSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, pDateTime, true);
        }
        */

        private static int GetDlstOffset(DateTime pDateTime)

        {
            //RoSchmi changed
            return 0;
            //return  DayLightSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, pDateTime, true);
        }       
        #endregion

        #region private method insertTableEntity
        private static HttpStatusCode insertTableEntity(CloudStorageAccount pCloudStorageAccount, X509Certificate[] pCaCerts, string pTable, TableEntity pTableEntity, out string pInsertETag)
        {          
            table = new TableClient(pCloudStorageAccount, pCaCerts, _debug, _debug_level, wifi);
            // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
            // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler

            //if (attachFiddler)
            //{ table.attachFiddler(true, fiddlerIPAddress, fiddlerPort); }

            var resultCode = table.InsertTableEntity(pTable, pTableEntity, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIjson, TableClient.ResponseType.dont_returnContent, useSharedKeyLite: false);
            pInsertETag = table.OperationResponseETag;
            //var body = table.OperationResponseBody;
            //Debug.Print("Entity inserted");
            return resultCode;
        }
        #endregion

        #region private method createTable
        private static HttpStatusCode createTable(CloudStorageAccount pCloudStorageAccount, X509Certificate[] pCaCerts, string pTableName )
        {           
            table = new TableClient(pCloudStorageAccount, pCaCerts, _debug, _debug_level, wifi);

            // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
            // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
            /*
            if (attachFiddler)
            { table.attachFiddler(true, fiddlerIPAddress, fiddlerPort); }
            */

            HttpStatusCode resultCode = table.CreateTable(pTableName, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIjson, TableClient.ResponseType.dont_returnContent, useSharedKeyLite: false);
            return resultCode;
        }
        #endregion

        #region private method queryTableEntities
        
        private static HttpStatusCode queryTableEntities(CloudStorageAccount pCloudStorageAccount, X509Certificate[] pCaCerts, string tableName, string query, out ArrayList queryResult)
        {
            table = new TableClient(pCloudStorageAccount, pCaCerts, _debug, _debug_level, wifi);


            // To use Fiddler as WebProxy include the  following line. Use the local IP-Address of the PC where Fiddler is running
            // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
            //if (attachFiddler)
            //{ table.attachFiddler(true, fiddlerIPAddress, fiddlerPort); }

           // HttpStatusCode resultCode = table.QueryTableEntities(tableName, query, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIatomIxml, useSharedKeyLite: false);
            HttpStatusCode resultCode = table.QueryTableEntities(tableName, query, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIatomIxml, useSharedKeyLite: false);

            // now we can get the results by reading the properties: table.OperationResponse......
            queryResult = table.OperationResponseQueryList;
            // var body = table.OperationResponseBody;
            // this shows how to get a special value (here the RowKey)of the first entity
            // var entityHashtable = queryResult[0] as Hashtable;
            // var theRowKey = entityHashtable["RowKey"];
            return resultCode;
        }

        #endregion

        /*
        // don't delete, is in some cases the better way to read FileListing
        private static string GetResponsebodyAsString(int bufSize)
        {
            if (bufSize < 1) throw new ArgumentException();
            byte[] buffer = new byte[bufSize];
            int sockRead = buffer.Length;
            int sockOffset = 0;
            int sockTotal = 0;
            byte[] sockLastBuf = new byte[0];
            byte[] sockTotalBuf = new byte[0];
            while (sockRead > 0)
            {               
                sockRead = wifi.ReadResponseBody(buffer, 0, buffer.Length);

                sockTotal += sockRead;                               
                sockOffset = sockLastBuf.Length;
                sockTotalBuf = new byte[sockOffset + sockRead];
                Array.Copy(sockLastBuf, 0, sockTotalBuf, 0, sockOffset);
                Array.Copy(buffer, 0, sockTotalBuf, sockOffset, sockRead);
                
                string intString = Encoding.UTF8.GetString(sockTotalBuf);
                sockLastBuf = sockTotalBuf;
            }
            return Encoding.UTF8.GetString(sockTotalBuf);
        }
        */
    }
}















