using System.Configuration;
using System.Data.SqlClient;

namespace PointOfSale.Infrastructure
{
    public class CentralAccountConnection
    {
        private readonly string _connectionString;

        public CentralAccountConnection()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["CentralAccountConnection"].ConnectionString;
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
