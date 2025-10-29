using System.Text;

namespace JobBoardScraper.Helper;

/// <summary>
/// TextWriter, который пишет одновременно в консоль и файл
/// </summary>
public sealed class DualWriter : TextWriter
{
    private readonly TextWriter _consoleWriter;
    private readonly TextWriter _fileWriter;

    public DualWriter(TextWriter consoleWriter, TextWriter fileWriter)
    {
        _consoleWriter = consoleWriter ?? throw new ArgumentNullException(nameof(consoleWriter));
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public override Encoding Encoding => _consoleWriter.Encoding;

    public override void Write(char value)
    {
        _consoleWriter.Write(value);
        _fileWriter.Write(value);
    }

    public override void Write(string? value)
    {
        _consoleWriter.Write(value);
        _fileWriter.Write(value);
    }

    public override void WriteLine(string? value)
    {
        _consoleWriter.WriteLine(value);
        _fileWriter.WriteLine(value);
    }

    public override void Flush()
    {
        _consoleWriter.Flush();
        _fileWriter.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fileWriter?.Dispose();
        }
        base.Dispose(disposing);
    }
}
