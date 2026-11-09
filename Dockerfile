FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./src/
RUN dotnet publish src/ThreeDManager.Web/ThreeDManager.Web.csproj \
    --configuration Release --output /out/web /p:UseAppHost=false

# Ferramenta restrita ao alvo opcional de migração; não entra na imagem web.
FROM build AS migrate
RUN dotnet tool install dotnet-ef --version 10.0.8 --tool-path /tools \
    && mkdir -p /usr/share/ThreeDManager/keys \
    && chown -R app:app /src /usr/share/ThreeDManager
ENV ASPNETCORE_ENVIRONMENT=Production
USER $APP_UID
ENTRYPOINT ["/tools/dotnet-ef", "database", "update", "--project", "src/ThreeDManager.Infrastructure/ThreeDmanager.Infrastructure.csproj", "--startup-project", "src/ThreeDManager.Web/ThreeDManager.Web.csproj", "--configuration", "Release", "--no-build"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS web
WORKDIR /app
# Program.cs persiste DataProtection em CommonApplicationData (Linux: /usr/share).
RUN mkdir -p /usr/share/ThreeDManager/keys \
    && chown -R app:app /usr/share/ThreeDManager
COPY --from=build /out/web/ ./
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ThreeDManager.Web.dll"]
