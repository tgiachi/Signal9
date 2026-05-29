using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Web.Data.Schedule;

namespace SignalNine.Web.Endpoints;

public static class ScheduleEndpoints
{
    public static WebApplication MapScheduleEndpoints(this WebApplication app)
    {
        var perChannel = app.MapGroup("/api/channels/{channelId:guid}/schedule").RequireAuthorization();
        var perBlock = app.MapGroup("/api/schedule").RequireAuthorization();

        perChannel.MapGet("/blocks", ListBlocks);
        perChannel.MapPost("/blocks", CreateBlock);
        perBlock.MapPut("/blocks/{blockId:guid}", UpdateBlock);
        perBlock.MapDelete("/blocks/{blockId:guid}", DeleteBlock);

        perChannel.MapGet("/timeline", GetTimeline);
        perChannel.MapGet("/now", GetNow);
        perChannel.MapPost("/rebuild", Rebuild);

        return app;
    }

    private static Ok<IReadOnlyList<ScheduleBlockResponse>> ListBlocks(
        Guid channelId,
        IDataAccess<ScheduleBlockEntity> blocks)
    {
        var items = blocks.List().Where(b => b.ChannelId == channelId).Select(ToResponse).ToList();
        return TypedResults.Ok<IReadOnlyList<ScheduleBlockResponse>>(items);
    }

    private static Results<Created<ScheduleBlockResponse>, NotFound, BadRequest<string>> CreateBlock(
        Guid channelId,
        ScheduleBlockRequest body,
        IDataAccess<ChannelEntity> channels,
        IDataAccess<ScheduleBlockEntity> blocks)
    {
        if (channels.GetByKey(channelId) is null) return TypedResults.NotFound();
        if (body.DurationMinutes <= 0) return TypedResults.BadRequest("DurationMinutes must be > 0");
        if (body.StartTime.TotalSeconds < 0 || body.StartTime.TotalSeconds >= 86400)
        {
            return TypedResults.BadRequest("StartTime must be in [00:00, 24:00)");
        }
        if (body.StartTime.TotalMinutes + body.DurationMinutes > 1440)
        {
            return TypedResults.BadRequest("Block must not span midnight");
        }

        var entity = new ScheduleBlockEntity
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            Name = body.Name,
            DayOfWeek = body.DayOfWeek,
            StartTime = body.StartTime,
            DurationMinutes = body.DurationMinutes,
            RuleType = body.RuleType,
            PinnedChannelMediaId = body.PinnedChannelMediaId,
            SeriesName = body.SeriesName,
            TagFilterCsv = body.TagFilterCsv,
            TypeFilterCsv = body.TypeFilterCsv,
            IsActive = body.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        blocks.Insert(entity);
        return TypedResults.Created($"/api/schedule/blocks/{entity.Id}", ToResponse(entity));
    }

    private static Results<Ok<ScheduleBlockResponse>, NotFound, BadRequest<string>> UpdateBlock(
        Guid blockId,
        ScheduleBlockRequest body,
        IDataAccess<ScheduleBlockEntity> blocks)
    {
        var entity = blocks.GetByKey(blockId);
        if (entity is null) return TypedResults.NotFound();
        if (body.DurationMinutes <= 0) return TypedResults.BadRequest("DurationMinutes must be > 0");
        if (body.StartTime.TotalMinutes + body.DurationMinutes > 1440)
        {
            return TypedResults.BadRequest("Block must not span midnight");
        }

        entity.Name = body.Name;
        entity.DayOfWeek = body.DayOfWeek;
        entity.StartTime = body.StartTime;
        entity.DurationMinutes = body.DurationMinutes;
        entity.RuleType = body.RuleType;
        entity.PinnedChannelMediaId = body.PinnedChannelMediaId;
        entity.SeriesName = body.SeriesName;
        entity.TagFilterCsv = body.TagFilterCsv;
        entity.TypeFilterCsv = body.TypeFilterCsv;
        entity.IsActive = body.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        blocks.Update(entity);

        return TypedResults.Ok(ToResponse(entity));
    }

    private static Results<NoContent, NotFound> DeleteBlock(
        Guid blockId,
        IDataAccess<ScheduleBlockEntity> blocks)
    {
        var entity = blocks.GetByKey(blockId);
        if (entity is null) return TypedResults.NotFound();
        blocks.Delete(entity.Id);
        return TypedResults.NoContent();
    }

