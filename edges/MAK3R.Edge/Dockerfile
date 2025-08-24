FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 9080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files and restore dependencies
COPY ["edges/MAK3R.Edge/MAK3R.Edge.csproj", "edges/MAK3R.Edge/"]
COPY ["libs/MAK3R.Core/MAK3R.Core.csproj", "libs/MAK3R.Core/"]
COPY ["libs/MAK3R.Shared/MAK3R.Shared.csproj", "libs/MAK3R.Shared/"]
COPY ["mcps/MAK3R.Connectors.FANUC/MAK3R.Connectors.FANUC.csproj", "mcps/MAK3R.Connectors.FANUC/"]
COPY ["mcps/MAK3R.Connectors.Siemens/MAK3R.Connectors.Siemens.csproj", "mcps/MAK3R.Connectors.Siemens/"]
COPY ["mcps/MAK3R.Connectors.HAAS/MAK3R.Connectors.HAAS.csproj", "mcps/MAK3R.Connectors.HAAS/"]
COPY ["mcps/MAK3R.Connectors.Mazak/MAK3R.Connectors.Mazak.csproj", "mcps/MAK3R.Connectors.Mazak/"]

RUN dotnet restore "edges/MAK3R.Edge/MAK3R.Edge.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/edges/MAK3R.Edge"
RUN dotnet build "MAK3R.Edge.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MAK3R.Edge.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Install required packages for native dependencies
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Create required directories
RUN mkdir -p /app/data /app/logs /app/config

# Copy SQL schema and default configs
COPY ["edges/MAK3R.Edge/Persistence/SqliteSchema.sql", "/app/Persistence/"]

ENTRYPOINT ["dotnet", "MAK3R.Edge.dll"]