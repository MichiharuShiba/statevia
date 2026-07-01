FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY service/action-host/Statevia.Service.ActionHost/Statevia.Service.ActionHost.csproj ./service/action-host/Statevia.Service.ActionHost/
COPY core/engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj ./core/engine/Statevia.Core.Engine/
COPY shared/Statevia.Actions.Abstractions/Statevia.Actions.Abstractions.csproj ./shared/Statevia.Actions.Abstractions/
COPY shared/Statevia.Actions.Grpc/Statevia.Actions.Grpc.csproj ./shared/Statevia.Actions.Grpc/
COPY shared/Statevia.Modules/Statevia.Modules.csproj ./shared/Statevia.Modules/
RUN dotnet restore ./service/action-host/Statevia.Service.ActionHost/Statevia.Service.ActionHost.csproj

COPY service/action-host/Statevia.Service.ActionHost/ ./service/action-host/Statevia.Service.ActionHost/
COPY core/engine/Statevia.Core.Engine/ ./core/engine/Statevia.Core.Engine/
COPY shared/Statevia.Actions.Abstractions/ ./shared/Statevia.Actions.Abstractions/
COPY shared/Statevia.Actions.Grpc/ ./shared/Statevia.Actions.Grpc/
COPY shared/Statevia.Modules/ ./shared/Statevia.Modules/
RUN dotnet publish ./service/action-host/Statevia.Service.ActionHost/Statevia.Service.ActionHost.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

ARG APP_UID=10001
ARG APP_GID=10001

RUN groupadd --gid "$APP_GID" statevia \
    && useradd --uid "$APP_UID" --gid "$APP_GID" --no-create-home --shell /usr/sbin/nologin statevia

WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/modules && chown -R statevia:statevia /app

USER statevia

ENV ASPNETCORE_URLS=http://+:5001
ENV STATEVIA_MODULES_PATH=/app/modules
EXPOSE 5001

ENTRYPOINT ["dotnet", "Statevia.Service.ActionHost.dll"]
