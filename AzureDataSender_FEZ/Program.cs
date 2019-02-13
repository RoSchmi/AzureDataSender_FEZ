﻿// Copyright RoSchmi 2019
// App to write sensor data to Azure Storage Table service
// For TinyCLR Board FEZ with SPWF04Sx Wifi module

// With #define UseTestValues you can select if data are read from sensors or if simulated data (sinus curves) are used

//#define UseTestValues

using System;
using System.Collections;
using System.Text;
using System.Resources;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInterface;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.Spi;
using GHIElectronics.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Devices.Adc;
using GHIElectronics.TinyCLR.Storage.Streams;
using RoSchmi.Net;
using RoSchmi.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx;

using RoSchmi.Net.Azure.Storage;
using AzureDataSender_FEZ.TableStorage;
using AzureDataSender_FEZ;
using RoSchmi.DayLightSavingTime;
using GHIElectronics.TinyCLR.Native;
using PervasiveDigital.Utilities;
using AzureDataSender.Models;
using RoSchmi.Interfaces;




namespace AzureDataSender_FEZ
{
    class Program
    {
        private static AzureStorageHelper.DebugMode _debug = AzureStorageHelper.DebugMode.StandardDebug;
        private static AzureStorageHelper.DebugLevel _debug_level = AzureStorageHelper.DebugLevel.DebugAll;
        private static TableClient table;

        
        private static int timeZoneOffset = 60;      // Berlin offest in minutes of your timezone to Greenwich Mean Time (GMT)                                                                           

        //DayLightSavingTimeSettings  //not used in this App
        // Europe       
        private static int dstOffset = 60; // 1 hour (Europe 2016)
        private static string dstStart = "Mar lastSun @2";
        private static string dstEnd = "Oct lastSun @3";
        /*  USA
        private static int dstOffset = 60; // 1 hour (US 2013)
        private static string dstStart = "Mar Sun>=8"; // 2nd Sunday March (US 2013)
        private static string dstEnd = "Nov Sun>=1"; // 1st Sunday Nov (US 2013)
        */

        
        private static DateTime _timeOfLastSend = DateTime.Now.AddMinutes(-5.0);
        private static TimeSpan sendInterval = new TimeSpan(0, 10, 0);       
        private static AzureStorageHelper.DebugMode _AzureDebugMode = AzureStorageHelper.DebugMode.StandardDebug;
        private static AzureStorageHelper.DebugLevel _AzureDebugLevel = AzureStorageHelper.DebugLevel.DebugAll;
        private static string fiddlerIPAddress = "0.0.0.0";
        private static bool attachFiddler = false;
        private static int fiddlerPort = 77;



        
        private static int AnalogCloudTableYear = 1900;

        // Create Datacontainer for values of 4 analog channels, Data invalidate time = 15 min
        private static DataContainer dataContainer = new DataContainer(new TimeSpan(0, 15, 0));

        
        // Set the name of the table for analog values (name must be conform to special rules: see Azure)

        private static string analogTableName = "AnalogTestValues";

        private static string analogTablePartPrefix = "Y2_";     // Your choice (name must be conform to special rules: see Azure)
        private static bool augmentPartitionKey = true;

        static string onOffTablePartPrefix = "Y3_";  // Your choice (name must be conform to special rules: see Azure)

        //private static string connectionString;

        // Set intervals (in seconds)

        static int readInterval = 4;            // in this interval analog sensors are read

        //static int writeToCloudInterval = 600;   // in this interval the analog data are stored to the cloud
        static int writeToCloudInterval = 30;   // in this interval the analog data are stored to the cloud

        static int OnOffToggleInterval = 420;    // in this interval the On/Off state is toggled (test values)

        static int invalidateInterval = 900;    // if analog values ar not actualized in this interval, they are set to invalid (999.9)

        //****************  End of Settings to be changed by user   ********************************* 


        private static Timer getSensorDataTimer;
        private static Timer writeAnalogToCloudTimer;
        private static Timer readLastAnalogRowTimer;



        private static AutoResetEvent waitForWiFiReady = new AutoResetEvent(false);

        private static readonly object LockProgram = new object();

        //private static GpioPin led1;



        //private static SPWF04SxInterface wifi;
        public static SPWF04SxInterfaceRoSchmi  wifi;
        //public static ISPWF04SxInterface wifi;


        private static WiFi_SPWF04S_Device wiFi_SPWF04S_Device;

