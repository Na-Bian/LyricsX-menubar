using System.Runtime.InteropServices;

namespace LyricsX.Platform.Windows.Taskbar;

/// <summary>
/// Reads occupied taskbar rectangles through UI Automation COM interop on a
/// dedicated MTA worker thread. Stale-element failures retry once; anything
/// else yields an empty result so the caller applies fallback reservations.
/// </summary>
public sealed class TaskbarAutomationReader : ITaskbarAutomationApi, IDisposable
{
    private const int MinRectSizePx = 4;
    private const int MergeThresholdPx = 2;

    private readonly Worker worker;

    public TaskbarAutomationReader()
    {
        worker = new Worker();
    }

    public IReadOnlyList<PixelRect> GetVisibleInteractiveBounds(nint taskbarWindow)
    {
        if (taskbarWindow == nint.Zero)
        {
            return [];
        }

        return worker.Invoke(taskbarWindow);
    }

    public void Dispose() => worker.Dispose();

    private static IReadOnlyList<PixelRect> ReadFromHandle(nint taskbarWindow)
    {
        try
        {
            var automation = UIAutomationInterop.Create();
            if (automation.ElementFromHandle(taskbarWindow, out var taskbar) != 0)
            {
                return [];
            }

            _ = automation.CreateTrueCondition(out var condition);
            if (taskbar.FindAll(
                    UIAutomationInterop.TreeScope_Descendants,
                    condition,
                    out var elements) != 0)
            {
                return [];
            }

            return Collect(elements, taskbarWindow);
        }
        catch (COMException)
        {
            // stale tree or access denied: one retry with a fresh instance
            try
            {
                var automation = UIAutomationInterop.Create();
                if (automation.ElementFromHandle(taskbarWindow, out var taskbar) != 0 ||
                    taskbar.FindAll(
                        UIAutomationInterop.TreeScope_Descendants,
                        null!,
                        out var elements) != 0)
                {
                    return [];
                }

                return Collect(elements, taskbarWindow);
            }
            catch (Exception)
            {
                return [];
            }
        }
        catch (Exception)
        {
            // unexpected Explorer tree: caller falls back
            return [];
        }
    }

    private static IReadOnlyList<PixelRect> Collect(
        UIAutomationInterop.IUIAutomationElementArray elements,
        nint taskbarWindow)
    {
        if (elements.get_Length(out var count) != 0)
        {
            return [];
        }

        if (!Interop.NativeMethods.GetWindowRect(
                taskbarWindow, out var taskbarRect))
        {
            return [];
        }

        var rects = new List<PixelRect>();
        for (var i = 0; i < count; i++)
        {
            if (elements.GetElement(i, out var element) != 0)
            {
                continue;
            }

            try
            {
                if (element.get_CurrentControlType(out var controlType) != 0)
                {
                    continue;
                }

                if (controlType is not
                    (UIAutomationInterop.ControlType_Button or
                     UIAutomationInterop.ControlType_List or
                     UIAutomationInterop.ControlType_MenuItem or
                     UIAutomationInterop.ControlType_ToolBar or
                     UIAutomationInterop.ControlType_Custom))
                {
                    continue;
                }

                if (element.get_CurrentBoundingRectangle(out var raw) != 0)
                {
                    continue;
                }

                var rect = new PixelRect(raw.Left, raw.Top, raw.Width, raw.Height);
                if (rect.Width < MinRectSizePx || rect.Height < MinRectSizePx)
                {
                    continue;
                }

                if (rect.Right <= taskbarRect.Left || rect.X >= taskbarRect.Right ||
                    rect.Bottom <= taskbarRect.Top || rect.Y >= taskbarRect.Bottom)
                {
                    continue;
                }

                rects.Add(rect);
            }
            catch (COMException)
            {
                // stale mid-walk element: skip it
            }
        }

        return MergeNearDuplicates(rects);
    }

    private static IReadOnlyList<PixelRect> MergeNearDuplicates(List<PixelRect> rects)
    {
        var merged = new List<PixelRect>();
        foreach (var rect in rects.OrderBy(rect => rect.X).ThenBy(rect => rect.Y))
        {
            if (merged.Count > 0 &&
                Math.Abs(merged[^1].X - rect.X) <= MergeThresholdPx &&
                Math.Abs(merged[^1].Y - rect.Y) <= MergeThresholdPx &&
                Math.Abs(merged[^1].Width - rect.Width) <= MergeThresholdPx &&
                Math.Abs(merged[^1].Height - rect.Height) <= MergeThresholdPx)
            {
                continue;
            }

            merged.Add(rect);
        }

        return merged;
    }

    /// <summary>Single MTA worker thread serializing UIA COM calls.</summary>
    private sealed class Worker : IDisposable
    {
        private readonly Thread thread;
        private readonly BlockingQueue<Action> queue = new();
        private int disposed;

        public Worker()
        {
            thread = new Thread(RunLoop)
            {
                IsBackground = true,
                Name = "LyricsX-UIA"
            };
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        public IReadOnlyList<PixelRect> Invoke(nint taskbarWindow)
        {
            ObjectDisposedException.ThrowIf(disposed != 0, this);
            var completion = new TaskCompletionSource<IReadOnlyList<PixelRect>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            queue.Enqueue(() =>
            {
                try
                {
                    completion.SetResult(ReadFromHandle(taskbarWindow));
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            });
            return completion.Task.GetAwaiter().GetResult();
        }

        private void RunLoop()
        {
            foreach (var action in queue.Consume())
            {
                action();
            }
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref disposed, 1);
            queue.Complete();
        }
    }

    private sealed class BlockingQueue<T>
    {
        private readonly Queue<T> items = new();
        private readonly object gate = new();
        private bool completed;

        public void Enqueue(T item)
        {
            lock (gate)
            {
                items.Enqueue(item);
                Monitor.PulseAll(gate);
            }
        }

        public void Complete()
        {
            lock (gate)
            {
                completed = true;
                Monitor.PulseAll(gate);
            }
        }

        public IEnumerable<T> Consume()
        {
            while (true)
            {
                lock (gate)
                {
                    while (items.Count == 0 && !completed)
                    {
                        Monitor.Wait(gate);
                    }

                    if (items.Count == 0)
                    {
                        yield break;
                    }

                    yield return items.Dequeue();
                }
            }
        }
    }
}
