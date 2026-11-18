FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish SolarUY.InversorSimulador/SolarUY.InversorSimulador.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .
USER app
ENV InversorTcp__Host=0.0.0.0
EXPOSE 5055
ENTRYPOINT ["dotnet", "SolarUY.InversorSimulador.dll"]
