FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY ./bin/ .
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true
ENV ASPNETCORE_ENVIRONMENT=Staging
ENTRYPOINT ["dotnet", "playground-check-service.dll"]
EXPOSE 8080