FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY ./bin/Staging/net6.0/publish/ .
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true
ENV ASPNETCORE_ENVIRONMENT=Staging
ENTRYPOINT ["dotnet", "playground-check-service.dll"]
EXPOSE 80
