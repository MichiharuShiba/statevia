namespace Statevia.Core.Api.Application.Security;

/// <summary>project_accesses.role の値。数値順が権限強度（Reader &lt; Executor &lt; Publisher &lt; Admin）。</summary>
public enum ProjectAccessRole
{
    /// <summary>定義の読み取り（一覧・取得・グラフ参照）。</summary>
    Reader = 0,

    /// <summary>ワークフロー開始。</summary>
    Executor = 1,

    /// <summary>定義 publish（版追加）。</summary>
    Publisher = 2,

    /// <summary>プロジェクト管理（将来 API 用）。</summary>
    Admin = 3
}

/// <summary>projects.visibility — discoverability のヒント（認可 truth ではない）。</summary>
public enum ProjectVisibility
{
    /// <summary>オーナーテナントのみ discoverability 対象。</summary>
    Private = 0,

    /// <summary>同一テナント内 discoverability。</summary>
    Tenant = 1,

    /// <summary>プラットフォーム全体 discoverability（認可 truth ではない）。</summary>
    Public = 2
}
