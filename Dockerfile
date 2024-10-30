#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Event.API/Events.API.csproj", "Event.API/"]
COPY ["Caching/Caching.csproj", "Caching/"]
COPY ["ETMP.MessageQueue/ETMP.MessageQueue.csproj", "ETMP.MessageQueue/"]
COPY ["ETMP.Models/ETMP.Models.csproj", "ETMP.Models/"]
COPY ["Event.Domain/Events.Domain.csproj", "Event.Domain/"]
COPY ["Event.Infrastructure/Events.Infrastructure.csproj", "Event.Infrastructure/"]
RUN dotnet restore "./Event.API/Events.API.csproj"
COPY . .
WORKDIR "/src/Event.API"
RUN dotnet build "./Events.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Events.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Events.API.dll"]