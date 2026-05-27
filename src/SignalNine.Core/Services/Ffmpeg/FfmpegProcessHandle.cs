using SignalNine.Core.Data.Ffmpeg;

namespace SignalNine.Core.Services.Ffmpeg;

public sealed class FfmpegProcessHandle : IAsyncDisposable
{
    private readonly Func<Task<bool>> _cancelAsync;

    public FfmpegProcessHandle(Guid id, Task<FfmpegProcessSnapshot> completion, Func<Task<bool>> cancelAsync)
    {
        Id = id;
        Completion = completion;
        _cancelAsync = cancelAsync;
    }

    public Guid Id { get; }
    public Task<FfmpegProcessSnapshot> Completion { get; }

    public async ValueTask DisposeAsync()
    {
        if (!Completion.IsCompleted)
        {
            await _cancelAsync().ConfigureAwait(false);
        }
    }
}
