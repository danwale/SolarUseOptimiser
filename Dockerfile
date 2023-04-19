FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS build-env
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:6.0-bullseye-slim
WORKDIR /app
COPY --from=build-env /app/out ./

RUN useradd -ms /bin/bash moduleuser
RUN mkdir /etc/solaruseoptimiser && chown moduleuser /etc/solaruseoptimiser
COPY --from=build-env /app/out/appsettings.json /etc/solaruseoptimiser/appsettings.json
USER moduleuser

ENTRYPOINT ["dotnet", "SolarUseOptimiser.dll"]