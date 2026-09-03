using System.ComponentModel.DataAnnotations;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Contracts.Auth;

namespace Statevia.Service.Api.Tests.Contracts.Validation;

/// <summary>移行対象 DTO の Data Annotations 回帰。</summary>
public sealed class RequestDtoValidationTests
{
    /// <summary>定義作成で空白 name は検証失敗する。</summary>
    [Fact]
    public void CreateDefinitionRequest_WhenNameWhitespace_FailsValidation()
    {
        // Arrange
        var request = new CreateDefinitionRequest { Name = " ", Yaml = "workflow: {}" };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateDefinitionRequest.Name)));
    }

    /// <summary>定義検証で空白 yaml は検証失敗する。name 省略は許可する。</summary>
    [Fact]
    public void ValidateDefinitionRequest_WhenYamlWhitespace_FailsValidation()
    {
        // Arrange
        var request = new ValidateDefinitionRequest { Yaml = " " };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ValidateDefinitionRequest.Yaml)));
        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(ValidateDefinitionRequest.Name)));
    }

    /// <summary>ログイン空入力は検証失敗する。</summary>
    [Fact]
    public void LoginRequest_WhenFieldsEmpty_FailsValidation()
    {
        // Arrange
        var request = new LoginRequest();

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginRequest.TenantKey)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginRequest.Username)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginRequest.Password)));
    }

    /// <summary>username に @ が含まれると検証失敗する。</summary>
    [Fact]
    public void LoginRequest_WhenUsernameContainsAt_FailsValidation()
    {
        // Arrange
        var request = new LoginRequest
        {
            TenantKey = "default",
            Username = "user@example.com",
            Password = "secret"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginRequest.Username)));
    }

    /// <summary>username が 65 文字だと検証失敗する。</summary>
    [Fact]
    public void LoginRequest_WhenUsernameLongerThan64_FailsValidation()
    {
        // Arrange
        var request = new LoginRequest
        {
            TenantKey = "default",
            Username = new string('a', 65),
            Password = "secret"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginRequest.Username)));
    }

    /// <summary>許可文字の username は検証成功する。</summary>
    [Fact]
    public void LoginRequest_WhenUsernameUsesAllowedCharset_PassesValidation()
    {
        // Arrange
        var request = new LoginRequest
        {
            TenantKey = "default",
            Username = "Ops.user_1-test",
            Password = "secret"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Empty(results);
    }

    /// <summary>本人パスワード更新の新パスワードが短いと検証失敗する。</summary>
    [Fact]
    public void ChangeOwnPasswordRequest_WhenNewPasswordShort_FailsValidation()
    {
        // Arrange
        var request = new ChangeOwnPasswordRequest
        {
            CurrentPassword = "password1",
            NewPassword = "short"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ChangeOwnPasswordRequest.NewPassword)));
    }

    /// <summary>本人パスワード更新の新パスワードが 8 文字なら検証成功する。</summary>
    [Fact]
    public void ChangeOwnPasswordRequest_WhenNewPasswordAlphanumeric8_PassesValidation()
    {
        // Arrange
        var request = new ChangeOwnPasswordRequest
        {
            CurrentPassword = "oldpass1",
            NewPassword = "password"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Empty(results);
    }

    /// <summary>state atSeq が 0 のとき検証失敗する。</summary>
    [Fact]
    public void ExecutionStateQuery_WhenAtSeqZero_FailsValidation()
    {
        // Arrange
        var query = new ExecutionStateQuery { AtSeq = 0 };

        // Act
        var results = Validate(query);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ExecutionStateQuery.AtSeq)));
    }

    /// <summary>events limit が範囲外のとき検証失敗する。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(5001)]
    public void ExecutionEventsQuery_WhenLimitOutOfRange_FailsValidation(int limit)
    {
        // Arrange
        var query = new ExecutionEventsQuery { AfterSeq = 0, Limit = limit };

        // Act
        var results = Validate(query);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ExecutionEventsQuery.Limit)));
    }

    /// <summary>event ingress で topic 未指定なら検証失敗する。</summary>
    [Fact]
    public void EventIngressRequest_WhenTopicMissing_FailsValidation()
    {
        // Arrange
        var request = new EventIngressRequest { Topic = "" };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(EventIngressRequest.Topic)));
    }

    /// <summary>event ingress で topic があれば key 省略でも検証成功する。</summary>
    [Fact]
    public void EventIngressRequest_WhenTopicPresent_PassesValidation()
    {
        // Arrange
        var request = new EventIngressRequest
        {
            Topic = "inventory.received",
            Key = "sku-1"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Empty(results);
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);
        return results;
    }
}
