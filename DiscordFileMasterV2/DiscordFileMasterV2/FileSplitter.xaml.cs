using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using FileMasterLibrary;
using Windows.Storage.Pickers;
using System.Diagnostics;
using System.Threading;
using Microsoft.UI.Input;

//https://stackoverflow.com/questions/60377718/hide-navigationview-pane-completely

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DiscordFileMasterV2
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FileSplitter : Page
    {
        public FileSplitter()
        {
            this.InitializeComponent();
        }

        //Helper Functions---------------------------------------------------------------------------------------------------------------------------------------------------------
        private void ToggleWindowEnabled(bool isEnabled)
        {
            //Disable navigation bar
            MainWindow.CurrentInstance.ToggleNavigationPane(isEnabled);

            //Disable buttons
            FileSelectButton.IsEnabled = isEnabled;
            FolderSelectButton.IsEnabled = isEnabled;
            SplitButton.IsEnabled = isEnabled;
        }

        //Event Handlers-----------------------------------------------------------------------------------------------------------------------------------------------------------
        private async void FileSelectButton_Click(object sender, RoutedEventArgs e)
        {
            //Get the window handle
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.CurrentInstance);

            //Initialize the file picker
            FileOpenPicker openPicker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.CommitButtonText = "Select File";
            openPicker.FileTypeFilter.Add("*"); /*This field is REQUIRED on Windows 10 or it will crash 
                                                 * (https://github.com/microsoft/WindowsAppSDK/issues/1188). 
                                                 * It is also slow. Try seeking an alternative*/
            openPicker.SettingsIdentifier = "Select File To Be Split";

            //Get file
            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                FileDirectoryBox.Text = file.Path;
                StatusBox.Text = "Waiting to split...";
            }

            else
            {
                StatusBox.Text = "File select failed!";
            }
        }

        private async void FolderSelectButton_Click(object sender, RoutedEventArgs e)
        {
            //Get the window handle
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.CurrentInstance);

            //Initialize the folder picker
            FolderPicker openPicker = new FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.CommitButtonText = "Select Folder";
            openPicker.FileTypeFilter.Add("*"); //This field is REQUIRED
            openPicker.SettingsIdentifier = "Select Folder To Write Split Files";

            //Get folder
            StorageFolder folder = await openPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                FolderDirectoryBox.Text = folder.Path;
                StatusBox.Text = "Waiting to split...";
            }

            else
            {
                StatusBox.Text = "Folder select failed!";
            }
        }

        private void SplitButton_Click(object sender, RoutedEventArgs e)
        {
            //Clear progress bar
            ProgressBar.Value = 0;

            //Get file directory
            string filePath = FileDirectoryBox.Text;

            //Get folder directory
            string folderPath = FolderDirectoryBox.Text;

            //Disable window
            ToggleWindowEnabled(IsEnabled);
            this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Wait);

            //Calculate piece size (8 MB for discord)
            int PIECE_SIZE = 8 * 1000 * 1000;

            //Start up thread that will start the "FileSplit" thread and update the UI thread on its progress
            Thread monitoringJoinThread = new Thread(() => StartSplit(filePath, folderPath, PIECE_SIZE));
            monitoringJoinThread.Start();
        }

        //Thread Functions---------------------------------------------------------------------------------------------------------------------------------------------------------
        private void StartSplit(string inputFile, string outputFolder, int pieceSize)
        {
            //Start up FileMaster instance
            FileMaster fileMasterInstance = new FileMaster();

            //Spin up thread to start splitting up large files into several pieces
            Thread splitThread = new Thread(() => fileMasterInstance.FileSplit(inputFile, outputFolder, pieceSize));
            Stopwatch timer = new Stopwatch();
            timer.Start();
            splitThread.Start();

            this.DispatcherQueue.TryEnqueue(() =>
            {
                StatusBox.Text = "Validating...";
            });
            while (!fileMasterInstance.IsFinished && !fileMasterInstance.IsValidated) ;

            //Check for errors and exit
            if (fileMasterInstance.ExitError)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    StatusBox.Text = $"ERROR: {fileMasterInstance.StatusMessage}";
                    this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                    ToggleWindowEnabled(true);
                });
                return;
            }

            this.DispatcherQueue.TryEnqueue(()=>
            {
                StatusBox.Text = $"Total Number Of Pieces: {fileMasterInstance.TotalFiles}";
            });

            long currentNumFilesProcessed = 0;
            while (!fileMasterInstance.IsFinished)
            {
                //Check if the number of files processed has changed (ensure the dispatcher isn't called unnecessarily when no change was made)
                if (currentNumFilesProcessed != fileMasterInstance.NumFilesProcessed)
                {
                    currentNumFilesProcessed = fileMasterInstance.NumFilesProcessed;
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusBox.Text = $"Pieces Created: {currentNumFilesProcessed}/{fileMasterInstance.TotalFiles}";
                        ProgressBar.Value = 100*(currentNumFilesProcessed / fileMasterInstance.TotalFiles);
                    });
                }
            }

            //Check for errors and exit
            if (fileMasterInstance.ExitError)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    StatusBox.Text = $"ERROR: {fileMasterInstance.StatusMessage}";
                    this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                    ToggleWindowEnabled(true);
                });
                return;
            }

            //Split must be over
            timer.Stop();
            this.DispatcherQueue.TryEnqueue(() =>
            {
                StatusBox.Text = $"SPLIT COMPLETE! Took {timer.ElapsedMilliseconds} milliseconds.";
                this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                ToggleWindowEnabled(true);
            });
            Process.Start("explorer.exe", outputFolder);
        }
    }
}
