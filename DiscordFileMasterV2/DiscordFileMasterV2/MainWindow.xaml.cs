using Microsoft.UI;
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
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Windowing;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DiscordFileMasterV2
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public static MainWindow CurrentInstance;

        public MainWindow()
        {
            this.InitializeComponent();

            //Record the current instance of the window so, it can be accessed by pages
            CurrentInstance = this;

            //Set title
            this.Title = "Discord FileMaster";

            //Set icon of window
            IntPtr HWND = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(HWND);
            AppWindow APP_WINDOW = AppWindow.GetFromWindowId(windowId);
            APP_WINDOW.SetIcon(Path.Combine(Package.Current.InstalledLocation.Path, "Icon.ico"));

            //Navigate to the Joiner on launch
            ContentFrame.Navigate(typeof(FileJoiner));

        }

        //Public Window Helpers----------------------------------------------------------------------------------------------------------------------------------------------------
        public void ToggleNavigationPane(bool isEnabled)
        {
            FileMasterMenu.IsEnabled = isEnabled;
        }

        //Event Handlers-----------------------------------------------------------------------------------------------------------------------------------------------------------
        private void FileMasterMenu_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            //Ensure the invoked item was actually defined
            if(args.InvokedItemContainer == null)
            {
                return;
            }

            //Get the selected tag to determine which page to navigate to
            string tag = (string)args.InvokedItemContainer.Tag;

            //Navigate to appropriate page
            if(tag == "Joiner")
            {
                ContentFrame.Navigate(typeof(FileJoiner));
            }

            else if(tag == "Splitter")
            {
                ContentFrame.Navigate(typeof(FileSplitter));
            }
        }
    }
}
