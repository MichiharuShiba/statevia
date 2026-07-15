using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace Statevia.Infrastructure.Modules;

/// <summary>AWSSDK.S3 を用いた <see cref="IS3ArtifactFetcher"/> 実装。</summary>
/// <remarks>
/// <para>
/// SDK 依存を本クラスに封じ込める唯一の境界。上位の <see cref="S3ModuleSource"/> は
/// <see cref="IS3ArtifactFetcher"/> 契約のみに依存する。
/// </para>
/// <para>
/// content identity は ETag を正規化し、VersionId 指定時は組み合わせる。
/// 認証情報はログへ出力しない。
/// </para>
/// </remarks>
internal sealed class AwsS3ArtifactFetcher(ILogger<AwsS3ArtifactFetcher> logger) : IS3ArtifactFetcher
{
    /// <inheritdoc />
    public async Task<string> ResolveContentIdentityAsync(
        S3ModuleReference reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        using var client = CreateClient(reference);
        var request = new GetObjectMetadataRequest
        {
            BucketName = reference.Bucket,
            Key = reference.Key,
        };
        if (!string.IsNullOrEmpty(reference.VersionId))
        {
            request.VersionId = reference.VersionId;
        }

        var response = await client.GetObjectMetadataAsync(request, cancellationToken).ConfigureAwait(false);
        var identity = BuildContentIdentity(response.ETag, reference.VersionId ?? response.VersionId);
        AwsS3ArtifactFetcherLog.IdentityResolved(logger, reference.Label, identity);
        return identity;
    }

    /// <inheritdoc />
    public async Task<S3FetchedModule> FetchModuleAsync(
        S3ModuleReference reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        using var client = CreateClient(reference);
        var request = new GetObjectRequest
        {
            BucketName = reference.Bucket,
            Key = reference.Key,
        };
        if (!string.IsNullOrEmpty(reference.VersionId))
        {
            request.VersionId = reference.VersionId;
        }

        using var response = await client.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);
        await using var memory = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);

        var identity = BuildContentIdentity(response.ETag, reference.VersionId ?? response.VersionId);
        AwsS3ArtifactFetcherLog.ModuleFetched(logger, reference.Label, identity);
        return new S3FetchedModule(memory.ToArray(), identity);
    }

    /// <summary>参照設定から S3 クライアントを生成する。</summary>
    private static AmazonS3Client CreateClient(S3ModuleReference reference)
    {
        var config = new AmazonS3Config();
        if (!string.IsNullOrWhiteSpace(reference.Region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(reference.Region);
        }

        if (!string.IsNullOrWhiteSpace(reference.ServiceUrl))
        {
            config.ServiceURL = reference.ServiceUrl;
            config.ForcePathStyle = true;
        }

        var hasExplicitCredentials = !string.IsNullOrEmpty(reference.AccessKeyId)
            && !string.IsNullOrEmpty(reference.SecretAccessKey);
        return hasExplicitCredentials
            ? new AmazonS3Client(reference.AccessKeyId, reference.SecretAccessKey, config)
            : new AmazonS3Client(config);
    }

    /// <summary>ETag / VersionId からキャッシュキー用 content identity を組み立てる。</summary>
    internal static string BuildContentIdentity(string? etag, string? versionId)
    {
        var normalizedEtag = NormalizeEtag(etag);
        if (!string.IsNullOrEmpty(versionId))
        {
            return string.IsNullOrEmpty(normalizedEtag)
                ? $"ver:{versionId}"
                : $"ver:{versionId}+etag:{normalizedEtag}";
        }

        return $"etag:{normalizedEtag ?? "unknown"}";
    }

    private static string? NormalizeEtag(string? etag)
    {
        if (string.IsNullOrWhiteSpace(etag))
        {
            return null;
        }

        return etag.Trim().Trim('"');
    }
}

internal static partial class AwsS3ArtifactFetcherLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Fetched S3 module '{Reference}' (content identity {Identity})")]
    public static partial void ModuleFetched(ILogger logger, string reference, string identity);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Resolved S3 content identity for '{Reference}' ({Identity})")]
    public static partial void IdentityResolved(ILogger logger, string reference, string identity);
}
