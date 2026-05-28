using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Data.Jobs.Results;

public sealed record EmptyJobResult(string Type) : IJobResult;
