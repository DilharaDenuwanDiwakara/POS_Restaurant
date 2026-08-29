using System;
using System.Configuration;
using System.Data.SqlClient;

namespace PointOfSale.Infrastructure
{
    public class DatabaseConnection
    {
        private const string DefaultConnectionName = "ActiveConnection";

        private readonly string _connectionString;

        public DatabaseConnection()
        {
            var activeConnectionName = ConfigurationManager.AppSettings["ActiveConnection"];
            if (string.IsNullOrWhiteSpace(activeConnectionName))
                activeConnectionName = DefaultConnectionName;

            var connection = ConfigurationManager.ConnectionStrings[activeConnectionName];
            if (connection == null)
                throw new InvalidOperationException($"Connection string '{activeConnectionName}' was not found in App.config.");

            _connectionString = connection.ConnectionString;
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
