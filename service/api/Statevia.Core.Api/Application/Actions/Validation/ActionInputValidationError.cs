namespace Statevia.Core.Api.Application.Actions.Validation;

/// <summary>action input schema 検証エラー 1 件（422 details 用。機微値は含めない）。</summary>
/// <param name="State">状態名。</param>
/// <param name="ActionId">canonical actionId。</param>
/// <param name="JsonPath">入力フィールドの JSONPath（例: <c>$.input.url</c>）。</param>
/// <param name="Message">人が読めるエラー説明。</param>
internal sealed record ActionInputValidationError(
    string State,
    string ActionId,
    string JsonPath,
    string Message);
