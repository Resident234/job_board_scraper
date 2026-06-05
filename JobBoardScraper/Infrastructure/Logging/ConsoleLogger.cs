using System.Text;

namespace JobBoardScraper.Infrastructure.Logging;

/// <summary>
/// Управление выводом в консоль и/или файл для конкретного процесса
/// </summary>
public sealed class ConsoleLogger : IDisposable
{
    private readonly string _processName;
    private readonly string _displayName;
    private readonly object _lock = new object();
    private StreamWriter? _fileWriter;
    private OutputMode _currentMode;
    private bool _disposed;

    public ConsoleLogger(string processName)
    {
        _processName = processName ?? throw new ArgumentNullException(nameof(processName));
        _displayName = FormatClassName(_processName);
        _currentMode = OutputMode.ConsoleOnly;
    }

    public static ConsoleLogger CreateForClass<T>()
    {
        return new ConsoleLogger(typeof(T).Name);
    }

    public static string FormatClassName(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return string.Empty;

        var result = new StringBuilder(className.Length + 8);

        for (var i = 0; i < className.Length; i++)
        {
            var current = className[i];
            var previous = i > 0 ? className[i - 1] : '\0';
            var next = i + 1 < className.Length ? className[i + 1] : '\0';

            if (i > 0
                && char.IsUpper(current)
                && !char.IsWhiteSpace(previous)
                && (!char.IsUpper(previous) || (next != '\0' && char.IsLower(next))))
            {
                result.Append(' ');
            }

            result.Append(current);
        }

        return result.ToString();
    }

    public OutputMode CurrentMode => _currentMode;
    public string? CurrentOutputFile { get; private set; }

    /// <summary>
    /// Установить режим вывода
    /// </summary>
    public void SetOutputMode(OutputMode mode, string? outputDirectory = null)
    {
        outputDirectory ??= AppConfig.LoggingOutputDirectory;
        SetOutputModeInternal(mode, outputDirectory);
    }

    private void SetOutputModeInternal(OutputMode mode, string outputDirectory)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ConsoleLogger));

        lock (_lock)
        {
            Console.WriteLine($"[ConsoleLogger] Установка режима вывода для {_processName}: {mode}, директория: {outputDirectory}");

            CloseFileWriter();
            _currentMode = mode;

            if (mode == OutputMode.FileOnly || mode == OutputMode.Both)
            {
                CurrentOutputFile = CreateOutputFile(outputDirectory);
                _fileWriter = new StreamWriter(CurrentOutputFile, append: false)
                {
                    AutoFlush = true
                };
                Console.WriteLine($"[ConsoleLogger] Создан лог-файл для {_processName}: {CurrentOutputFile}");
            }
            else
            {
                CurrentOutputFile = null;
            }
        }
    }

    public void RestoreConsole() => SetOutputMode(OutputMode.ConsoleOnly);

    public void WriteLine(string message)
    {
        var formattedMessage = $"[{_displayName}] {message}";
        
        lock (_lock)
        {
            if (_currentMode == OutputMode.ConsoleOnly || _currentMode == OutputMode.Both)
                Console.WriteLine(formattedMessage);

            if ((_currentMode == OutputMode.FileOnly || _currentMode == OutputMode.Both) && _fileWriter != null)
                _fileWriter.WriteLine(formattedMessage);
        }
    }

    public void WriteLineWithTime(string message)
    {
        var formattedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{_displayName}] {message}";
        
        lock (_lock)
        {
            if (_currentMode == OutputMode.ConsoleOnly || _currentMode == OutputMode.Both)
                Console.WriteLine(formattedMessage);

            if ((_currentMode == OutputMode.FileOnly || _currentMode == OutputMode.Both) && _fileWriter != null)
                _fileWriter.WriteLine(formattedMessage);
        }
    }

    private string CreateOutputFile(string outputDirectory)
    {
        var absoluteDir = Path.GetFullPath(outputDirectory);
        
        if (!Directory.Exists(absoluteDir))
        {
            Directory.CreateDirectory(absoluteDir);
            Console.WriteLine($"[ConsoleLogger] Создана директория для логов: {absoluteDir}");
        }

        var fileName = $"{_processName}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        return Path.Combine(absoluteDir, fileName);
    }

    private void CloseFileWriter()
    {
        if (_fileWriter != null)
        {
            _fileWriter.Flush();
            _fileWriter.Dispose();
            _fileWriter = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_lock)
        {
            CloseFileWriter();
            _disposed = true;
        }
    }
}
