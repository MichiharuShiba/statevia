FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# csproj だけ先にコピーして restore をキャッシュ効かせる
COPY service/api/Statevia.Service.Api/Statevia.Service.Api.csproj ./service/api/Statevia.Service.Api/
COPY core/engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj ./core/engine/Statevia.Core.Engine/
COPY core/actions/Statevia.Core.Actions.Abstractions/Statevia.Core.Actions.Abstractions.csproj ./core/actions/Statevia.Core.Actions.Abstractions/
COPY core/application/Statevia.Core.Application.Contracts/Statevia.Core.Application.Contracts.csproj ./core/application/Statevia.Core.Application.Contracts/
COPY core/application/Statevia.Core.Application/Statevia.Core.Application.csproj ./core/application/Statevia.Core.Application/
COPY infrastructure/Statevia.Infrastructure.Persistence/Statevia.Infrastructure.Persistence.csproj ./infrastructure/Statevia.Infrastructure.Persistence/
COPY infrastructure/Statevia.Infrastructure.Security/Statevia.Infrastructure.Security.csproj ./infrastructure/Statevia.Infrastructure.Security/
COPY infrastructure/Statevia.Infrastructure.Notification/Statevia.Infrastructure.Notification.csproj ./infrastructure/Statevia.Infrastructure.Notification/
COPY infrastructure/Statevia.Infrastructure.Common/Statevia.Infrastructure.Common.csproj ./infrastructure/Statevia.Infrastructure.Common/
COPY infrastructure/Statevia.Infrastructure.Actions.Grpc/Statevia.Infrastructure.Actions.Grpc.csproj ./infrastructure/Statevia.Infrastructure.Actions.Grpc/
COPY infrastructure/Statevia.Infrastructure.Modules/Statevia.Infrastructure.Modules.csproj ./infrastructure/Statevia.Infrastructure.Modules/
COPY modules/reference/Statevia.Reference.Http/Statevia.Reference.Http.csproj ./modules/reference/Statevia.Reference.Http/
COPY modules/reference/Statevia.Reference.Notification/Statevia.Reference.Notification.csproj ./modules/reference/Statevia.Reference.Notification/
COPY service/runtime/Statevia.Runtime/Statevia.Runtime.csproj ./service/runtime/Statevia.Runtime/
RUN dotnet restore ./service/api/Statevia.Service.Api/Statevia.Service.Api.csproj

# ソースをコピーして publish
COPY service/api/Statevia.Service.Api/ ./service/api/Statevia.Service.Api/
COPY service/runtime/Statevia.Runtime/ ./service/runtime/Statevia.Runtime/
COPY core/engine/Statevia.Core.Engine/ ./core/engine/Statevia.Core.Engine/
COPY core/actions/Statevia.Core.Actions.Abstractions/ ./core/actions/Statevia.Core.Actions.Abstractions/
COPY modules/reference/Statevia.Reference.Http/ ./modules/reference/Statevia.Reference.Http/
COPY modules/reference/Statevia.Reference.Notification/ ./modules/reference/Statevia.Reference.Notification/
COPY core/application/Statevia.Core.Application.Contracts/ ./core/application/Statevia.Core.Application.Contracts/
COPY core/application/Statevia.Core.Application/ ./core/application/Statevia.Core.Application/
COPY infrastructure/Statevia.Infrastructure.Notification/ ./infrastructure/Statevia.Infrastructure.Notification/
COPY infrastructure/Statevia.Infrastructure.Security/ ./infrastructure/Statevia.Infrastructure.Security/
COPY infrastructure/Statevia.Infrastructure.Persistence/ ./infrastructure/Statevia.Infrastructure.Persistence/
COPY infrastructure/Statevia.Infrastructure.Common/ ./infrastructure/Statevia.Infrastructure.Common/
COPY infrastructure/Statevia.Infrastructure.Actions.Grpc/ ./infrastructure/Statevia.Infrastructure.Actions.Grpc/
COPY infrastructure/Statevia.Infrastructure.Modules/ ./infrastructure/Statevia.Infrastructure.Modules/
RUN dotnet publish \
    ./service/api/Statevia.Service.Api/Statevia.Service.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

ARG APP_UID=10001
ARG APP_GID=10001

RUN groupadd --gid "$APP_GID" statevia \
    && useradd --uid "$APP_UID" --gid "$APP_GID" --no-create-home --shell /usr/sbin/nologin statevia

WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/modules/default /app/modules/default
RUN chown -R statevia:statevia /app

USER statevia

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Statevia.Service.Api.dll"]
