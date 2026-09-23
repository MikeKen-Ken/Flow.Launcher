using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Flow.Launcher.Helper;
using NUnit.Framework;

namespace Flow.Launcher.Test;

[TestFixture]
[NonParallelizable]
[Apartment(ApartmentState.STA)]
public class QueryTextBoxClipboardHangTest
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    private static bool TryOpenClipboard()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
                return true;

            Thread.Sleep(50);
        }

        return false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [Test]
    public void CollapseToSingleLine_ReplacesLineBreaksWithSpaces()
    {
        Assert.That(QueryTextBoxClipboard.CollapseToSingleLine("a\r\nb\nc\rd"), Is.EqualTo("a b c d"));
    }

    [Test]
    [CancelAfter(5000)]
    public void PasteCanExecute_ReturnsWhileClipboardIsHeld()
    {
        var box = new TextBox();
        box.CommandBindings.Add(new CommandBinding(
            ApplicationCommands.Paste,
            (_, e) => e.Handled = true,
            (_, e) =>
            {
                e.CanExecute = QueryTextBoxClipboard.CanPaste(box);
                e.Handled = true;
            }));

        using var hold = ClipboardHold.Acquire();
        var elapsed = Time(() => ApplicationCommands.Paste.CanExecute(null, box));

        Assert.That(elapsed.TotalMilliseconds, Is.LessThan(400),
            $"Paste.CanExecute took {elapsed.TotalMilliseconds:0} ms");
        Assert.That(ApplicationCommands.Paste.CanExecute(null, box), Is.True);
    }

    [Test]
    [CancelAfter(5000)]
    public void Cut_RemovesSelectionWhileClipboardIsHeld()
    {
        var box = new TextBox { Text = "hello world" };
        box.Select(6, 5);
        string copied = null;
        box.CommandBindings.Add(new CommandBinding(
            ApplicationCommands.Cut,
            (_, e) =>
            {
                QueryTextBoxClipboard.Cut(box, text => copied = text);
                e.Handled = true;
            }));

        using var hold = ClipboardHold.Acquire();
        var elapsed = Time(() => ApplicationCommands.Cut.Execute(null, box));

        Assert.That(elapsed.TotalMilliseconds, Is.LessThan(400),
            $"Cut.Execute took {elapsed.TotalMilliseconds:0} ms");
        Assert.That(box.Text, Is.EqualTo("hello "));
        Assert.That(copied, Is.EqualTo("world"));
        Assert.That(box.CaretIndex, Is.EqualTo(6));
    }

    [Test]
    [CancelAfter(5000)]
    public void Paste_ReturnsWhileClipboardIsHeld()
    {
        var box = new TextBox { Text = "keep" };
        box.CommandBindings.Add(new CommandBinding(
            ApplicationCommands.Paste,
            (_, e) =>
            {
                QueryTextBoxClipboard.Paste(box, _ => { });
                e.Handled = true;
            },
            (_, e) =>
            {
                e.CanExecute = QueryTextBoxClipboard.CanPaste(box);
                e.Handled = true;
            }));

        using var hold = ClipboardHold.Acquire();
        var elapsed = Time(() => ApplicationCommands.Paste.Execute(null, box));

        Assert.That(elapsed.TotalMilliseconds, Is.LessThan(400),
            $"Paste.Execute took {elapsed.TotalMilliseconds:0} ms");
        Assert.That(box.Text, Is.EqualTo("keep"));
    }

    [Test]
    [CancelAfter(5000)]
    public void Paste_InsertsClipboardTextAsOneLine()
    {
        var previous = TryGetClipboard();
        try
        {
            SetClipboardText("a\r\nb");
            var box = new TextBox { Text = "xy" };
            box.CaretIndex = 1;

            QueryTextBoxClipboard.Paste(box, ex => throw ex);
            PumpUntil(box, () => box.Text != "xy");

            Assert.That(box.Text, Is.EqualTo("xa by"));
        }
        finally
        {
            RestoreClipboard(previous);
        }
    }

    [Test]
    [CancelAfter(5000)]
    public void Paste_InsertsCopiedFilePath()
    {
        var previous = TryGetClipboard();
        try
        {
            var files = new System.Collections.Specialized.StringCollection { @"C:\temp\a.txt" };
            var data = new DataObject();
            data.SetFileDropList(files);
            SetClipboardData(data);

            var box = new TextBox();
            QueryTextBoxClipboard.Paste(box, ex => throw ex);
            PumpUntil(box, () => box.Text.Length > 0);

            Assert.That(box.Text, Is.EqualTo(@"C:\temp\a.txt"));
        }
        finally
        {
            RestoreClipboard(previous);
        }
    }

    private static TimeSpan Time(Action action)
    {
        var started = Stopwatch.GetTimestamp();
        action();
        return Stopwatch.GetElapsedTime(started);
    }

    private static void PumpUntil(TextBox box, Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!done() && DateTime.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            box.Dispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
    }

    private static IDataObject TryGetClipboard()
    {
        try
        {
            return Clipboard.GetDataObject();
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private static void RestoreClipboard(IDataObject previous)
    {
        if (previous == null)
            return;

        try
        {
            Clipboard.SetDataObject(previous, copy: false);
        }
        catch (ExternalException)
        {
        }
    }

    private static void SetClipboardText(string text) => SetClipboardData(text);

    private static void SetClipboardData(object data)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: false);
                return;
            }
            catch (ExternalException) when (attempt < 10)
            {
                Thread.Sleep(50);
            }
        }
    }

    private sealed class ClipboardHold : IDisposable
    {
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _release;

        private ClipboardHold(Thread thread, ManualResetEventSlim release)
        {
            _thread = thread;
            _release = release;
        }

        public static ClipboardHold Acquire()
        {
            var locked = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            Exception error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    if (!TryOpenClipboard())
                    {
                        error = new InvalidOperationException($"OpenClipboard failed: {Marshal.GetLastWin32Error()}");
                    }
                    else
                    {
                        locked.Set();
                        release.Wait();
                        CloseClipboard();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                locked.Set();
            })
            {
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!locked.Wait(TimeSpan.FromSeconds(2)))
                throw new TimeoutException("Clipboard was not locked.");
            if (error != null)
                throw error;

            return new ClipboardHold(thread, release);
        }

        public void Dispose()
        {
            _release.Set();
            _thread.Join(TimeSpan.FromSeconds(2));
            _release.Dispose();
        }
    }
}
