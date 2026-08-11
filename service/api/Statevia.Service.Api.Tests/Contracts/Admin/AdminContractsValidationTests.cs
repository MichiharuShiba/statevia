using System.ComponentModel.DataAnnotations;
using Statevia.Service.Api.Contracts.Admin;

namespace Statevia.Service.Api.Tests.Contracts.Admin;

/// <summary>AdminContracts の IValidatableObject 検証。</summary>
public sealed class AdminContractsValidationTests
{
    /// <summary>CreateAdminUserRequest は email / password 必須。</summary>
    [Fact]
    public void CreateAdminUserRequest_Validate_RequiresEmailAndPassword()
    {
        // Arrange
        var request = new CreateAdminUserRequest { Email = " ", Password = "" };
        var context = new ValidationContext(request);

        // Act
        var results = request.Validate(context).ToList();

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateAdminUserRequest.Email)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateAdminUserRequest.Password)));
    }

    /// <summary>CreateAdminApiKeyRequest の name / scopes / expiresAt を検証する。</summary>
    [Fact]
    public void CreateAdminApiKeyRequest_Validate_CoversNameScopesAndExpiry()
    {
        // Arrange
        var blank = new CreateAdminApiKeyRequest
        {
            Name = " ",
            AllowedScopes = Array.Empty<string>(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        var tooLong = new CreateAdminApiKeyRequest
        {
            Name = new string('a', 200),
            AllowedScopes = ["executions.read"]
        };

        // Act
        var blankResults = blank.Validate(new ValidationContext(blank)).ToList();
        var longResults = tooLong.Validate(new ValidationContext(tooLong)).ToList();

        // Assert
        Assert.Contains(blankResults, r => r.MemberNames.Contains(nameof(CreateAdminApiKeyRequest.Name)));
        Assert.Contains(blankResults, r => r.MemberNames.Contains(nameof(CreateAdminApiKeyRequest.AllowedScopes)));
        Assert.Contains(blankResults, r => r.MemberNames.Contains(nameof(CreateAdminApiKeyRequest.ExpiresAt)));
        Assert.Contains(longResults, r => r.MemberNames.Contains(nameof(CreateAdminApiKeyRequest.Name)));
    }
}
