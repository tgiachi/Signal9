namespace SignalNine.Core.Data.Jellyfin;

public record JellyfinConnectionStatus(bool IsConfigured, string? BaseUrl, DateTime? LastVerifiedAt);
