// Version 2.0 28.05.2016
using System;
//using Microsoft.SPOT;
using System.Net;
//using Microsoft.SPOT.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Collections;
using System.Text;
using System.Threading;
using System.Diagnostics;
using GHIElectronics.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx;
using GHIElectronics.TinyCLR.Native;
using RoSchmi.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx;
using RoSchmi.Utilities;
using AzureDataSender_FEZ;


namespace RoSchmi.Net.Azure.Storage
{

    /// <summary>
    /// A common helper class for HTTP access to Windows Azure Storage
    /// </summary>
    public static class AzureStorageHelper
    {
        /// <summary>
        /// Sends a Web Request prepared for Azure Storage
        /// </summary>
        /// <param name="url"></param>
        /// <param name="authHeader"></param>
        /// <param name="dateHeader"></param>
        /// <param name="versionHeader"></param>
        /// <param name="fileBytes"></param>
        /// <param name="contentLength"></param>
        /// <param name="httpVerb"></param>
        /// <param name="expect100Continue"></param>
        /// <param name="Accept-Type"></param>
        /// <param name="additionalHeaders"></param>
        /// <returns></returns>
        ///

        private static Object theLock1 = new Object();


        private static bool _fiddlerIsAttached = false;
        private static IPAddress _fiddlerIP = null;
        private static int _fiddlerPort = 8888;

        private static SPWF04SxInterfaceRoSchmi wifi;
        private static X509Certificate[] caCerts;

        public static bool SocketWasClosed
        { get; set; }

        public static bool SocketDataPending
        { get; set; }

        public static bool WiFiAssociationState
        { get; set; }

        public static bool WiFiNetworkLost
        { get; set; }



        #region "Debugging"
        private static DebugMode _debug = DebugMode.NoDebug;
        private static DebugLevel _debug_level = DebugLevel.DebugErrors;

        

        /// <summary>
        /// Represents the debug mode.
        /// </summary>
        public enum DebugMode
        {
            /// <summary>
            /// Use no debugging
            /// </summary>
            NoDebug,

            /// <summary>
            /// Report debugging to Visual Studio debug output
            /// </summary>
            StandardDebug,

            /// <summary>
            /// Re-direct debugging to a given serial port.
            /// Console Debugging
            /// </summary>
            SerialDebug
        };

        /// <summary>
        /// Represents the debug level.
        /// </summary>
        public enum DebugLevel
        {
            /// <summary>
            /// Only debug errors.
            /// </summary>
            DebugErrors,
            /// <summary>
            /// Debug everything.
            /// </summary>
            DebugErrorsPlusMessages,
            /// <summary>
            /// Debug everything.
            /// </summary>
            DebugAll
        };


        private static void _Print_Debug(string message)
        {
            lock (theLock1)
            {
                switch (_debug)
                {
                    //Do nothing
                    case DebugMode.NoDebug:
                        break;

                    //Output Debugging info to the serial port
                    case DebugMode.SerialDebug:
                        //Convert the message to bytes
                        /*
                        byte[] message_buffer = System.Text.Encoding.UTF8.GetBytes(message);
                        _debug_port.Write(message_buffer,0,message_buffer.Length);
                        */
                        break;

                    //Print message to the standard debug output
                    case DebugMode.StandardDebug:
                        Debug.WriteLine(message);
                        break;
                }
            }
        }
        #endregion
        /// <summary>
        /// Set the debugging level.
        /// </summary>
        /// <param name="Debug_Level">The debug level</param>
        public static void SetDebugLevel(DebugLevel Debug_Level)
        {
            lock (theLock1)
            {
                _debug_level = Debug_Level;
            }
        }
        /// <summary>
        /// Set the debugging mode.
        /// </summary>
        /// <param name="Debug_Level">The debug level</param>
        public static void SetDebugMode(DebugMode Debug_Mode)
        {
            lock (theLock1)
            {
                _debug = Debug_Mode;
            }
        }



        public static void AttachFiddler(bool pfiddlerIsAttached, IPAddress pfiddlerIP, int pfiddlerPort)
        {
            lock (theLock1)
            {
                _fiddlerIsAttached = pfiddlerIsAttached;
                _fiddlerIP = pfiddlerIP;
                _fiddlerPort = pfiddlerPort;
            }
        }

