using System;
using System.Collections;
using System.Text;
using System.Resources;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInterface;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.Spi;
using GHIElectronics.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx;
using GHIElectronics.TinyCLR.Pins;
using RoSchmi.Net;



namespace AzureDataSender_FEZ
{
    class Program
    {
        private static GpioPin led1;

        private static GpioPin btn1;

        private static SPWF04SxInterface wifi;

        private static WiFi_SPWF04S_Device wiFi_SPWF04S_Device;

        static byte[] caAzure = Resources.GetBytes(Resources.BinaryResources.DigiCert_Baltimore_Root);

        static byte[] caStackExcange = (Resources.GetBytes(Resources.BinaryResources.Digicert___StackExchange));

        static string wiFiSSID_1 = Resources.GetString(Resources.StringResources.SSID_1);
        static string wiFiKey_1 = Resources.GetString(Resources.StringResources.Key_1);

        static string wiFiSSID_2 = Resources.GetString(Resources.StringResources.SSID_2);
        static string wiFiKey_2 = Resources.GetString(Resources.StringResources.Key_2);

        private static X509Certificate[] caCerts;

        private static GpioPin _pinPyton;

        static void Main()
        {
            Debug.WriteLine("Hello FEZ");
            var cont = GpioController.GetDefault();

            //FEZ
            var reset = cont.OpenPin(FEZ.GpioPin.WiFiReset);

            _pinPyton = cont.OpenPin(FEZCLR.GpioPin.PA0);
            _pinPyton.SetDriveMode(GpioPinDriveMode.InputPullDown);

            var irq = cont.OpenPin(FEZ.GpioPin.WiFiInterrupt);
            var scont = SpiController.FromName(FEZ.SpiBus.WiFi);
            var spi = scont.GetDevice(SPWF04SxInterface.GetConnectionSettings(SpiChipSelectType.Gpio, FEZ.GpioPin.WiFiChipSelect));
            led1 = cont.OpenPin(FEZ.GpioPin.Led1);
            btn1 = cont.OpenPin(FEZ.GpioPin.Btn1);


            //UC5550
            //var reset = cont.OpenPin(UC5550.GpioPin.PG12);
            //var irq = cont.OpenPin(UC5550.GpioPin.PB11);
            //var scont = SpiController.FromName(UC5550.SpiBus.Spi5);
            //var spi = scont.GetDevice(SPWF04SxInterface.GetConnectionSettings(SpiChipSelectType.Gpio, UC5550.GpioPin.PB10));
            //led1 = cont.OpenPin(UC5550.GpioPin.PG3);
            //btn1 = cont.OpenPin(UC5550.GpioPin.PI8);

            led1.SetDriveMode(GpioPinDriveMode.Output);
            btn1.SetDriveMode(GpioPinDriveMode.InputPullUp);

            wifi = new SPWF04SxInterface(spi, irq, reset);

            caCerts = new X509Certificate[] { new X509Certificate(caStackExcange), new X509Certificate(caAzure) };

            wiFi_SPWF04S_Device = new WiFi_SPWF04S_Device(wifi, NetworkInterface.ActiveNetworkInterface, caCerts, wiFiSSID_1, wiFiKey_1);
            //wiFi_SPWF04S_Device = new WiFi_SPWF04S_Device(wifi, NetworkInterface.ActiveNetworkInterface, caCerts, wiFiSSID_2, wiFiKey_2);

            wiFi_SPWF04S_Device.Initialize();

            while(true)
            {
                Thread.Sleep(1000);
                if (wiFi_SPWF04S_Device.WiFiIPAddress != null)
                {
                    Debug.WriteLine("IPAddress is: " + wiFi_SPWF04S_Device.WiFiIPAddress.ToString());
                }
                else
                {
                    Debug.WriteLine("IPAddress is: null");
                }

            }

            
            


            //wifi.IndicationReceived += (s, e) => Debug.WriteLine($"WIND: {Program.WindToName(e.Indication)} {e.Message}");
            //wifi.ErrorReceived += (s, e) => Debug.WriteLine($"ERROR: {e.Error} {e.Message}");
            //wifi.TurnOn();
            //NetworkInterface.ActiveNetworkInterface = wifi;
            //Run();
        }

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
                led1.Write(led1.Read() == GpioPinValue.High ? GpioPinValue.Low : GpioPinValue.High);
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















