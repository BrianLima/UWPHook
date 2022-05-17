using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace UWPHook
{
    /// <summary>
    /// Functions to manage UWP apps
    /// </summary>
    static class AppManager
    {
        private static int runningProcessId;
        private static bool isLauncherProcess;

        /// <summary>
        /// Launch a UWP App using a ApplicationActivationManager and sets a internal id to launched proccess id
        /// </summary>
        /// <param name="aumid">The AUMID of the app to launch</param>
        public static void LaunchUWPApp(string[] args)
        {
            string aumid = args[1]; // We receive the args from Steam, 
                                    // 0 is application location, 
                                    // 1 is the aumid, the rest are extras

            var mgr = new ApplicationActivationManager();
            uint processId;

            string extra_args = String.Join(" ", args.Skip(2)
                                                 .Take(args.Length - 2)
                                                 .Select(eachElement => eachElement.Clone()
                                            ).ToArray());

            try
            {
                mgr.ActivateApplication(aumid, extra_args, ActivateOptions.None, out processId);
                runningProcessId = (int)processId;

                //if this is a launch aided by GameLaunchHelper, deal with child launch later
                var possibleLauncher = Process.GetProcessById(runningProcessId);
                if (possibleLauncher.ProcessName.Equals("gamelaunchhelper", StringComparison.OrdinalIgnoreCase))
                {
                    isLauncherProcess = true;
                }

                //Bring the launched app to the foreground, this fixes in-home streaming
                BringProcess();
            }
            catch (Exception e)
            {
                throw new Exception("Error while trying to launch your app." + Environment.NewLine + e.Message);
            }
        }

        /// <summary>
        /// Checks if the launched app is running
        /// </summary>
        /// <returns>True if the perviously launched app is running, false otherwise</returns>
        public static Boolean IsRunning()
        {
            //If 0, no app was launched most probably
            if (runningProcessId != 0)
            {
                try
                {
                    Process.GetProcessById(runningProcessId);
                    return true;
                }
                catch
                {
                    //the process we launched is no longer running. did it spawn any others?
                    if (isLauncherProcess)
                    {
                        var tree = GetProcessChildren();
                        if (tree.TryGetValue(runningProcessId, out var children))
                        {
                            //retarget to the first child we find
                            isLauncherProcess = false;

                            var newId = children.First();
                            Debug.WriteLine($"Launcher opened child process ({runningProcessId}->{newId}), using new process as target");
                            runningProcessId = newId;

                            //bring the "real" launched process
                            BringProcess();
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Find pid<->parent_pid relationships
        /// </summary>
        /// <returns>Map of processes to their child IDs. Any process in this object may have already terminated</returns>
        private static Dictionary<int, List<int>> GetProcessChildren()
        {
            var result = new Dictionary<int, List<int>>();

            using (var searcher = new ManagementObjectSearcher("select processid,parentprocessid from win32_process"))
            {
                foreach (var process in searcher.Get())
                {
                    int processId = Convert.ToInt32(process.Properties["processid"].Value);
                    int parentId = Convert.ToInt32(process.Properties["parentprocessid"].Value);

                    if (!result.ContainsKey(parentId))
                    {
                        result.Add(parentId, new List<int>());
                    }

                    result[parentId].Add(processId);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets a list of installed UWP Apps on the system, containing each app name + AUMID, separated by '|' 
        /// </summary>
        /// <returns>List of installed UWP Apps</returns>
        public static List<String> GetInstalledApps()
        {
            List<String> result = null;
            var assembly = Assembly.GetExecutingAssembly();
            //Load the powershell script to get installed apps
            var resourceName = "UWPHook.Resources.GetAUMIDScript.ps1";
            try
            {
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        //Every entry is listed separated by ;
                        result = ScriptManager.RunScript(reader.ReadToEnd()).Split(';').ToList<string>();
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error trying to get installed apps on your PC " + Environment.NewLine + e.Message, e.InnerException);
            }

            return result;
        }

        /// <summary>
        /// Try to convert an aumid into a human-readable app name
        /// </summary>
        /// <param name="appName">Application user model ID (aumid)</param>
        /// <param name="readableName">User-friendly app name</param>
        /// <returns>Whether this is a known app</returns>
        public static bool IsKnownApp(string appName, out string readableName)
        {
            string appsJson = File.ReadAllText(@"Resources\KnownApps.json");
            var apps = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(appsJson);

            foreach (var kvp in apps)
            {
                if (appName.StartsWith(kvp.Key + "_"))
                {
                    readableName = kvp.Value;
                    return true;
                }
            }

            readableName = null;
            return false;
        }

        [DllImport("user32.dll")]
        private static extern
        bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern
        bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern
        bool IsIconic(IntPtr hWnd);

        public static void BringProcess()
        {
            /*
            const int SW_HIDE = 0;
            const int SW_SHOWNORMAL = 1;
            const int SW_SHOWMINIMIZED = 2;
            const int SW_SHOWMAXIMIZED = 3;
            const int SW_SHOWNOACTIVATE = 4;
            const int SW_RESTORE = 9;
            const int SW_SHOWDEFAULT = 10;
            */

            var arrProcesses = Process.GetProcessById(runningProcessId);

            // get the window handle
            IntPtr hWnd = arrProcesses.MainWindowHandle;

            // if iconic, we need to restore the window
            if (IsIconic(hWnd))
            {
                ShowWindowAsync(hWnd, 3);
            }

            // bring it to the foreground
            SetForegroundWindow(hWnd);

        }

    }

    public enum ActivateOptions
    {
        None = 0x00000000,  // No flags set
        DesignMode = 0x00000001,  // The application is being activated for design mode, and thus will not be able to
                                  // to create an immersive window. Window creation must be done by design tools which
                                  // load the necessary components by communicating with a designer-specified service on
                                  // the site chain established on the activation manager.  The splash screen normally
                                  // shown when an application is activated will also not appear.  Most activations
                                  // will not use this flag.
        NoErrorUI = 0x00000002,  // Do not show an error dialog if the app fails to activate.
        NoSplashScreen = 0x00000004,  // Do not show the splash screen when activating the app.
    }

    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IApplicationActivationManager
    {
        // Activates the specified immersive application for the "Launch" contract, passing the provided arguments
        // string into the application.  Callers can obtain the process Id of the application instance fulfilling this contract.
        IntPtr ActivateApplication([In] String appUserModelId, [In] String arguments, [In] ActivateOptions options, [Out] out UInt32 processId);
        IntPtr ActivateForFile([In] String appUserModelId, [In] [MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] /*IShellItemArray* */ IShellItemArray itemArray, [In] String verb, [Out] out UInt32 processId);
        IntPtr ActivateForProtocol([In] String appUserModelId, [In] IntPtr /* IShellItemArray* */itemArray, [Out] out UInt32 processId);
    }

    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]//Application Activation Manager
    class ApplicationActivationManager : IApplicationActivationManager
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)/*, PreserveSig*/]
        public extern IntPtr ActivateApplication([In] String appUserModelId, [In] String arguments, [In] ActivateOptions options, [Out] out UInt32 processId);
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern IntPtr ActivateForFile([In] String appUserModelId, [In] [MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)]  /*IShellItemArray* */ IShellItemArray itemArray, [In] String verb, [Out] out UInt32 processId);
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern IntPtr ActivateForProtocol([In] String appUserModelId, [In] IntPtr /* IShellItemArray* */itemArray, [Out] out UInt32 processId);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    interface IShellItem
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
    interface IShellItemArray
    {
    }
}
