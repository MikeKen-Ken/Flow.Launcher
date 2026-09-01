using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Exception;
using Flow.Launcher.Infrastructure.Logger;
using NLog;

namespace Flow.Launcher.Helper;

public static class ErrorReporting
{
    private static void Report(Exception e, bool silent = false, [CallerMemberName] string methodName = "UnHandledException")
    {
        var logger = LogManager.GetLogger(methodName);
        logger.Fatal(ExceptionFormatter.FormatExcpetion(e));
        if (silent) return;

        // Workaround for issue https://github.com/Flow-Launcher/Flow.Launcher/issues/4016
        // The crash occurs in PresentationFramework.dll, not necessarily when the Runner UI is visible, originating from this line:
        // https://github.com/dotnet/wpf/blob/3439f20fb8c685af6d9247e8fd2978cac42e74ac/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Shell/WindowChromeWorker.cs#L1005
        // Many bug reports because users see the "Error report UI" after the crash with System.Runtime.InteropServices.COMException 0xD0000701 or 0x80263001.
        // However, displaying this "Error report UI" during WPF crashes, especially when DWM composition is changing, is not ideal; some users reported it hangs for up to a minute before the it appears.
        // This change modifies the behavior to log the exception instead of showing the "Error report UI".
        if (ExceptionHelper.IsRecoverableDwmCompositionException(e)) return;

        var reportWindow = new ReportWindow(e);
        reportWindow.Show();
    }

    public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // handle non-ui thread exceptions
        Report((Exception)e.ExceptionObject);
    }

    public static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (ExceptionHelper.IsRecoverableDwmCompositionException(e.Exception))
        {
            Log.Warn(nameof(ErrorReporting), $"Ignored recoverable DWM composition exception: {e.Exception.Message}");
            e.Handled = true;
            return;
        }

#if DEBUG
        // Keep non-DWM exceptions unhandled in Debug so the debugger still breaks.
        return;
#else
        Report(e.Exception);
        e.Handled = true;
#endif
    }

    public static void TaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        // Do not call Log.Exception here: in DEBUG it rethrows, and this callback
        // often runs during GC of faulted fire-and-forget tasks a few minutes later.
        Log.Error(nameof(ErrorReporting), $"Unobserved task exception occurred: {e.Exception}");
        e.SetObserved();
    }

    public static string RuntimeInfo()
    {
        var info =
            $"""

             Flow Launcher version: {Constant.Version}
             OS Version: {ExceptionFormatter.GetWindowsFullVersionFromRegistry()}
             IntPtr Length: {IntPtr.Size}
             x64: {Environment.Is64BitOperatingSystem}
             """;
        return info;
    }

    public static string DependenciesInfo()
    {
        var info = $"""

                    Python Path: {Constant.PythonPath}
                    Node Path: {Constant.NodePath}
                    """;
        return info;
    }
}
