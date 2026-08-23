using System.Configuration;
using System.Data.SqlClient;

namespace PointOfSale.Infrastructure
{
    public class DatabaseConnection
    {
        private readonly string _connectionString;

        public DatabaseConnection()
        {
            var connection = ConfigurationManager.ConnectionStrings["LocalConnection"]
                ?? ConfigurationManager.ConnectionStrings["LocalConnection"];

            _connectionString = connection.ConnectionString;
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
