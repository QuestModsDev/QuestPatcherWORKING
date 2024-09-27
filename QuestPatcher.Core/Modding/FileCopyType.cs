﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace QuestPatcher.Core.Modding
{
    public class FileCopyType : INotifyPropertyChanged
    {
        /// <summary>
        /// Name of the file copy, singular. E.g. "gorilla tag hat"
        /// </summary>
        public string NameSingular { get; set; }

        /// <summary>
        /// Name of the file copy, plural. E.g. "gorilla tag hats"
        /// </summary>
        public string NamePlural { get; set; }

        /// <summary>
        /// Path to copy files to/list files from
        /// </summary>
        public string Path { get; set; }


        /// <summary>
        /// List of support file extensions for this file copy destination
        /// </summary>
        public List<string> SupportedExtensions { get; set; }

        /// <summary>
        /// The current files in the destination folder.
        /// </summary>
        public ObservableCollection<string> ExistingFiles { get; } = new();

        /// <summary>
        /// Whether the loading attempt has finished successfully or not.
        /// </summary>
        public bool HasLoaded
        {
            get => _hasLoaded;
            private set
            {
                if (_hasLoaded != value)
                {
                    _hasLoaded = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether or not the last loading attempt failed
        /// </summary>
        public bool LoadingFailed
        {
            get => _loadingFailed;
            private set
            {
                if (_loadingFailed != value)
                {
                    _loadingFailed = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly AndroidDebugBridge _debugBridge;
        private bool _hasLoaded;
        private bool _loadingFailed;


        public FileCopyType(AndroidDebugBridge debugBridge, FileCopyInfo info)
        {
            _debugBridge = debugBridge;
            NameSingular = info.NameSingular;
            NamePlural = info.NamePlural;
            Path = info.Path;
            SupportedExtensions = info.SupportedExtensions;
        }

        /// <summary>
        /// Loads the contents of this destination, replacing the old contents if any.
        /// </summary>
        public async Task LoadContents()
        {
            HasLoaded = false;
            LoadingFailed = false;
            try
            {
                await _debugBridge.CreateDirectory(Path); // Create the destination if it does not exist

                var currentFiles = await _debugBridge.ListDirectoryFiles(Path);
                ExistingFiles.Clear();
                foreach (string file in currentFiles)
                {
                    ExistingFiles.Add(file);
                }
            }
            catch (Exception)
            {
                LoadingFailed = true;
                throw; // Rethrow for calling UI to handle if they want to
            }
            finally
            {
                HasLoaded = true;
            }
        }

        /// <summary>
        /// Copies a file to this destination.
        /// </summary>
        /// <param name="localPath">The path of the file on the PC.</param>
        public async Task PerformCopy(string localPath)
        {
            await _debugBridge.CreateDirectory(Path); // Create the destination if it does not exist

            string destinationPath = System.IO.Path.Combine(Path, System.IO.Path.GetFileName(localPath));

            await _debugBridge.UploadFile(localPath, destinationPath);
            if (!ExistingFiles.Contains(destinationPath))
            {
                ExistingFiles.Add(destinationPath);
            }
        }

        /// <summary>
        /// Removes the copied file name and deletes it from the ExistingFiles list (no need to refresh the list to take effect).
        /// </summary>
        /// <param name="name">The full path to the file to delete.</param>
        public async Task RemoveFile(string name)
        {
            await _debugBridge.DeleteFile(name);
            ExistingFiles.Remove(name);
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
