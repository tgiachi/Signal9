using StackExchange.Redis;
using Testcontainers.Redis;

namespace SignalNine.Tests.Support;

/// <summary>
/// xUnit collection fixture that spins up a shared Redis 7 container for all tests in the
/// "Redis" collection. Container is started once and torn down when the test run finishes.
/// </summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    private RedisContainer? _container;

    public IConnectionMultiplexer Connection { get; private set; } = default!;
    public string ConnectionString { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _container = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        Connection = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        Connection?.Dispose();
        if (_container is not null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class RedisCollection : ICollectionFixture<RedisContainerFixture>
{
    public const string Name = "Redis";
}
