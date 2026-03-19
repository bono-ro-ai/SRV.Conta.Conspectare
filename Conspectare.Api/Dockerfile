FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Conspectare.Api/Conspectare.Api.csproj", "Conspectare.Api/"]
COPY ["Conspectare.Services/Conspectare.Services.csproj", "Conspectare.Services/"]
COPY ["Conspectare.Infrastructure/Conspectare.Infrastructure.csproj", "Conspectare.Infrastructure/"]
COPY ["Conspectare.Domain/Conspectare.Domain.csproj", "Conspectare.Domain/"]
RUN dotnet restore "Conspectare.Api/Conspectare.Api.csproj"
COPY . .
RUN dotnet publish "Conspectare.Api/Conspectare.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser
EXPOSE 5100
ENV ASPNETCORE_URLS=http://+:5100
ENTRYPOINT ["dotnet", "Conspectare.Api.dll"]
