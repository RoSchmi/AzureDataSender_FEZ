using System;

namespace AzureDataSender_FEZ
{
    public class SampleValue
    {
        string _partitionKey;
        DateTime _timeOfSample;
        double _T_1;
        double _T_2;
        double _T_3;
        double _T_4;
        bool _forceSend;
        

        public SampleValue(string pPartitionKey, DateTime pTimeOfSample, double pT_1, double pT_2, double pT_3, double pT_4, bool pForceSend)
        {
            this._partitionKey = pPartitionKey;
            this._timeOfSample = pTimeOfSample;
            
            this._T_1 = pT_1;
            this._T_2 = pT_2;
            this._T_3 = pT_3;
            this._T_4 = pT_4;
            this._forceSend = pForceSend;
        }

        public string PartitionKey
        {
            get { return this._partitionKey; }
            set { this._partitionKey = value; }
        }

        public DateTime TimeOfSample
        {
            get { return this._timeOfSample;}
            set { this._timeOfSample = value; }
        }
        
        
        public double T_1
        {
            get { return this._T_1; }
            set { this._T_1 = value; }
        }
        public double T_2
        {
            get { return this._T_2; }
            set { this._T_2= value; }
        }
        public double T_3
        {
            get { return this._T_3; }
            set { this._T_3 = value; }
        }
        public double T_4
        {
            get { return this._T_4; }
            set { this._T_4 = value; }
        }
        public bool ForceSend
        {
            get { return this._forceSend; }
            set { this._forceSend = value; }
        }

    }
}
