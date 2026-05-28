FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/SignalNine.Worker/SignalNine.Worker.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV SIGNAL9_REDIS_URL=redis://redis:6379
ENV SIGNAL9_MAX_CONCURRENT_JOBS=2
ENTRYPOINT ["dotnet", "SignalNine.Worker.dll"]
