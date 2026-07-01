using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Visibility;

namespace Statevia.Service.Api.Application.Actions.Visibility;

/// <summary>Phase 1〜2 の Tenant / Builtin Visibility 判定。</summary>
internal sealed class DefaultActionVisibilityResolver : IActionVisibilityResolver
{
    /// <inheritdoc />
    public bool CanUse(string tenantId, ActionDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.Visibility switch
        {
            ActionVisibility.Builtin or ActionVisibility.Marketplace => true,
            ActionVisibility.Tenant => string.Equals(
                descriptor.OwnerTenantId,
                tenantId.Trim(),
                StringComparison.OrdinalIgnoreCase),
            ActionVisibility.Organization => false,
            _ => false,
        };
    }
}
