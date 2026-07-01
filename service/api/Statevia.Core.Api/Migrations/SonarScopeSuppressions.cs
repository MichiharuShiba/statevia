using System.Diagnostics.CodeAnalysis;

// SonarQube for IDE（Connected mode）では .editorconfig の S1192 が効かない場合がある。
// Migrations 名前空間全体を Sonar リファクタ対象外とする。
[assembly: SuppressMessage(
    "Major Code Smell",
    "S1192",
    Scope = "namespace",
    Target = "~N:Statevia.Core.Api.Migrations",
    Justification = "EF Core migrations are schema snapshots; excluded from Sonar refactors.")]
