namespace JobBoardScraper.Helper;

/// <summary>
/// Управление выводом в консоль и/или файл для конкретного процесса
/// </summary>
public sealed class ConsoleLogger : IDisposable
{
    private readonly string _processName;
    private readonly TextWriter _originalOut;
    private TextWriter? _fileWriter;
    private TextWriter? _currentWriter;
    private OutputMode _currentMode;
    private bool _disposed;

    public ConsoleLogger(string processName)
    {
        _processName = processName ?? throw new ArgumentNullException(nameof(processName));
        _originalOut = Console.Out;
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

        Console.WriteLine($"[ConsoleLogger] Установка режима вывода: {mode}, директория: {outputDirectory}");

        // Закрываем предыдущий файл, если был
        CloseFileWriter();

        _currentMode = mode;

        switch (mode)
        {
            case OutputMode.ConsoleOnly:
                Console.SetOut(_originalOut);
                CurrentOutputFile = null;
                break;

            case OutputMode.FileOnly:
                CurrentOutputFile = CreateOutputFile(outputDirectory);
                _fileWriter = File.CreateText(CurrentOutputFile);
                ((StreamWriter)_fileWriter).AutoFlush = true;
                Console.SetOut(_fileWriter);
                break;

            case OutputMode.Both:
                CurrentOutputFile = CreateOutputFile(outputDirectory);
                _fileWriter = File.CreateText(CurrentOutputFile);
                ((StreamWriter)_fileWriter).AutoFlush = true;
                _currentWriter = new DualWriter(_originalOut, _fileWriter);
                Console.SetOut(_currentWriter);
                break;
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
        Console.WriteLine($"[{_processName}] {message}");
    }

    /// <summary>
    /// Форматированный вывод с префиксом процесса и timestamp
    /// </summary>
    public void WriteLineWithTime(string message)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{_processName}] {message}");
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
        
        Console.WriteLine($"[ConsoleLogger] Лог-файл будет создан: {fullPath}");
        
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

        if (_currentWriter != null)
        {
            _currentWriter.Dispose();
            _currentWriter = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        CloseFileWriter();
        Console.SetOut(_originalOut);
        _disposed = true;
    }
}
