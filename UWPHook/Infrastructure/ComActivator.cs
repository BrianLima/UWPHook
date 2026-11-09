using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Serilog;
using UWPHook.Core.Launch;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// Starts an app through Windows' <c>IApplicationActivationManager</c>, the same way the Start menu does,
    /// passing Steam's extra launch options as the activation arguments.
    /// </summary>
    internal sealed class ComActivator : IAppActivator
    {
        public int Activate(LaunchRequest request)
        {
            Log.Verbose("Launch request => {Request}", request);

            try
            {
                var manager = (IApplicationActivationManager)new ApplicationActivationManager();
                manager.ActivateApplication(request.Aumid, request.ExtraArguments, ActivateOptions.None, out var processId);
                Log.Verbose("Activated {Aumid}, process id {ProcessId}", request.Aumid, processId);

                // Bring the launched app to the foreground: Steam in-home streaming needs it.
                ForegroundWindow.TryBring((int)processId);
                return (int)processId;
            }
            catch (Exception e)
            {
                Log.Error($"Activating {request.Aumid} failed with HRESULT 0x{e.HResult:X8} ({e.GetType().Name}): {e.Message}");
                throw new InvalidOperationException(
                    $"Error while trying to launch {request.Aumid}." + Environment.NewLine + e.Message, e);
            }
        }

        /// <summary>Creates Windows' activation manager.</summary>
        internal static IApplicationActivationManager CreateManager() => null!;

        private enum ActivateOptions
        {
            None = 0x00000000,
        }

        [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IApplicationActivationManager
        {
            // Activates the app for the "Launch" contract with the given arguments and returns its process id.
            IntPtr ActivateApplication([In] string appUserModelId, [In] string arguments, [In] ActivateOptions options, [Out] out uint processId);
            IntPtr ActivateForFile([In] string appUserModelId, [In] IntPtr itemArray, [In] string verb, [Out] out uint processId);
            IntPtr ActivateForProtocol([In] string appUserModelId, [In] IntPtr itemArray, [Out] out uint processId);
        }

        [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")] // ApplicationActivationManager
        private class ApplicationActivationManager
        {
        }
    }
}
