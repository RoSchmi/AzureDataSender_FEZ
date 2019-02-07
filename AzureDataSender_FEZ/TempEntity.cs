using System;
//using Microsoft.SPOT;
using System.Collections;
//using PervasiveDigital.Json;
using RoSchmi.Net.Azure.Storage;

namespace AzureDataSender_FEZ
{
    public class TempEntity : TableEntity
    {
        public string actTemperature { get; set; }
        public string location { get; set; }

        // Your entity type must expose a parameter-less constructor
        public TempEntity() { }

        // Define the PK and RK
        public TempEntity(string partitionKey, string rowKey, ArrayList pProperties)
            : base(partitionKey, rowKey)
        {
            //this.TimeStamp = DateTime.Now;
            this.Properties = pProperties;    // store the ArrayList

            var myProperties = new PropertyClass()
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                
                // get the values out of the ArrayList
                actTemp = ((string[])this.Properties[0])[2],        // Row 0, arrayfield 2
                /*
                min = ((string[])this.Properties[1])[2],            // Row 1, arrayfield 2
                max = ((string[])this.Properties[2])[2],            // Row 2, arrayfield 2   
                report = ((string[])this.Properties[3])[2],         
                Status = ((string[])this.Properties[4])[2],                                     
                location = ((string[])this.Properties[5])[2],        
                sampleTime = ((string[])this.Properties[6])[2],       
                timeFromLast = ((string[])this.Properties[7])[2],
                Iterations = ((string[])this.Properties[8])[2],
                Sends = ((string[])this.Properties[9])[2],
                RemainingRam = ((string[])this.Properties[10])[2],
                forcedReboots = ((string[])this.Properties[11])[2],
                badReboots = ((string[])this.Properties[12])[2],
                sendErrors = ((string[])this.Properties[13])[2],
                bootReason = ((string[])this.Properties[14])[2],
                forcedSend = ((string[])this.Properties[15])[2],
                message = ((string[])this.Properties[16])[2]
                */
            };

            //this.JsonString = JsonConverter.Serialize(myProperties).ToString();
            this.JsonString = "Not implemented";
        }
        private class PropertyClass
        {
            public string RowKey;
            public string PartitionKey;
            public string sampleTime;
            public string actTemp;
            public string min;
            public string max;
            public string report;
            public string Status;
            public string location;
            public string timeFromLast;
            public string Iterations;
            public string Sends;
            public string RemainingRam;
            public string forcedReboots;
            public string badReboots;
            public string sendErrors;
            public string bootReason;
            public string forcedSend;
            public string message;
        }

    }
}



