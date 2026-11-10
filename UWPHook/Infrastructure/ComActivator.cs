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
                var manager = CreateManager();
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

        private const uint ClsctxLocalServer = 0x4;

        private static readonly Guid ApplicationActivationManagerClsid = new("45BA127D-10A8-46EA-8AB7-56EA9078943C");

        /// <summary>
        /// Creates Windows' activation manager out of process (<c>CLSCTX_LOCAL_SERVER</c>), as Microsoft documents
        /// for a process that only starts to launch an app.
        /// </summary>
        /// <exception cref="COMException">Windows could not create it; the HRESULT is the exception's.</exception>
        internal static IApplicationActivationManager CreateManager()
        {
            var clsid = ApplicationActivationManagerClsid;
            var iid = typeof(IApplicationActivationManager).GUID;
            Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid, IntPtr.Zero, ClsctxLocalServer, ref iid, out var manager));
            return (IApplicationActivationManager)manager;
        }

        [DllImport("ole32.dll", ExactSpelling = true)]
        private static extern int CoCreateInstance(
            ref Guid classId, IntPtr outer, uint context, ref Guid interfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object instance);

        internal enum ActivateOptions
        {
            None = 0x00000000,
        }

        /// <remarks>
        /// The methods return an HRESULT natively. Declared <c>void</c> without <c>[PreserveSig]</c>, the runtime turns a
        /// failure HRESULT into an exception, and the managed signature matches the native one (a non-void return would
        /// be marshalled as an extra out argument the native method doesn't have).
        /// </remarks>
        [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IApplicationActivationManager
        {
            // Activates the app for the "Launch" contract with the given arguments and returns its process id.
            void ActivateApplication([In] string appUserModelId, [In] string arguments, [In] ActivateOptions options, [Out] out uint processId);
            void ActivateForFile([In] string appUserModelId, [In] IntPtr itemArray, [In] string verb, [Out] out uint processId);
            void ActivateForProtocol([In] string appUserModelId, [In] IntPtr itemArray, [Out] out uint processId);
        }
    }
}
