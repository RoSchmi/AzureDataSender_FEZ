using System;

namespace GHIElectronics.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx
{     
    public class SPWF04SxSocketErrorException : SPWF04SxException
    {
        public SPWF04SxSocketErrorException(string message) : base(message) { }
    }      
}
