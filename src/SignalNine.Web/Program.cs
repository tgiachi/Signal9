using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Serilog;
using SignalNine.Core.Directories;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Types;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Services;
using SignalNine.Web.Endpoints;
using SignalNine.Web.Hubs;
using SignalNine.Web.Services;

const string SwaggerBearerSchemeName = "Bearer";
const string SwaggerDocumentName = "v1";
const string SwaggerTitle = "SignalNine API";

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
builder.Services.AddSingleton<IConfigService>(configService);
builder.Services.AddSingleton<ISerilogService>(serilogService);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IJobNotificationPublisher, SignalRJobNotificationPublisher>();
builder.Services.AddSingleton<IJobManager, InMemoryJobManager>();
builder.Services.AddSingleton(freeSqlFactory);
builder.Services.AddSingleton(freeSql);
builder.Services.AddScoped(typeof(IDataAccess<>), typeof(FreeSqlDataAccess<>));
builder.Services.AddHostedService<JobWorkerService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(
           options =>
           {
               options.TokenValidationParameters = JwtTokenService.CreateTokenValidationParameters(signalNineConfig.Jwt);
           }
       );
builder.Services.AddAuthorization();
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
app.MapJobEndpoints();
app.MapHub<JobStatusHub>("/hubs/jobs/status")
   .RequireAuthorization();
app.MapHub<JobLogHub>("/hubs/jobs/logs")
   .RequireAuthorization();

app.Run();
