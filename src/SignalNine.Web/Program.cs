using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;
using Serilog;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Directories;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Types;
using SignalNine.Persistence.Entities.Users;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Services;
using SignalNine.Web.Endpoints;
using SignalNine.Web.Hubs;
using SignalNine.Web.Interfaces;
using SignalNine.Web.Services;
using SignalNine.Web.Services.Config;
using SignalNine.Web.Services.Pipeline;
using SignalNine.Core.Services.Ffmpeg;

const string SwaggerBearerSchemeName = "Bearer";
const string SwaggerDocumentName = "v1";
const string SwaggerTitle = "SignalNine API";
const string JobsHubPathPrefix = "/hubs/jobs";
const long ChannelLogoUploadMultipartBodyLengthLimit = 2 * 1024 * 1024;
const string ChannelLogosDirectoryName = "channel-logos";
const string ChannelLogosRequestPath = "/assets/channel-logos";
const string PreviewsDirectoryName = "previews";
const string PreviewsRequestPath = "/assets/previews";

var builder = WebApplication.CreateBuilder(args);

var rootDirectory = Environment.GetEnvironmentVariable("SIGNAL9_ROOT_DIRECTORY") ??
                    Path.Combine(Directory.GetCurrentDirectory(), "signal9");

var directoriesConfig = new DirectoriesConfig(rootDirectory, Enum.GetNames<DirectoryType>());
var configService = new ConfigService(directoriesConfig);
var signalNineConfig = await configService.LoadAsync();
var serilogService = new SerilogService(directoriesConfig);
var freeSqlFactory = new FreeSqlFactory(directoriesConfig);
var freeSql = freeSqlFactory.Create(signalNineConfig);

serilogService.Configure(signalNineConfig);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: true);

