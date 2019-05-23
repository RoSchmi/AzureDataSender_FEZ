using System;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.Spi;
using GHIElectronics.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx;

namespace RoSchmi.TinyCLR.Drivers.STMicroelectronics.SPWF04Sx
{
    public class SPWF04SxInterfaceExtension : SPWF04SxInterface
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

        // Added by RoSchmi
        /// <summary>
        /// Returns the properties 'Length', 'Volume' and 'Name' of the specified file
        /// If the file doesn't exist null is returned (so can be used as kind of FileExists command
        /// </summary>
        /// <param name="fileList">The list of files (getfilelisting command.</param>
        /// <param name="filename">The file from where to retrieve the data.</param>

        public FileEntity GetFileProperties(string fileList, string filename)
        {
            if ((filename == null) || (fileList == null)) throw new ArgumentNullException();

            FileEntity selectedFile = null;
            string[] filesArray = fileList.Split(':');

            for (int i = 1; i < filesArray.Length - 1; i++)
            {
                if (filesArray[i].LastIndexOf("File") == filesArray[i].Length - 4)
                {
                    filesArray[i] = filesArray[i].Substring(0, filesArray[i].Length - 4);
                    string[] properties = filesArray[i].Split('\t');
                    if (properties.Length == 3)
                    {
                        if (properties[2] == filename)
                        {
                            selectedFile = new FileEntity(properties[0], properties[1], properties[2]);
                            break;
                        }
                    }
                }
            }
            return selectedFile;
        }

        // Added by RoSchmi
        /// <summary>
        /// Returns the contents of the specified file as byte array       
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <param name="fileLenth">The length of the file wich must be known before (commands GetFileListing() and GetFileProperties(..).</param>

        public byte[] GetFileDataBinary(string fileName, int fileLength)
        {
            if (fileName == null) throw new ArgumentNullException();
            if (fileLength <= 0) throw new ArgumentOutOfRangeException();
            byte[] readBuffer = new byte[fileLength + 15];
            int total = this.ReadFile(fileName, readBuffer, 0, readBuffer.Length);
            byte[] fileContent = new byte[fileLength];
            Array.Copy(readBuffer, 9 + fileLength.ToString().Length, fileContent, 0, fileLength);           
            return fileContent;
        }
    }
}
