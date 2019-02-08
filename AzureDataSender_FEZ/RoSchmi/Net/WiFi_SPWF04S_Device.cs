using System;
using System.Collections;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Net.NetworkInterface;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using GHIElectronics.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx;
using RoSchmi.Interfaces;


namespace RoSchmi.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx
{
    class WiFi_SPWF04S_Device
    {
        private static SPWF04SxInterfaceRoSchmi wiFiSPWF04S;
        //private static ISPWF04SxInterface wiFiSPWF04S;

        private static NetworkInterface networkInterface;

        private string wiFiSSID;
        private string wiFiKey;

        private static AutoResetEvent waitForConsoleActive = new AutoResetEvent(false);

        private IPAddress iPAddress = null;
        private DateTime dateTimeNtpServerDelivery = DateTime.MinValue;
        private TimeSpan timeDeltaNTPServerDelivery = new TimeSpan(0);

        public IPAddress WiFiIPAddress { get { return iPAddress; } }


        public WiFi_SPWF04S_Device(SPWF04SxInterfaceRoSchmi pWiFiSPWF04S, string pWifiSSID, string pWifiKey)
        //public WiFi_SPWF04S_Device(ISPWF04SxInterface pWiFiSPWF04S, string pWifiSSID, string pWifiKey)
        {
            wiFiSPWF04S = pWiFiSPWF04S;
            wiFiSSID = pWifiSSID;
            wiFiKey = pWifiKey;
        }

        public void Initialize()
        {
            
            wiFiSPWF04S.IndicationReceived += WiFiSPWF04S_IndicationReceived;   //  (s, e) => { Debug.WriteLine($"WIND: {WindToName(e.Indication)} {e.Message}"); this.resetEventWiFiConnected.Set();};

            wiFiSPWF04S.ErrorReceived += (s, e) => Debug.WriteLine($"ERROR: {e.Error} {e.Message}");

            wiFiSPWF04S.TurnOn();

            //networkInterface = wiFiSPWF04S;
            NetworkInterface.ActiveNetworkInterface = wiFiSPWF04S;

            Thread.Sleep(500);

            Thread InitThread = new Thread(new ThreadStart(runInitThread));
            InitThread.Start();

        }