    private static Ok<ScheduleTimelineResponse> GetTimeline(
        Guid channelId,
        DateTime? from,
        DateTime? to,
        IDataAccess<ScheduledEntryEntity> entries,
        IDataAccess<ChannelMediaEntity> media)
    {
        var nowUtc = DateTime.UtcNow;
        var fromUtc = (from ?? nowUtc).ToUniversalTime();
        var toUtc = (to ?? fromUtc.AddHours(24)).ToUniversalTime();

        var mediaTitles = media.List().ToDictionary(m => m.Id, m => m.Title);
        var rows = entries.List()
            .Where(e =>
            {
                var startUtc = NormalizeToUtc(e.StartAt);
                return e.ChannelId == channelId
                    && startUtc < toUtc
                    && startUtc.AddSeconds(e.DurationSeconds) > fromUtc;
            })
            .OrderBy(e => NormalizeToUtc(e.StartAt))
            .Select(e => ToEntryResponse(e, mediaTitles))
            .ToList();

        return TypedResults.Ok(new ScheduleTimelineResponse(channelId, fromUtc, toUtc, rows));
    }

    private static Ok<ScheduleNowResponse> GetNow(
        Guid channelId,
        IDataAccess<ScheduledEntryEntity> entries,
        IDataAccess<ChannelMediaEntity> media)
    {
        var nowUtc = DateTime.UtcNow;
        var mediaTitles = media.List().ToDictionary(m => m.Id, m => m.Title);
        var ordered = entries.List()
            .Where(e => e.ChannelId == channelId)
            .OrderBy(e => NormalizeToUtc(e.StartAt))
            .ToList();

        ScheduledEntryEntity? current = null;
        ScheduledEntryEntity? next = null;
        for (var i = 0; i < ordered.Count; i++)
        {
            var e = ordered[i];
            var startUtc = NormalizeToUtc(e.StartAt);
            if (startUtc <= nowUtc && startUtc.AddSeconds(e.DurationSeconds) > nowUtc)
            {
                current = e;
                if (i + 1 < ordered.Count) next = ordered[i + 1];
                break;
            }
            if (startUtc > nowUtc)
            {
                next = e;
                break;
            }
        }

        var seconds = current is null ? 0 : (int)(nowUtc - NormalizeToUtc(current.StartAt)).TotalSeconds;
        return TypedResults.Ok(new ScheduleNowResponse(
            current is null ? null : ToEntryResponse(current, mediaTitles),
            next is null ? null : ToEntryResponse(next, mediaTitles),
            seconds,
            nowUtc));
    }

    /// <summary>
    /// FreeSql SQLite reads UTC datetimes back as local time (Kind=Unspecified, local value).
    /// ToUniversalTime on an Unspecified datetime treats it as local, converting it to UTC.
    /// </summary>
    private static DateTime NormalizeToUtc(DateTime dt)
    {
        return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
    }

    private static async Task<Results<Accepted, NotFound>> Rebuild(
        Guid channelId,
        ScheduleRebuildRequest body,
        IDataAccess<ChannelEntity> channels,
        SignalNine.Core.Interfaces.IJobManager jobs,
        CancellationToken cancellationToken)
    {
        if (channels.GetByKey(channelId) is null) return TypedResults.NotFound();

        var fromUtc = body.FromUtc ?? DateTime.UtcNow;
        var hours = body.HoursAhead.GetValueOrDefault(48);
        if (hours <= 0) hours = 48;

        var payload = System.Text.Json.JsonSerializer.Serialize(
            new SchedulePlanJobPayload(channelId, fromUtc, hours));

        await jobs.EnqueueAsync(
            new SignalNine.Core.Data.Jobs.EnqueueJobCommand
            {
                Type = SignalNine.Web.Services.Scheduling.SchedulePlanJobHandler.JobType,
                PayloadJson = payload
            },
            cancellationToken
        ).ConfigureAwait(false);

        return TypedResults.Accepted($"/api/channels/{channelId}/schedule/timeline");
    }

    private static ScheduleBlockResponse ToResponse(ScheduleBlockEntity e)
    {
        return new(
            e.Id, e.ChannelId, e.Name, e.DayOfWeek, e.StartTime, e.DurationMinutes,
            e.RuleType, e.PinnedChannelMediaId, e.SeriesName, e.SeriesCursorChannelMediaId,
            e.TagFilterCsv, e.TypeFilterCsv, e.IsActive, e.CreatedAt, e.UpdatedAt);
    }

    private static ScheduledEntryResponse ToEntryResponse(
        ScheduledEntryEntity e,
        IReadOnlyDictionary<Guid, string> mediaTitles)
    {
        var title = mediaTitles.TryGetValue(e.ChannelMediaId, out var t) ? t : "";
        return new(
            e.Id, e.SourceBlockId, e.StartAt, e.DurationSeconds, e.Kind, e.ChannelMediaId,
            title, e.MediaPartIndex, e.MediaPartCount, e.MediaOffsetSeconds);
    }
}
