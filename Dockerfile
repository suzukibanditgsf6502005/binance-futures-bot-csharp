# syntax=docker/dockerfile:1
# Multi-stage build for Binance Futures Bot

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY BinanceBot.csproj ./
RUN dotnet restore

# copy source and publish
COPY src ./src
RUN dotnet publish BinanceBot.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# logs are written to /app/logs; mount this as a volume
VOLUME ["/app/logs"]

ENTRYPOINT ["dotnet", "BinanceBot.dll"]
