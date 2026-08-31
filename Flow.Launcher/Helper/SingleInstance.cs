using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32.SafeHandles;

// http://blogs.microsoft.co.il/arik/2010/05/28/wpf-single-instance-application/
// modified to allow single instace restart
namespace Flow.Launcher.Helper
{
    public interface ISingleInstanceApp
    {
        void OnSecondAppStarted(string[] args);
    }

    /// <summary>
    /// This class checks to make sure that only one instance of 
    /// this application is running at a time.
    /// </summary>
    /// <remarks>
    /// Note: this class should be used with some caution, because it does no
    /// security checking. For example, if one instance of an app that uses this class
    /// is running as Administrator, any other instance, even if it is not
    /// running as Administrator, can activate it with command line arguments.
    /// For most apps, this will not be much of an issue.
    /// </remarks>
    public static class SingleInstance<TApplication> where TApplication : Application, ISingleInstanceApp
    {
        #region Private Fields

        /// <summary>
        /// String delimiter used in channel names.
        /// </summary>
        private const string Delimiter = ":";

        /// <summary>
        /// Suffix to the channel name.
        /// </summary>
        private const string ChannelNameSuffix = "SingeInstanceIPCChannel";
        private const string InstanceMutexName = "Flow.Launcher_Unique_Application_Mutex";
        private const int MaxPayloadBytes = 64 * 1024;
        private const int ConnectTimeoutMs = 5000;
        private const int PayloadReadTimeoutMs = 5000;
        private const int ServerRetryDelayMs = 100;

        /// <summary>
        /// Application mutex.
        /// </summary>
        internal static Mutex SingleInstanceMutex { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if the instance of the application attempting to start is the first instance. 
        /// If not, activates the first instance.
        /// </summary>
        /// <returns>True if this is the first instance of the application.</returns>
        public static bool InitializeAsFirstInstance()
        {
            // Build unique application Id and the IPC channel name.
            string applicationIdentifier = InstanceMutexName + Environment.UserName;

            string channelName = string.Concat(applicationIdentifier, Delimiter, ChannelNameSuffix);

            // Create mutex based on unique application Id to check if this is the first instance of the application. 
            SingleInstanceMutex = new Mutex(true, applicationIdentifier, out var firstInstance);
            if (firstInstance)
            {
                _ = CreateRemoteServiceAsync(channelName);
                return true;
            }

            SignalFirstInstance(channelName, Environment.GetCommandLineArgs());
            return false;
        }

        /// <summary>
        /// Cleans up single-instance code, clearing shared resources, mutexes, etc.
        /// </summary>
        public static void Cleanup()
        {
            SingleInstanceMutex?.ReleaseMutex();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates a remote server pipe for communication. 
        /// Once receives signal from client, will activate first instance.
        /// </summary>
        /// <param name="channelName">Application's IPC channel name.</param>
        private static async Task CreateRemoteServiceAsync(string channelName)
        {
            while (true)
            {
                try
                {
                    await ServeConnectionAsync(channelName);
                }
                catch (Exception)
                {
                    // Keep the activation channel available after transient pipe or dispatcher failures.
                    await Task.Delay(ServerRetryDelayMs);
                }
            }
        }

        private static async Task ServeConnectionAsync(string channelName)
        {
            using NamedPipeServerStream pipeServer = new(
                channelName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await pipeServer.WaitForConnectionAsync();

            string[] args = [];
            try
            {
                using var timeout = new CancellationTokenSource(PayloadReadTimeoutMs);
                args = await ReadArgsAsync(pipeServer, timeout.Token);
            }
            catch (Exception)
            {
                // Still activate the first instance if the payload is invalid or incomplete.
            }

            var capturedArgs = args;
            var application = Application.Current;
            if (application != null)
                await application.Dispatcher.InvokeAsync(() => ActivateFirstInstance(capturedArgs));
        }

        /// <summary>
        /// Creates a client pipe and sends a signal to server to launch first instance
        /// </summary>
        /// <param name="channelName">Application's IPC channel name.</param>
        /// <param name="args">
        /// Command line arguments for the second instance, passed to the first instance to take appropriate action.
        /// </param>
        private static void SignalFirstInstance(string channelName, string[] args)
        {
            try
            {
                using NamedPipeClientStream pipeClient = new(".", channelName, PipeDirection.Out);
                pipeClient.Connect(ConnectTimeoutMs);

                // Explorer launched this process, so pass foreground rights only to the running instance.
                AllowPipeServerToSetForegroundWindow(pipeClient.SafePipeHandle);
                WriteArgs(pipeClient, args);
            }
            catch (TimeoutException)
            {
                // The first instance is not accepting activations; exit the second instance cleanly.
            }
            catch (IOException)
            {
                // The first instance closed or faulted its pipe; exit the second instance cleanly.
            }
            catch (UnauthorizedAccessException)
            {
                // The first instance is running in a context this process cannot signal.
            }
        }

        /// <summary>
        /// Activates the first instance of the application with arguments from a second instance.
        /// </summary>
        /// <param name="args">List of arguments to supply the first instance of the application.</param>
        private static void ActivateFirstInstance(string[] args)
        {
            if (Application.Current == null)
            {
                return;
            }

            ((TApplication)Application.Current).OnSecondAppStarted(args ?? []);
        }

        private static async Task<string[]> ReadArgsAsync(PipeStream pipe, CancellationToken cancellationToken)
        {
            var lengthBuffer = new byte[4];
            if (!await ReadExactAsync(pipe, lengthBuffer, cancellationToken))
                return [];

            var length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0 || length > MaxPayloadBytes)
                return [];

            var payload = new byte[length];
            if (!await ReadExactAsync(pipe, payload, cancellationToken))
                return [];

            return JsonSerializer.Deserialize<string[]>(payload) ?? [];
        }

        private static void WriteArgs(PipeStream pipe, string[] args)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(args ?? []);
            if (payload.Length > MaxPayloadBytes)
                payload = JsonSerializer.SerializeToUtf8Bytes(Array.Empty<string>());

            pipe.Write(BitConverter.GetBytes(payload.Length), 0, 4);
            pipe.Write(payload, 0, payload.Length);
            pipe.Flush();
        }

        private static async Task<bool> ReadExactAsync(
            PipeStream pipe,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await pipe.ReadAsync(
                    buffer.AsMemory(offset, buffer.Length - offset),
                    cancellationToken);
                if (read == 0)
                    return false;
                offset += read;
            }

            return true;
        }

        private static void AllowPipeServerToSetForegroundWindow(SafePipeHandle pipeHandle)
        {
            if (SingleInstanceNativeMethods.GetNamedPipeServerProcessId(pipeHandle, out var serverProcessId)
                && serverProcessId <= int.MaxValue)
            {
                _ = SingleInstanceNativeMethods.AllowSetForegroundWindow((int)serverProcessId);
            }
        }

        #endregion
    }

    /// <summary>
    /// Native APIs used by <see cref="SingleInstance{TApplication}"/>.
    /// Isolated because DllImport cannot be applied inside a generic type.
    /// </summary>
    internal static class SingleInstanceNativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetNamedPipeServerProcessId(
            SafePipeHandle pipe,
            out uint serverProcessId);
    }
}
