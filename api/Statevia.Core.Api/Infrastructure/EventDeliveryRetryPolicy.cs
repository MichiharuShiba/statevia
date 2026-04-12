using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Configuration;

namespace Statevia.Core.Api.Infrastructure;

/// <summary>
/// イベント配送 dedup の DB 書き込みに対する再試行可否とバックオフ計算。
/// 一意制約・一時障害の判定は Npgsql 等へのコンパイル時依存を避け、実行時に読み込まれたプロバイダ例外の型名とプロパティで行う。
/// </summary>
internal static class EventDeliveryRetryPolicy
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>PostgreSQL <c>unique_violation</c> の SQLSTATE。</summary>
    private const string PostgresUniqueViolationSqlState = "23505";

    /// <summary>SQL Server: 一意キー違反（行）。</summary>
    private const int SqlServerErrorUniqueKeyRow = 2627;

    /// <summary>SQL Server: 一意インデックス違反。</summary>
    private const int SqlServerErrorUniqueIndex = 2601;

    /// <summary>SQLite: <c>SQLITE_CONSTRAINT</c>（一意違反を含む制約系）。</summary>
    private const int SqliteErrorCodeConstraint = 19;

    /// <summary>MySQL / MariaDB（MySqlConnector）: 重複エントリ（<c>ER_DUP_ENTRY</c> / エラー番号 1062）。</summary>
    private const int MySqlErrorDuplicateKeyEntry = 1062;

    /// <summary>
    /// タイムアウト・キャンセル系のため再試行しない例外かどうかを返す。
    /// </summary>
    internal static bool IsNonRetryableTimeoutOrCancellation(Exception exception) =>
        exception is OperationCanceledException or TaskCanceledException or TimeoutException;

    /// <summary>
    /// <see cref="DbUpdateException"/> が一意制約違反（重複キー）に起因するか。
    /// <see cref="Exception.InnerException"/> チェーン上のプロバイダ例外を走査する。
    /// </summary>
    internal static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (IsPostgresUniqueViolation(current)
                || IsSqliteUniqueViolation(current)
                || IsSqlServerUniqueViolation(current)
                || IsMySqlUniqueViolation(current))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 指数バックオフに基づく待機時間（ミリ秒）。<paramref name="failureIndex"/> は 0 始まり（初回失敗後は 0）。
    /// </summary>
    internal static int ComputeBackoffDelayMs(int failureIndex, EventDeliveryRetryOptions options, Random random)
    {
        Debug.Assert(failureIndex >= 0, "failureIndex must be non-negative.");

        var exponent = Math.Min(failureIndex, 30);
        var raw = (long)options.BaseDelayMs << exponent;
        var capped = (int)Math.Min(raw, options.MaxDelayMs);
        if (capped < 0)
            capped = options.MaxDelayMs;

        if (!options.Jitter || capped == 0)
            return capped;

        // 指数値の 50%〜100% の範囲でジッタ（平均を下げつつスパイク回避）
        var low = (int)(capped * 0.5);
        return low + random.Next(0, Math.Max(1, capped - low + 1));
    }

    /// <summary>
    /// 再試行に値する一時的な障害か。一意制約・タイムアウトは含めない。
    /// </summary>
    internal static bool IsTransientInfrastructureFailure(Exception exception)
    {
        if (IsNonRetryableTimeoutOrCancellation(exception))
            return false;

        if (exception is DbUpdateException dbUpdateException)
        {
            if (IsUniqueConstraintViolation(dbUpdateException))
                return false;

            if (dbUpdateException.InnerException is { } inner
                && IsPostgresExceptionTransient(inner))
                return true;

            return false;
        }

        if (IsPostgresExceptionTransient(exception))
            return true;

        if (IsNpgsqlExceptionTransient(exception))
            return true;

        return exception is IOException;
    }

    private static bool IsPostgresUniqueViolation(Exception exception)
    {
        if (!TypeFullNameEquals(exception, "Npgsql.PostgresException"))
            return false;

        var sqlState = exception.GetType().GetProperty("SqlState", PublicInstance)?.GetValue(exception) as string;
        return string.Equals(sqlState, PostgresUniqueViolationSqlState, StringComparison.Ordinal);
    }

    private static bool IsSqliteUniqueViolation(Exception exception)
    {
        if (!TypeFullNameEquals(exception, "Microsoft.Data.Sqlite.SqliteException"))
            return false;

        var code = exception.GetType().GetProperty("SqliteErrorCode", PublicInstance)?.GetValue(exception);
        return code is int sqliteCode && sqliteCode == SqliteErrorCodeConstraint;
    }

    private static bool IsSqlServerUniqueViolation(Exception exception)
    {
        var fullName = exception.GetType().FullName;
        if (fullName is not ("Microsoft.Data.SqlClient.SqlException" or "System.Data.SqlClient.SqlException"))
            return false;

        var number = exception.GetType().GetProperty("Number", PublicInstance)?.GetValue(exception);
        return number is int sqlNumber
            && (sqlNumber == SqlServerErrorUniqueKeyRow || sqlNumber == SqlServerErrorUniqueIndex);
    }

    /// <summary>MySqlConnector の重複キー（1062 / <c>DuplicateKeyEntry</c>）。</summary>
    private static bool IsMySqlUniqueViolation(Exception exception)
    {
        if (!TypeFullNameEquals(exception, "MySqlConnector.MySqlException"))
            return false;

        var errorCode = exception.GetType().GetProperty("ErrorCode", PublicInstance)?.GetValue(exception);
        if (errorCode is null)
            return false;

        if (errorCode is int code && code == MySqlErrorDuplicateKeyEntry)
            return true;

        return string.Equals(errorCode.ToString(), "DuplicateKeyEntry", StringComparison.Ordinal);
    }

    private static bool IsPostgresExceptionTransient(Exception exception)
    {
        if (!TypeFullNameEquals(exception, "Npgsql.PostgresException"))
            return false;

        return exception.GetType().GetProperty("IsTransient", PublicInstance)?.GetValue(exception) is true;
    }

    private static bool IsNpgsqlExceptionTransient(Exception exception)
    {
        if (!TypeFullNameEquals(exception, "Npgsql.NpgsqlException"))
            return false;

        return exception.GetType().GetProperty("IsTransient", PublicInstance)?.GetValue(exception) is true;
    }

    private static bool TypeFullNameEquals(Exception exception, string expectedFullName) =>
        string.Equals(exception.GetType().FullName, expectedFullName, StringComparison.Ordinal);
}
