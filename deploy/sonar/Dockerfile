FROM mcr.microsoft.com/dotnet/sdk:8.0
RUN apt-get update && apt-get install -y --no-install-recommends default-jre && rm -rf /var/lib/apt/lists/*
RUN dotnet tool install --tool-path /opt/sonar dotnet-sonarscanner --version 12.0.0 \
    && mkdir -p /analysis /resultados /home/app/.nuget/packages \
    && chown -R app:app /analysis /resultados /home/app
ENV PATH="$PATH:/opt/sonar"
ENV DOTNET_CLI_HOME=/home/app
ENV NUGET_PACKAGES=/home/app/.nuget/packages
COPY analizar.sh /opt/sonar/analizar.sh
RUN sed -i 's/\r$//' /opt/sonar/analizar.sh
USER app
WORKDIR /analysis
