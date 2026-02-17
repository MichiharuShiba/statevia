using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Statevia.Core.Definition;

/// <summary>
/// Deserialize 先が object のとき、YAML スカラーを文字列ではなく bool / int / float として扱うための NodeTypeResolver。
/// </summary>
internal sealed class ScalarPreservingNodeTypeResolver : INodeTypeResolver
{
    private const string TagBool = "tag:yaml.org,2002:bool";
    private const string TagInt = "tag:yaml.org,2002:int";
    private const string TagFloat = "tag:yaml.org,2002:float";

    public bool Resolve(NodeEvent? nodeEvent, ref Type currentType)
    {
        if (currentType != typeof(object) || nodeEvent is not Scalar scalar)
        {
            return false;
        }

        var tag = nodeEvent.Tag;

        // 明示タグがあればそれで解決
        if (!tag.IsEmpty)
        {
            if (tag == TagBool) { currentType = typeof(bool); return true; }
            if (tag == TagInt) { currentType = typeof(long); return true; }
            if (tag == TagFloat) { currentType = typeof(double); return true; }
            return false;
        }

        // プレーンスカラー: 値から bool / int / float を推定
        var value = scalar.Value;
        if (value is "true" or "false")
        {
            currentType = typeof(bool);
            return true;
        }
        if (long.TryParse(value, out _))
        {
            currentType = typeof(long);
            return true;
        }
        if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            currentType = typeof(double);
            return true;
        }

        return false;
    }
}
