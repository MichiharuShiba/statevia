namespace Statevia.Core.Api.Application.Actions.Modules;

/// <summary>Module load 結果の読み取り専用カタログ。</summary>
internal sealed class ModuleLoadCatalog
{
    private readonly object _sync = new();
    private readonly List<ModuleLoadRecord> _records = [];

    /// <summary>登録済みレコードのスナップショットを返す。</summary>
    public IReadOnlyList<ModuleLoadRecord> GetRecords()
    {
        lock (_sync)
        {
            return _records.ToList();
        }
    }

    /// <summary>load 結果を追記する（同一 entry パスは上書き更新）。</summary>
    /// <param name="record">追記するレコード。</param>
    public void Upsert(ModuleLoadRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_sync)
        {
            var index = _records.FindIndex(
                existing => EntryAssemblyPathEquals(existing.EntryAssemblyPath, record.EntryAssemblyPath));
            if (index >= 0)
            {
                _records[index] = record;
                return;
            }

            _records.Add(record);
        }
    }

    /// <summary>entry パスが Loaded のレコードとして存在するか。</summary>
    /// <param name="entryAssemblyPath">entry DLL 絶対パス。</param>
    public bool IsLoaded(string entryAssemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryAssemblyPath);

        lock (_sync)
        {
            return _records.Any(
                record => EntryAssemblyPathEquals(record.EntryAssemblyPath, entryAssemblyPath)
                    && record.Status == ModuleLoadStatus.Loaded);
        }
    }

    private static bool EntryAssemblyPathEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