builder.Services.AddSingleton(directoriesConfig);
builder.Services.AddSingleton(signalNineConfig);
builder.Services.AddSingleton(signalNineConfig.Pipeline);
builder.Services.AddSingleton<IConfigService>(configService);
builder.Services.AddSingleton<ISerilogService>(serilogService);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IJellyfinConnectionService, JellyfinConnectionService>();
builder.Services.AddScoped<IJellyfinService, JellyfinService>();
builder.Services.AddDataProtection();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("jellyfin", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("jellyfin-stream", c => c.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddSingleton<IProcessLauncher, DefaultProcessLauncher>();
builder.Services.AddSingleton<IFfmpegPool>(sp =>
    new FfmpegPool(
        sp.GetRequiredService<IProcessLauncher>(),
        sp.GetRequiredService<SignalNineConfig>().FfmpegPool
    )
);
builder.Services.AddHostedService<FfmpegPoolBroadcastService>();
builder.Services.AddSingleton<ILocalLibraryWalker, LocalLibraryWalker>();
builder.Services.AddSingleton<IJobHandler, LibraryScanJobHandler>();
builder.Services.AddScoped<IMediaPathResolver, DefaultMediaPathResolver>();
builder.Services.AddScoped<IPipelineTask, TagMediaTask>();
builder.Services.AddScoped<IPipelineTask, JellyfinTagsTask>();
builder.Services.AddScoped<IPipelineTask, ProbeMediaTask>();
builder.Services.AddScoped<IPipelineTask, JellyfinPreviewTask>();
builder.Services.AddScoped<IPipelineTask, ExtractPreviewsTask>();
builder.Services.AddSingleton<IPipelineTaskConfigSchemaProvider, TagMediaTaskConfigSchemaProvider>();
builder.Services.AddSingleton<IPipelineTaskConfigSchemaProvider, JellyfinTagsTaskConfigSchemaProvider>();
builder.Services.AddSingleton<IPipelineTaskConfigSchemaProvider, ProbeMediaTaskConfigSchemaProvider>();
builder.Services.AddSingleton<IPipelineTaskConfigSchemaProvider, JellyfinPreviewTaskConfigSchemaProvider>();
builder.Services.AddSingleton<IPipelineTaskConfigSchemaProvider, ExtractPreviewsTaskConfigSchemaProvider>();
builder.Services.AddSingleton<ConfigSchemaService>();
builder.Services.AddSingleton<IJobHandler, MediaPipelineJobHandler>();
builder.Services.AddSingleton<SignalNine.Web.Services.Scheduling.SchedulePlannerService>();
builder.Services.AddSingleton<IJobHandler, SignalNine.Web.Services.Scheduling.SchedulePlanJobHandler>();
builder.Services.AddHostedService<SignalNine.Web.Services.Scheduling.SchedulePlannerCronService>();
builder.Services.AddSingleton<IJobNotificationPublisher, SignalRJobNotificationPublisher>();
builder.Services.AddSingleton<IJobManager, InMemoryJobManager>();
builder.Services.AddSingleton(freeSqlFactory);
builder.Services.AddSingleton(freeSql);
builder.Services.AddScoped(typeof(IDataAccess<>), typeof(FreeSqlDataAccess<>));
builder.Services.AddScoped<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>();
builder.Services.AddScoped<DefaultUserSeeder>();
builder.Services.AddHostedService<JobWorkerService>();
builder.Services.AddHostedService<LogsBroadcastService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(
           options =>
           {
               options.TokenValidationParameters = JwtTokenService.CreateTokenValidationParameters(signalNineConfig.Jwt);
               options.Events = new JwtBearerEvents
               {
                   OnMessageReceived = context =>
                   {
                       var accessToken = context.Request.Query["access_token"].ToString();
                       var path = context.HttpContext.Request.Path;

                       if (
                           !string.IsNullOrWhiteSpace(accessToken) &&
                           (path.StartsWithSegments(JobsHubPathPrefix) ||
                               (path.StartsWithSegments("/api/media") && path.Value!.EndsWith("/stream")))
                       )
                       {
                           context.Token = accessToken;
                       }

                       return Task.CompletedTask;
                   }
               };
           }
       );
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.Configure<FormOptions>(
    options =>
    {
        options.MultipartBodyLengthLimit = ChannelLogoUploadMultipartBodyLengthLimit;
    }
);
builder.Services.AddHealthChecks()
       .AddCheck<FreeSqlHealthCheck>("database");

builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(
    options =>
    {
        options.SwaggerDoc(
            SwaggerDocumentName,
            new OpenApiInfo
            {
                Title = SwaggerTitle,
                Version = SwaggerDocumentName
            }
        );
        options.AddSecurityDefinition(
            SwaggerBearerSchemeName,
            new OpenApiSecurityScheme
            {
                BearerFormat = "JWT",
                Description = "JWT Bearer token.",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Scheme = "bearer",
                Type = SecuritySchemeType.Http
            }
        );
        options.AddSecurityRequirement(
            document => new OpenApiSecurityRequirement
            {
                [
                    new OpenApiSecuritySchemeReference(SwaggerBearerSchemeName, document, null)
                ] = new List<string>()
            }
        );
    }
);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var defaultUserSeeder = scope.ServiceProvider.GetRequiredService<DefaultUserSeeder>();
    defaultUserSeeder.Seed();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(
        options =>
        {
            options.SwaggerEndpoint($"/swagger/{SwaggerDocumentName}/swagger.json", $"{SwaggerTitle} {SwaggerDocumentName}");
        }
    );
}

app.UseHttpsRedirection();
var channelLogosDirectory = Path.Combine(directoriesConfig[DirectoryType.Assets], ChannelLogosDirectoryName);
Directory.CreateDirectory(channelLogosDirectory);
app.UseStaticFiles(
    new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(channelLogosDirectory),
        RequestPath = ChannelLogosRequestPath
    }
);
var previewsDirectory = Path.Combine(directoriesConfig[DirectoryType.Assets], PreviewsDirectoryName);
Directory.CreateDirectory(previewsDirectory);
app.UseStaticFiles(
    new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(previewsDirectory),
        RequestPath = PreviewsRequestPath
    }
);
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks(
       "/live",
       new HealthCheckOptions
       {
           Predicate = _ => false
       }
   )
   .AllowAnonymous();
app.MapHealthChecks("/health")
   .AllowAnonymous();
app.MapAuthenticationEndpoints();
app.MapFilesystemEndpoints();
app.MapConfigEndpoints();
app.MapJellyfinEndpoints();
app.MapMediaLibraryEndpoints();
app.MapChannelEndpoints();
app.MapChannelMediaEndpoints();
app.MapTagEndpoints();
app.MapJobEndpoints();
app.MapHub<JobStatusHub>("/hubs/jobs/status")
   .RequireAuthorization();
app.MapHub<JobLogHub>("/hubs/jobs/logs")
   .RequireAuthorization();
app.MapHub<LogsHub>("/hubs/logs")
   .AllowAnonymous();
app.MapFfmpegPoolEndpoints();
app.MapHub<FfmpegPoolHub>("/hubs/ffmpeg").RequireAuthorization();
app.MapScheduleEndpoints();

app.Run();
