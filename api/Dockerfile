FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# csproj だけ先にコピーして restore をキャッシュ効かせる
COPY api/Statevia.Core.Api/Statevia.Core.Api.csproj ./api/Statevia.Core.Api/
COPY engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj ./engine/Statevia.Core.Engine/
RUN dotnet restore ./api/Statevia.Core.Api/Statevia.Core.Api.csproj

# ソースをコピーして publish
COPY api/Statevia.Core.Api/ ./api/Statevia.Core.Api/
COPY engine/Statevia.Core.Engine/ ./engine/Statevia.Core.Engine/
RUN dotnet publish ./api/Statevia.Core.Api/Statevia.Core.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Statevia.Core.Api.dll"]

