using System.Diagnostics.CodeAnalysis;

// SonarQube Connected mode では .editorconfig の CA1515 が効かない場合がある。
// HTTP / DI 公開面および同一アセンブリ内セキュリティ型の CA1515 を抑止する。
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "namespace",
    Target = "~N:Statevia.Core.Api.Abstractions.Security",
    Justification = "AuthController / ミドルウェアの DI 境界として public を維持する。")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "namespace",
    Target = "~N:Statevia.Core.Api.Contracts.Auth",
    Justification = "認証 API の JSON 契約 DTO は HTTP 公開面として public を維持する。")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "namespace",
    Target = "~N:Statevia.Core.Api.Contracts.Admin",
    Justification = "管理者 API の JSON 契約 DTO は HTTP 公開面として public を維持する。")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "namespace",
    Target = "~N:Statevia.Core.Api.Contracts.Actions",
    Justification = "Action Schema API の JSON 契約 DTO は HTTP 公開面として public を維持する。")]
