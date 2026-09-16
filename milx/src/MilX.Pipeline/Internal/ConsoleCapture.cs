using System.Text;

namespace MilX.Pipeline.Internal;

/// <summary>
/// The upstream algorithms write diagnostics with Console.WriteLine. While a pipeline runs we
/// swap Console.Out for a writer that forwards complete lines to a callback (and still echoes to
/// the original writer), so the GUI log sees the upstream messages.
/// </summary>
internal sealed class ConsoleCapture : IDisposable
{
    private static readonly object Gate = new();
    private readonly TextWriter _original;
    private readonly TextWriter _originalError;
    private bool _disposed;

    private ConsoleCapture(TextWriter original, TextWriter originalError)
    {
        _original = original;
        _originalError = originalError;
    }

    public static ConsoleCapture Start(Action<string> onLine, bool echo)
    {
        lock (Gate)
        {
            var original = Console.Out;
            var originalError = Console.Error;
            var capture = new ConsoleCapture(original, originalError);
            Console.SetOut(TextWriter.Synchronized(new LineWriter(onLine, echo ? original : null)));
            Console.SetError(TextWriter.Synchronized(new LineWriter(onLine, echo ? originalError : null)));
            return capture;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (Gate)
        {
            Console.Out.Flush();
            Console.SetOut(_original);
            Console.SetError(_originalError);
        }
    }

    private sealed class LineWriter : TextWriter
    {
        private readonly Action<string> _onLine;
        private readonly TextWriter? _echo;
        private readonly StringBuilder _buffer = new();

        public LineWriter(Action<string> onLine, TextWriter? echo)
        {
            _onLine = onLine;
            _echo = echo;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            _echo?.Write(value);
            if (value == '\n')
            {
                Emit();
            }
            else if (value != '\r')
            {
                _buffer.Append(value);
            }
        }

        public override void Write(string? value)
        {
            if (value is null) return;
            foreach (var c in value) Write(c);
        }

        public override void Flush()
        {
            _echo?.Flush();
            if (_buffer.Length > 0) Emit();
        }

        private void Emit()
        {
            var line = _buffer.ToString();
            _buffer.Clear();
            if (line.Trim().Length > 0)
            {
                try { _onLine(line); } catch { /* never let a log sink break processing */ }
            }
        }
    }
}
