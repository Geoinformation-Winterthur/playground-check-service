FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY ./bin/Staging/net6.0/publish/ .
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true
ENV ASPNETCORE_ENVIRONMENT=Staging
COPY ./resolv.conf /etc/resolv.conf.override
CMD cp /etc/resolv.conf.override /etc/resolv.conf && dotnet playground-check-service.dll
EXPOSE 80
