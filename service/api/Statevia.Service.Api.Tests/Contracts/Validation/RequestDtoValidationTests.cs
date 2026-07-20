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
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginRequest.Email)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginRequest.Password)));
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

    private static List<ValidationResult> Validate(object instance)
    {
        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);
        return results;
    }
}
