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
using RoSchmi.Net;
using RoSchmi.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx;

using RoSchmi.Net.Azure.Storage;
using AzureDataSender_FEZ.TableStorage;
using AzureDataSender_FEZ;
using RoSchmi.DayLightSavingTime;
using GHIElectronics.TinyCLR.Native;
using PervasiveDigital.Utilities;




namespace AzureDataSender_FEZ
{
    class Program
    {
        private static AzureStorageHelper.DebugMode _debug = AzureStorageHelper.DebugMode.StandardDebug;
        private static AzureStorageHelper.DebugLevel _debug_level = AzureStorageHelper.DebugLevel.DebugAll;
        private static TableClient table;

        //private static int timeZoneOffset = -720;
        //private static int timeZoneOffset = -720;
        //private static int timeZoneOffset = -300;     // New York offest in minutes of your timezone to Greenwich Mean Time (GMT)
        //private static int timeZoneOffset = -60;
        //private static int timeZoneOffset = 0;       // Lissabon offest in minutes of your timezone to Greenwich Mean Time (GMT)
        private static int timeZoneOffset = 60;      // Berlin offest in minutes of your timezone to Greenwich Mean Time (GMT)

        //private static int timeZoneOffset = 120;
        //private static int timeZoneOffset = 180;     // Moskau offest in minutes of your timezone to Greenwich Mean Time (GMT)                                              
        //private static int timeZoneOffset = 240;                                            
        //private static int timeZoneOffset = 680;                                             
        //private static int timeZoneOffset = 720;                                                                                          

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

        private static string _sensorValueHeader = "None";
        private static string _socketSensorHeader = "None";
        private static DateTime _timeOfLastSend = DateTime.Now.AddMinutes(-5.0);
        private static TimeSpan sendInterval = new TimeSpan(0, 10, 0);
        private static int _azureSends = 1;
        private static AzureStorageHelper.DebugMode _AzureDebugMode = AzureStorageHelper.DebugMode.StandardDebug;
        private static AzureStorageHelper.DebugLevel _AzureDebugLevel = AzureStorageHelper.DebugLevel.DebugAll;
        private static string fiddlerIPAddress = "0.0.0.0";
        private static bool attachFiddler = false;
        private static int fiddlerPort = 77;



        private static bool AnalogCloudTableExists = false;

        private static DataContainer dataContainer = new DataContainer(new TimeSpan(0, 15, 0));

        private static int _azureSendThreads = 0;

        private static AzureSendManager myAzureSendManager;

        // Set the name of the table for analog values (name must be conform to special rules: see Azure)

        private static string analogTableName = "AnalogRolTable";

        private static string analogTablePartPrefix = "Y2_";     // Your choice (name must be conform to special rules: see Azure)
        private static bool augmentPartitionKey = true;

        // Set the names of 4 properties (Columns) of the table for analog values

        static string analog_Property_1 = "T_1";  // Your choice (name must be conform to special rules: see Azure)

        static string analog_Property_2 = "T_2";

        static string analog_Property_3 = "T_3";

        static string analog_Property_4 = "T_4";



        static string onOffTablePartPrefix = "Y3_";  // Your choice (name must be conform to special rules: see Azure)

        private static string connectionString;

        // Set intervals (in seconds)

        static int readInterval = 4;            // in this interval analog sensors are read

        //static int writeToCloudInterval = 600;   // in this interval the analog data are stored to the cloud
        static int writeToCloudInterval = 10;   // in this interval the analog data are stored to the cloud

        static int OnOffToggleInterval = 420;    // in this interval the On/Off state is toggled (test values)

        static int invalidateInterval = 900;    // if analog values ar not actualized in this interval, they are set to invalid (999.9)

        //****************  End of Settings to be changed by user   ********************************* 



        private static Timer writeAnalogToCloudTimer;

        private static AutoResetEvent waitForWiFiReady = new AutoResetEvent(false);

        private static readonly object LockProgram = new object();

        //private static GpioPin led1;

        private static GpioPin btn1;

        //private static SPWF04SxInterface wifi;
        private static SPWF04SxInterfaceRoSchmi  wifi;


        private static WiFi_SPWF04S_Device wiFi_SPWF04S_Device;

