using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher.Helper;

/// <summary>
/// Cut and paste for the query box.
/// WPF handles both by calling the clipboard on the UI thread, and retries for about a
/// second when another app has the clipboard open. The query box freezes for that wait,
/// and the cut or paste is then dropped. These helpers keep that wait off the UI thread.
/// </summary>
internal static class QueryTextBoxClipboard
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromMilliseconds(1500);

    internal static bool CanPaste(TextBox textBox) => !textBox.IsReadOnly;

    internal static void Cut(TextBox textBox, Action<string> copyText)
    {
        var selected = textBox.SelectedText;
        if (string.IsNullOrEmpty(selected))
            return;

        var caret = textBox.SelectionStart;
        textBox.SelectedText = string.Empty;
        textBox.CaretIndex = caret;
        copyText(selected);
    }

    internal static void Paste(TextBox textBox, Action<Exception> onError)
    {
        _ = PasteCoreAsync(textBox, onError);
    }

    internal static string CollapseToSingleLine(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
    }

    internal static async Task<string> ReadTextAsync(TimeSpan timeout)
    {
        var readTask = Win32Helper.StartSTATaskAsync(ReadText);
        var completed = await Task.WhenAny(readTask, Task.Delay(timeout)).ConfigureAwait(false);
        if (completed != readTask)
        {
            Observe(readTask);
            return null;
        }

        return await readTask.ConfigureAwait(false);
    }

    private static async Task PasteCoreAsync(TextBox textBox, Action<Exception> onError)
    {
        try
        {
            var text = await ReadTextAsync(ReadTimeout).ConfigureAwait(false);
            if (string.IsNullOrEmpty(text))
                return;

            var singleLine = CollapseToSingleLine(text);
            textBox.Dispatcher.Invoke(() => textBox.SelectedText = singleLine);
        }
        catch (Exception ex)
        {
            onError(ex);
        }
    }

    private static string ReadText()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data == null)
                return null;

            // autoConvert stays false. The true flag makes WPF ask the clipboard owner to
            // render every other format, which is what freezes paste.
            if (data.GetDataPresent(DataFormats.UnicodeText, autoConvert: false))
                return data.GetData(DataFormats.UnicodeText) as string;

            if (data.GetDataPresent(DataFormats.Text, autoConvert: false))
                return data.GetData(DataFormats.Text) as string;

            if (data.GetData(DataFormats.FileDrop, autoConvert: false) is string[] files && files.Length > 0)
                return string.Join(Environment.NewLine, files);

            return null;
        }
        catch (Exception ex) when (ex is COMException or ExternalException)
        {
            return null;
        }
    }

    private static void Observe(Task task)
    {
        _ = task.ContinueWith(
            static finished => _ = finished.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
