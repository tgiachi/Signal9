namespace SignalNine.Core.Data.Config;

/// <summary>
/// Outcome of validating a raw TOML configuration string.
/// </summary>
public class TomlValidationResult
{
    /// <summary>
    /// Whether the TOML text is a valid <c>SignalNineConfig</c>.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation error message when <see cref="IsValid" /> is false.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 1-based line number of the error when available.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// 1-based column number of the error when available.
    /// </summary>
    public int? Column { get; init; }

    /// <summary>
    /// Convenience factory for the success case.
    /// </summary>
    public static TomlValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Convenience factory for the failure case.
    /// </summary>
    public static TomlValidationResult Failure(string message, int? line = null, int? column = null)
        => new()
        {
            IsValid = false,
            Message = message,
            Line = line,
            Column = column
        };
}
