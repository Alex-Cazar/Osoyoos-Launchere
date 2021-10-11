﻿using System.IO;
using System.Windows;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ToolkitLauncher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static readonly string DeleteOldCommand = "-DeleteOldInternal";
        private static readonly int MAX_DELETE_RETRY = 10;

        private async void handleDeleteCommand(string file)
        {
            for (int i = 0; i < MAX_DELETE_RETRY; i++) {
                await Task.Delay(2000); // give the parent time to exit
                try
                {
                    File.Delete(file);
                    Debug.Print($"Deleted \"{file}\" on attempt {i}");
                    return;
                }
                catch
                {
                    Debug.Print($"Failed to delete \"{file}\" on attempt {i}");
                }
            }
            Debug.Print($"Gave up attempted to delete \"{file}\" after reaching MAX_DELETE_RETRY ({MAX_DELETE_RETRY})");
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            Documentation.Contents.GetHashCode(); // touch
            // check startup commands
            if (e.Args.Length >= 2)
            {
                if (e.Args[0] == DeleteOldCommand)
                    handleDeleteCommand(e.Args[1]);
            }

            base.OnStartup(e);
        }
    }
}
