# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet publish "src/Dibk.Ftpb.Api.Email.Web/Dibk.Ftpb.Api.Email.Web.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

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
