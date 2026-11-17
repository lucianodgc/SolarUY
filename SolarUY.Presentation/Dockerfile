FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish SolarUY.Presentation/SolarUY.Presentation.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/dpkeys \
    && chown app:app /var/dpkeys \
    && chmod 700 /var/dpkeys
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=30s --retries=3 \
  CMD curl --fail --silent http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "SolarUY.Presentation.dll"]
