using FileMasterLibrary;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DiscordFileMasterV2
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FileJoiner : Page
    {
        public FileJoiner()
        {
            this.InitializeComponent();

            //Ensure checkbox has a value
            WillAutoLaunchFile.IsChecked = false;
        }

        //Helper Functions---------------------------------------------------------------------------------------------------------------------------------------------------------
        private void ToggleWindowEnabled(bool isEnabled)
        {
            //Disable navigation bar
            MainWindow.CurrentInstance.ToggleNavigationPane(isEnabled);

            //Disable buttons
            InputFolderSelectButton.IsEnabled = isEnabled;
            OutputFolderSelectButton.IsEnabled = isEnabled;
            JoinButton.IsEnabled = isEnabled;
            WillAutoLaunchFile.IsEnabled = isEnabled;
        }

        private async void InputFolderSelectButton_Click(object sender, RoutedEventArgs e)
        {
            //Get the window handle
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.CurrentInstance);

            //Initialize the folder picker
            FolderPicker openPicker = new FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.CommitButtonText = "Select Folder";
            openPicker.FileTypeFilter.Add("*"); //This field is REQUIRED
            openPicker.SettingsIdentifier = "Split Files Folder";

            //Get folder
            StorageFolder folder = await openPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                InputFolderDirectoryBox.Text = folder.Path;
                StatusBox.Text = "Waiting to split...";
            }

            else
            {
                StatusBox.Text = "Folder select failed!";
            }
        }

        private async void OutputFolderSelectButton_Click(object sender, RoutedEventArgs e)
        {
            //Get the window handle
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.CurrentInstance);

            //Initialize the folder picker
            FolderPicker openPicker = new FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.CommitButtonText = "Select Folder";
            openPicker.FileTypeFilter.Add("*"); //This field is REQUIRED
            openPicker.SettingsIdentifier = "Joined File Folder";

            //Get folder
            StorageFolder folder = await openPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                OutputFolderDirectoryBox.Text = folder.Path;
                StatusBox.Text = "Waiting to split...";
            }

            else
            {
                StatusBox.Text = "Folder select failed!";
            }
        }

        private void JoinButton_Click(object sender, RoutedEventArgs e)
        {
            //Clear progress bar
            ProgressBar.Value = 0;

            //Get input folder directory
            string inputFolderPath = InputFolderDirectoryBox.Text;

            //Get output folder directory
            string outputFolderPath = OutputFolderDirectoryBox.Text;

            //Get the new filename
            string newFileName = NewFilenameBox.Text;

            //Get the extension
            string extension = NewExtension.Text;

            //Disable window
            ToggleWindowEnabled(false);
            this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Wait);

            //Start up thread that will start the "FileJoin" thread and update the UI thread on its progress
            Thread monitoringJoinThread = new Thread(() => StartJoin(inputFolderPath, outputFolderPath, newFileName, extension));
            monitoringJoinThread.Start();
        }

        //Thread Functions---------------------------------------------------------------------------------------------------------------------------------------------------------
        private void StartJoin(string inputFolderPath, string outputFolderPath, string outputFileName, string outputExtension)
        {
            //Create FileMaster Instance
            FileMaster fileMasterInstance = new FileMaster();

            //Spin up thread to start joining the pieces
            Thread joinThread = new Thread(() => fileMasterInstance.FileJoin(inputFolderPath, outputFolderPath, outputFileName, outputExtension));
            Stopwatch timer = new Stopwatch();
            timer.Start();
            joinThread.Start();

            this.DispatcherQueue.TryEnqueue(() =>
            {
                StatusBox.Text = "Validating...";
            });
            while (!fileMasterInstance.IsFinished && !fileMasterInstance.IsValidated);

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

            this.DispatcherQueue.TryEnqueue(() =>
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
                        StatusBox.Text = $"Pieces Joined: {currentNumFilesProcessed}/{fileMasterInstance.TotalFiles}";
                        ProgressBar.Value = 100 * ((double)currentNumFilesProcessed / fileMasterInstance.TotalFiles);
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

            //Join must be over
            timer.Stop();
            this.DispatcherQueue.TryEnqueue(() =>
            {
                StatusBox.Text = $"JOIN COMPLETE! Took {timer.ElapsedMilliseconds} milliseconds.";
                this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                ToggleWindowEnabled(true);
            });

            //Check if program needs to be launched
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (WillAutoLaunchFile.IsChecked.Value)
                {
                    var p = new Process();
                    p.StartInfo = new ProcessStartInfo(Path.Join(outputFolderPath, $"{outputFileName}.{outputExtension}"))
                    {
                        UseShellExecute = true
                    };
                    p.Start();
                }
            });
            
        }
    }
}