        private void runInitThread()
        {
            wiFiSPWF04S.ResetConfiguration();
            Thread.Sleep(100);
            waitForConsoleActive.Reset();
            wiFiSPWF04S.Reset();
            waitForConsoleActive.WaitOne();

            wiFiSPWF04S.ClearTlsServerRootCertificate();
            SPWF04SxWiFiState theState = SPWF04SxWiFiState.ScanInProgress;

            for (int i = 0; i < 30; i++)    // Try for maximal time of 15 sec
            {
                try
                {
                    theState = wiFiSPWF04S.State;
                    Thread.Sleep(20);
                    if (theState == SPWF04SxWiFiState.ReadyToTransmit)
                    {
                        Debug.WriteLine("TheState is: " + "(" + i * 500 + ") " + (int)theState + " " + StateToName(theState));
                        break;
                    }
                    else
                    {
                        try
                        {
                            Debug.WriteLine("TheState is: " + "(" + i * 500 + ") " + (int)theState + " " + StateToName(theState));
                        }
                        catch
                        {
                            Debug.WriteLine("Error: SPWF04SxWiFiState is not defined: " + "(" + i * 500 + ") ");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Exception: " + "(" + i * 500 + ") " + ex.Message);
                }
                Thread.Sleep(500);
            }

            try
            {
                wiFiSPWF04S.JoinNetwork(wiFiSSID, wiFiKey);
            }
            catch (Exception ex)
            {
                var message = ex.Message;
            }
            /*
            while (true)
            {
                Thread.Sleep(100);
            }
            */
        }


        private void WiFiSPWF04S_IndicationReceived(SPWF04SxInterfaceRoSchmi sender, SPWF04SxIndicationReceivedEventArgs e)
        {
            Debug.WriteLine($"WIND: {WindToName(e.Indication)} {e.Message}");

            switch (e.Indication)
            {
                case SPWF04SxIndication.PendingData:
                    {
                        OnPendingData(this, new PendingDataEventArgs(true));
                    }
                    break;
                case SPWF04SxIndication.SocketClosed:
                    {
                        OnSocketClosed(this, new SocketClosedEventArgs(true));
                    }
                    break;
                case SPWF04SxIndication.ConsoleActive:
                    {
                        waitForConsoleActive.Set();
                    }
                    break;
                case SPWF04SxIndication.WiFiUp:
                    {
                        string[] iPStringArray = e.Message.Split(new char[] { ':' });
                        if (iPStringArray.Length == 2)
                        {
                            try
                            {
                                iPAddress = IPAddress.Parse(iPStringArray[1]);
                                OnIp4AddressAssigned(this, new Ip4AssignedEventArgs(iPAddress));
                            }
                            catch
                            { }
                        }
                    }
                    break;
                case SPWF04SxIndication.NtpServerDelivery:
                    {
                        var stringDateTime = e.Message;
                        try
                        {
                            string[] splitDateTime = stringDateTime.Split(new char[] { ':' });
                            string[] splitDate = splitDateTime[0].Split(new char[] { '.' });
                            string[] splitTime = splitDateTime[2].Split(new char[] { '.' });
                            dateTimeNtpServerDelivery = new DateTime(int.Parse(splitDate[0]), int.Parse(splitDate[1]), int.Parse(splitDate[2]), int.Parse(splitTime[0]), int.Parse(splitTime[1]), int.Parse(splitTime[2]));
                            timeDeltaNTPServerDelivery = new TimeSpan(int.Parse(splitDateTime[1]));
                            OnDateTimeNtpServerDelivered(this, new NTPServerDeliveryEventArgs(dateTimeNtpServerDelivery, timeDeltaNTPServerDelivery));
                        }
                        catch
                        {
                            throw new Exception("NTP Server data could not be parsed");
                        }
                    }
                    break;
                default:
                    { }
                    break;
            }
        }

        #region Delegates
        /// <summary>        
        /// The delegate that is used to handle the Pending Data event.
        /// </summary>
        /// <param name="sender">The <see cref="WiFi_SPWF04S_Device"/> object that raised the event.</param>
        /// <param name="e">The event arguments.</param>        
        public delegate void PendingDataEventHandler(WiFi_SPWF04S_Device sender, PendingDataEventArgs e);

        /// <summary>
        /// Raised when data are pending.
        /// </summary>
        public event PendingDataEventHandler PendingSocketData;
        private PendingDataEventHandler onPendingData;

        private void OnPendingData(WiFi_SPWF04S_Device sender, PendingDataEventArgs e)
        {
            if (this.onPendingData == null)
            {
                this.onPendingData = this.OnPendingData;
            }
            this.PendingSocketData(sender, e);
        }




        /// <summary>        
        /// The delegate that is used to handle the SocketClosed event.
        /// </summary>
        /// <param name="sender">The <see cref="WiFi_SPWF04S_Device"/> object that raised the event.</param>
        /// <param name="e">The event arguments.</param>        
        public delegate void SocketClosedEventHandler(WiFi_SPWF04S_Device sender, SocketClosedEventArgs e);

        /// <summary>
        /// Raised when the Socket was closed.
        /// </summary>
        public event SocketClosedEventHandler SocketWasClosed;
        private SocketClosedEventHandler onSocketClosed;

        private void OnSocketClosed(WiFi_SPWF04S_Device sender, SocketClosedEventArgs e)
        {
            if (this.onSocketClosed == null)
            {
                this.onSocketClosed = this.OnSocketClosed;
            }
            this.SocketWasClosed(sender, e);
        }

        /// <summary>        
        /// The delegate that is used to handle the IP4 Address received event.
        /// </summary>
        /// <param name="sender">The <see cref="WiFi_SPWF04S_Device"/> object that raised the event.</param>
        /// <param name="e">The event arguments.</param>        
        public delegate void Ip4AssignedEventHandler(WiFi_SPWF04S_Device sender, Ip4AssignedEventArgs e);


        /// <summary>
        /// Raised when the NTP Server delivers a DateTime.
        /// </summary>
        public event Ip4AssignedEventHandler Ip4AddressAssigned;

        private Ip4AssignedEventHandler onIp4AddressAssigned;

        private void OnIp4AddressAssigned(WiFi_SPWF04S_Device sender, Ip4AssignedEventArgs e)
        {
            if (this.onIp4AddressAssigned == null)
            {
                this.onIp4AddressAssigned = this.OnIp4AddressAssigned;
            }
            this.Ip4AddressAssigned(sender, e);
        }

        /// <summary>        
        /// The delegate that is used to handle the DateTimeNtpServerDeliveryEvent event.
        /// </summary>
        /// <param name="sender">The <see cref="WiFi_SPWF04S_Device"/> object that raised the event.</param>
        /// <param name="e">The event arguments.</param>        
        public delegate void DateTimeNtpServerDeliveryEventHandler(WiFi_SPWF04S_Device sender, NTPServerDeliveryEventArgs e);

        /// <summary>
        /// Raised when the NTP Server delivers a DateTime.
        /// </summary>
        public event DateTimeNtpServerDeliveryEventHandler DateTimeNtpServerDelivered;

        private DateTimeNtpServerDeliveryEventHandler onDateTimeNtpServerDelivered;

        private void OnDateTimeNtpServerDelivered(WiFi_SPWF04S_Device sender, NTPServerDeliveryEventArgs e)
        {
            if (this.onDateTimeNtpServerDelivered == null)
            {
                this.onDateTimeNtpServerDelivered = this.OnDateTimeNtpServerDelivered;
            }
            this.DateTimeNtpServerDelivered(sender, e);
        }

        #endregion

        #region Region EventArgs
        public class PendingDataEventArgs : EventArgs
        {
            /// <summary>
            /// Indicates that Socket data are pending
            /// </summary>
            ///
            public bool SocketDataPending
            { get; private set; }
            internal PendingDataEventArgs(bool pSocketDataPending)
            {
                this.SocketDataPending = pSocketDataPending;
            }
        }



        public class SocketClosedEventArgs :EventArgs
        {
            /// <summary>
            /// Indicates that Socket was closed
            /// </summary>
            ///
            public bool SocketIsClosed
            { get; private set; }
            internal SocketClosedEventArgs(bool pSocketIsClosed)
            {
                this.SocketIsClosed = pSocketIsClosed;
            }
        }
        public class Ip4AssignedEventArgs : EventArgs
        {
            /// <summary>
            /// The IP4 IpAddress
            /// </summary>
            /// 
            public IPAddress Ip4Address
            { get; private set; }
            internal Ip4AssignedEventArgs(IPAddress pIp4Address)
            {
                this.Ip4Address = pIp4Address;               
            }

        }


            public class NTPServerDeliveryEventArgs : EventArgs
        {
            /// <summary>
            /// The timezone delta to uct
            /// </summary>
            /// 
            public TimeSpan TimeDeltaNTPServer
            { get; private set; }

            /// <summary>
            /// The Dateime delivered by the NTP-Server
            /// </summary>
            public DateTime DateTimeNTPServer
            { get; private set; }

            internal NTPServerDeliveryEventArgs(DateTime pDateTimeNTPServer, TimeSpan pTimeDeltaNTPServer)
            {
                this.DateTimeNTPServer = pDateTimeNTPServer;
                this.TimeDeltaNTPServer = pTimeDeltaNTPServer;
            }
        }
        #endregion

        #region Region StateToName (SPWF04SxWiFiState)
        private static string StateToName(SPWF04SxWiFiState state)
        {
            switch ((int)state)
            {
                case 0: return "SPWF04SxWiFiState.HardwarePowerUp";
                case 1: return "SPWF04SxWiFiState.HardwareFailure";
                case 2: return "SPWF04SxWiFiState.RadioTerminatedByUser";
                case 3: return "SPWF04SxWiFiState.RadioIdle";
                case 4: return "SPWF04SxWiFiState.ScanInProgress";
                case 5: return "SPWF04SxWiFiState.ScanComplete";
                case 6: return "SPWF04SxWiFiState.JoinInProgress";
                case 7: return "SPWF04SxWiFiState.Joined";
                case 8: return "SPWF04SxWiFiState.AccessPointStarted";
                case 9: return "SPWF04SxWiFiState.HandshakeComplete";
                case 10: return "SPWF04SxWiFiState.ReadyToTransmit";
                default: return "Error: SPWF04SxWiFiState: Is not defined";
            }
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

