using System.Windows.Automation;

namespace LyricsX.Platform.Windows.Taskbar;

/// <summary>
/// Reads occupied taskbar rectangles through UI Automation on a dedicated
/// MTA worker thread (UIA calls are COM-backed and benefit from a stable
/// apartment). Stale-element failures retry once; anything else yields an
/// empty result so the caller applies fallback reservations.
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
            var root = AutomationElement.FromHandle(taskbarWindow);
            return Collect(root, taskbarWindow);
        }
        catch (ElementNotAvailableException)
        {
            // one retry with a fresh root
            try
            {
                var root = AutomationElement.FromHandle(taskbarWindow);
                return Collect(root, taskbarWindow);
            }
            catch (Exception)
            {
                return [];
            }
        }
        catch (Exception)
        {
            // access denied or unexpected Explorer tree: caller falls back
            return [];
        }
    }

    private static IReadOnlyList<PixelRect> Collect(AutomationElement root, nint taskbarWindow)
    {
        var taskbarRect = root.Current.BoundingRectangle;
        var condition = new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.List),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom));
        var elements = root.FindAll(TreeScope.Descendants, condition);

        var rects = new List<PixelRect>();
        foreach (AutomationElement element in elements)
        {
            try
            {
                var raw = element.Current.BoundingRectangle;
                if (raw.IsEmpty || raw.Width < MinRectSizePx || raw.Height < MinRectSizePx)
                {
                    continue;
                }

                var rect = new PixelRect(
                    (int)raw.Left, (int)raw.Top, (int)raw.Width, (int)raw.Height);
                if (rect.Right <= taskbarRect.Left || rect.X >= taskbarRect.Right ||
                    rect.Bottom <= taskbarRect.Top || rect.Y >= taskbarRect.Bottom)
                {
                    continue;
                }

                rects.Add(rect);
            }
            catch (ElementNotAvailableException)
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

    /// <summary>Single STA-free MTA worker thread serializing UIA calls.</summary>
    private sealed class Worker : IDisposable
    {
        private readonly Thread thread;
        private readonly BlockingQueue<Action> queue = new();
        private bool disposed;

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
            ObjectDisposedException.ThrowIf(disposed, this);
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
            disposed = true;
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
