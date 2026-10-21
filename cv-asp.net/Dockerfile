# Build context: the repository root (see docker-compose.yml), so that cv-shared is reachable.

# Build: restores and publishes the web project in Release; a compile error (warnings are errors) fails the image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source/cv-asp.net

# The project files first, so that a code change does not repeat the restore.
COPY cv-asp.net/Directory.Build.props ./
COPY cv-asp.net/src/CvAspNet.Poker/CvAspNet.Poker.csproj src/CvAspNet.Poker/
COPY cv-asp.net/src/CvAspNet.Web/CvAspNet.Web.csproj src/CvAspNet.Web/
RUN dotnet restore src/CvAspNet.Web/CvAspNet.Web.csproj

# "run webcv.ps1" passes a new BUILD_ID every time, so the code below is compiled on every run even when it did not change.
ARG BUILD_ID=manual
RUN echo "build $BUILD_ID"
COPY cv-shared /source/cv-shared
COPY cv-asp.net/src/ src/
RUN dotnet publish src/CvAspNet.Web/CvAspNet.Web.csproj -c Release -o /app --no-restore

# Run: the ASP.NET Core runtime only, as the image's non-root user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID

# Urls replaces appsettings.json's localhost-only address; the image's own HTTP_PORTS is emptied so that
# the two do not compete.
ENV Urls=http://+:8080 \
    ASPNETCORE_HTTP_PORTS=
EXPOSE 8080
ENTRYPOINT ["dotnet", "CvAspNet.Web.dll"]
