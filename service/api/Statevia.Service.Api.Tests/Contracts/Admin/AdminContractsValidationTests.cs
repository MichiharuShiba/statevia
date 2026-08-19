using System.ComponentModel.DataAnnotations;
using Statevia.Service.Api.Contracts.Admin;

namespace Statevia.Service.Api.Tests.Contracts.Admin;

/// <summary>AdminContracts の IValidatableObject 検証。</summary>
public sealed class AdminContractsValidationTests
{
    /// <summary>CreateAdminUserRequest は username / password 必須。</summary>
    [Fact]
    public void CreateAdminUserRequest_Validate_RequiresUsernameAndPassword()
    {
        // Arrange
        var request = new CreateAdminUserRequest { Username = " ", Password = "" };
        var context = new ValidationContext(request);

        // Act
        var results = request.Validate(context).ToList();

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateAdminUserRequest.Username)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateAdminUserRequest.Password)));
    }

    /// <summary>CreateAdminUserRequest は許可文字以外の username を拒否する。</summary>
    [Fact]
    public void CreateAdminUserRequest_Validate_RejectsUsernameWithAt()
    {
        // Arrange
        var request = new CreateAdminUserRequest { Username = "user@example.com", Password = "password1" };
        var context = new ValidationContext(request);

        // Act
        var results = request.Validate(context).ToList();

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateAdminUserRequest.Username)));
    }

    /// <summary>CreateAdminUserRequest は 65 文字の username を拒否する。</summary>
    [Fact]
    public void CreateAdminUserRequest_Validate_RejectsUsernameLongerThan64()
    {
        // Arrange
        var request = new CreateAdminUserRequest { Username = new string('a', 65), Password = "password1" };
        var context = new ValidationContext(request);

        // Act
        var results = request.Validate(context).ToList();

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateAdminUserRequest.Username)));
    }

    /// <summary>CreateAdminUserRequest は 257 文字の email を拒否する。</summary>
    [Fact]
    public void CreateAdminUserRequest_Validate_RejectsEmailLongerThan256()
    {
        // Arrange
        var request = new CreateAdminUserRequest
        {
            Username = "ops",
            Email = $"{new string('a', 64)}@{new string('b', 190)}.com",
            Password = "password1"
        };
        var context = new ValidationContext(request);

        // Act
        var results = request.Validate(context).ToList();

        // Assert
        Assert.True(request.Email!.Length > 256);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateAdminUserRequest.Email)));
    }

    /// <summary>CreateAdminUserRequest は短いパスワードを拒否する。</summary>
    [Fact]
    public void CreateAdminUserRequest_Validate_RejectsShortPassword()
    {
        // Arrange
        var request = new CreateAdminUserRequest { Username = "ops", Password = "short" };
        var context = new ValidationContext(request);

        // Act
        var results = request.Validate(context).ToList();

        // Assert
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
