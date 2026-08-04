FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY service/runtime/Statevia.Service.Runtime.Scheduler/Statevia.Service.Runtime.Scheduler.csproj ./service/runtime/Statevia.Service.Runtime.Scheduler/
COPY service/runtime/Statevia.Runtime/Statevia.Runtime.csproj ./service/runtime/Statevia.Runtime/
COPY core/application/Statevia.Core.Application.Contracts/Statevia.Core.Application.Contracts.csproj ./core/application/Statevia.Core.Application.Contracts/
COPY core/application/Statevia.Core.Application/Statevia.Core.Application.csproj ./core/application/Statevia.Core.Application/
COPY core/engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj ./core/engine/Statevia.Core.Engine/
COPY core/actions/Statevia.Core.Actions.Abstractions/Statevia.Core.Actions.Abstractions.csproj ./core/actions/Statevia.Core.Actions.Abstractions/
COPY infrastructure/Statevia.Infrastructure.Persistence/Statevia.Infrastructure.Persistence.csproj ./infrastructure/Statevia.Infrastructure.Persistence/
COPY infrastructure/Statevia.Infrastructure.Security/Statevia.Infrastructure.Security.csproj ./infrastructure/Statevia.Infrastructure.Security/
COPY infrastructure/Statevia.Infrastructure.Common/Statevia.Infrastructure.Common.csproj ./infrastructure/Statevia.Infrastructure.Common/
RUN dotnet restore ./service/runtime/Statevia.Service.Runtime.Scheduler/Statevia.Service.Runtime.Scheduler.csproj

COPY service/runtime/Statevia.Service.Runtime.Scheduler/ ./service/runtime/Statevia.Service.Runtime.Scheduler/
COPY service/runtime/Statevia.Runtime/ ./service/runtime/Statevia.Runtime/
COPY core/application/Statevia.Core.Application.Contracts/ ./core/application/Statevia.Core.Application.Contracts/
COPY core/application/Statevia.Core.Application/ ./core/application/Statevia.Core.Application/
COPY core/engine/Statevia.Core.Engine/ ./core/engine/Statevia.Core.Engine/
COPY core/actions/Statevia.Core.Actions.Abstractions/ ./core/actions/Statevia.Core.Actions.Abstractions/
COPY infrastructure/Statevia.Infrastructure.Persistence/ ./infrastructure/Statevia.Infrastructure.Persistence/
COPY infrastructure/Statevia.Infrastructure.Security/ ./infrastructure/Statevia.Infrastructure.Security/
COPY infrastructure/Statevia.Infrastructure.Common/ ./infrastructure/Statevia.Infrastructure.Common/
RUN dotnet publish \
    ./service/runtime/Statevia.Service.Runtime.Scheduler/Statevia.Service.Runtime.Scheduler.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

ARG APP_UID=10001
ARG APP_GID=10001

RUN groupadd --gid "$APP_GID" statevia \
    && useradd --uid "$APP_UID" --gid "$APP_GID" --no-create-home --shell /usr/sbin/nologin statevia

WORKDIR /app
COPY --from=build /app/publish .
RUN chown -R statevia:statevia /app

USER statevia

ENTRYPOINT ["dotnet", "Statevia.Service.Runtime.Scheduler.dll"]