        public static BasicHttpResponse SendWebRequest(SPWF04SxInterfaceRoSchmi spwf04sx, X509Certificate[] certificates, Uri url, string authHeader, string dateHeader, string versionHeader, byte[] payload = null, int contentLength = 0, string httpVerb = "GET", bool expect100Continue = false, string acceptType = "application/json;odata=minimalmetadata", Hashtable additionalHeaders = null)
        {
            caCerts = certificates;
            wifi = spwf04sx;
            string responseBody = "";
            HttpStatusCode responseStatusCode = HttpStatusCode.Ambiguous;
            try
            {
                HttpWebResponse response = null;
                string _responseHeader_ETag = null;
                string _responseHeader_Content_MD5 = null;
                HttpWebRequest request = null;
                string SPWF04SxRequest = null;
                try
                {
                    //HttpWebRequest request = PrepareRequest(url, authHeader, dateHeader, versionHeader, payload, contentLength, httpVerb, expect100Continue, acceptType, additionalHeaders);

                    // request = PrepareRequest(url, authHeader, dateHeader, versionHeader, payload, contentLength, httpVerb, expect100Continue, acceptType, additionalHeaders);
                    if (wifi != null)
                    {
                        bool isSocketRequest = true;
                        SPWF04SxRequest = PrepareSPWF04SxRequest(url, authHeader, dateHeader, versionHeader, payload, contentLength, httpVerb, isSocketRequest, expect100Continue, acceptType, additionalHeaders);

                        // Print the Request
                        //Debug.WriteLine(SPWF04SxRequest);

                        byte[] requestBinary = Encoding.UTF8.GetBytes(SPWF04SxRequest);

                        SPWF04SxRequest = "";
                        var buffer = new byte[200];
                        string protocol = "https";

                        if (httpVerb == "GET")
                        {
                            wifi.ClearTlsServerRootCertificate();
                            wifi.ForceSocketsTls = false;
                            protocol = "http";
                        }
                        else   // httpVerb = POST
                        {
                            wifi.SetTlsServerRootCertificate(Resources.GetBytes(Resources.BinaryResources.BaltimoreCyberTrustRoot));
                            wifi.SetTlsServerRootCertificate(caCerts[0].GetRawCertData());
                            //Program.wifi.SetTlsServerRootCertificate(Resources.GetBytes(Resources.BinaryResources.DigiCert_Baltimore_Root));
                            wifi.ForceSocketsTls = true;
                            protocol = "https";
                        }

                        int port = protocol == "https" ? 443 : 80;
                        SPWF04SxConnectionSecurityType securityType = protocol == "https" ? SPWF04SxConnectionSecurityType.Tls : SPWF04SxConnectionSecurityType.None;




                        #region HttpGET Request outcommented
                        /*
                        int httpResult = Program.wifi.SendHttpGet(host, path, port, securityType, "httpresponse01.resp", "httprequest01.requ", requestBinary);

                         buffer = new byte[50];
                         var start = DateTime.UtcNow;
                         var total = 0;

                         while (Program.wifi.ReadHttpResponse(buffer, 0, buffer.Length) is var read && read > 0)
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


                         string fileContent = Program.wifi.PrintFile("httpresponse01.resp");

                        */
                        #endregion

                        #region Wait 2 seconds for positive WifiAssociation or NetworkLost recovery
                        int timeCtr = 0;
                        while (((WiFiAssociationState == false) || WiFiNetworkLost) && (timeCtr < 20 ))   // Wait 2 sec for WifiAssociation, if not there, return
                        {
                            if (timeCtr < 0)
                            {
                                Debug.WriteLine("Didn't try to open socket (Disassociation or WifiNetworkLost)");
                            }
                            Thread.Sleep(100);
                            timeCtr++;
                        }
                        if (!WiFiAssociationState)
                        {
                            Debug.WriteLine("Gave up finally to open socket (Disassociation)");
                            return new BasicHttpResponse() { ETag = null, Body = "", StatusCode = HttpStatusCode.NotFound };
                        }
                        #endregion

                        DateTime start = DateTime.UtcNow;
                        int id = -1;
                        timeCtr = 0;
                        int loopLimit = 15;     // max 15 retries
                        long totalMemory = 0;
                        long freeMemory = 0;
                        do
                        {
                            totalMemory = GC.GetTotalMemory(true);
                            freeMemory = GHIElectronics.TinyCLR.Native.Memory.FreeBytes;
                            Debug.WriteLine("Starting Send Webrequest. Total Memory: " + totalMemory.ToString() + "Free Bytes: " + freeMemory);


                            //id = Program.wifi.OpenSocket(url.Host, port, SPWF04SxConnectionType.Tcp, securityType, "*.table.core.windows.net");

                            #region try 1 second repeatedly to open socket (10 times)
                            id = -1;
                            int socketTimeCtr = 0;
                            while ((id == -1) && (socketTimeCtr < 10))
                            {
                                Debug.WriteLine("Going to open socket");
                                id = wifi.OpenSocket(url.Host, port, SPWF04SxConnectionType.Tcp, securityType);
                                
                                if (socketTimeCtr > 0)
                                {
                                    Debug.WriteLine("Failed to open socket one time");
                                }
                                Thread.Sleep(100);
                                socketTimeCtr++;                              
                            }

                            if (id == -1)
                            {
                                Debug.WriteLine("Finally failed to open socket");
                                return new BasicHttpResponse() { ETag = null, Body = "", StatusCode = HttpStatusCode.NotFound };
                            }
                            #endregion

                            Debug.WriteLine("Succeeded to open socket on try: " + socketTimeCtr.ToString());
                            SocketDataPending = false;

                            wifi.WriteSocket(id, requestBinary);
                            if (httpVerb == "POST")
                            {
                                wifi.WriteSocket(id, payload);
                            }
                            for (int i = 0; i < 5; i++)    // wait for 500 ms for pending data
                            {
                                Thread.Sleep(100);
                            }
                            
                            timeCtr++;
                            Debug.WriteLine("Write Try: " + timeCtr.ToString());
                            if (!SocketDataPending && timeCtr < loopLimit)
                            {
                                wifi.CloseSocket(id);
                                Thread.Sleep(100);
                            }
                        } while (!SocketDataPending && timeCtr < loopLimit);    // if there were no data for loopLimit retries, we go on 

                        var total = 0;

                        var first = true;

                        byte[] totalBuf = new byte[0];
                        byte[] lastBuf = new byte[0];
                        int offset = 0;
                        int read = buffer.Length;
                        timeCtr = 0;

                        totalMemory = GC.GetTotalMemory(true);
                        freeMemory = GHIElectronics.TinyCLR.Native.Memory.FreeBytes;
                        Debug.WriteLine("Total Memory: " + totalMemory.ToString() + "Free Bytes: " + freeMemory);


                        //while ((Program.wifi.QuerySocket(id) is var avail && avail > 0) || first || total < 120)
                        while (((wifi.QuerySocket(id) is var avail && avail > 0) || first || total < 120) && (timeCtr < 30))
                        {
                            if (avail > 0)
                            {
                                first = false;
                                read = wifi.ReadSocket(id, buffer, 0, Math.Min(avail, buffer.Length));
                                total += read;
                                //Debugger.Log(0, "", Encoding.UTF8.GetString(buffer, 0, read));
                                lastBuf = totalBuf;
                                offset = lastBuf.Length;
                                totalBuf = new byte[offset + read];
                                Array.Copy(lastBuf, 0, totalBuf, 0, offset);
                                Array.Copy(buffer, 0, totalBuf, offset, read);
                            }
                            Thread.Sleep(100);
                            timeCtr++;
                        }
                        Debug.WriteLine($"\r\nRead: {total:N0} in {(DateTime.UtcNow - start).TotalMilliseconds:N0}ms");

                        wifi.CloseSocket(id);
                        lastBuf = new byte[0];




                        totalMemory = GC.GetTotalMemory(true);

                        //extract httpStatusCode                         
                        byte[] searchSequence = Encoding.UTF8.GetBytes("HTTP/1.1 ");
                        var searcher = new BoyerMoore(searchSequence);
                        int[] foundIdxArray = searcher.Search(totalBuf, true);
                        string httpStatCode = total > 12 ? Encoding.UTF8.GetString(totalBuf, foundIdxArray[0] + 9, 3) : "300";

                        if ((foundIdxArray.Length > 0) && (foundIdxArray[0] == 0))    // response starts with "HTTP/1.1 "
                        {
                            // extract ETag                       
                            searchSequence = Encoding.UTF8.GetBytes("ETag:");
                            searcher = new BoyerMoore(searchSequence);
                            foundIdxArray = searcher.Search(totalBuf, true);
                            if ((foundIdxArray.Length > 0) && totalBuf.Length > (foundIdxArray[0] + 6))
                            {
                                int startOfETag = foundIdxArray[0] + 6;
                                int endOfETag = startOfETag;
                                while ((endOfETag + 1 < totalBuf.Length) && (totalBuf[endOfETag] != 0x0D) && (totalBuf[endOfETag + 1] != 0x0A))
                                {
                                    endOfETag++;
                                }
                                _responseHeader_ETag = Encoding.UTF8.GetString(totalBuf, startOfETag, endOfETag - startOfETag);
                            }


                            // Print first line of response or all headers of response                       

                            searchSequence = new byte[] { 0x0D, 0x0A };
                            searcher = new BoyerMoore(searchSequence);
                            foundIdxArray = searcher.Search(totalBuf, false);
                            int printStart = 0;
                            // Print Response Line
                            Debug.WriteLine("\r\n" + Encoding.UTF8.GetString(totalBuf, 0, foundIdxArray[0]) +"\r\n");
                            printStart = foundIdxArray[0] + 2;
                            // Print response headers. This throws 'no free memory exception' (I don't know why). If you wand to see the headers, inactivate printing the request
                            /*
                            for (int i = 1; i < foundIdxArray.Length - 3; i++)
                            {
                                totalMemory = GC.GetTotalMemory(true);
                                Debug.WriteLine("Remaining Ram: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);                              
                                PrindLine(totalBuf, printStart, foundIdxArray[i] - printStart);                          
                                printStart = foundIdxArray[i] + 2;
                            }
                            */

                            totalMemory = GC.GetTotalMemory(true);
                            freeMemory = GHIElectronics.TinyCLR.Native.Memory.FreeBytes;
                            Debug.WriteLine("Total Memory before extract body: " + totalMemory.ToString() + "Free Bytes: " + freeMemory);

                            // extract start and end of body
                            int startOfBody = 0;
                            int endOfBody = 0;
                            searchSequence = new byte[] { 0x0D, 0x0A, 0x0D, 0x0A };
                            searcher = new BoyerMoore(searchSequence);
                            foundIdxArray = searcher.Search(totalBuf, false);
                            if (foundIdxArray.Length > 1)
                            {
                                int startOfLength = foundIdxArray[0] + 4;

                                if ((startOfLength + 4) < totalBuf.Length)
                                {
                                    int endOfLength = startOfLength;
                                    while ((endOfLength + 1 < totalBuf.Length) && (totalBuf[endOfLength] != 0x0D) && (totalBuf[endOfLength + 1] != 0x0A))
                                    {
                                        endOfLength++;
                                    }
                                    startOfBody = endOfLength + 2;
                                    if (foundIdxArray[foundIdxArray.Length - 1] > (totalBuf.Length - 5))
                                    {
                                        endOfBody = foundIdxArray[foundIdxArray.Length - 1] - 3;
                                    }
                                    else
                                    {
                                        endOfBody = totalBuf.Length - 1;
                                        httpStatCode = "300";
                                    }
                                    totalMemory = GC.GetTotalMemory(true);
                                    freeMemory = GHIElectronics.TinyCLR.Native.Memory.FreeBytes;
                                    Debug.WriteLine("Total Memory before encoding body: " + totalMemory.ToString() + "Free Bytes: " + freeMemory);
                                    
                                    responseBody = Encoding.UTF8.GetString(totalBuf, startOfBody, endOfBody - startOfBody);
                                    totalBuf = null;

                                    totalMemory = GC.GetTotalMemory(true);
                                    freeMemory = GHIElectronics.TinyCLR.Native.Memory.FreeBytes;

                                    Debug.WriteLine("Total Memory after encoding body: " + totalMemory.ToString() + "Free Bytes: " + freeMemory);

                                   // Debug.WriteLine(responseBody);
                                }
                                else
                                {
                                    responseBody = "";
                                }
                            }
                            else
                            {                              
                                responseBody = "";
                            }
                        }
                        else
                        {                        
                            httpStatCode = "404";
                            responseBody = "";
                        }
                                                   
                        switch (httpStatCode)
                        {
                            case "200":
                                {
                                    responseStatusCode = HttpStatusCode.OK;
                                }
                                break;
                            case "204":
                                {
                                    responseStatusCode = HttpStatusCode.NoContent;
                                }
                                break;
                            case "300":
                                {
                                    responseStatusCode = HttpStatusCode.Ambiguous;
                                }
                                break;
                            case "400":
                                {
                                    responseStatusCode = HttpStatusCode.BadRequest;
                                }
                                break;
                            case "403":
                                {
                                    responseStatusCode = HttpStatusCode.Forbidden;
                                }
                                break;
                            case "404":
                                {
                                    responseStatusCode = HttpStatusCode.NotFound;
                                }
                                break;

                            case "409":
                                {
                                    responseStatusCode = HttpStatusCode.Conflict;
                                }
                                break;
                            default:
                                {
                                    responseStatusCode = HttpStatusCode.Ambiguous;
                                }
                                break;
                        }
                        return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };
                    }
                    else
                    {
                        // insert NET Http request here
                        return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };                       
                    }
                }
                catch
                {
                    return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };
                }               
            }
            catch
            {
               return new BasicHttpResponse() { ETag = null, Body = responseBody, StatusCode = responseStatusCode };

            }

          
        }

        private static void PrindLine(byte[] buffer, int start, int count)
        {
            Debug.WriteLine(Encoding.UTF8.GetString(buffer, start, count));
        }

        /*
        string host = "www.roschmionline.de";
        string commonName = "*.roschmionline.de";
        string url = "/index.html";
        string protocol = "https";
        int port = protocol == "https" ? 443 : 80;

        string requestString = "GET " + host + url + " HTTP/1.1\r\nAccept-Language: en-us";
        byte[] requestBinary = Encoding.UTF8.GetBytes(requestString);

        var buffer = new byte[50];

        wifi.ClearTlsServerRootCertificate();

        //wifi.SetTlsServerRootCertificate(caAzure);                   // azure
        wifi.SetTlsServerRootCertificate(caDigiCertGlobalRootCA);    // roschmionline

        if (commonName != null)
        {
            wifi.ForceSocketsTls = true;
            wifi.ForceSocketsTlsCommonName = commonName;
        }

        while (true)
        {

            //int httpResult = wifi.SendHttpGet(host, "/index.html", 443, SPWF04SxConnectionSecurityType.Tls);

            int httpResult = wifi.SendHttpGet(host, "/index.html", port, SPWF04SxConnectionSecurityType.Tls, "httpresponse01.resp", "httprequest01.requ", requestBinary);
         */

        /*
        string host1 = "https://meta.stackexchange.com";
        string url1 = "/";
        string commonName1 = "*.stackexchange.com";
        */

        //  string host1 = "http://www.roschmionline.de";
        //  string url1 = "/index.html";



        //  Debug.WriteLine("Remaining Ram before Creating Request: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);
        //  Debug.WriteLine("Remaining Ram before Creating Request: " + GHIElectronics.TinyCLR.Native.Memory.FreeBytes + " used Bytes: " + GHIElectronics.TinyCLR.Native.Memory.UsedBytes);

        //  request = (HttpWebRequest)HttpWebRequest.Create(host1 + url1);

        /*

        if (request != null)
        {
            // Assign the certificates. The value must not be null if the
            // connection is HTTPS.

            //   request.HttpsAuthentCerts = TableClient.caCerts;
            request.HttpsAuthentCerts = new[] { new X509Certificate() };
            //var res = (HttpWebResponse)req.GetResponse();

            HttpWebResponse resp = null;
            try
            {
                resp = (HttpWebResponse)request.GetResponse();
            }
            catch (Exception ex)
            {
                var theMessage = ex.Message;
            }

            DateTime start = DateTime.Now;
            byte[] buffer1 = new byte[256];
            var str = resp.GetResponseStream();
            Debug.WriteLine($"HTTP {response.StatusCode}");
            int total = 0;
            while (str.Read(buffer1, 0, buffer1.Length) is var read && read > 0)
            {
                total += read;
                try
                {
                    Debugger.Log(0, "", Encoding.UTF8.GetString(buffer1, 0, read));
                }
                catch
                {
                    Debugger.Log(0, "", Encoding.UTF8.GetString(buffer1, 0, read - 1));
                }
                if (responseBody.Length < 500)
                {
                    responseBody += Encoding.UTF8.GetString(buffer1, 0, read);
                }
                Thread.Sleep(100);
            }
            Debug.WriteLine($"\r\nRead: {total:N0} in {(DateTime.UtcNow - start).TotalMilliseconds:N0}ms");

          //  return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };

            //HttpWebRequest.DefaultWebProxy = new WebProxy("4.2.2.2", true);

            // Evtl. set request.KeepAlive to use a persistent connection.
            */

        /*
        request.KeepAlive = false;
        request.Timeout = 100000;               // timeout 100 sec = standard
        request.ReadWriteTimeout = 100000;      // timeout 100 sec, standard = 300
        lock (theLock1)
        {
            if (_debug_level == DebugLevel.DebugErrorsPlusMessages)
            {
                _Print_Debug("Time of request (no DLST): " + DateTime.Now);
                _Print_Debug("Url: " + url.AbsoluteUri);
            }
        }
        */

        // This is needed since there is an exception if the GetRequestStream method is called with GET or HEAD

        /*
        if ((httpVerb != "GET") && (httpVerb != "HEAD"))
        {
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(payload, 0, contentLength);
            }
        }
        */
        /*
        if ((httpVerb != "GET") && (httpVerb != "HEAD"))
        {
            Stream requestStream = null;
            try
            {
                requestStream = request.GetRequestStream();
            }
            catch (Exception ex)
            {
                var theMess = ex.Message;
            }
            if (requestStream != null)
            {
                requestStream.Write(payload, 0, contentLength);
            }
        }
        */



        //    response = (HttpWebResponse)request.GetResponse();



        /*
        if (response != null)
        {
            if (response.Headers.Count > 0)
            {
                try
                {
                    _responseHeader_ETag = response.GetResponseHeader("ETag");
                }
                catch { }

                try
                {
                    _responseHeader_Content_MD5 = response.GetResponseHeader("Content-MD5");
                }
                catch { }
            }
            responseStatusCode = response.StatusCode;
            Stream dataStream = response.GetResponseStream();
            Debug.WriteLine($"HTTP {response.StatusCode}");
            int totalCnt = 0;
            var buffer = new byte[512];
            while (dataStream.Read(buffer, 0, buffer.Length) is var read && read > 0)
            {
                totalCnt += read;
                try
                {
                    Debugger.Log(0, "", Encoding.UTF8.GetString(buffer, 0, read));
                }
                catch
                {
                    Debugger.Log(0, "", Encoding.UTF8.GetString(buffer, 0, read - 1));
                }
                if (responseBody.Length < 3000)
                {
                    responseBody += Encoding.UTF8.GetString(buffer, 0, read);
                }
                Thread.Sleep(100);
            }

            //Report all incomming data to the debug
            lock (theLock1)
            {
                if (_debug_level == DebugLevel.DebugAll)
                {
                    _Print_Debug(responseBody);
                }
            }
            //reader.Close();
            if (response.StatusCode == HttpStatusCode.Forbidden)
            //if ((response.StatusCode == HttpStatusCode.Forbidden) || (response.StatusCode == HttpStatusCode.NotFound))
            {
                lock (theLock1)
                {
                    _Print_Debug("Problem with signature. Check next debug statement for stack");
                }
                throw new WebException("Forbidden", null, WebExceptionStatus.TrustFailure, response);
            }
            response.Close();
            if (responseBody == null)
                responseBody = "No body content";

            //_Print_Debug(responseBody);
            return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };

        }
        else
        {
            return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };
        }
        */
        /*
        }
        else
        {
            lock (theLock1)
            {
                _Print_Debug("Failure: Request is null");
            }
            return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };
        }
    }
    catch (WebException ex)
    {
        lock (theLock1)
        {
            _Print_Debug("An error occured. Status code:" + ((HttpWebResponse)ex.Response).StatusCode);
        }
        responseStatusCode = ((HttpWebResponse)ex.Response).StatusCode;


        using (Stream stream = ex.Response.GetResponseStream())
        {
        */
        /*
        using (StreamReader sr = new StreamReader(stream))
        {
            StringBuilder sB = new StringBuilder("");
            Char[] chunk = new char[20];

            while (sr.Peek() > -1)
            {
                int readBytes = sr.Read(chunk, 0, chunk.Length);
                sB.Append(chunk, 0, readBytes);
            }
            responseBody = sB.ToString();
            lock (theLock1)
            {
                _Print_Debug(responseBody);
            }


            //var s = sr.ReadToEnd();
            //lock (theLock1)
            //{
            //    _Print_Debug(s);
            //}
            //responseBody = s;


            return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };
        }
        */

        /*
        return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };
    }


}

catch (Exception ex2)
{
    lock (theLock1)
    {
        _Print_Debug("Exception in HttpWebRequest.GetResponse(): " + ex2.Message);
        _Print_Debug("ETag: " + _responseHeader_ETag + " Body: " + responseBody + " StatusCode: " + responseStatusCode);
    }
    return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };
}
finally
{
    if (response != null)
    {
        response.Dispose();
    }
    if (request != null)
    {
        request.Dispose();
    }
}
}
catch (Exception ex)
{
lock (theLock1)
{
    _Print_Debug("Exception in HttpWebRequest: " + ex.Message);
}
return new BasicHttpResponse() { ETag = null, Body = responseBody, StatusCode = responseStatusCode };
*/
        // }
        //         }



        /*
        public static BasicHttpResponse SendWebRequest(Uri url, string authHeader, string dateHeader, string versionHeader, byte[] payload = null, int contentLength = 0, string httpVerb = "GET", bool expect100Continue = false, string acceptType = "application/json;odata=minimalmetadata", Hashtable additionalHeaders = null)
            {
                string responseBody = "";
                HttpStatusCode responseStatusCode = HttpStatusCode.Ambiguous;
                try
                {
                    HttpWebResponse response = null;
                    string _responseHeader_ETag = null;
                    string _responseHeader_Content_MD5 = null;
                    HttpWebRequest request = null;
                    try
                    {
                        // HttpWebRequest request = PrepareRequest(url, authHeader, dateHeader, versionHeader, payload, contentLength, httpVerb, expect100Continue, acceptType, additionalHeaders);
                        request = PrepareRequest(url, authHeader, dateHeader, versionHeader, payload, contentLength, httpVerb, expect100Continue, acceptType, additionalHeaders);
                        if (request != null)
                        {
                            // Assign the certificates. The value must not be null if the
                            // connection is HTTPS.
                            request.HttpsAuthentCerts = TableClient.caCerts;

                            //HttpWebRequest.DefaultWebProxy = new WebProxy("4.2.2.2", true);

                            // Evtl. set request.KeepAlive to use a persistent connection.
                            request.KeepAlive = false;
                            request.Timeout = 100000;               // timeout 100 sec = standard
                            request.ReadWriteTimeout = 100000;      // timeout 100 sec, standard = 300
                            lock (theLock1)
                            {
                                if (_debug_level == DebugLevel.DebugErrorsPlusMessages)
                                {
                                    _Print_Debug("Time of request (no DLST): " + DateTime.Now);
                                    _Print_Debug("Url: " + url.AbsoluteUri);
                                }
                            }
                            // This is needed since there is an exception if the GetRequestStream method is called with GET or HEAD
                            if ((httpVerb != "GET") && (httpVerb != "HEAD"))
                            {
                                using (Stream requestStream = request.GetRequestStream())
                                {
                                    requestStream.Write(payload, 0, contentLength);
                                }
                            }
                                response = (HttpWebResponse)request.GetResponse();

                                if (response != null)
                                {
                                    if (response.Headers.Count > 0)
                                    {
                                        try
                                        {
                                            _responseHeader_ETag = response.GetResponseHeader("ETag");
                                        }
                                        catch { }

                                        try
                                        {
                                            _responseHeader_Content_MD5 = response.GetResponseHeader("Content-MD5");
                                        }
                                        catch { }
                                    }
                                    responseStatusCode = response.StatusCode;
                                    Stream dataStream = response.GetResponseStream();
                                   
                                    StreamReader reader = new StreamReader(dataStream);
                                    responseBody = reader.ReadToEnd();
                                    //Report all incomming data to the debug
                                    lock (theLock1)
                                    {
                                        if (_debug_level == DebugLevel.DebugAll)
                                        {
                                            _Print_Debug(responseBody);
                                        }
                                    }
                                    reader.Close();
                                    if (response.StatusCode == HttpStatusCode.Forbidden)
                                    //if ((response.StatusCode == HttpStatusCode.Forbidden) || (response.StatusCode == HttpStatusCode.NotFound))
                                    {
                                        lock (theLock1)
                                        {
                                            _Print_Debug("Problem with signature. Check next debug statement for stack");
                                        }
                                        throw new WebException("Forbidden", null, WebExceptionStatus.TrustFailure, response);
                                    }
                                        response.Close();
                                    if (responseBody == null)
                                        responseBody = "No body content";

                                    //_Print_Debug(responseBody);
                                    return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };

                                }
                                else
                                {
                                    return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };
                                }
                            }
                            else
                            {
                                lock (theLock1)
                                {
                                    _Print_Debug("Failure: Request is null");
                                }
                                return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };
                            }
                    }
                    catch (WebException ex)
                    {
                        lock (theLock1)
                        {
                            _Print_Debug("An error occured. Status code:" + ((HttpWebResponse)ex.Response).StatusCode);
                        }
                        responseStatusCode = ((HttpWebResponse)ex.Response).StatusCode;
                        using (Stream stream = ex.Response.GetResponseStream())
                        {
                            using (StreamReader sr = new StreamReader(stream))
                            {
                                StringBuilder sB = new StringBuilder("");
                                Char[] chunk = new char[20];

                                while (sr.Peek() > -1 )
                                {
                                    int readBytes = sr.Read(chunk, 0, chunk.Length);
                                    sB.Append(chunk, 0, readBytes);
                                }
                                responseBody = sB.ToString();
                                lock (theLock1)
                                {
                                    _Print_Debug(responseBody);
                                }

                                
                                //var s = sr.ReadToEnd();
                                //lock (theLock1)
                                //{
                                //    _Print_Debug(s);
                                //}
                                //responseBody = s;
                                

                                return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };
                            }
                        }
                    }
                    
                    catch (Exception ex2)
                    {
                        lock (theLock1)
                        {
                            _Print_Debug("Exception in HttpWebRequest.GetResponse(): " + ex2.Message);
                            _Print_Debug("ETag: " + _responseHeader_ETag + " Body: " + responseBody + " StatusCode: " + responseStatusCode);
                        }
                        return new BasicHttpResponse() { ETag = _responseHeader_ETag, Body = responseBody, StatusCode = responseStatusCode };
                    }
                    finally
                    {
                        if (response != null)
                        {
                            response.Dispose();
                        }
                        if (request != null)
                        {
                            request.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (theLock1)
                    {
                        _Print_Debug("Exception in HttpWebRequest: " + ex.Message);
                    }
                    return new BasicHttpResponse() { ETag = null, Body = responseBody, StatusCode = responseStatusCode };
                }
            }
            */



        private static string PrepareSPWF04SxRequest(Uri url, string authHeader, string dateHeader, string versionHeader, byte[] fileBytes, int contentLength, string httpVerb, bool isSocketRequest,  bool expect100Continue = false, string acceptType = "application/json;odata=minimalmetadata", Hashtable additionalHeaders = null)
            {
            StringBuilder requ;
            if (isSocketRequest)
            {
                requ = new StringBuilder(httpVerb + " " + url.AbsolutePath + " HTTP/1.1");
            }
            else
            {
                //requ = new StringBuilder(httpVerb + " " + url.AbsolutePath + " HTTP/1.1");
                requ = new StringBuilder(httpVerb + " " + url.AbsoluteUri + " HTTP/1.1");
            }
                    requ.Append("\r\n" + "User-Agent: " + "Http-Client");
                    requ.Append("\r\n" + "x-ms-date: " + dateHeader);
                    requ.Append("\r\n" + "x-ms-version: " + versionHeader);
                    requ.Append("\r\n" + "Authorization: " + authHeader);



                    if (expect100Continue)
                    {
                        requ.Append("\r\n" + "Expect: " + "100-continue");
                    }
                    if (additionalHeaders != null)
                    {
                        foreach (var additionalHeader in additionalHeaders.Keys)
                        {
                            requ.Append("\r\n" + additionalHeader.ToString() + ": " + additionalHeaders[additionalHeader].ToString());
                        }
                    }
                    // RoSchmi
                    requ.Append("\r\n" + "Content-Length: " + contentLength.ToString());
                    requ.Append("\r\n" + "Connection: " + "Close");
                    requ.Append("\r\n" + "Host: " + url.Host);

            if (isSocketRequest)
            {
                //requ.Append("\r\n\r\n0\r\n\r\n");
                { requ.Append("\r\n\r\n");}
            }
            else
            {
                requ.Append("\r\n");
            }


                    return requ.ToString();
                }
        /*
        private static string PrepareSPWF04SxRequest(Uri url, string authHeader, string dateHeader, string versionHeader, byte[] fileBytes, int contentLength, string httpVerb, bool expect100Continue = false, string acceptType = "application/json;odata=minimalmetadata", Hashtable additionalHeaders = null)
        {
            //StringBuilder requ = new StringBuilder(httpVerb + " " + url.AbsoluteUri.Substring(7) + " HTTP/1.1");
            StringBuilder requ = new StringBuilder(httpVerb + " " + url.AbsoluteUri + " HTTP/1.1");
            requ.Append("\r\n" + "User-Agent: " + "Http-Client");
            requ.Append("\r\n" + "x-ms-date: " + dateHeader);
            requ.Append("\r\n" + "x-ms-version: " + versionHeader);
            requ.Append("\r\n" + "Authorization: " + authHeader);



            if (expect100Continue)
            {
                requ.Append("\r\n" + "Expect: " + "100-continue");
            }
            if (additionalHeaders != null)
            {
                foreach (var additionalHeader in additionalHeaders.Keys)
                {
                    requ.Append("\r\n" + additionalHeader.ToString() + ": " + additionalHeaders[additionalHeader].ToString());
                }
            }
            requ.Append("\r\n" + "ContentLength: " + contentLength.ToString());
            requ.Append("\r\n" + "Connection: " + "Close");
            requ.Append("\r\n" + "Host: " + "prax47.table.core.windows.net");


            return requ.ToString();
        }
        */


        /// <summary>
        /// Prepares a HttpWebRequest with required headers of x-ms-date, x-ms-version, Authorization and others
        /// </summary>
        /// <param name="url"></param>
        /// <param name="authHeader"></param>
        /// <param name="dateHeader"></param>
        /// <param name="versionHeader"></param>
        /// <param name="fileBytes"></param>
        /// <param name="contentLength"></param>
        /// <param name="httpVerb"></param>
        /// <param name="expect100Continue"></param>
        /// <param name="acceptType"></param>
        /// <param name="additionalHeaders"></param>
        /// <returns></returns>
        private static HttpWebRequest PrepareRequest(Uri url, string authHeader, string dateHeader, string versionHeader, byte[] fileBytes, int contentLength, string httpVerb, bool expect100Continue = false, string acceptType = "application/json;odata=minimalmetadata", Hashtable additionalHeaders = null)
                {
                    System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)WebRequest.Create(url);
                    request.Method = httpVerb;
                    request.ContentLength = contentLength;
                    request.UserAgent = "RsNetmfHttpClient";
                    request.Headers.Add("x-ms-date", dateHeader);
                    request.Headers.Add("x-ms-version", versionHeader);
                    request.Headers.Add("Authorization", authHeader);

                    if (expect100Continue)
                    {
                        request.Expect = "100-continue";
                    }
                    if (additionalHeaders != null)
                    {
                        foreach (var additionalHeader in additionalHeaders.Keys)
                        {
                            request.Headers.Add(additionalHeader.ToString(), additionalHeaders[additionalHeader].ToString());
                        }
                    }

                    //*******************************************************
                    // To use Fiddler as WebProxy include this code segment
                    // Use the local IP-Address of the PC where Fiddler is running
                    // See here how to configurate Fiddler; -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
                    lock (theLock1)
                    {
                        if (_fiddlerIsAttached)
                        {
                            request.Proxy = new WebProxy(_fiddlerIP.ToString(), _fiddlerPort);
                        }
                    }
                    //**********

                    //PrintKeysAndValues(request.Headers);

                    return request;
                }
/*
                public static void PrintKeysAndValues(WebHeaderCollection myHT)
                {
                    lock (theLock1)
                    {
                        string[] allKeys = myHT.AllKeys;
                        _Print_Debug("\r\nThe request was sent with the following headers");
                        foreach (string Key in allKeys)
                        {
                            _Print_Debug(Key + ":");
                        }
                        _Print_Debug("\r\n");
                    }
                }
                */
/*
                public static void PrintKeysAndValues(Hashtable myHT)
                {
                    lock (theLock1)
                    {
                        _Print_Debug("\r\nThe request was sent with the following headers");
                        foreach (DictionaryEntry de in myHT)
                        {
                            _Print_Debug(de.Key + ":" + de.Value);
                        }
                        _Print_Debug("\r\n");
                    }

                }
*/
            }
        }
    


