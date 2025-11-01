namespace JobBoardScraper.Helper.ConsoleHelper;

/// <summary>
/// Режим вывода логов
/// </summary>
public enum OutputMode
{
    ConsoleOnly,
    FileOnly,
    Both
}

/// <summary>
/// Управление выводом в консоль и/или файл для конкретного процесса
/// </summary>
public sealed class ConsoleLogger : IDisposable
{
    private readonly string _processName;
    private readonly object _lock = new object();
    private StreamWriter? _fileWriter;
    private OutputMode _currentMode;
    private bool _disposed;

    public ConsoleLogger(string processName)
    {
        _processName = processName ?? throw new ArgumentNullException(nameof(processName));
        _currentMode = OutputMode.ConsoleOnly;
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

            // Закрываем предыдущий файл, если был
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

    /// <summary>
    /// Вернуть вывод в консоль
    /// </summary>
    public void RestoreConsole()
    {
        SetOutputMode(OutputMode.ConsoleOnly);
    }

    /// <summary>
    /// Форматированный вывод с префиксом процесса
    /// </summary>
    public void WriteLine(string message)
    {
        var formattedMessage = $"[{_processName}] {message}";
        
        lock (_lock)
        {
            if (_currentMode == OutputMode.ConsoleOnly || _currentMode == OutputMode.Both)
            {
                Console.WriteLine(formattedMessage);
            }

            if ((_currentMode == OutputMode.FileOnly || _currentMode == OutputMode.Both) && _fileWriter != null)
            {
                _fileWriter.WriteLine(formattedMessage);
            }
        }
    }

    /// <summary>
    /// Форматированный вывод с префиксом процесса и timestamp
    /// </summary>
    public void WriteLineWithTime(string message)
    {
        var formattedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{_processName}] {message}";
        
        lock (_lock)
        {
            if (_currentMode == OutputMode.ConsoleOnly || _currentMode == OutputMode.Both)
            {
                Console.WriteLine(formattedMessage);
            }

            if ((_currentMode == OutputMode.FileOnly || _currentMode == OutputMode.Both) && _fileWriter != null)
            {
                _fileWriter.WriteLine(formattedMessage);
            }
        }
    }

    private string CreateOutputFile(string outputDirectory)
    {
        // Получаем абсолютный путь
        var absoluteDir = Path.GetFullPath(outputDirectory);
        
        if (!Directory.Exists(absoluteDir))
        {
            Directory.CreateDirectory(absoluteDir);
            Console.WriteLine($"[ConsoleLogger] Создана директория для логов: {absoluteDir}");
        }

        var fileName = $"{_processName}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        var fullPath = Path.Combine(absoluteDir, fileName);
        
        return fullPath;
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
        if (_disposed)
            return;

        lock (_lock)
        {
            CloseFileWriter();
            _disposed = true;
        }
    }
}
