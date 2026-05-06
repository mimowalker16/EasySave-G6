FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "LogCentralizer.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV EASYSAVE_CENTRAL_LOG_DIR=/data
COPY --from=build /app/publish .
VOLUME ["/data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "LogCentralizer.dll"]
