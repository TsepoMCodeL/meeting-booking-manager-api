FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MeetingManagerAPI.csproj ./
RUN dotnet restore MeetingManagerAPI.csproj

COPY . ./
RUN dotnet publish MeetingManagerAPI.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
RUN mkdir -p /data && chown "$APP_UID:$APP_UID" /data

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
VOLUME ["/data"]
USER $APP_UID

ENTRYPOINT ["dotnet", "MeetingManagerAPI.dll"]
