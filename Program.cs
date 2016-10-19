//----------------------------------------------------------------------------------
// Microsoft Developer & Platform Evangelism
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
//----------------------------------------------------------------------------------
// The example companies, organizations, products, domain names,
// e-mail addresses, logos, people, places, and events depicted
// herein are fictitious.  No association with any real company,
// organization, product, domain name, email address, logo, person,
// places, or events is intended or should be inferred.
//----------------------------------------------------------------------------------

namespace Incremental_Snapshot_Backup
{
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.WindowsAzure.Storage.RetryPolicies;
    using Microsoft.WindowsAzure.Storage.DataMovement;

    using System;
    using System.Threading.Tasks;
    using System.IO;
    using System.Collections.Generic;

    public class Program
    {
        // References to the containers that will be used in this simulation
        static CloudBlobContainer container;
        static CloudBlobContainer backupContainer;

        static void Main(string[] args)
        {
            Console.WriteLine("Incremental Snapshot Backup Sample\n");
            try
            {
                IncrementalSnapshotBackupAsync().Wait();
            }
            finally
            {
                // Clean up after the demo. To inspect the results of this sample in the Azure Portal,
                // comment out the following two lines of code.
                container.DeleteIfExists();
                backupContainer.DeleteIfExists();
            }

            Console.WriteLine("Completed succesfully\nPress the enter key to exit");
            Console.ReadLine();
        }

        /// <summary>
        /// Demonstrates how to do backup using incremental snapshots. See this article on 
        /// backing up Azure virtual machine disks with incremental snapshots:
        /// https://azure.microsoft.com/en-us/documentation/articles/storage-incremental-snapshots/
        /// The article also describes how to restore a disk from incremental snapshots.
        /// </summary>
        /// <returns>Task</returns>
        public static async Task IncrementalSnapshotBackupAsync()
        {
            const int TotalBlobSize = 512 * 6;      // The total amount of data in the blob
            const int UpdateWriteSize = 512 * 2;    // The amount of data to update in the blob
            const int UpdateWriteOffset = 512 * 2;  // The offset at which to write updated data
            const int ClearPagesOffset = 512 * 5;   // The offset at which to begin clearing page data
            const int ClearPagesSize = 512;         // The amount of data to clear in the blob

            string SimulationID = Guid.NewGuid().ToString();      // The simulation ID
            string ContainerName = "container-" + SimulationID;   // The name of the primary container
            string PageBlobName = "binary-data";                  // The name of the primary page blob

            // Retrieve storage account information from connection strings
            // How to create storage connection strings - http://msdn.microsoft.com/en-us/library/azure/ee758697.aspx
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
               CloudConfigurationManager.GetSetting("StorageConnectionString"));
            CloudStorageAccount backupStorageAccount = CloudStorageAccount.Parse(
               CloudConfigurationManager.GetSetting("BackupStorageConnectionString"));

            // Create blob clients for interacting with the blob service.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobClient backupBlobClient = backupStorageAccount.CreateCloudBlobClient();

            // Create containers for organizing blobs within the storage accounts.
            container = blobClient.GetContainerReference(ContainerName);
            backupContainer = backupBlobClient.GetContainerReference("copy-of-" + ContainerName);
            try
            {
                // The call below will fail if the sample is configured to use the storage emulator 
                // in the connection string, but the emulator is not running.
                BlobRequestOptions requestOptions = new BlobRequestOptions()
                { RetryPolicy = new NoRetry() };
                await container.CreateIfNotExistsAsync(requestOptions, null);
                await backupContainer.CreateIfNotExistsAsync(requestOptions, null);
            }
            catch (StorageException)
            {
                Console.WriteLine("If you are running with the default connection string, please make " +
                    "sure you have started the storage emulator. Press the Windows key and type Azure " +
                    "Storage to select and run it from the list of applications - then restart the sample.");
                Console.ReadLine();
                throw;
            }

            // Create, write to, and snapshot a page blob in the newly created container.  
            CloudPageBlob pageBlob = container.GetPageBlobReference(PageBlobName);
            await pageBlob.CreateAsync(TotalBlobSize);
            byte[] samplePagedata = new byte[TotalBlobSize];
            Random random = new Random();
            random.NextBytes(samplePagedata);
            await pageBlob.UploadFromByteArrayAsync(samplePagedata, 0, samplePagedata.Length);
            CloudPageBlob pageBlobSnap = await pageBlob.CreateSnapshotAsync();

            // Create a blob in the backup account
            CloudPageBlob pageBlobBackup = backupContainer.GetPageBlobReference("copy-of-" + PageBlobName);

            // Copy to the blob in the backup account. Notice that the <isServiceCopy> flag here is 
            // set to 'false'. This forces data to be first downloaded from the source, then uploaded 
            // to the destination. If <isServiceCopy> were set to 'true' we could avoid the I/O of 
            // downloading and then uploading, but could not guarantee  that the copy completed even 
            // after the local task completed. Since the remainder of this sample depends on the 
            // successful completion of this operation, we must set <isServiceCopy> to 'false.'
            await TransferManager.CopyAsync(pageBlobSnap, pageBlobBackup, false);

            // Snapshot the backup copy
            CloudPageBlob pageBlobBackupSnap = await pageBlob.CreateSnapshotAsync();

            // Change the contents of the original page blob (note: we must convert the byte array to a 
            // stream in order to write to a non-zero offset in the page blob) and snapshot.
            byte[] updatePageData = new byte[UpdateWriteSize];
            random.NextBytes(updatePageData);
            Stream updatePageDataStream = new MemoryStream(updatePageData);
            await pageBlob.WritePagesAsync(updatePageDataStream, UpdateWriteOffset, null);
            await pageBlob.ClearPagesAsync(ClearPagesOffset, ClearPagesSize);
            CloudPageBlob newPageBlobSnap = await pageBlob.CreateSnapshotAsync();

            // Get the incremental changes and write to the backup.
            IEnumerable<PageDiffRange> changedPages = 
                await newPageBlobSnap.GetPageRangesDiffAsync(pageBlobSnap.SnapshotTime.Value);
            foreach (PageDiffRange pageRange in changedPages)
            {
                // If this page range is cleared, remove the old data in the backup.
                if (pageRange.IsClearedPageRange) await pageBlobBackup.ClearPagesAsync(
                    pageRange.StartOffset, pageRange.EndOffset - pageRange.StartOffset + 1);

                // If this page range is not cleared, write the new data to the backup.
                else
                {
                    byte[] toWrite = new byte[pageRange.EndOffset - pageRange.StartOffset + 1];
                    await newPageBlobSnap.DownloadRangeToByteArrayAsync(
                        toWrite, 0, pageRange.StartOffset, pageRange.EndOffset - pageRange.StartOffset + 1);
                    Stream toWriteStream = new MemoryStream(toWrite);
                    await pageBlobBackup.WritePagesAsync(toWriteStream, pageRange.StartOffset, null);
                }
            }
            // Snapshot the backup blob and delete the old snapshot from the primary account 
            CloudPageBlob pageBlobBackupSnapv2 = await pageBlobBackup.CreateSnapshotAsync();
            await pageBlobSnap.DeleteAsync();
        }
    }
}
