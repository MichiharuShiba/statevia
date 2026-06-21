FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# csproj だけ先にコピーして restore をキャッシュ効かせる
COPY api/Statevia.Core.Api/Statevia.Core.Api.csproj ./api/Statevia.Core.Api/
COPY engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj ./engine/Statevia.Core.Engine/
COPY shared/Statevia.Actions.Abstractions/Statevia.Actions.Abstractions.csproj ./shared/Statevia.Actions.Abstractions/
COPY shared/Statevia.Modules/Statevia.Modules.csproj ./shared/Statevia.Modules/
RUN dotnet restore ./api/Statevia.Core.Api/Statevia.Core.Api.csproj

# ソースをコピーして publish
COPY api/Statevia.Core.Api/ ./api/Statevia.Core.Api/
COPY engine/Statevia.Core.Engine/ ./engine/Statevia.Core.Engine/
COPY shared/Statevia.Actions.Abstractions/ ./shared/Statevia.Actions.Abstractions/
COPY shared/Statevia.Modules/ ./shared/Statevia.Modules/
RUN dotnet publish ./api/Statevia.Core.Api/Statevia.Core.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

ARG APP_UID=10001
ARG APP_GID=10001

RUN groupadd --gid "$APP_GID" statevia \
    && useradd --uid "$APP_UID" --gid "$APP_GID" --no-create-home --shell /usr/sbin/nologin statevia

WORKDIR /app
COPY --from=build /app/publish .
RUN chown -R statevia:statevia /app

USER statevia

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Statevia.Core.Api.dll"]

