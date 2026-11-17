#!/bin/sh
set -eu
: "${SONAR_TOKEN:?Define SONAR_TOKEN fuera del repositorio}"
: "${SONAR_HOST_URL:?Define SONAR_HOST_URL}"
: "${PROJECT_KEY:?Define PROJECT_KEY}"
# Separar los binarios Linux del entorno de desarrollo y omitir evidencia anterior.
mkdir -p /analysis
tar -C /app --exclude='./.git' --exclude='./.env*' --exclude='./.agents' --exclude='./.aws' --exclude='./.codex' \
  --exclude='./.sonarqube' --exclude='./artifacts' --exclude='./tmp' \
  --exclude='bin' --exclude='obj' --exclude='node_modules' --exclude='TestResults' \
  --exclude='.runtime' --exclude='test-results' --exclude='playwright-report' -cf - . | tar -C /analysis -xf -
cd /analysis
dotnet sonarscanner begin /k:"$PROJECT_KEY" \
  /d:sonar.host.url="$SONAR_HOST_URL" /d:sonar.token="$SONAR_TOKEN" \
  /d:sonar.cs.opencover.reportsPaths="artifacts/calidad/cobertura/**/coverage.opencover.xml" \
  /d:sonar.qualitygate.wait=true /d:sonar.qualitygate.timeout=300
dotnet restore SolarUY.sln --disable-parallel
dotnet build SolarUY.sln --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false
dotnet SolarUY.InversorSimulador/bin/Debug/net8.0/SolarUY.InversorSimulador.dll >/tmp/simulador.log 2>&1 &
simulador=$!
trap 'kill "$simulador" 2>/dev/null || true' EXIT
dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj --no-build --no-restore \
  --logger trx --results-directory artifacts/calidad/cobertura
for proyecto in SolarUY.Domain.Tests SolarUY.Application.Tests SolarUY.Integration.Tests; do
  dotnet test "$proyecto/$proyecto.csproj" --no-build --no-restore -m:1 \
    --settings coverage.runsettings --collect:"XPlat Code Coverage" --logger trx --results-directory artifacts/calidad/cobertura
done
estado=0
dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN" || estado=$?
cp -R artifacts/calidad/cobertura /resultados/
if [ -f .sonarqube/out/.sonar/report-task.txt ]; then
  cp .sonarqube/out/.sonar/report-task.txt /resultados/report-task.txt
fi
exit "$estado"
