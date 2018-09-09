using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.Storage;
using static Microsoft.Tools.WindowsDevicePortal.DevicePortal;

namespace HoloLensCommander
{
    class FileTransferSpec
    {
        private class FileDetails
        {
            public string RemotePackageFullName { get; set; }
            public string RemoteKnownFolderId { get; set; }
            public string RemoteSubPath { get; set; }
            public string LocalPath { get; set; }
            public long LocalSizeInBytes { get; set; }
            public long RemoteSizeInBytes { get; set; }
            public bool ExistsOnRemote { get; set; }
        }

        private StorageFolder uploadStorageFolder;
        private FileGroup[] fileGroups;
        private List<FileDetails> fileDetails = new List<FileDetails>();

        public static async Task<FileTransferSpec> LoadFromFolderAsync(StorageFolder uploadStorageFolder)
        {
            var uploadSpecStorageFile = await uploadStorageFolder.GetFileAsync("manifest.json");

            using (Stream stream = await uploadSpecStorageFile.OpenStreamForReadAsync())
            {
                var serializer = new DataContractJsonSerializer(typeof(FileGroup[]));
                // TODO - report parse error
                var fileGroupArray = (FileGroup[])serializer.ReadObject(stream);
                return new FileTransferSpec(uploadStorageFolder, fileGroupArray);
            }
        }

        private FileTransferSpec(StorageFolder uploadFolder, FileGroup[] fileGroups)
        {
            this.uploadStorageFolder = uploadFolder;
            this.fileGroups = fileGroups;
        }

        /// <summary>
        /// Gets data about the local and remote files to
        /// figure out what files actually need to be uploaded
        /// </summary>
        /// <returns></returns>
        public async Task GatherFileData(bool forceOverwrite, Func<string, string, string, Task<FolderContents>> getFolderContents)
        {
            foreach(var fileGroup in this.fileGroups)
            {
                FolderContents folderContents = null;
                if (!forceOverwrite)
                {
                    folderContents = await getFolderContents(fileGroup.RemoteKnownFolderId, fileGroup.RemoteSubPath, fileGroup.RemotePackageFullName);
                }

                foreach (var fileName in fileGroup.FileNames)
                {
                    var fileDetails = new FileDetails();
                    fileDetails.RemoteKnownFolderId = fileGroup.RemoteKnownFolderId;
                    fileDetails.RemotePackageFullName = fileGroup.RemotePackageFullName;
                    fileDetails.RemoteSubPath = fileGroup.RemoteSubPath;

                    var storageFile = await this.uploadStorageFolder.GetFileAsync(fileName);
                    var basicProperties = await storageFile.GetBasicPropertiesAsync();
                    fileDetails.LocalSizeInBytes = (long)basicProperties.Size;
                    fileDetails.LocalPath = storageFile.Path;

                    if (folderContents != null)
                    {
                        foreach (var fileOrFolderInformation in folderContents.Contents)
                        {
                            if (fileOrFolderInformation.IsFolder) continue;
                            if (0 == string.Compare(fileName, fileOrFolderInformation.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                fileDetails.ExistsOnRemote = true;
                                fileDetails.RemoteSizeInBytes = fileOrFolderInformation.SizeInBytes;
                                break;
                            }
                        }
                    }

                    this.fileDetails.Add(fileDetails);
                }
            }
        }

        internal async Task UploadFiles(Func<string, string, string, string, Task> uploadFileAsync)
        {
            foreach(var fileDetail in this.fileDetails)
            {
                if(fileDetail.ExistsOnRemote && (fileDetail.LocalSizeInBytes == fileDetail.RemoteSizeInBytes))
                {
                    // file exists both on remote and local and is the same size
                    // and we weren't told to force it above.  So skip it.
                    continue;
                }

                Debug.WriteLine("Uploading file " + fileDetail.LocalPath);
                await uploadFileAsync(fileDetail.RemoteKnownFolderId, fileDetail.LocalPath, fileDetail.RemoteSubPath, fileDetail.RemotePackageFullName);
                Debug.WriteLine("\tDone " + fileDetail.LocalPath);
            }
        }
    }

    [DataContract]
    class FileGroup
    {
        [DataMember]
        public string RemotePackageFullName { get; private set; }

        [DataMember]
        public string RemoteKnownFolderId { get; private set; }

        [DataMember]
        public string RemoteSubPath { get; private set; }


        [DataMember(Name = "FileNames")]
        public IList<string> FileNames { get; private set; }
    }
}
