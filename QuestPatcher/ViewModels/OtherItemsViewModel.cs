﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using QuestPatcher.Core.Modding;
using QuestPatcher.Models;
using QuestPatcher.Resources;
using ReactiveUI;
using Serilog;

namespace QuestPatcher.ViewModels
{
    public class OtherItemsViewModel : ViewModelBase
    {
        public OtherFilesManager FilesManager { get; }

        public OperationLocker Locker { get; }

        public ProgressViewModel ProgressView { get; }

        public FileCopyType? SelectedFileCopy
        {
            get => _selectedFileCopy;
            set
            {
                if (_selectedFileCopy != value)
                {
                    _selectedFileCopy = value;
                    this.RaisePropertyChanged();
                    OnSelectedFileCopyChanged();
                    this.RaisePropertyChanged(nameof(CanUseFileCopies));
                }
            }
        }
        private FileCopyType? _selectedFileCopy;

        public ObservableCollection<string> SelectedFiles { get; } = new();

        public bool CanDeleteSelectedFiles => SelectedFiles.Count > 0 && Locker.IsFree;

        public bool CanUseFileCopies => _selectedFileCopy != null && Locker.IsFree;

        private readonly Window _mainWindow;
        private readonly BrowseImportManager _browseManager;

        public OtherItemsViewModel(OtherFilesManager filesManager, Window mainWindow, BrowseImportManager browseManager, OperationLocker locker, ProgressViewModel progressView)
        {
            FilesManager = filesManager;
            Locker = locker;
            ProgressView = progressView;
            _mainWindow = mainWindow;
            _browseManager = browseManager;

            // Whenever the App ID changes, reset the selected file copy to the first in this list
            filesManager.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(filesManager.CurrentDestinations)) { OnCurrentDestinationsChanged(); }
            };

            locker.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(locker.IsFree)) { this.RaisePropertyChanged(nameof(CanDeleteSelectedFiles)); this.RaisePropertyChanged(nameof(CanUseFileCopies)); }
            };
            SelectedFiles.CollectionChanged += (_, _) =>
            {
                this.RaisePropertyChanged(nameof(CanDeleteSelectedFiles));
            };

            OnCurrentDestinationsChanged();
        }

        private void OnCurrentDestinationsChanged()
        {
            if (FilesManager.CurrentDestinations.Count > 0)
            {
                SelectedFileCopy = FilesManager.CurrentDestinations[0];
            }
            else
            {
                SelectedFileCopy = null;
            }
        }


        private async void OnSelectedFileCopyChanged()
        {
            if (SelectedFileCopy == null) { return; }
            if (!SelectedFileCopy.HasLoaded && Locker.IsFree)
            {
                await RefreshFiles();
            }
        }

        public async Task RefreshFiles()
        {
            if (SelectedFileCopy == null) { return; }

            try
            {
                await SelectedFileCopy.LoadContents();
            }
            catch (Exception ex)
            {
                var builder = new DialogBuilder
                {
                    Title = Strings.OtherItems_LoadFailed_Title,
                    Text = string.Format(Strings.OtherItems_LoadFailed_Text, SelectedFileCopy.Path),
                    HideCancelButton = true
                };
                builder.WithException(ex);
                await builder.OpenDialogue(_mainWindow);
            }
        }

        public async Task DeleteFiles(params string[] filePaths)
        {
            if (!Locker.IsFree) { return; }

            Locker.StartOperation();
            try
            {
                Debug.Assert(SelectedFileCopy != null); // This button is only available when there is a selected file copy

                int failed = 0;
                Exception? lastException = null;
                string? lastFailed = null;
                // Remove the given files, and catch exceptions for later
                foreach (string filePath in filePaths)
                {
                    try
                    {
                        await SelectedFileCopy.RemoveFile(filePath);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to delete file {FilePath}", filePath);
                        lastException = ex;
                        lastFailed = filePath;
                        failed++;
                    }
                }

                if (failed == 0) { return; }

                var builder = new DialogBuilder
                {
                    HideCancelButton = true
                };
                
                if (failed > 1)
                {
                    // If multiple files failed, we can display a dialog saying how many succeeded and how many failed
                    builder.Title = Strings.OtherItems_DeleteFailed_Title_Multiple;
                    builder.Text = string.Format(Strings.OtherItems_DeleteFailed_Text_Multiple, failed, filePaths.Length);
                }
                else
                {
                    // Otherwise, it'd be more useful to display a dialog with just the one exception
                    builder.Title = Strings.OtherItems_DeleteFailed_Title;
                    builder.Text = string.Format(Strings.OtherItems_DeleteFailed_Text, Path.GetFileName(lastFailed)); 
                    Debug.Assert(lastException != null);
                    builder.WithException(lastException);
                }

                await builder.OpenDialogue(_mainWindow);
            }
            finally
            {
                Locker.FinishOperation();
            }
        }

        public async void BrowseForSelectedType()
        {
            Debug.Assert(_selectedFileCopy != null);
            await _browseManager.ShowFileCopyBrowse(_selectedFileCopy);
        }

        public async void DeleteSelectedFiles()
        {
            await DeleteFiles(SelectedFiles.ToArray());
        }

        public async void DeleteAllFiles()
        {
            Debug.Assert(SelectedFileCopy != null);

            // Make sure that people don't delete all their hats accidentally!
            if (SelectedFileCopy.ExistingFiles.Count > 1)
            {
                var builder = new DialogBuilder
                {
                    Title = Strings.OtherItems_DeleteAll_Title,
                    Text = string.Format(Strings.OtherItems_DeleteAll_Text, SelectedFileCopy.ExistingFiles.Count, SelectedFileCopy.NamePlural)
                };
                builder.OkButton.Text = Strings.Generic_Yes;
                builder.CancelButton.Text = Strings.Generic_No;

                if (!await builder.OpenDialogue(_mainWindow))
                {
                    return;
                }
            }

            await DeleteFiles(SelectedFileCopy.ExistingFiles.ToArray());
        }
    }
}
