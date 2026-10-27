using YTMusic.BLL.Models;

namespace YTMusic.BLL.Infrastructure;

internal static class DownloadTransferControl
{
    public static void WaitIfPaused(DownloadTaskInfo task, object syncRoot, CancellationToken cancellationToken)
    {
        while (true)
        {
            lock (syncRoot)
            {
                if (task.Status != DownloadStatus.Paused)
                {
                    return;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(150);
        }
    }

    public static IProgress<double> CreateProgressReporter(
        DownloadTaskInfo task,
        object syncRoot,
        CancellationToken cancellationToken,
        double threshold,
        Action notify)
    {
        return new InlineDownloadProgress(task, syncRoot, cancellationToken, threshold, notify);
    }

    /// <summary>
    /// 不使用 <see cref="Progress{T}"/>，避免在 UI 同步上下文上排队导致下载线程与界面互相等待。
    /// </summary>
    private sealed class InlineDownloadProgress : IProgress<double>
    {
        private readonly DownloadTaskInfo _task;
        private readonly object _syncRoot;
        private readonly CancellationToken _cancellationToken;
        private readonly double _threshold;
        private readonly Action _notify;

        public InlineDownloadProgress(
            DownloadTaskInfo task,
            object syncRoot,
            CancellationToken cancellationToken,
            double threshold,
            Action notify)
        {
            _task = task;
            _syncRoot = syncRoot;
            _cancellationToken = cancellationToken;
            _threshold = threshold;
            _notify = notify;
        }

        public void Report(double value)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            bool shouldNotify;
            lock (_syncRoot)
            {
                if (_task.Status == DownloadStatus.Paused)
                {
                    return;
                }

                var previous = _task.Progress;
                shouldNotify = Math.Abs(previous - value) >= _threshold || value >= 1.0 || (previous <= 0 && value > 0);
                _task.Progress = value;
            }

            if (shouldNotify)
            {
                _notify();
            }
        }
    }
}
