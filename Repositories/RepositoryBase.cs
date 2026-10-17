using Npgsql;
using NpgsqlTypes;

namespace LmsApp.Repositories;

public abstract class RepositoryBase
{
    private readonly NpgsqlConnection _conn;
    private readonly NpgsqlTransaction _tx;

    protected RepositoryBase(NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        _conn = conn;
        _tx = tx;
    }

    protected async Task CallAsync(string sql, params NpgsqlParameter[] ps)
    {
        await using var cmd = Create(sql, ps);
        await cmd.ExecuteNonQueryAsync();
    }

    protected async Task<int> CallForIdAsync(string sql, params NpgsqlParameter[] ps)
    {
        await using var cmd = Create(sql, ps);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    protected async Task<T> CallForRowAsync<T>(string sql, Func<NpgsqlDataReader, T> map, params NpgsqlParameter[] ps)
    {
        await using var cmd = Create(sql, ps);
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return map(r);
    }

    protected async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql, Func<NpgsqlDataReader, T> map, params NpgsqlParameter[] ps)
    {
        var list = new List<T>();
        await using var cmd = Create(sql, ps);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(map(r));
        return list;
    }

    protected async Task<T?> QuerySingleAsync<T>(
        string sql, Func<NpgsqlDataReader, T> map, params NpgsqlParameter[] ps) where T : class
    {
        var rows = await QueryAsync(sql, map, ps);
        return rows.Count == 0 ? null : rows[0];
    }

    private NpgsqlCommand Create(string sql, NpgsqlParameter[] ps)
    {
        var cmd = new NpgsqlCommand(sql, _conn, _tx);
        cmd.Parameters.AddRange(ps);
        return cmd;
    }

    protected static NpgsqlParameter P(string name, int? value) =>
        new(name, NpgsqlDbType.Integer) { Value = (object?)value ?? DBNull.Value };

    protected static NpgsqlParameter P(string name, string? value) =>
        new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    protected static NpgsqlParameter P(string name, bool value) =>
        new(name, NpgsqlDbType.Boolean) { Value = value };

    protected static NpgsqlParameter P(string name, int[] value) =>
        new(name, NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = value };


    protected static int? NInt(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt32(i);
    protected static string? NStr(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
}
