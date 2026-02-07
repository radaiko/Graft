using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Graft.VS2026Extension.Graft
{
    internal sealed class FileWatcher : IDisposable
    {
        private FileSystemWatcher? _watcher;
        private Timer? _debounceTimer;
        private readonly object _lock = new object();
        private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(300);

        public event EventHandler? Changed;

        public void Watch(string graftDir)
        {
            Stop();

            if (!Directory.Exists(graftDir))
                return;

            _watcher = new FileSystemWatcher(graftDir)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                             | NotifyFilters.LastWrite
                             | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };

            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            _watcher.Deleted += OnFileEvent;
            _watcher.Renamed += OnRenamedEvent;
            _watcher.Error += OnError;
        }

        public void Stop()
        {
            if (_watcher != null)
                _watcher.EnableRaisingEvents = false;

            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }

            if (_watcher != null)
            {
                _watcher.Dispose();
                _watcher = null;
            }
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            ScheduleNotification();
        }

        private void OnRenamedEvent(object sender, RenamedEventArgs e)
        {
            ScheduleNotification();
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            Debug.WriteLine($"Graft FileWatcher error: {e.GetException().Message}");
        }

        private void ScheduleNotification()
        {
            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(
                    _ => Changed?.Invoke(this, EventArgs.Empty),
                    null,
                    _debounceInterval,
                    Timeout.InfiniteTimeSpan);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
