# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the SDK pin and project files first so restore is cached independently of source changes.
COPY global.json ./
COPY ["src/Dibk.Ftpb.Api.Email.Web/Dibk.Ftpb.Api.Email.Web.csproj", "Dibk.Ftpb.Api.Email.Web/"]
COPY ["src/Dibk.Ftpb.Api.Email.Provider.GraphApi/Dibk.Ftpb.Api.Email.Provider.GraphApi.csproj", "Dibk.Ftpb.Api.Email.Provider.GraphApi/"]
COPY ["src/Dibk.Ftpb.Api.Email.Interfaces/Dibk.Ftpb.Api.Email.Interfaces.csproj", "Dibk.Ftpb.Api.Email.Interfaces/"]
COPY ["src/Dibk.Ftpb.Api.Email.Models/Dibk.Ftpb.Api.Email.Models.csproj", "Dibk.Ftpb.Api.Email.Models/"]
RUN dotnet restore "Dibk.Ftpb.Api.Email.Web/Dibk.Ftpb.Api.Email.Web.csproj"

# Copy the remaining source and publish the Web host (which pulls in the referenced projects).
COPY src/ ./
RUN dotnet publish "Dibk.Ftpb.Api.Email.Web/Dibk.Ftpb.Api.Email.Web.csproj" \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Listen on 8080 (the .NET 10 image default) over plain HTTP; TLS is terminated by App Service.
EXPOSE 8080
# Honour X-Forwarded-* from the App Service proxy so request scheme/IP are correct.
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

# Run as the non-root user provided by the base image.
USER $APP_UID

ENTRYPOINT ["dotnet", "Dibk.Ftpb.Api.Email.Web.dll"]
