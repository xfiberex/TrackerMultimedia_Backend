FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["TrackerMultimedia_Backend.csproj", "./"]
RUN dotnet restore "TrackerMultimedia_Backend.csproj"

COPY . .
RUN dotnet publish "TrackerMultimedia_Backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

RUN useradd --no-create-home --shell /bin/false appuser
USER appuser

EXPOSE 10000

ENTRYPOINT ["sh", "-c", "dotnet TrackerMultimedia.dll --urls http://0.0.0.0:${PORT:-10000}"]