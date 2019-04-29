using System;
using System.Collections;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.NetworkInterface;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.Spi;
using GHIElectronics.TinyCLR.Net.NetworkInterface;
using GHIElectronics.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx.Helpers;
using GHIElectronics.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx;
using System.Diagnostics;
using GHIElectronics.TinyCLR.Pins;

namespace RoSchmi.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx
{
    class SPWF04SxInterfaceExtension : SPWF04SxInterface
    {
        public SPWF04SxInterfaceExtension(SpiDevice spi, GpioPin irq, GpioPin reset) : base(spi, irq, reset)
        { }
       
        public void Reset()
        {
            var cmd = this.GetCommand()
               .Finalize(SPWF04SxCommandIds.RESET);
            this.EnqueueCommand(cmd);
            cmd.ReadBuffer();
            this.FinishCommand(cmd);
        }

        // Added by RoSchmi
        /// <summary>
        /// Sets the configuration of the SPWF04 module.
        /// Make sure that only valid parameters are used.
        /// No control mechanisms are implemented to control that things worked properly.
        /// </summary>
        public void SetConfiguration(string confParameter, string value)
        {
        var cmd = this.GetCommand()
        .AddParameter(confParameter)
        .AddParameter(value)
        .Finalize(SPWF04SxCommandIds.SCFG);
        this.EnqueueCommand(cmd);
        byte[] readBuf = new byte[50];
        int len = cmd.ReadBuffer(readBuf, 0, 50);
        this.FinishCommand(cmd);
        }               
    }
}