        private static DateTime dateTimeNtpServerDelivery = DateTime.MinValue;
        private static TimeSpan timeDeltaNTPServerDelivery = new TimeSpan(0);
        private static bool dateTimeAndIpAddressAreSet = false;

        private static IPAddress ip4Address = IPAddress.Parse("0.0.0.0");

        static byte[] caDigiCertGlobalRootCA = Resources.GetBytes(Resources.BinaryResources.DigiCertGlobalRootCA);

        static byte[] caGHI = Resources.GetBytes(Resources.BinaryResources.Digicert___GHI);

        static byte[] caAzure =  Resources.GetBytes(Resources.BinaryResources.DigiCert_Baltimore_Root);

        static byte[] caStackExcange = (Resources.GetBytes(Resources.BinaryResources.Digicert___StackExchange));

        static string wiFiSSID_1 = ResourcesSecret.GetString(ResourcesSecret.StringResources.SSID_1);
        static string wiFiKey_1 = ResourcesSecret.GetString(ResourcesSecret.StringResources.Key_1);

        //static string wiFiSSID_2 = ResourcesSecret.GetString(ResourcesSecret.StringResources.SSID_2);
        //static string wiFiKey_2 = ResourcesSecret.GetString(ResourcesSecret.StringResources.Key_2);

         // Set your Azure Storage Account Credentials here

        //static string storageAccount = "your Accountname";
        static string storageAccountName = ResourcesSecret.GetString(ResourcesSecret.StringResources.AzureAccountName);

        //private static bool Azure_useHTTPS = true;
        private static bool Azure_useHTTPS = false;

        private static CloudStorageAccount myCloudStorageAccount;


        //static string storageKey = "your key";
        static string storageKey = ResourcesSecret.GetString(ResourcesSecret.StringResources.AzureAccountKey);

        private static X509Certificate[] caCerts;

        private static GpioPin _pinPyton;

