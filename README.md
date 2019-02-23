# AzureDataSender_FEZ
This is work in progress. It works now (23.02.2019) quite stable but isn't tested over longer periods.

App for [GHI Electronics FEZ](https://ghielectronics.com/products/fez) with SPWF04SA Board writes data to Azure Storage Tables to be visualized by Microsoft Azure Storage Explorer or the iOS App ['AzureTabStorClient'](https://itunes.apple.com/us/app/azuretabstorclient/id1399683806). (For details visit this [Page](https://azuretabstorclient.wordpress.com/))

The FEZ Mainboard can be programmed in C# on top of GHI-Electronics TinyCLR firmware (an OS with a subset of .NET like the still perhaps better known .NET Micro Framework NETMF)

Sensor data are transfered to Azure using TLS 1.2 secured transmission (https). Reading back stored Data from Azure actually works only over unsecure transmission (http).

Data stored (in special format) in Azure Storage Tables can then be visulized with the iOS App Store App: ['Charts4Azure'](https://itunes.apple.com/us/app/charts4azure/id1442910354?mt=8)

For details visit this [Page](https://azureiotcharts.home.blog/)

![gallery](Charts4AzureGitHub.png)

#### How it works:

In this example App the values of 4 analog inputs are periodically read (timer event) and stored in an intermediate 'Container'.
Periodically (another timer) these data are stored to an Azure Storage Table. Right after loading these data to Azure they are read back to prove that the transmission was successful (this is normally not needed).
On Pressing or Relasing of BTN1 of the FEZ-Board (between press and release should be enough time to complete the upload of this event) the state of the button is stored in an Azure Storage Table.  

#### Known Issues/Limitations:

Because of the limited Ram of the used MCU (STM32F401) on the GHI Electronics FEZ Board only one Channel for On/Off sensor data is implemented. Loading up On/Off-Sensor-Data (Press and release Btn1) should only occur in a time when no analog data are loaded up. The first pressing of BTN1 after powering up the board is not uplpaded to Azure. Because of Ram limitations of the board (Out of memory exceptions) actually there are frequent reboots if the Watchdog is activated.
Reading back stored data from Azure actually works only unsecure (http).


Instructions to a similar project for the Beaglebone Green are provided on Hackster.io:
https://www.hackster.io/RoSchmi/mono-c-app-sending-sensor-data-to-azure-storage-tables-0de137
