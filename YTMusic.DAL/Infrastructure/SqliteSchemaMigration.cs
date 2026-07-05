using Dapper;
using Microsoft.Data.Sqlite;

namespace YTMusic.DAL.Infrastructure;

internal static class SqliteSchemaMigration
{
    public static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
    {
        var exists = connection.ExecuteScalar<int>(
            "SELECT COUNT(1) FROM pragma_table_info(@TableName) WHERE name = @ColumnName;",
            new { TableName = tableName, ColumnName = columnName }) > 0;

        if (exists)
        {
            return;
        }

        connection.Execute($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};");
    }
}
