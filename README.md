---
services: storage
platforms: dotnet
author: michaelhauss
---

# Backup Azure Virtual Machine Disks with Incremental Snapshots

In this sample, we demonstrate the basics of how to maintain backups of virtual machine disks using snapshots. You can use this methodology when you choose not to use Azure Backup and Recovery Service, and wish to create a custom backup strategy for your virtual machine disks.

If you don't have a Microsoft Azure subscription you can
get a FREE trial account [here](http://go.microsoft.com/fwlink/?LinkId=330212).

## Running this sample

This sample can be run using either the Azure Storage Emulator that installs as part of the Azure SDK (In Windows only) - or by
updating the storage connection strings in the app.config file.

To run the sample using the Storage Emulator (Azure SDK):

1. Download and Install the Azure Storage Emulator [here](http://azure.microsoft.com/en-us/downloads/).
2. Start the Azure Storage Emulator (once only) by pressing the Start button or the Windows key and searching for it by typing "Azure Storage Emulator". Select it from the list of applications to start it.
3. Set breakpoints and run the project using F10.

To run the sample using the Storage Service

1. Open the app.config file and comment out the connection string for the emulator (UseDevelopmentStorage=True) and uncomment the connection string for the storage service (AccountName=[]...)
2. Create a two Storage Accounts through the Azure Portal (one primary, one for backup)
3. Provide your [AccountName] and [AccountKey] in the App.Config file for each of the keys: "StorageConnectionString" and "BackupStorageConnectionString".
4. Set breakpoints and run the project using F10.

## More information
- [Protecting Your Blobs Against Application Errors](https://blogs.msdn.microsoft.com/windowsazurestorage/2010/04/29/protecting-your-blobs-against-application-errors/)
- [Back up Azure Virtual Machines](https://docs.microsoft.com/en-us/azure/backup/backup-azure-vms-first-look-arm)
- [Back up Azure Virtual Machine Disks with Incremental Snapshots](https://docs.microsoft.com/en-us/azure/virtual-machines/windows/incremental-snapshots)
- [Storage Emulator](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator)
