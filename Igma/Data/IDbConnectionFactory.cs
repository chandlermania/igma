using System.Data;

namespace Igma.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
