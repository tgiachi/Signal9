using SignalNine.Core.Data.Jobs;
using SignalNine.Persistence.Entities.Channels;

namespace SignalNine.Web.Data.Pipeline;

public record PipelineContext(
    ChannelMediaEntity Media,
    MediaLibraryEntity Library,
    string ResolvedPath,
    JobExecutionContext Job
);
