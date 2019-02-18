using System;

namespace AzureDataSender_FEZ
{
    public class SampleValue
    {
        
        public SampleValue(string pPartitionKey, DateTime pTimeOfSample, double pT_1, double pT_2, double pT_3, double pT_4, bool pForceSend)
        {
            this.PartitionKey = pPartitionKey;
            this.TimeOfSample = pTimeOfSample;
            
            this.T_1 = pT_1;
            this.T_2 = pT_2;
            this.T_3 = pT_3;
            this.T_4 = pT_4;
            this.ForceSend = pForceSend;
        }
        public string PartitionKey { get; set; }        
        public DateTime TimeOfSample { get; set; }                     
        public double T_1 { get; set; }       
        public double T_2 { get; set; }      
        public double T_3 { get; set; }       
        public double T_4 { get; set; }        
        public bool ForceSend { get; set; }        
    }
}
