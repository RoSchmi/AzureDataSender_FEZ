# AzureDataSender_FEZ
This is work in progress. It works now (23.02.2019) quite stable but isn't tested over longer periods.

App for [GHI Electronics FEZ](https://ghielectronics.com/products/fez) with SPWF04SA Board writes data to Azure Storage Tables to be visualized by Microsoft Azure Storage Explorer or the iOS App ['AzureTabStorClient'](https://itunes.apple.com/us/app/azuretabstorclient/id1399683806). For details visit this [Page]https://azuretabstorclient.wordpress.com/

Sensor data are transfered to Azure using TLS 1.2 secured transmission (https). Reading back stored Data from Azure actually works only over unsecure transmission (http).

Data stored (in special format) in Azure Storage Tables can then be visulized with the iOS App Store App: ['Charts4Azure'](https://itunes.apple.com/us/app/charts4azure/id1442910354?mt=8)

For details visit this [Page](https://azureiotcharts.home.blog/)

![gallery](Charts4AzureGitHub.png)

#### Known Issues/Limitations:

Because of the limited Ram of the used MCU (STM32F401) on the GHI Electronics FEZ Board only one Channel for On/Off sensor data is implemented. For the same reason (Out of memory exceptions) actually there are frequent Reboots if the Watchdog is activated.
Readin back stored data from Azure actually works only unsecure (http).


Instructions to a similar project for the Beaglebone Green are provided on Hackster.io:
https://www.hackster.io/RoSchmi/mono-c-app-sending-sensor-data-to-azure-storage-tables-0de137
