using System.Globalization;

namespace Object1688.Shared.Logging;

/// <summary>
/// 日志文件写入器（架构 §7.1）：当日写入 <c>{prefix}.log</c>，
/// 日期变化或文件超过阈值时归档为 <c>{prefix}.YYYY-MM-DD.log</c>，
/// 并删除超出保留数量（默认 10 个）的最旧归档。线程安全。
/// 写入失败不抛出（LOG-E-4001 语义由调用方日志承载），轮转失败沿用当前文件（LOG-W-4002）。
/// </summary>
public sealed class LogFileWriter : IDisposable
{
    /// <summary>默认单文件大小轮转阈值（5MB）。</summary>
    public const long DefaultRotationThresholdBytes = 5 * 1024 * 1024;

    /// <summary>默认保留归档文件数量。</summary>
    public const int DefaultRetainedFileCount = 10;

    private readonly string _directory;
    private readonly string _prefix;
    private readonly long _rotationThresholdBytes;
    private readonly int _retainedFileCount;
    private readonly object _gate = new();
    private StreamWriter? _writer;
    private string? _activeDate;

    /// <summary>初始化写入器。</summary>
    /// <param name="directory">日志目录（不存在则创建）。</param>
    /// <param name="prefix">日志文件名前缀（如 "app" → app.log / app.YYYY-MM-DD.log）。</param>
    /// <param name="rotationThresholdBytes">单文件大小轮转阈值。</param>
    /// <param name="retainedFileCount">保留归档文件数量（不含当日 app.log）。</param>
    public LogFileWriter(
        string directory,
        string prefix = "app",
        long rotationThresholdBytes = DefaultRotationThresholdBytes,
        int retainedFileCount = DefaultRetainedFileCount)
    {
        _directory = directory;
        _prefix = prefix;
        _rotationThresholdBytes = rotationThresholdBytes;
        _retainedFileCount = retainedFileCount;
        Directory.CreateDirectory(directory);
        Open();
    }

    /// <summary>当前活动文件路径（app.log）。</summary>
    public string ActivePath => Path.Combine(_directory, _prefix + ".log");

    /// <summary>
    /// 追加一行日志：日期变化或超阈值时先轮转，再写入并落盘。
    /// </summary>
    /// <param name="line">完整日志行。</param>
    /// <param name="when">日志时间（默认当前 UTC；测试可传入固定值驱动轮转）。</param>
    public void Append(string line, DateTimeOffset? when = null)
    {
        lock (_gate)
        {
            if (_writer is null)
            {
                return;
            }

            var dateKey = (when ?? DateTimeOffset.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var needDateRotate = _activeDate is not null && _activeDate != dateKey;
            var needSizeRotate = _writer.BaseStream.Length >= _rotationThresholdBytes;
            if (needDateRotate || needSizeRotate)
            {
                // 归档名采用被轮转内容的日期（旧日期）；_activeDate 为空时（首条即超阈值）退回当前日期。
                Rotate(_activeDate ?? dateKey);
            }

            _activeDate = dateKey;
            _writer.WriteLine(line);
            _writer.Flush();
        }
    }

    /// <summary>强制刷新缓冲区。</summary>
    public void Flush()
    {
        lock (_gate)
        {
            _writer?.Flush();
        }
    }

    /// <summary>释放文件句柄。</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void Open()
    {
        try
        {
            // FileShare.ReadWrite|Delete：允许诊断读取与轮转 rename/清理删除，
            // 否则持有句柄将导致 File.Move/File.Delete 抛 IOException（LOG-W-4002 实因）。
            _writer = new StreamWriter(
                new FileStream(ActivePath, FileMode.Append, FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete))
            {
                AutoFlush = true,
            };
        }
        catch (IOException)
        {
            _writer = null; // 打开失败：本进程放弃落盘（LOG-E-4001 语义由调用方日志承载）
        }
    }

    private void Rotate(string dateKey)
    {
        try
        {
            _writer?.Dispose();
            _writer = null;

            // 目标归档名：app.YYYY-MM-DD.log；同日多次超限时递增 app.YYYY-MM-DD.1.log
            var target = ArchivePath(dateKey, 0);
            var version = 1;
            while (File.Exists(target))
            {
                target = ArchivePath(dateKey, version++);
            }

            File.Move(ActivePath, target);
            EnforceRetainedFileCount();
            Open();
        }
        catch (IOException)
        {
            // 轮转失败：继续使用当前文件（LOG-W-4002 语义）
        }
    }

    private string ArchivePath(string dateKey, int version)
        => Path.Combine(
            _directory,
            version == 0
                ? $"{_prefix}.{dateKey}.log"
                : $"{_prefix}.{dateKey}.{version}.log");

    private void EnforceRetainedFileCount()
    {
        try
        {
            var archives = Directory.GetFiles(_directory, $"{_prefix}.*.log")
                .OrderByDescending(File.GetLastWriteTime)
                .ToList();
            foreach (var stale in archives.Skip(_retainedFileCount))
            {
                File.Delete(stale);
            }
        }
        catch (IOException)
        {
            // 清理失败不影响写入（LOG-W-4002 语义）
        }
    }
}