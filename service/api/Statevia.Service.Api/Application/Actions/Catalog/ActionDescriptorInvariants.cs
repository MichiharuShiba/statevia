using Statevia.Core.Actions.Abstractions.Catalog;

namespace Statevia.Service.Api.Application.Actions.Catalog;

/// <summary><see cref="ActionDescriptor"/> 登録時の不変条件検証。</summary>
internal static class ActionDescriptorInvariants
{
    /// <summary>Descriptor の Visibility / OwnerTenantId 整合を検証する。</summary>
    /// <param name="descriptor">検証対象。</param>
    /// <exception cref="ArgumentException">不変条件違反時。</exception>
    public static void Validate(ActionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ActionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Version);

        _ = descriptor.Visibility switch
        {
            ActionVisibility.Builtin or ActionVisibility.Marketplace when descriptor.OwnerTenantId is not null =>
                throw new ArgumentException(
                    $"Action '{descriptor.ActionId}': OwnerTenantId must be null for Visibility={descriptor.Visibility}."),
            ActionVisibility.Tenant when string.IsNullOrWhiteSpace(descriptor.OwnerTenantId) =>
                throw new ArgumentException(
                    $"Action '{descriptor.ActionId}': OwnerTenantId is required for Visibility=Tenant."),
            ActionVisibility.Organization =>
                throw new ArgumentException(
                    $"Action '{descriptor.ActionId}': Visibility=Organization is not supported in Phase 1."),
            ActionVisibility.Builtin or ActionVisibility.Marketplace or ActionVisibility.Tenant => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(descriptor),
                descriptor.Visibility,
                "Unsupported ActionVisibility value."),
        };
    }
}