        private static DateTime dateTimeNtpServerDelivery = DateTime.MinValue;
        
        private static bool dateTimeAndIpAddressAreSet = false;

        private static IPAddress ip4Address = IPAddress.Parse("0.0.0.0");

        public static byte[] caAzure = Resources.GetBytes(Resources.BinaryResources.BaltimoreCyberTrustRoot);

        // Set your WiFi Credentials here or store them in the Resources
        static string wiFiSSID_1 = ResourcesSecret.GetString(ResourcesSecret.StringResources.SSID_1);
        // static string wiFiSSID_1 = "myWiFiSSID";
        static string wiFiKey_1 = ResourcesSecret.GetString(ResourcesSecret.StringResources.Key_1);
        // static string wiFiKey_1 = "mysupersecretWiFiKey";

        //static string wiFiSSID_2 = ResourcesSecret.GetString(ResourcesSecret.StringResources.SSID_2);
        //static string wiFiKey_2 = ResourcesSecret.GetString(ResourcesSecret.StringResources.Key_2);

        // Set your Azure Storage Account Credentials here or store them in the Resources      
        static string storageAccountName = ResourcesSecret.GetString(ResourcesSecret.StringResources.AzureAccountName);
        //static string storageAccount = "your Accountname";
       
        static string storageKey = ResourcesSecret.GetString(ResourcesSecret.StringResources.AzureAccountKey);
        //static string storageKey = "your key";


        private static bool Azure_useHTTPS = true;
        //private static bool Azure_useHTTPS = false;

        private static CloudStorageAccount myCloudStorageAccount;

        private static X509Certificate[] caCerts;

        private static GpioPin _pinPyton;

        private static AdcController adc = AdcController.GetDefault();
        private static AdcChannel analog0 = adc.OpenChannel(FEZ.AdcChannel.A0);
        private static AdcChannel analog1 = adc.OpenChannel(FEZ.AdcChannel.A1);
        private static AdcChannel analog2 = adc.OpenChannel(FEZ.AdcChannel.A2);
        private static AdcChannel analog3 = adc.OpenChannel(FEZ.AdcChannel.A3);



