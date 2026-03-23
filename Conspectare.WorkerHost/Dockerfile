FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY ["Conspectare.WorkerHost/Conspectare.WorkerHost.csproj", "Conspectare.WorkerHost/"]
COPY ["Conspectare.Workers/Conspectare.Workers.csproj", "Conspectare.Workers/"]
COPY ["Conspectare.Services/Conspectare.Services.csproj", "Conspectare.Services/"]
COPY ["Conspectare.Infrastructure/Conspectare.Infrastructure.csproj", "Conspectare.Infrastructure/"]
COPY ["Conspectare.Domain/Conspectare.Domain.csproj", "Conspectare.Domain/"]
COPY ["Conspectare.Infrastructure.Llm/Conspectare.Infrastructure.Llm.csproj", "Conspectare.Infrastructure.Llm/"]
RUN dotnet restore "Conspectare.WorkerHost/Conspectare.WorkerHost.csproj"
ARG CACHE_BUST=1
COPY . .
RUN dotnet publish "Conspectare.WorkerHost/Conspectare.WorkerHost.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
WORKDIR /app
COPY --from=build /app/publish .
RUN adduser -D appuser && chown -R appuser /app
USER appuser
EXPOSE 5101
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:5101/health || exit 1
ENTRYPOINT ["dotnet", "Conspectare.WorkerHost.dll"]
