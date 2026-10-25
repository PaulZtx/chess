FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BackChess/BackChess.csproj", "BackChess/"]
COPY ["ChessInfrastructure/ChessInfrastructure.csproj", "ChessInfrastructure/"]
RUN dotnet restore "BackChess/BackChess.csproj"
COPY . .
WORKDIR "/src/BackChess"
RUN dotnet build "./BackChess.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BackChess.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BackChess.dll"]
