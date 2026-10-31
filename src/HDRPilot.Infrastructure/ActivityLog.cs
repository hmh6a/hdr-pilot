using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using HDRPilot.Core;

namespace HDRPilot.Infrastructure;

public sealed class ActivityLog : ILoggerProvider
{
    public static string LogDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HDRPilot", "logs");
    private readonly ConcurrentQueue<ActivityEntry> recent = new();
    private readonly Channel<ActivityEntry> writes = Channel.CreateBounded<ActivityEntry>(new BoundedChannelOptions(512) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly Task writer;
    public event Action<ActivityEntry>? Added;
    public ActivityLog() => writer = Task.Run(WriteLoopAsync);
    public IReadOnlyList<ActivityEntry> Entries => recent.ToArray();
    public void Add(string message, string level = "Info")
    {
        var item = new ActivityEntry(DateTimeOffset.Now, level, message);
        recent.Enqueue(item); while (recent.Count > 300) recent.TryDequeue(out _);
        writes.Writer.TryWrite(item); Added?.Invoke(item);
    }
    private async Task WriteLoopAsync()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var path = Path.Combine(LogDirectory, "hdrpilot.log");
            await foreach (var entry in writes.Reader.ReadAllAsync())
            {
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
                {
                    for (var i = 3; i >= 1; i--)
                    {
                        var from = i == 1 ? path : path + "." + (i - 1);
                        if (File.Exists(from)) File.Move(from, path + "." + i, true);
                    }
                }
                await File.AppendAllTextAsync(path, $"{entry.Time:O} [{entry.Level}] {entry.Message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Logging must never stop HDR restoration. */ }
    }
    public ILogger CreateLogger(string categoryName) => new Logger(this);
    public void Dispose() { writes.Writer.TryComplete(); writer.GetAwaiter().GetResult(); }
    private sealed class Logger(ActivityLog owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (IsEnabled(level)) owner.Add(formatter(state, exception), level.ToString()); }
    }
}
