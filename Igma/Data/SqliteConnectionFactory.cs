using System.Data;
using Microsoft.Data.Sqlite;

namespace Igma.Data;

public class SqliteConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public IDbConnection CreateConnection()
    {
        var conn = new SqliteConnection(connectionString);
        conn.Open();
        return conn;
    }
}
