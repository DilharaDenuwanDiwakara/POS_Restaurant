using System.Configuration;
using System.Data.SqlClient;

namespace PointOfSale.Infrastructure
{
    public class DatabaseConnection
    {
        private readonly string _connectionString;

        public DatabaseConnection()
        {
            var connection = ConfigurationManager.ConnectionStrings["ServerConnection"]
                ?? ConfigurationManager.ConnectionStrings["ServerConnection"];

            _connectionString = connection.ConnectionString;
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
