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



        
        private static int AnalogCloudTableYear = 1900;

       // private static DataContainer dataContainer = new DataContainer(new TimeSpan(0, 15, 0));

        private static int _azureSendThreads = 0;

      

        // Set the name of the table for analog values (name must be conform to special rules: see Azure)

        private static string analogTableName = "AnalogTestValues";

        private static string analogTablePartPrefix = "Y2_";     // Your choice (name must be conform to special rules: see Azure)
        private static bool augmentPartitionKey = true;

        // Set the names of 4 properties (Columns) of the table for analog values

        /*
        static string analog_Property_1 = "T_1";  // Your choice (name must be conform to special rules: see Azure)

        static string analog_Property_2 = "T_2";

        static string analog_Property_3 = "T_3";

        static string analog_Property_4 = "T_4";
        */


        static string onOffTablePartPrefix = "Y3_";  // Your choice (name must be conform to special rules: see Azure)

        private static string connectionString;

        // Set intervals (in seconds)

        static int readInterval = 4;            // in this interval analog sensors are read

        //static int writeToCloudInterval = 600;   // in this interval the analog data are stored to the cloud
        static int writeToCloudInterval = 1;   // in this interval the analog data are stored to the cloud

        static int OnOffToggleInterval = 420;    // in this interval the On/Off state is toggled (test values)

        static int invalidateInterval = 900;    // if analog values ar not actualized in this interval, they are set to invalid (999.9)

        //****************  End of Settings to be changed by user   ********************************* 



        private static Timer writeAnalogToCloudTimer;

        private static AutoResetEvent waitForWiFiReady = new AutoResetEvent(false);

        private static readonly object LockProgram = new object();

        //private static GpioPin led1;



        //private static SPWF04SxInterface wifi;
        public static SPWF04SxInterfaceRoSchmi  wifi;
        //public static ISPWF04SxInterface wifi;


        private static WiFi_SPWF04S_Device wiFi_SPWF04S_Device;

        private static DateTime dateTimeNtpServerDelivery = DateTime.MinValue;
        private static TimeSpan timeDeltaNTPServerDelivery = new TimeSpan(0);
        private static bool dateTimeAndIpAddressAreSet = false;

        private static IPAddress ip4Address = IPAddress.Parse("0.0.0.0");

        //static byte[] caDigiCertGlobalRootCA = Resources.GetBytes(Resources.BinaryResources.DigiCertGlobalRootCA);   // roschmionline

       // static byte[] caGHI = Resources.GetBytes(Resources.BinaryResources.Digicert___GHI);

       // public static byte[] caAzure =  Resources.GetBytes(Resources.BinaryResources.DigiCert_Baltimore_Root);

        //static byte[] caStackExcange = (Resources.GetBytes(Resources.BinaryResources.Digicert___StackExchange));

        static string wiFiSSID_1 = ResourcesSecret.GetString(ResourcesSecret.StringResources.SSID_1);
        static string wiFiKey_1 = ResourcesSecret.GetString(ResourcesSecret.StringResources.Key_1);

        //static string wiFiSSID_2 = ResourcesSecret.GetString(ResourcesSecret.StringResources.SSID_2);
        //static string wiFiKey_2 = ResourcesSecret.GetString(ResourcesSecret.StringResources.Key_2);

         // Set your Azure Storage Account Credentials here

        //static string storageAccount = "your Accountname";
        static string storageAccountName = ResourcesSecret.GetString(ResourcesSecret.StringResources.AzureAccountName);

        private static bool Azure_useHTTPS = true;
        //private static bool Azure_useHTTPS = false;

        private static CloudStorageAccount myCloudStorageAccount;


        //static string storageKey = "your key";
        static string storageKey = ResourcesSecret.GetString(ResourcesSecret.StringResources.AzureAccountKey);

        private static X509Certificate[] caCerts;

        private static GpioPin _pinPyton;

        #region Region Main
        static void Main()
        {
            
            Debug.WriteLine("Remaining Ram at start  Main: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);
            Debug.WriteLine("DateTime at Start: " + DateTime.Now.ToString());
        
            var cont = GpioController.GetDefault();

            

            //FEZ
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

            Debug.WriteLine("Remaining Ram before initialize: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);

            wiFi_SPWF04S_Device.Initialize();

           
            


            //wifi.GetPhysicalAddress



            connectionString = "DefaultEndpointsProtocol=https;AccountName=" + storageAccountName + "; AccountKey=" + storageKey;

            myCloudStorageAccount = new CloudStorageAccount(storageAccountName, storageKey, useHttps: Azure_useHTTPS);

            // Initialization for each table must be done in main
            //myAzureSendManager = new AzureSendManager(myCloudStorageAccount, analogTableName, _sensorValueHeader, _socketSensorHeader, caCerts, _timeOfLastSend, sendInterval, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS); 

            //AzureSendManager.sampleTimeOfLastSent = DateTime.Now.AddDays(-10.0);    // Date in the past
            //AzureSendManager.InitializeQueue();


            waitForWiFiReady.WaitOne(10000, true);  // ******** Wait 20 sec for IP Address and NTP Time ready   *****************************************************

            var dummy4 = 1;
            for (int i = 0; i < 300; i++)    // Wait for 30 sec
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
                    GHIElectronics.TinyCLR.Native.Power.Reset(true);      // Reset Board if no time over internet     
            }

            if (DateTime.Now < new DateTime(2019,01,01))                  // Actualize TinyCLR Datetime if not up to date
            {               
                DateTime nowDate = new DateTime(int.Parse("20" + theTime.Substring(5, 2) ), int.Parse(theTime.Substring(8, 2)), int.Parse(theTime.Substring(11, 2)), int.Parse(theTime.Substring(21, 2)), int.Parse(theTime.Substring(24, 2)), int.Parse(theTime.Substring(27, 2)));               
                SystemTime.SetTime(nowDate, timeZoneOffset);
            }
          
            wifi.SetConfiguration("ramdisk_memsize", "18");

            



            string host = "www.roschmionline.de";
            string commonName = "*.roschmionline.de";
            string url = "/index.html";
            string protocol = "https";
            int port = protocol == "https" ? 443 : 80;

          //  string requestString = "GET " + host + url + " HTTP/1.1\r\nAccept-Language: en-us";
          //  byte[] requestBinary = Encoding.UTF8.GetBytes(requestString);

           // var buffer = new byte[50];

            wifi.ClearTlsServerRootCertificate();

            //wifi.SetTlsServerRootCertificate(caAzure);                   // azure
          //  wifi.SetTlsServerRootCertificate(caDigiCertGlobalRootCA);    // roschmionline

          //  if (commonName != null)
          //  {
          //      wifi.ForceSocketsTlsCommonName = commonName;
          //  }

            ArrayList theQuery = new ArrayList();

            // while (true)
            //{


            // HttpStatusCode resultCreate = createTable(myCloudStorageAccount, "tableoftoday");

            //    HttpStatusCode resultQuery = queryTableEntities(myCloudStorageAccount, "mypeople", "$top=1", out theQuery);

            //  HttpStatusCode resultQuery = queryTableEntities(myCloudStorageAccount, "Refrigerator2019", "$top=1", out theQuery);

            // TestSocket(host, url, port, SPWF04SxConnectionType.Tcp, SPWF04SxConnectionSecurityType.Tls, commonName);







            //int httpResult = wifi.SendHttpGet(host, "/index.html", 443, SPWF04SxConnectionSecurityType.Tls);

            //*****     This is a working example HttpGET Request    ***************
            /*  
            int httpResult = wifi.SendHttpGet(host, "/index.html", port, SPWF04SxConnectionSecurityType.Tls, "httpresponse01.resp", "httprequest01.requ", requestBinary);

            buffer = new byte[50];
            var start = DateTime.UtcNow;
            var total = 0;

            while (wifi.ReadHttpResponse(buffer, 0, buffer.Length) is var read && read > 0)
            {
                total += read;
                try
                {
                  //  Debugger.Log(0, "", Encoding.UTF8.GetString(buffer, 0, read));

                }
                catch
                {
                   // Debugger.Log(0, "", Encoding.UTF8.GetString(buffer, 0, read - 1));
                }
                Thread.Sleep(100);
            }

            Debug.WriteLine($"\r\nRead: {total:N0} in {(DateTime.UtcNow - start).TotalMilliseconds:N0}ms");



            string fileContent = wifi.PrintFile("httpresponse01.resp");
            */

            //  HttpStatusCode  createTableReturnCode = createTable(myCloudStorageAccount, "TestVonHeute");






            //  while (true)
            //  {
            //      Thread.Sleep(200);
            //  }

            //   Debug.WriteLine("Remaining Ram after Request: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);
            //   Thread.Sleep(3000);              

            // }


            /*   List of additional commands   -- do not delete  ++++++
            
            string theTime = wifi.GetTime();

            wifi.MountMemoryVolume("2");

            string theFiles = wifi.GetDiskContent();
         
            wifi.SetConfiguration("ramdisk_memsize", "18");
           
            wifi.CreateRamFile("monikasfile", Encoding.UTF8.GetBytes("Das hat geklappt, ganz wunderbar. ABCDEFGHIJKLMNOPQRSTUVWXYZ.ABCDEFGHIJKLMNOPQRSTUVWXYZ.ABCDEFGHIJKLMNOPQRSTUVWXYZ.ABCDEFGHIJKLMNOPQRSTUVWXYZ"));

            FileEntity fileEntity = wifi.GetFileProperties("monikasfile");   // Can be used as 'FileExists' equivalent 

            byte[] theData = wifi.GetFileDataBinary("monikasfile");

            string fileContent = wifi.PrintFile("monikasfile");

            wifi.DeleteRamFile("monikasfile");

            theFiles = wifi.GetDiskContent();
            */






            //TestHttp("http://files.ghielectronics.com", "/");

            //string host = "https://www.roschmionline.de";
            //string commonName = "*.roschmionline.de";

            //string host = "http://files.ghielectronics.com";
            //string host = "https://meta.stackexchange.com";

            //string url = "/";
            //string url = "/index.html";

            //string commonName = "*.stackexchange.com";


            //string commonName = null;


            //wifi.ClearTlsServerRootCertificate();
            //Thread.Sleep(10);
            //wifi.SetTlsServerRootCertificate(Resources.GetBytes(Resources.BinaryResources.Digicert___StackExchange));

            // Debug.WriteLine("Remaining Ram before creating Request in Main: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);

            //wifi.ClearTlsServerRootCertificate();
            //wifi.SetTlsServerRootCertificate(caDigiCertGlobalRootCA);

            //wifi.SetTlsServerRootCertificate(caGHI);

            //Thread.Sleep(10);


            //if (commonName != null)
            //{
            //     wifi.ForceSocketsTls = true;
            //     wifi.ForceSocketsTlsCommonName = commonName;
            //}


            // Thread.Sleep(50);


            // string responseBody = string.Empty;

            // var start = DateTime.UtcNow;
            // var req = (HttpWebRequest)HttpWebRequest.Create(host + url);
            //req.HttpsAuthentCerts = caCerts;
            // req.HttpsAuthentCerts = new[] { new X509Certificate() };

            /*
            HttpWebResponse res = null;
            try
            {
                res = (HttpWebResponse)req.GetResponse();
            }
            catch (Exception ex)
            {
                var theMessage = ex.Message;
            }
            */
            /*
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
           // Debug.WriteLine($"\r\nRead: {total:N0} in {(DateTime.UtcNow - start).TotalMilliseconds:N0}ms");
            */
            long totalMemory = GC.GetTotalMemory(true);
            Debug.WriteLine("Total Memory: " + totalMemory.ToString());


            Debug.WriteLine("Remaining Ram at end of main: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);

          // writeAnalogToCloudTimer = new System.Threading.Timer(new TimerCallback(writeAnalogToCloudTimer_tick), null, writeToCloudInterval * 1000, Timeout.Infinite);

            writeAnalogToCloudTimer = new System.Threading.Timer(new TimerCallback(writeAnalogToCloudTimer_tick), null, writeToCloudInterval * 10 * 1000, Timeout.Infinite);


            while (true)
            {
                Thread.Sleep(100);
            }              
        }
        #endregion

        private static void WiFi_SPWF04S_Device_PendingSocketData(WiFi_SPWF04S_Device sender, WiFi_SPWF04S_Device.PendingDataEventArgs e)
        {
            AzureStorageHelper.SocketDataPending = e.SocketDataPending;
        }

        private static void WiFi_SPWF04S_Device_SocketWasClosed(WiFi_SPWF04S_Device sender, WiFi_SPWF04S_Device.SocketClosedEventArgs e)
        {
            AzureStorageHelper.SocketWasClosed = e.SocketIsClosed;
        }
        


        //     *************************************   Event writeAnalogToCloudTimer_tick     *******************************************************

        // When this timer fires an Entity containing 4 analog values is stored to an Azure Cloud Table

        #region Timer Event writeAnalogToCloudTimer_tick  --- Entity with analog values is written to the Cloud
        private static void writeAnalogToCloudTimer_tick(object state)
        {

            writeAnalogToCloudTimer.Change(10 * 60 * 1000, 10 * 60 * 1000);    // Set to a long interval, so will not fire again before completed

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
                   

            myCloudStorageAccount = new CloudStorageAccount(storageAccountName, storageKey, useHttps: Azure_useHTTPS);

            int yearOfSend = DateTime.Now.Year;

            #region Region Create analogTable if not exists
            HttpStatusCode resultTableCreate = HttpStatusCode.Ambiguous;
            if (AnalogCloudTableYear != yearOfSend)
            {
                resultTableCreate = createTable(myCloudStorageAccount, analogTableName + DateTime.Today.Year.ToString());
            }

            if ((resultTableCreate == HttpStatusCode.Created) || (resultTableCreate == HttpStatusCode.NoContent) || (resultTableCreate == HttpStatusCode.Conflict))
            {
                AnalogCloudTableYear = yearOfSend;
            }
            #endregion


            var dummy56 = 1;
                
          

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

            // Populate Analog Table with values for the actual day
            ArrayList propertiesAL = AnalogTablePropertiesAL.AnalogPropertiesAL(sampleTime, 10.1, 20.2, 30.3, 40.4);

            AnalogTableEntity analogTableEntity = new AnalogTableEntity(partitionKey, reverseDate, propertiesAL);

            string insertEtag = string.Empty;
                
            HttpStatusCode insertResult = insertTableEntity(myCloudStorageAccount, analogTableName + yearOfSend.ToString(), analogTableEntity, out insertEtag);


            // do not delete
            ArrayList queryResult = new ArrayList();
            HttpStatusCode resultQuery = queryTableEntities(myCloudStorageAccount, analogTableName + yearOfSend.ToString(), "$top=1", out queryResult);
            var entityHashtable = queryResult[0] as Hashtable;
            var theRowKey = entityHashtable["RowKey"];
            var SampleTime = entityHashtable["SampleTime"];
            Debug.WriteLine("Entity read back from Azure, SampleTime: " + SampleTime);
         
            // Debug.WriteLine(GC.GetTotalMemory(true).ToString("N0"));


            writeAnalogToCloudTimer.Change(writeToCloudInterval * 10 * 1000, writeToCloudInterval * 10 * 1000);



            //Console.WriteLine("Analog data written to Cloud");

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
            table = new TableClient(pCloudStorageAccount, caCerts, _debug, _debug_level);
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

        #region private method queryTableEntities
        
        private static HttpStatusCode queryTableEntities(CloudStorageAccount pCloudStorageAccount, string tableName, string query, out ArrayList queryResult)
        {
            table = new TableClient(pCloudStorageAccount, caCerts, _debug, _debug_level);


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

      





        private static void WiFi_SPWF04S_Device_Ip4AddressAssigned(WiFi_SPWF04S_Device sender, WiFi_SPWF04S_Device.Ip4AssignedEventArgs e)
        {
            lock (LockProgram)
            {
                ip4Address = e.Ip4Address;

                // RoSchmi: has to be deleted
                // waitForWiFiReady.Set();
                // dateTimeAndIpAddressAreSet = true;
            }
        }

        private static void WiFi_SPWF04S_Device_DateTimeNtpServerDelivered(WiFi_SPWF04S_Device sender, WiFi_SPWF04S_Device.NTPServerDeliveryEventArgs e)
        {
            lock (LockProgram)
            {
                dateTimeNtpServerDelivery = e.DateTimeNTPServer;
                timeDeltaNTPServerDelivery = e.TimeDeltaNTPServer;
                SystemTime.SetTime(dateTimeNtpServerDelivery, timeZoneOffset);               
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















