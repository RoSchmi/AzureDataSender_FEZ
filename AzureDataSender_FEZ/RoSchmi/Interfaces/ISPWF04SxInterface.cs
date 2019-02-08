using System;
using System.Collections;
using System.Text;
using System.Threading;

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



namespace RoSchmi.Interfaces
{
    public interface ISPWF04SxInterface
    {
       

        SpiDevice spi { get; set; }
        GpioPin irq { get; set; }
        GpioPin reset { get; set; }

        SpiDevice GetConnectionSettings(SpiChipSelectType spiChipSelectType, int gpioPin);
    }
}
