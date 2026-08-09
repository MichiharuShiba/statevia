using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Application.Contracts.Services;

namespace Statevia.Infrastructure.Common.DependencyInjection;

/// <summary>横断インフラ（ID 生成等）の DI 登録。</summary>
public static class CommonServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="IIdGenerator"/> の既定実装（Sequential=v7 / Random=v4）を登録する。
    /// </summary>
    /// <param name="services">サービスコレクション。</param>
    public static IServiceCollection AddStateviaInfrastructureCommon(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IIdGenerator, DefaultIdGenerator>();

        return services;
    }
}