        #region Region Main
        static void Main()
        {          
            Debug.WriteLine("Remaining Ram at start  Main: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);
            Debug.WriteLine("DateTime at Start: " + DateTime.Now.ToString());
        
            var cont = GpioController.GetDefault();
           
            caCerts = new X509Certificate[] { new X509Certificate(caAzure)};
                     
            var reset = cont.OpenPin(FEZ.GpioPin.WiFiReset);

            _pinPyton = cont.OpenPin(FEZCLR.GpioPin.PA0);
            _pinPyton.SetDriveMode(GpioPinDriveMode.InputPullDown);

            var irq = cont.OpenPin(FEZ.GpioPin.WiFiInterrupt);
            
            var scont = SpiController.FromName(FEZ.SpiBus.WiFi);
            
            var spi = scont.GetDevice(SPWF04SxInterfaceRoSchmi.GetConnectionSettings(SpiChipSelectType.Gpio, FEZ.GpioPin.WiFiChipSelect));
            //var spi = scont.GetDevice(ISPWF04SxInterface.GetConnectionSettings(SpiChipSelectType.Gpio, FEZ.GpioPin.WiFiChipSelect));


            wifi = new SPWF04SxInterfaceRoSchmi(spi, irq, reset);
         
            wiFi_SPWF04S_Device = new WiFi_SPWF04S_Device(wifi, wiFiSSID_1, wiFiKey_1);


            wiFi_SPWF04S_Device.PendingSocketData += WiFi_SPWF04S_Device_PendingSocketData;
            wiFi_SPWF04S_Device.SocketWasClosed += WiFi_SPWF04S_Device_SocketWasClosed;
            wiFi_SPWF04S_Device.Ip4AddressAssigned += WiFi_SPWF04S_Device_Ip4AddressAssigned;
            wiFi_SPWF04S_Device.DateTimeNtpServerDelivered += WiFi_SPWF04S_Device_DateTimeNtpServerDelivered;
            wiFi_SPWF04S_Device.WiFiAssociationChanged += WiFi_SPWF04S_Device_WiFiAssociationChanged;
            wiFi_SPWF04S_Device.WiFiNetworkLost += WiFi_SPWF04S_Device_WiFiNetworkLost;

            Debug.WriteLine("Remaining Ram before initialize: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);

            wiFi_SPWF04S_Device.Initialize();
           
            myCloudStorageAccount = new CloudStorageAccount(storageAccountName, storageKey, useHttps: Azure_useHTTPS);

            // Initialization for each table must be done in main
            //myAzureSendManager = new AzureSendManager(myCloudStorageAccount, analogTableName, _sensorValueHeader, _socketSensorHeader, caCerts, _timeOfLastSend, sendInterval, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS); 

            //AzureSendManager.sampleTimeOfLastSent = DateTime.Now.AddDays(-10.0);    // Date in the past
            //AzureSendManager.InitializeQueue();


            waitForWiFiReady.WaitOne(10000, true);  // ******** Wait 10 sec to scan for wlan devices   ********************

         
            for (int i = 0; i < 400; i++)    // Wait up to 40 sec for IP-Address and time
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
                DateTime nowDate = new DateTime(int.Parse("20" + theTime.Substring(5, 2) ), int.Parse(theTime.Substring(8, 2)), int.Parse(theTime.Substring(11, 2)), int.Parse(theTime.Substring(21, 2)), int.Parse(theTime.Substring(24, 2)), int.Parse(theTime.Substring(27, 2)));               
                SystemTime.SetTime(nowDate, timeZoneOffset);
            }
          
            wifi.SetConfiguration("ramdisk_memsize", "18");   // Reserve more Ram on SPWF04Sx  (not needed in this App)
                  
            wifi.ClearTlsServerRootCertificate();

            ArrayList theQuery = new ArrayList();

            #region Region: List of additional commands for file handling  *** only for demonstration  ***
            /*
            theTime = wifi.GetTime();

            wifi.MountMemoryVolume("2");

            string theFiles = wifi.GetDiskContent();
         
            wifi.SetConfiguration("ramdisk_memsize", "18");
           
            wifi.CreateRamFile("mytestfile", Encoding.UTF8.GetBytes("Das hat geklappt, ganz wunderbar. ABCDEFGHIJKLMNOPQRSTUVWXYZ.ABCDEFGHIJKLMNOPQRSTUVWXYZ.ABCDEFGHIJKLMNOPQRSTUVWXYZ.ABCDEFGHIJKLMNOPQRSTUVWXYZ"));

            FileEntity fileEntity = wifi.GetFileProperties("mytestfile");   // Can be used as 'FileExists' equivalent 

            byte[] theData = wifi.GetFileDataBinary("mytestfile");

            string fileContent = wifi.PrintFile("mytestfile");

            wifi.DeleteRamFile("mytestfile");

            theFiles = wifi.GetDiskContent();
            */
            #endregion

            long totalMemory = GC.GetTotalMemory(true);
            Debug.WriteLine("Total Memory: " + totalMemory.ToString());
            Debug.WriteLine("Remaining Ram a end of main: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);


            getSensorDataTimer = new System.Threading.Timer(new TimerCallback(getSensorDataTimer_tick), null, readInterval * 1000, 10 * 60 * 1000);
            // start timer to write analog data to the Cloud
            writeAnalogToCloudTimer = new System.Threading.Timer(new TimerCallback(writeAnalogToCloudTimer_tick), null, writeToCloudInterval * 1000, Timeout.Infinite);
            readLastAnalogRowTimer = new System.Threading.Timer(new TimerCallback(readLastAnalogRowTimer_tick), null, Timeout.Infinite, Timeout.Infinite);

            while (true)
            {
                Thread.Sleep(100);
            }              
        }        
        #endregion





        //     *************************************   Event writeAnalogToCloudTimer_tick     *******************************************************

        // When this timer fires an Entity containing 4 analog values is stored to an Azure Cloud Table

        #region Timer Event writeAnalogToCloudTimer_tick  --- Entity with analog values is written to the Cloud
        private static void writeAnalogToCloudTimer_tick(object state)
        {

            writeAnalogToCloudTimer.Change(10 * 60 * 1000, 10 * 60 * 1000);    // Set to a long interval, so will not fire again before completed

            myCloudStorageAccount = new CloudStorageAccount(storageAccountName, storageKey, useHttps: Azure_useHTTPS);

            int yearOfSend = DateTime.Now.Year;

            #region Region Create analogTable if not exists
            HttpStatusCode resultTableCreate = HttpStatusCode.Ambiguous;
            if (AnalogCloudTableYear != yearOfSend)
            {
                resultTableCreate = createTable(myCloudStorageAccount, analogTableName + DateTime.Today.Year.ToString());              
            }
            // Set flag to indicate that table already exists, avoid trying to crea
            if ((resultTableCreate == HttpStatusCode.Created) || (resultTableCreate == HttpStatusCode.NoContent) || (resultTableCreate == HttpStatusCode.Conflict))
            {
                AnalogCloudTableYear = yearOfSend;
            }
            #endregion
         
            #region Set the partitionKey
            string partitionKey = analogTablePartPrefix;            // Set Partition Key for Azure storage table
            if (augmentPartitionKey == true)                // if wanted, augment with year and month (12 - month for right order)                                                          
            { partitionKey = partitionKey + DateTime.Today.Year + "-" + (12 - DateTime.Now.Month).ToString("D2"); }
            #endregion

            DateTime actDate = DateTime.Now;

            // formatting the RowKey (= reverseDate) this way to have the tables sorted with last added row upmost
            string reverseDate = (10000 - actDate.Year).ToString("D4") + (12 - actDate.Month).ToString("D2") + (31 - actDate.Day).ToString("D2")
                       + (23 - actDate.Hour).ToString("D2") + (59 - actDate.Minute).ToString("D2") + (59 - actDate.Second).ToString("D2");


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

            AnalogTableEntity analogTableEntity = new AnalogTableEntity(partitionKey, reverseDate, propertiesAL);

            string insertEtag = string.Empty;

            HttpStatusCode insertResult = HttpStatusCode.BadRequest;
            insertResult = insertTableEntity(myCloudStorageAccount, analogTableName + yearOfSend.ToString(), analogTableEntity, out insertEtag);
            if (insertResult == HttpStatusCode.NoContent)
            {
                Debug.WriteLine(insertResult == HttpStatusCode.NoContent ? "Succeded to insert Entity\r\n" : "Failed to insert Entity\r\n");
            }
            
            long freeMemory = GHIElectronics.TinyCLR.Native.Memory.FreeBytes;
            Debug.WriteLine("At end of Timer event. Total memory: " + GC.GetTotalMemory(true).ToString("N0") + " Free Memory: " + freeMemory);
           
            writeAnalogToCloudTimer.Change(writeToCloudInterval * 1000, writeToCloudInterval * 1000);

            // trigger the timer to read the last row
            readLastAnalogRowTimer.Change(1000, Timeout.Infinite);
        }
        #endregion

        #region TimerEvent getSensorDataTimer_tick
        private static void getSensorDataTimer_tick(object state)
        {
           
            DateTime actDateTime = DateTime.Now;

            dataContainer.SetNewAnalogValue(1, actDateTime, ReadAnalogSensor(0));           
            dataContainer.SetNewAnalogValue(2, actDateTime, ReadAnalogSensor(1));        
            dataContainer.SetNewAnalogValue(3, actDateTime, ReadAnalogSensor(2));
            dataContainer.SetNewAnalogValue(4, actDateTime, ReadAnalogSensor(3));

            //Debug.WriteLine("Got Sensor Data");

          //  getSensorDataTimer.Change(readInterval * 1000, readInterval * 1000);
        }
        #endregion

        #region Timer event readLastAnalogRowTimer_tick
        private static void readLastAnalogRowTimer_tick(object state)
        {           
            ArrayList queryResult = new ArrayList();
            HttpStatusCode resultQuery = queryTableEntities(myCloudStorageAccount, analogTableName + DateTime.Now.Year.ToString(), "$top=1", out queryResult);
            if (resultQuery == HttpStatusCode.OK)
            {
                var entityHashtable = queryResult[0] as Hashtable;
                var theRowKey = entityHashtable["RowKey"];
                var SampleTime = entityHashtable["SampleTime"];
                Debug.WriteLine("Entity read back from Azure, SampleTime: " + SampleTime);
            }
            else
            {
                Debug.WriteLine("Failed to read back last entity from Azure");
            }

            readLastAnalogRowTimer.Change(Timeout.Infinite, Timeout.Infinite);
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
            // Console.WriteLine("Value returned");
            //return 1.0;
            //return Math.Round(2.5f * (double)Math.Sin(Math.PI / 2.0 + (secondsOnDayElapsed * ((frequDeterminer * Math.PI) / (double)86400))), 1) + y_offset;
            return Math.Round(2.5f * (double)Math.Sin(Math.PI / 2.0 + (secondsOnDayElapsed * ((frequDeterminer * Math.PI) / (double)86400)))) + y_offset;
#endif
        }
        #endregion


        #region Region Eventhandler
        private static void WiFi_SPWF04S_Device_WiFiNetworkLost(WiFi_SPWF04S_Device sender, WiFi_SPWF04S_Device.WiFiNetworkLostEventArgs e)
        {
            AzureStorageHelper.WiFiNetworkLost = e.WiFiNetworkLost;
        }

        private static void WiFi_SPWF04S_Device_WiFiAssociationChanged(WiFi_SPWF04S_Device sender, WiFi_SPWF04S_Device.WiFiAssociationEventArgs e)
        {
            AzureStorageHelper.WiFiAssociationState = e.WiFiAssociationState ? true : false;
        }

        private static void WiFi_SPWF04S_Device_PendingSocketData(WiFi_SPWF04S_Device sender, WiFi_SPWF04S_Device.PendingDataEventArgs e)
        {
            AzureStorageHelper.SocketDataPending = e.SocketDataPending;
        }

        private static void WiFi_SPWF04S_Device_SocketWasClosed(WiFi_SPWF04S_Device sender, WiFi_SPWF04S_Device.SocketClosedEventArgs e)
        {
            AzureStorageHelper.SocketWasClosed = e.SocketIsClosed;
        }

        private static void WiFi_SPWF04S_Device_Ip4AddressAssigned(WiFi_SPWF04S_Device sender, WiFi_SPWF04S_Device.Ip4AssignedEventArgs e)
        {
            lock (LockProgram)
            {
                ip4Address = e.Ip4Address;
                AzureStorageHelper.WiFiNetworkLost = false;
            }
        }

        private static void WiFi_SPWF04S_Device_DateTimeNtpServerDelivered(WiFi_SPWF04S_Device sender, WiFi_SPWF04S_Device.NTPServerDeliveryEventArgs e)
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


        /*
        string[] propertyNames = new string[4] { Analog_1.Text, Analog_2.Text, Analog_3.Text, Analog_4.Text };
        Dictionary<string, EntityProperty> entityDictionary = new Dictionary<string, EntityProperty>();


        string sampleTime = actDate.Month.ToString("D2") + "/" + actDate.Day.ToString("D2") + "/" + actDate.Year + " " + actDate.Hour.ToString("D2") + ":" + actDate.Minute.ToString("D2") + ":" + actDate.Second.ToString("D2");
        //string sampleTime = actDate.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

        entityDictionary.Add("SampleTime", EntityProperty.GeneratePropertyForString(sampleTime));
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

            entityDictionary.Add(propertyNames[i - 1], EntityProperty.GeneratePropertyForString(measuredValue.ToString("f1", System.Globalization.CultureInfo.InvariantCulture)));
        }

        //DynamicTableEntity sendEntity = new DynamicTableEntity(partitionKey, reverseDate, null, entityDictionary);

        //DynamicTableEntity dynamicTableEntity = await Common.InsertOrMergeEntityAsync(cloudTable, sendEntity);
        */

        //#region Write new row to the buffer

        //bool forceSend = true;

        // RoSchmi  tochange
        //SampleValue theRow = new SampleValue(partitionKey, DateTime.Now.AddMinutes(RoSchmi.DayLightSavingTime.DayLightSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)), 10.1, 10.2, 10.3, 10.4, forceSend);

        //SampleValue theRow = new SampleValue(partitionKey, DateTime.Now.AddMinutes(0), 10.1, 10.2, 10.3, 10.4, forceSend);

        /*
        SampleValue theRow = new SampleValue(partitionKey, DateTime.Now.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)), RoundedDecTempDiv10, _dayMin, _dayMax,
                _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_1_Sel - 1].RandomId, _sensorValueArr_Out[Ch_1_Sel - 1].Hum, _sensorValueArr_Out[Ch_1_Sel - 1].BatteryIsLow,
                _sensorValueArr_Out[Ch_2_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_2_Sel - 1].RandomId, _sensorValueArr_Out[Ch_2_Sel - 1].Hum, _sensorValueArr_Out[Ch_2_Sel - 1].BatteryIsLow,
                _sensorValueArr_Out[Ch_3_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_3_Sel - 1].RandomId, _sensorValueArr_Out[Ch_3_Sel - 1].Hum, _sensorValueArr_Out[Ch_3_Sel - 1].BatteryIsLow,
                _sensorValueArr_Out[Ch_4_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_4_Sel - 1].RandomId, _sensorValueArr_Out[Ch_4_Sel - 1].Hum, _sensorValueArr_Out[Ch_4_Sel - 1].BatteryIsLow,
                _sensorValueArr_Out[Ch_5_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_5_Sel - 1].RandomId, _sensorValueArr_Out[Ch_5_Sel - 1].Hum, _sensorValueArr_Out[Ch_5_Sel - 1].BatteryIsLow,
                _sensorValueArr_Out[Ch_6_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_6_Sel - 1].RandomId, _sensorValueArr_Out[Ch_6_Sel - 1].Hum, _sensorValueArr_Out[Ch_6_Sel - 1].BatteryIsLow,
                _sensorValueArr_Out[Ch_7_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_7_Sel - 1].RandomId, _sensorValueArr_Out[Ch_7_Sel - 1].Hum, _sensorValueArr_Out[Ch_7_Sel - 1].BatteryIsLow,
                _sensorValueArr_Out[Ch_8_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_8_Sel - 1].RandomId, _sensorValueArr_Out[Ch_8_Sel - 1].Hum, _sensorValueArr_Out[Ch_8_Sel - 1].BatteryIsLow,
                actCurrent, switchState, _location, timeFromLastSend, 0, _iteration, remainingRam, _forcedReboots, _badReboots, _azureSendErrors, willReboot ? 'X' : '.', forceSend, forceSend ? switchMessage : "");
        */

        /*
        if (AzureSendManager.hasFreePlaces())
        {
            AzureSendManager.EnqueueSampleValue(theRow);
            //Debug.Print("\r\nRow was writen to the Buffer. Number of rows in the buffer = " + AzureSendManager.Count + " " + (AzureSendManager.capacity - AzureSendManager.Count).ToString() + " places free");
        }
        // optionally send message to Debug.Print  *****************************************************
        SampleValue theReturn = AzureSendManager.PreViewNextSampleValue();
        */


        //DateTime thatTime = theReturn.TimeOfSample;
        //double thatDouble = theReturn.TheSampleValue;


        // *********************************************************************************************


        //#endregion






        // _azureSendThreads++;


        //bool Azure_useHTTPS = true;

        //  #region Send contents of the buffer to Azure

        // myAzureSendManager = new AzureSendManager(myCloudStorageAccount, analogTableName, _sensorValueHeader, _socketSensorHeader, caCerts, _timeOfLastSend, sendInterval, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
        // myAzureSendManager.AzureCommandSend += MyAzureSendManager_AzureCommandSend;

        // myAzureSendManager.Start();








        //CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

        // Create analog table if not existing           

        //CloudTable cloudTable = tableClient.GetTableReference(analogTableName + DateTime.Today.Year);


        //DateTime timeZoneCorrectedDateTime = DateTime.Now.AddMinutes(timeZoneOffset);



        // actDateTime is corrected for timeZoneOffset and DayLightSavingTime

        //DateTime actDateTime = timeZoneCorrectedDateTime.AddMinutes(GetDlstOffset(timeZoneCorrectedDateTime));



        //int timeZoneAndDlstCorrectedYear = timeZoneAndDlstCorrectedDateTime.Year;

        //CloudTable cloudTable = tableClient.GetTableReference(analogTableName + DateTime.Today.AddMinutes(timeZoneOffset).AddMinutes(GetDlstOffset()).Year);

        // CloudTable cloudTable = tableClient.GetTableReference(analogTableName + actDateTime.Year);


        /*
        if (!AnalogCloudTableExists)
        {
            try
            {
               // await cloudTable.CreateIfNotExistsAsync();

                AnalogCloudTableExists = true;

            }

            catch

            {

              //  Debug.WriteLine("Could not create Analog Table with name: \r\n" + cloudTable.Name + "\r\nCheck your Internet Connection.\r\nAction aborted.");



                writeAnalogToCloudTimer.Change(writeToCloudInterval * 1000, 30 * 60 * 1000);

                return;

            }

        }
        */




        // Populate Analog Table with Sinus Curve values for the actual day

        // cloudTable = tableClient.GetTableReference(analogTableName + DateTime.Today.Year);



        // cloudTable = tableClient.GetTableReference(analogTableName + actDateTime.Year);







        // formatting the PartitionKey this way to have the tables sorted with last added row upmost

        // string partitionKey = analogTablePartPrefix + actDateTime.Year + "-" + (12 - actDateTime.Month).ToString("D2");



        // formatting the RowKey (= revereDate) this way to have the tables sorted with last added row upmost
        /*
        string reverseDate = (10000 - actDateTime.Year).ToString("D4") + (12 - actDateTime.Month).ToString("D2") + (31 - actDateTime.Day).ToString("D2")
                   + (23 - actDateTime.Hour).ToString("D2") + (59 - actDateTime.Minute).ToString("D2") + (59 - actDateTime.Second).ToString("D2");
        */


        //string[] propertyNames = new string[4] { analog_Property_1, analog_Property_2, analog_Property_3, analog_Property_4 };

        // Dictionary<string, EntityProperty> entityDictionary = new Dictionary<string, EntityProperty>();

        //string sampleTime = actDateTime.Month.ToString("D2") + "/" + actDateTime.Day.ToString("D2") + "/" + actDateTime.Year + " " + actDateTime.Hour.ToString("D2") + ":" + actDateTime.Minute.ToString("D2") + ":" + actDateTime.Second.ToString("D2");

        //string sampleTime = actDate.Month.ToString("D2") + "/" + actDate.Day.ToString("D2") + "/" + actDate.Year + " " + actDate.Hour.ToString("D2") + ":" + actDate.Minute.ToString("D2") + ":" + actDate.Second.ToString("D2");

        //string sampleTime = actDateTime.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);



        // entityDictionary.Add("SampleTime", EntityProperty.GeneratePropertyForString(sampleTime));

        /*
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



           // entityDictionary.Add(propertyNames[i - 1], EntityProperty.GeneratePropertyForString(measuredValue.ToString("f1", System.Globalization.CultureInfo.InvariantCulture)));

        }
        */

        // DynamicTableEntity sendEntity = new DynamicTableEntity(partitionKey, reverseDate, null, entityDictionary);



        //  DynamicTableEntity dynamicTableEntity = await Common.InsertOrMergeEntityAsync(cloudTable, sendEntity);



        // Set timer to fire again


        // do not delete
        // ArrayList theQuery = new ArrayList();
        // HttpStatusCode resultQuery = queryTableEntities(myCloudStorageAccount, "mypeople", "$top=1", out theQuery);


        // AnalogValueSet analogValueSet = new AnalogValueSet(1, DateTime.Now, 10.0);

        //TableEntity tableEntity = new TableEntity(partitionKey, reverseDate);




        //  HttpStatusCode insertTableEntityResult = insertTableEntity(myCloudStorageAccount, analogTableName, tableEntity, out insertEtag);

        // Debug.WriteLine(GC.GetTotalMemory(true).ToString("N0"));


        //writeAnalogToCloudTimer.Change(  writeToCloudInterval * 10 * 1000, writeToCloudInterval * 10 * 1000);



        //Console.WriteLine("Analog data written to Cloud");

        // }

        #region private method insertTableEntity
        private static HttpStatusCode insertTableEntity(CloudStorageAccount pCloudStorageAccount, string pTable, TableEntity pTableEntity, out string pInsertETag)
        {
            table = new TableClient(pCloudStorageAccount, caCerts, _debug, _debug_level, wifi);
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
        private static HttpStatusCode createTable(CloudStorageAccount pCloudStorageAccount, string pTableName)
        {
            table = new TableClient(pCloudStorageAccount, caCerts, _debug, _debug_level, wifi);

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
        
        private static HttpStatusCode queryTableEntities(CloudStorageAccount pCloudStorageAccount, string tableName, string query, out ArrayList queryResult)
        {
            table = new TableClient(pCloudStorageAccount, caCerts, _debug, _debug_level, wifi);


            // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
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





        private static void MyAzureSendManager_AzureCommandSend(AzureSendManager sender, AzureSendManager.AzureSendEventArgs e)
        {
            Debug.WriteLine("Callback: commant sent");
        }

      





        

        #region Private method GetDlstOffset

        private static int GetDlstOffset(DateTime pDateTime)

        {
            //RoSchmi changed
            return 0;
            //return  DayLightSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, pDateTime, true);

        }

        #endregion

        /*
        private static void Run()
        {
           
            Debug.WriteLine("/r/nWaiting for Press BTN1 (1)");
            WaitForButton();



            //You only need to do this once, it'll get saved to the Wi-Fi internal config to be reused on reboot.

            wifi.JoinNetwork("", "");


            Debug.WriteLine("/r/nWaiting for Press BTN1 (2)");
            WaitForButton();

            wifi.ClearTlsServerRootCertificate();



            //You'll need to download and use the correct root certificates for the site you want to connect to.

            //Debug.WriteLine("/r/nWaiting for Press BTN1 (3)");
            //WaitForButton();

            //wifi.SetTlsServerRootCertificate(Resources.GetBytes(Resources.BinaryResources.Digicert___GHI));


            wifi.SetTlsServerRootCertificate(Resources.GetBytes(Resources.BinaryResources.Digicert___StackExchange));

            //wifi.SetTlsServerRootCertificate(Resources.GetBytes(Resources.BinaryResources.DigiCert_High_Assurance___StackOverflow));


            wifi.OpenSocket("any", 80, SPWF04SxConnectionType.Tcp, SPWF04SxConnectionSecurityType.None, "Socket1");
           

           
            listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);


            // Bind the listening socket to the port
            //IPAddress hostIP = IPAddress.Parse(_IP);
            //IPEndPoint ep = new IPEndPoint(hostIP, _Port);
            //Debug.Print("Bin vor Bindung des Listen Sockets");
            //listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            //listenSocket.Bind(ep);
            
            // Start listening

            //listenSocket.Listen(1);
          



            //clientSocket = AcceptWithTimeout(listenSocket, 20000);

     





            

               

            }
        }
        */

        


        


        #region Region TestHttp(host, url, port, security, get) - for WiFi - 
        private static void TestHttp(string host, string url, int port, SPWF04SxConnectionSecurityType security, bool get)
        {
            var buffer = new byte[512];
            var start = DateTime.UtcNow;
            var code = get ? wifi.SendHttpGet(host, url, port, security) : wifi.SendHttpPost(host, url, port, security);
            Debug.WriteLine($"HTTP {code}");
            var total = 0;
            while (wifi.ReadHttpResponse(buffer, 0, buffer.Length) is var read && read > 0)
            {
                total += read;
                try
                {
                    Debugger.Log(0, "", Encoding.UTF8.GetString(buffer, 0, read));

                }
                catch
                {
                    Debugger.Log(0, "", Encoding.UTF8.GetString(buffer, 0, read - 1));
                }
                Thread.Sleep(100);
            }
            Debug.WriteLine($"\r\nRead: {total:N0} in {(DateTime.UtcNow - start).TotalMilliseconds:N0}ms");
        }
        #endregion


        #region Region TestHttp (host, url, commonName  - for NET.  -
        private static void TestHttp(string host, string url, string commonName = null)
        {
            if (commonName != null)
            {
                wifi.ForceSocketsTls = true;
                wifi.ForceSocketsTlsCommonName = commonName;
            }
            var buffer = new byte[512];
            var start = DateTime.UtcNow;
            var req = (HttpWebRequest)HttpWebRequest.Create(host + url);
            req.HttpsAuthentCerts = new[] { new X509Certificate() };
            var res = (HttpWebResponse)req.GetResponse();
            var str = res.GetResponseStream();
            Debug.WriteLine($"HTTP {res.StatusCode}");
            var total = 0;
            while (str.Read(buffer, 0, buffer.Length) is var read && read > 0)
            {
                total += read;
                try
                {
                    Debugger.Log(0, "", Encoding.UTF8.GetString(buffer, 0, read));
                }
                catch
                {
                    Debugger.Log(0, "", Encoding.UTF8.GetString(buffer, 0, read - 1));
                }
                Thread.Sleep(100);
            }
            Debug.WriteLine($"\r\nRead: {total:N0} in {(DateTime.UtcNow - start).TotalMilliseconds:N0}ms");
        }
        #endregion


        #region Region WaitForButton
        /*
        private static void WaitForButton()
        {
            while (btn1.Read() == GpioPinValue.High)
            {
               // led1.Write(led1.Read() == GpioPinValue.High ? GpioPinValue.Low : GpioPinValue.High);
                Thread.Sleep(50);
            }
            while (btn1.Read() == GpioPinValue.Low)
                Thread.Sleep(50);
        }
        */
        #endregion

    }
}















