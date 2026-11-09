using System.Globalization;

namespace MilX.Cli;

/// <summary>
/// The arguments of one request, whether they came from a command line (<c>--mz 313.2 --tol 0.01</c>)
/// or from a request file (<c>"args": {"mz": "313.2", "tol": 0.01}</c>). Names are the contract's flag
/// names without dashes; a flag given twice keeps every value, which is how <c>--subtract</c> repeats.
/// </summary>
public sealed class Args
{
    private readonly Dictionary<string, List<string>> _values = new(StringComparer.Ordinal);

    public List<string> Positional { get; } = new();

    public static Args Parse(IReadOnlyList<string> argv)
    {
        var args = new Args();
        for (var i = 0; i < argv.Count; i++)
        {
            var a = argv[i];
            if (a == "--")
            {
                for (var j = i + 1; j < argv.Count; j++) args.Positional.Add(argv[j]);
                break;
            }
            if (a.StartsWith("--", StringComparison.Ordinal) && a.Length > 2)
            {
                var body = a[2..];
                var eq = body.IndexOf('=');
                if (eq > 0) { args.Add(body[..eq], body[(eq + 1)..]); continue; }
                // a value follows unless the next token is another flag (or there is none): then it is a switch
                if (i + 1 < argv.Count && !LooksLikeFlag(argv[i + 1])) { args.Add(body, argv[++i]); }
                else args.Add(body, "true");
                continue;
            }
            if (a.StartsWith('-') && a.Length > 1 && !char.IsDigit(a[1]) && a[1] != '.')
            {
                // the MS-DIAL console's short options (-i, -o, -m, -p) pass through under their letter
                var body = a[1..];
                if (i + 1 < argv.Count && !LooksLikeFlag(argv[i + 1])) args.Add(body, argv[++i]);
                else args.Add(body, "true");
                continue;
            }
            args.Positional.Add(a);
        }
        return args;
    }

    private static bool LooksLikeFlag(string token) => token.StartsWith('-') && token.Length > 1 && !char.IsDigit(token[1]) && token[1] != '.';

    public void Add(string name, string value)
    {
        if (!_values.TryGetValue(name, out var list)) _values[name] = list = new List<string>();
        list.Add(value);
    }

    public bool Has(string name) => _values.ContainsKey(name);
    public IReadOnlyList<string> All(string name) => _values.TryGetValue(name, out var list) ? list : Array.Empty<string>();
    public string? Get(string name) => _values.TryGetValue(name, out var list) ? list[^1] : null;
    public string Get(string name, string fallback) => Get(name) ?? fallback;
    public IEnumerable<string> Names => _values.Keys;

    public bool Flag(string name)
    {
        var v = Get(name);
        if (v is null) return false;
        return v is "true" or "1" or "yes" or "on";
    }

    public double Double(string name, double fallback)
    {
        var v = Get(name);
        if (v is null) return fallback;
        return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : throw new CliError(ErrorCode.BadArguments, $"--{name} needs a number, not '{v}'");
    }

    public double? OptionalDouble(string name) => Get(name) is null ? null : Double(name, 0);

    public int Int(string name, int fallback)
    {
        var v = Get(name);
        if (v is null) return fallback;
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : throw new CliError(ErrorCode.BadArguments, $"--{name} needs an integer, not '{v}'");
    }

    /// <summary>"A:B" as two numbers, or null when absent.</summary>
    public (double Lo, double Hi)? Range(string name)
    {
        var v = Get(name);
        if (string.IsNullOrWhiteSpace(v)) return null;
        var parts = v.Split(new[] { ':', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lo) && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var hi))
        {
            return lo <= hi ? (lo, hi) : (hi, lo);
        }
        throw new CliError(ErrorCode.BadArguments, $"--{name} needs a range written A:B, not '{v}'");
    }

    /// <summary>A copy with one value replaced, for the server's path rewriting.</summary>
    public Args With(string name, string value)
    {
        var copy = new Args();
        copy.Positional.AddRange(Positional);
        foreach (var (k, list) in _values) copy._values[k] = new List<string>(list);
        copy._values[name] = new List<string> { value };
        return copy;
    }
}

public enum ErrorCode
{
    BadArguments = 2,
    Unreadable = 3,
    NothingFound = 4,
    Internal = 5,
}

/// <summary>An error the contract knows how to print: a code, a message, an exit status.</summary>
public sealed class CliError : Exception
{
    public CliError(ErrorCode code, string message, string? name = null) : base(message)
    {
        Code = code;
        Name = name ?? code switch
        {
            ErrorCode.BadArguments => "bad_arguments",
            ErrorCode.Unreadable => "unreadable",
            ErrorCode.NothingFound => "nothing_found",
            _ => "internal",
        };
    }

    public ErrorCode Code { get; }
    /// <summary>The contract's error name: bad_arguments, unreadable, nothing_found, internal, or the server's not_allowed, path_outside_roots, timeout, interrupted.</summary>
    public string Name { get; }
    public int ExitCode => (int)Code;
}
