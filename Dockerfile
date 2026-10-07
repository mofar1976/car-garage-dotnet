FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/CarGarage.Domain/CarGarage.Domain.csproj src/CarGarage.Domain/
COPY src/CarGarage.Application/CarGarage.Application.csproj src/CarGarage.Application/
COPY src/CarGarage.Infrastructure/CarGarage.Infrastructure.csproj src/CarGarage.Infrastructure/
COPY src/CarGarage.Api/CarGarage.Api.csproj src/CarGarage.Api/
COPY tools/CarGarage.AdminCli/CarGarage.AdminCli.csproj tools/CarGarage.AdminCli/
RUN dotnet restore src/CarGarage.Api/CarGarage.Api.csproj
RUN dotnet restore tools/CarGarage.AdminCli/CarGarage.AdminCli.csproj
COPY src/ src/
COPY tools/ tools/
RUN dotnet publish src/CarGarage.Api/CarGarage.Api.csproj -c Release --no-restore -o /app/publish

RUN dotnet publish tools/CarGarage.AdminCli/CarGarage.AdminCli.csproj -c Release --no-restore -o /app/admin

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/admin ./admin
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CarGarage.Api.dll"]