        #region Region Main
        static void Main()
        {
            
            Debug.WriteLine("Remaining Ram at start  Main: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);

            var cont = GpioController.GetDefault();

            //FEZ
            var reset = cont.OpenPin(FEZ.GpioPin.WiFiReset);

            _pinPyton = cont.OpenPin(FEZCLR.GpioPin.PA0);
            _pinPyton.SetDriveMode(GpioPinDriveMode.InputPullDown);

            var irq = cont.OpenPin(FEZ.GpioPin.WiFiInterrupt);
            
            var scont = SpiController.FromName(FEZ.SpiBus.WiFi);
            //var spi = scont.GetDevice(SPWF04SxInterface.GetConnectionSettings(SpiChipSelectType.Gpio, FEZ.GpioPin.WiFiChipSelect));
            var spi = scont.GetDevice(SPWF04SxInterfaceRoSchmi.GetConnectionSettings(SpiChipSelectType.Gpio, FEZ.GpioPin.WiFiChipSelect));

            //led1 = cont.OpenPin(FEZ.GpioPin.Led1);
            btn1 = cont.OpenPin(FEZ.GpioPin.Btn1);


            //UC5550
            //var reset = cont.OpenPin(UC5550.GpioPin.PG12);
            //var irq = cont.OpenPin(UC5550.GpioPin.PB11);
            //var scont = SpiController.FromName(UC5550.SpiBus.Spi5);
            //var spi = scont.GetDevice(SPWF04SxInterface.GetConnectionSettings(SpiChipSelectType.Gpio, UC5550.GpioPin.PB10));
            //led1 = cont.OpenPin(UC5550.GpioPin.PG3);
            //btn1 = cont.OpenPin(UC5550.GpioPin.PI8);

            //led1.SetDriveMode(GpioPinDriveMode.Output);
            btn1.SetDriveMode(GpioPinDriveMode.InputPullUp);

            //wifi = new SPWF04SxInterface(spi, irq, reset);
            wifi = new SPWF04SxInterfaceRoSchmi(spi, irq, reset);

            caCerts = new X509Certificate[] { new X509Certificate(caDigiCertGlobalRootCA), new X509Certificate(caGHI)};




            //wiFi_SPWF04S_Device = new WiFi_SPWF04S_Device(wifi, NetworkInterface.ActiveNetworkInterface, caCerts, wiFiSSID_1, wiFiKey_1);


            wiFi_SPWF04S_Device = new WiFi_SPWF04S_Device(wifi, wiFiSSID_1, wiFiKey_1);
            //wiFi_SPWF04S_Device = new WiFi_SPWF04S_Device(wifi, wiFiSSID_2, wiFiKey_2);
            //wiFi_SPWF04S_Device = new WiFi_SPWF04S_Device(wifi, NetworkIcaCerts, wiFiSSID_2, wiFiKey_2);

            wiFi_SPWF04S_Device.Ip4AddressAssigned += WiFi_SPWF04S_Device_Ip4AddressAssigned;

            wiFi_SPWF04S_Device.DateTimeNtpServerDelivered += WiFi_SPWF04S_Device_DateTimeNtpServerDelivered;
            wiFi_SPWF04S_Device.Initialize();

           // wifi.ClearTlsServerRootCertificate();

            //wifi.Id

            //wifi.GetPhysicalAddress

            
           
            connectionString = "DefaultEndpointsProtocol=https;AccountName=" + storageAccountName + "; AccountKey=" + storageKey;

            myCloudStorageAccount = new CloudStorageAccount(storageAccountName, storageKey, useHttps: Azure_useHTTPS);

            // Initialization for each table must be done in main
            //myAzureSendManager = new AzureSendManager(myCloudStorageAccount, analogTableName, _sensorValueHeader, _socketSensorHeader, caCerts, _timeOfLastSend, sendInterval, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS); 
            
            //AzureSendManager.sampleTimeOfLastSent = DateTime.Now.AddDays(-10.0);    // Date in the past
            //AzureSendManager.InitializeQueue();


            waitForWiFiReady.WaitOne();  // ******** Wait for IP Address and NTP Time ready   *****************************************************

            //wifi.ClearTlsServerRootCertificate();

            var dummy4 = 1;
            for (int i = 0; i < 100; i++)    // Wait for 15 sec
            {
                Thread.Sleep(100);
            }

            string theTime = wifi.GetTime();

         

            string theFiles = wifi.ListRamFiles();
            string convertString  = theFiles.Replace("File:I", "\r\n");
            string[] fileArray = convertString.Split((char)0x0D);

            wifi.SetConfiguration("ramdisk_memsize", "18");


            wifi.CreateRamFile("rolandsfile", Encoding.UTF8.GetBytes("Das hat geklappt"));

            theFiles = wifi.ListRamFiles();
            convertString = theFiles.Replace("File:I", "\r\n");
            fileArray = convertString.Split((char)0x0D);



            wifi.ListRamFiles();

            for (int i = 0; i < 100; i++)
            {
                Thread.Sleep(100);
            }

           


            //TestHttp("http://files.ghielectronics.com", "/");

             string host = "https://www.roschmionline.de";
            string commonName = "*.roschmionline.de";

            //string host = "http://files.ghielectronics.com";
            //string host = "https://meta.stackexchange.com";

            //string url = "/";
            string url = "/index.html";

            //string commonName = "*.stackexchange.com";
            

            //string commonName = null;


            wifi.ClearTlsServerRootCertificate();
            Thread.Sleep(10);
            //wifi.SetTlsServerRootCertificate(Resources.GetBytes(Resources.BinaryResources.Digicert___StackExchange));

            Debug.WriteLine("Remaining Ram before creating Request in Main: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);

            wifi.ClearTlsServerRootCertificate();
            wifi.SetTlsServerRootCertificate(caDigiCertGlobalRootCA);

            //wifi.SetTlsServerRootCertificate(caGHI);

            Thread.Sleep(10);
           
           if (commonName != null)
           {
                wifi.ForceSocketsTls = true;
                wifi.ForceSocketsTlsCommonName = commonName;
           }
           
            
            Thread.Sleep(50);
            string responseBody = string.Empty;
            
            var start = DateTime.UtcNow;
            var req = (HttpWebRequest)HttpWebRequest.Create(host + url);
            //req.HttpsAuthentCerts = caCerts;
            req.HttpsAuthentCerts = new[] { new X509Certificate() };

            HttpWebResponse res = null;
            try
            {
                res = (HttpWebResponse)req.GetResponse();
            }
            catch (Exception ex)
            {
                var theMessage = ex.Message;
            }

            var buffer = new byte[512];
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
                
                if (responseBody.Length < 500)
                {
                    responseBody += Encoding.UTF8.GetString(buffer, 0, read);
                }
                
                Thread.Sleep(100);
            }
            Debug.WriteLine($"\r\nRead: {total:N0} in {(DateTime.UtcNow - start).TotalMilliseconds:N0}ms");
            

            Debug.WriteLine("Remaining Ram at end of main: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);

            writeAnalogToCloudTimer = new System.Threading.Timer(new TimerCallback(writeAnalogToCloudTimer_tick), null, writeToCloudInterval * 1000, Timeout.Infinite);
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

            writeAnalogToCloudTimer.Change(10 * 60 * 1000, 10 * 60 * 1000);

            bool validStorageAccount = false;

            CloudStorageAccount storageAccount = null;

            /*
            Exception CreateStorageAccountException = null;
            try
            {
                storageAccount = Common.CreateStorageAccountFromConnectionString(connectionString);
                validStorageAccount = true;
            }
            catch (Exception ex0)
            {
                CreateStorageAccountException = ex0;
            }

            if (!validStorageAccount)
            {

                // MessageBox.Show("Storage Account not valid\r\nEnter valid Storage Account and valid Key", "Alert", MessageBoxButton.OK);
                writeAnalogToCloudTimer.Change(writeToCloudInterval * 1000, 30 * 60 * 1000);
                return;

            }
            */
            //TestHttp("http://files.ghielectronics.com", "/");

            


            wifi.SetTlsServerRootCertificate(Resources.GetBytes(Resources.BinaryResources.Digicert___StackExchange));

            
            string commonName = "*.stackexchange.com";

            if (commonName != null)
            {
                wifi.ForceSocketsTls = true;
                wifi.ForceSocketsTlsCommonName = commonName;
            }


            bool Azure_useHTTPS = true;

            myCloudStorageAccount = new CloudStorageAccount(storageAccountName, storageKey, useHttps: Azure_useHTTPS);

            var theRes = createTable(myCloudStorageAccount, "AnalogRoland2018");

            #region Set the partitionKey
            string partitionKey = analogTablePartPrefix;            // Set Partition Key for Azure storage table
            if (augmentPartitionKey == true)                // if wanted, augment with year and month (12 - month for right order)                                                          
            { partitionKey = partitionKey + DateTime.Today.Year + "-" + (12 - DateTime.Now.Month).ToString("D2"); }
            #endregion

            // Populate Analog Table with Sinus Curve values for the actual day
            // cloudTable = tableClient.GetTableReference(TextBox_AnalogTable.Text + DateTime.Today.Year);
           

            DateTime actDate = DateTime.Now;




            // formatting the RowKey (= revereDate) this way to have the tables sorted with last added row upmost
            string reverseDate = (10000 - actDate.Year).ToString("D4") + (12 - actDate.Month).ToString("D2") + (31 - actDate.Day).ToString("D2")
                       + (23 - actDate.Hour).ToString("D2") + (59 - actDate.Minute).ToString("D2") + (59 - actDate.Second).ToString("D2");

            string sampleTime = actDate.Month.ToString("D2") + "/" + actDate.Day.ToString("D2") + "/" + actDate.Year + " " + actDate.Hour.ToString("D2") + ":" + actDate.Minute.ToString("D2") + ":" + actDate.Second.ToString("D2");

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

            #region Write new row to the buffer

            bool forceSend = true;

            // RoSchmi  tochange
            //SampleValue theRow = new SampleValue(partitionKey, DateTime.Now.AddMinutes(RoSchmi.DayLightSavingTime.DayLightSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)), 10.1, 10.2, 10.3, 10.4, forceSend);
            SampleValue theRow = new SampleValue(partitionKey, DateTime.Now.AddMinutes(0), 10.1, 10.2, 10.3, 10.4, forceSend);

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


            #endregion

            




            _azureSendThreads++;
           

            //bool Azure_useHTTPS = true;

            #region Send contents of the buffer to Azure

            // myAzureSendManager = new AzureSendManager(myCloudStorageAccount, analogTableName, _sensorValueHeader, _socketSensorHeader, caCerts, _timeOfLastSend, sendInterval, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
            // myAzureSendManager.AzureCommandSend += MyAzureSendManager_AzureCommandSend;
            
            // myAzureSendManager.Start();
#if DebugPrint
                Debug.Print("\r\nRemaining Ram:" + remainingRam.ToString() + "\r\n");
#endif
            #endregion




            


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

            writeAnalogToCloudTimer.Change(writeToCloudInterval * 1000, 30 * 60 * 1000);



            //Console.WriteLine("Analog data written to Cloud");

        }

        #region private method createTable
        private static HttpStatusCode createTable(CloudStorageAccount pCloudStorageAccount, string pTableName)
        {
            table = new TableClient(pCloudStorageAccount, caCerts, _debug, _debug_level);

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



        private static void MyAzureSendManager_AzureCommandSend(AzureSendManager sender, AzureSendManager.AzureSendEventArgs e)
        {
            Debug.WriteLine("Callback: commant sent");
        }

        #endregion





        private static void WiFi_SPWF04S_Device_Ip4AddressAssigned(WiFi_SPWF04S_Device sender, WiFi_SPWF04S_Device.Ip4AssignedEventArgs e)
        {
            lock (LockProgram)
            {
                ip4Address = e.Ip4Address;

                // RoSchmi: has to be deleted
                waitForWiFiReady.Set();
                dateTimeAndIpAddressAreSet = true;
            }
        }

        private static void WiFi_SPWF04S_Device_DateTimeNtpServerDelivered(WiFi_SPWF04S_Device sender, WiFi_SPWF04S_Device.NTPServerDeliveryEventArgs e)
        {
            lock (LockProgram)
            {
                dateTimeNtpServerDelivery = e.DateTimeNTPServer;
                timeDeltaNTPServerDelivery = e.TimeDeltaNTPServer;
                SystemTime.SetTime(dateTimeNtpServerDelivery, 60);
                var theControl = DateTime.Now;
                dateTimeAndIpAddressAreSet = true;
            }
            waitForWiFiReady.Set();
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

     





            while (true)
            {
                Debug.WriteLine("/r/nWaiting for Press BTN1 (4) to repeat webrequest");
                WaitForButton();



                //.NET

                //TestHttp("http://files.ghielectronics.com", "/");

                TestHttp("https://meta.stackexchange.com", "/", "*.stackexchange.com");

                //TestSocket("www.ghielectronics.com", "/robots.txt", 80);

                //TestSocket("meta.stackoverflow.com", "/", 443, "*.stackexchange.com");



                //WiFi

                // TestHttp("files.ghielectronics.com", "/", 80, SPWF04SxConnectionSecurityType.None, true);

                //TestHttp("www.google.com", "/?gws_rd=ssl", 80, SPWF04SxConnectionSecurityType.None, true);

                //TestHttp("meta.stackexchange.com", "/", 443, SPWF04SxConnectionSecurityType.Tls, true);

                //TestSocket("www.ghielectronics.com", "/robots.txt", 80, SPWF04SxConnectionType.Tcp, SPWF04SxConnectionSecurityType.None);

                //TestSocket("www.ghielectronics.com", "/robots.txt", 443, SPWF04SxConnectionType.Tcp, SPWF04SxConnectionSecurityType.Tls, "*.ghielectronics.com");

                //TestSocket("www.google.com", "/?gws_rd=ssl", 80, SPWF04SxConnectionyType.Tcp, SPWF04SxConnectionSecurityType.None);

                //TestSocket("meta.stackoverflow.com", "/", 443, SPWF04SxConnectionyType.Tcp, SPWF04SxConnectionSecurityType.Tls, "*.stackexchange.com");



                Debug.WriteLine(GC.GetTotalMemory(true).ToString("N0"));

            }
        }
        */

        #region Region TestSocket (host, url, port, connectionType, connectionSecurity, commonName)
        private static void TestSocket(string host, string url, int port, SPWF04SxConnectionType connectionType, SPWF04SxConnectionSecurityType connectionSecurity, string commonName = null)
        {
            var buffer = new byte[512];
            var id = wifi.OpenSocket(host, port, connectionType, connectionSecurity, commonName);
            var cont = true;

            while (cont)
            {
                var start = DateTime.UtcNow;

                wifi.WriteSocket(id, Encoding.UTF8.GetBytes($"GET {url} HTTP/1.1\r\nHost: {host}\r\n\r\n"));

                Thread.Sleep(100);
                var total = 0;

                var first = true;

                while ((wifi.QuerySocket(id) is var avail && avail > 0) || first || total < 120)
                {
                    if (avail > 0)
                    {
                        first = false;
                        var read = wifi.ReadSocket(id, buffer, 0, Math.Min(avail, buffer.Length));
                        total += read;
                        Debugger.Log(0, "", Encoding.UTF8.GetString(buffer, 0, read));
                    }
                    Thread.Sleep(100);
                }
                Debug.WriteLine($"\r\nRead: {total:N0} in {(DateTime.UtcNow - start).TotalMilliseconds:N0}ms");
                WaitForButton();
            }
            wifi.CloseSocket(id);
        }
        #endregion


        #region Region TestSocket(host, url, port, commonName)
        private static void TestSocket(string host, string url, int port, string commonName = null)
        {
            if (commonName != null)
            {
                wifi.ForceSocketsTls = true;

                wifi.ForceSocketsTlsCommonName = commonName;
            }
            var buffer = new byte[512];

            var data = Encoding.UTF8.GetBytes($"GET {url} HTTP/1.1\r\nHost: {host}\r\n\r\n");

            var entry = Dns.GetHostEntry(host);

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Connect(new IPEndPoint(entry.AddressList[0], port));
            socket.ReceiveTimeout = 250;
            var cont = true;

            while (cont)
            {
                var start = DateTime.UtcNow;
                var written = socket.Send(data);
                Thread.Sleep(100);
                var total = 0;

                var first = true;

                while ((socket.Poll(0, SelectMode.SelectRead) is var ready && ready) || first || total < 120)
                {
                    if (ready && socket.Receive(buffer) is var read && read > 0)
                    {
                        first = false;
                        Debugger.Log(0, "", Encoding.UTF8.GetString(buffer, 0, read));
                        total += read;
                    }
                    Thread.Sleep(100);
                }
                Debug.WriteLine($"\r\nRead: {total:N0} in {(DateTime.UtcNow - start).TotalMilliseconds:N0}ms");
                WaitForButton();
            }
            socket.Close();
        }
        #endregion


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
        #endregion


        #region Region WindToName
        private static string WindToName(SPWF04SxIndication wind)
        {
            switch (wind)
            {
                case SPWF04SxIndication.ConsoleActive: return nameof(SPWF04SxIndication.ConsoleActive);

                case SPWF04SxIndication.PowerOn: return nameof(SPWF04SxIndication.PowerOn);

                case SPWF04SxIndication.Reset: return nameof(SPWF04SxIndication.Reset);

                case SPWF04SxIndication.WatchdogRunning: return nameof(SPWF04SxIndication.WatchdogRunning);

                case SPWF04SxIndication.LowMemory: return nameof(SPWF04SxIndication.LowMemory);

                case SPWF04SxIndication.WiFiHardwareFailure: return nameof(SPWF04SxIndication.WiFiHardwareFailure);

                case SPWF04SxIndication.ConfigurationFailure: return nameof(SPWF04SxIndication.ConfigurationFailure);

                case SPWF04SxIndication.HardFault: return nameof(SPWF04SxIndication.HardFault);

                case SPWF04SxIndication.StackOverflow: return nameof(SPWF04SxIndication.StackOverflow);

                case SPWF04SxIndication.MallocFailed: return nameof(SPWF04SxIndication.MallocFailed);

                case SPWF04SxIndication.RadioStartup: return nameof(SPWF04SxIndication.RadioStartup);

                case SPWF04SxIndication.WiFiPSMode: return nameof(SPWF04SxIndication.WiFiPSMode);

                case SPWF04SxIndication.Copyright: return nameof(SPWF04SxIndication.Copyright);

                case SPWF04SxIndication.WiFiBssRegained: return nameof(SPWF04SxIndication.WiFiBssRegained);

                case SPWF04SxIndication.WiFiSignalLow: return nameof(SPWF04SxIndication.WiFiSignalLow);

                case SPWF04SxIndication.WiFiSignalOk: return nameof(SPWF04SxIndication.WiFiSignalOk);

                case SPWF04SxIndication.BootMessages: return nameof(SPWF04SxIndication.BootMessages);

                case SPWF04SxIndication.KeytypeNotImplemented: return nameof(SPWF04SxIndication.KeytypeNotImplemented);

                case SPWF04SxIndication.WiFiJoin: return nameof(SPWF04SxIndication.WiFiJoin);

                case SPWF04SxIndication.WiFiJoinFailed: return nameof(SPWF04SxIndication.WiFiJoinFailed);

                case SPWF04SxIndication.WiFiScanning: return nameof(SPWF04SxIndication.WiFiScanning);

                case SPWF04SxIndication.ScanBlewUp: return nameof(SPWF04SxIndication.ScanBlewUp);

                case SPWF04SxIndication.ScanFailed: return nameof(SPWF04SxIndication.ScanFailed);

                case SPWF04SxIndication.WiFiUp: return nameof(SPWF04SxIndication.WiFiUp);

                case SPWF04SxIndication.WiFiAssociationSuccessful: return nameof(SPWF04SxIndication.WiFiAssociationSuccessful);

                case SPWF04SxIndication.StartedAP: return nameof(SPWF04SxIndication.StartedAP);

                case SPWF04SxIndication.APStartFailed: return nameof(SPWF04SxIndication.APStartFailed);

                case SPWF04SxIndication.StationAssociated: return nameof(SPWF04SxIndication.StationAssociated);

                case SPWF04SxIndication.DhcpReply: return nameof(SPWF04SxIndication.DhcpReply);

                case SPWF04SxIndication.WiFiBssLost: return nameof(SPWF04SxIndication.WiFiBssLost);

                case SPWF04SxIndication.WiFiException: return nameof(SPWF04SxIndication.WiFiException);

                case SPWF04SxIndication.WiFiHardwareStarted: return nameof(SPWF04SxIndication.WiFiHardwareStarted);

                case SPWF04SxIndication.WiFiNetwork: return nameof(SPWF04SxIndication.WiFiNetwork);

                case SPWF04SxIndication.WiFiUnhandledEvent: return nameof(SPWF04SxIndication.WiFiUnhandledEvent);

                case SPWF04SxIndication.WiFiScan: return nameof(SPWF04SxIndication.WiFiScan);

                case SPWF04SxIndication.WiFiUnhandledIndication: return nameof(SPWF04SxIndication.WiFiUnhandledIndication);

                case SPWF04SxIndication.WiFiPoweredDown: return nameof(SPWF04SxIndication.WiFiPoweredDown);

                case SPWF04SxIndication.HWInMiniAPMode: return nameof(SPWF04SxIndication.HWInMiniAPMode);

                case SPWF04SxIndication.WiFiDeauthentication: return nameof(SPWF04SxIndication.WiFiDeauthentication);

                case SPWF04SxIndication.WiFiDisassociation: return nameof(SPWF04SxIndication.WiFiDisassociation);

                case SPWF04SxIndication.WiFiUnhandledManagement: return nameof(SPWF04SxIndication.WiFiUnhandledManagement);

                case SPWF04SxIndication.WiFiUnhandledData: return nameof(SPWF04SxIndication.WiFiUnhandledData);

                case SPWF04SxIndication.WiFiUnknownFrame: return nameof(SPWF04SxIndication.WiFiUnknownFrame);

                case SPWF04SxIndication.Dot11Illegal: return nameof(SPWF04SxIndication.Dot11Illegal);

                case SPWF04SxIndication.WpaCrunchingPsk: return nameof(SPWF04SxIndication.WpaCrunchingPsk);

                case SPWF04SxIndication.WpaTerminated: return nameof(SPWF04SxIndication.WpaTerminated);

                case SPWF04SxIndication.WpaStartFailed: return nameof(SPWF04SxIndication.WpaStartFailed);

                case SPWF04SxIndication.WpaHandshakeComplete: return nameof(SPWF04SxIndication.WpaHandshakeComplete);

                case SPWF04SxIndication.GpioInterrupt: return nameof(SPWF04SxIndication.GpioInterrupt);

                case SPWF04SxIndication.Wakeup: return nameof(SPWF04SxIndication.Wakeup);

                case SPWF04SxIndication.PendingData: return nameof(SPWF04SxIndication.PendingData);

                case SPWF04SxIndication.InputToRemote: return nameof(SPWF04SxIndication.InputToRemote);

                case SPWF04SxIndication.OutputFromRemote: return nameof(SPWF04SxIndication.OutputFromRemote);

                case SPWF04SxIndication.SocketClosed: return nameof(SPWF04SxIndication.SocketClosed);

                case SPWF04SxIndication.IncomingSocketClient: return nameof(SPWF04SxIndication.IncomingSocketClient);

                case SPWF04SxIndication.SocketClientGone: return nameof(SPWF04SxIndication.SocketClientGone);

                case SPWF04SxIndication.SocketDroppingData: return nameof(SPWF04SxIndication.SocketDroppingData);

                case SPWF04SxIndication.RemoteConfiguration: return nameof(SPWF04SxIndication.RemoteConfiguration);

                case SPWF04SxIndication.FactoryReset: return nameof(SPWF04SxIndication.FactoryReset);

                case SPWF04SxIndication.LowPowerMode: return nameof(SPWF04SxIndication.LowPowerMode);

                case SPWF04SxIndication.GoingIntoStandby: return nameof(SPWF04SxIndication.GoingIntoStandby);

                case SPWF04SxIndication.ResumingFromStandby: return nameof(SPWF04SxIndication.ResumingFromStandby);

                case SPWF04SxIndication.GoingIntoDeepSleep: return nameof(SPWF04SxIndication.GoingIntoDeepSleep);

                case SPWF04SxIndication.ResumingFromDeepSleep: return nameof(SPWF04SxIndication.ResumingFromDeepSleep);

                case SPWF04SxIndication.StationDisassociated: return nameof(SPWF04SxIndication.StationDisassociated);

                case SPWF04SxIndication.SystemConfigurationUpdated: return nameof(SPWF04SxIndication.SystemConfigurationUpdated);

                case SPWF04SxIndication.RejectedFoundNetwork: return nameof(SPWF04SxIndication.RejectedFoundNetwork);

                case SPWF04SxIndication.RejectedAssociation: return nameof(SPWF04SxIndication.RejectedAssociation);

                case SPWF04SxIndication.WiFiAuthenticationTimedOut: return nameof(SPWF04SxIndication.WiFiAuthenticationTimedOut);

                case SPWF04SxIndication.WiFiAssociationTimedOut: return nameof(SPWF04SxIndication.WiFiAssociationTimedOut);

                case SPWF04SxIndication.MicFailure: return nameof(SPWF04SxIndication.MicFailure);

                case SPWF04SxIndication.UdpBroadcast: return nameof(SPWF04SxIndication.UdpBroadcast);

                case SPWF04SxIndication.WpsGeneratedDhKeyset: return nameof(SPWF04SxIndication.WpsGeneratedDhKeyset);

                case SPWF04SxIndication.WpsEnrollmentAttemptTimedOut: return nameof(SPWF04SxIndication.WpsEnrollmentAttemptTimedOut);

                case SPWF04SxIndication.SockdDroppingClient: return nameof(SPWF04SxIndication.SockdDroppingClient);

                case SPWF04SxIndication.NtpServerDelivery: return nameof(SPWF04SxIndication.NtpServerDelivery);

                case SPWF04SxIndication.DhcpFailedToGetLease: return nameof(SPWF04SxIndication.DhcpFailedToGetLease);

                case SPWF04SxIndication.MqttPublished: return nameof(SPWF04SxIndication.MqttPublished);

                case SPWF04SxIndication.MqttClosed: return nameof(SPWF04SxIndication.MqttClosed);

                case SPWF04SxIndication.WebSocketData: return nameof(SPWF04SxIndication.WebSocketData);

                case SPWF04SxIndication.WebSocketClosed: return nameof(SPWF04SxIndication.WebSocketClosed);

                case SPWF04SxIndication.FileReceived: return nameof(SPWF04SxIndication.FileReceived);

                default: return "Other";
            }
        }
        #endregion
    }
}















