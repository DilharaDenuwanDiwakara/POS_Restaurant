using System;
using System.Data;
using System.Data.SqlClient;

namespace PointOfSale.Infrastructure.Repositories
{
    public abstract class BaseRepository
    {
        protected readonly DatabaseConnection _databaseConnection;

        protected BaseRepository(DatabaseConnection databaseConnection)
        {
            _databaseConnection = databaseConnection;
        }

        protected SqlConnection GetConnection()
        {
            return _databaseConnection.GetConnection();
        }

        protected SqlCommand CreateCommand(SqlConnection connection, string storedProcedure)
        {
            var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = storedProcedure;
            return command;
        }
        protected SqlCommand CreateCommand(SqlConnection connection, string storedProcedure, SqlTransaction transaction)
        {
            var command = CreateCommand(connection, storedProcedure); // Reuse logic above
            command.Transaction = transaction; // Assign the transaction
            return command;
        }
        protected T GetValue<T>(IDataRecord record, string columnName)
        {
            if (!HasColumn(record, columnName))
            {
                throw new IndexOutOfRangeException(
                    $"Column '{columnName}' was not returned. Available columns: {GetColumnList(record)}");
            }

            var value = record[columnName];

            if (value == DBNull.Value || value == null)
            {
                return default(T); // Returns 0 for int, null for string, etc.
            }

            Type t = typeof(T);
            Type underlyingType = Nullable.GetUnderlyingType(t);

            return (T)Convert.ChangeType(value, underlyingType ?? t);
        }

        private static bool HasColumn(IDataRecord record, string columnName)
        {
            for (var i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetColumnList(IDataRecord record)
        {
            var names = new string[record.FieldCount];
            for (var i = 0; i < record.FieldCount; i++)
            {
                names[i] = record.GetName(i);
            }

            return string.Join(", ", names);
        }
    }
}
