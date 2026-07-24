using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading.Tasks;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Infrastructure.Repositories.Restaurant
{
    public class RoomBookingRepository : BaseRepository, IRoomBookingRepository
    {
        public RoomBookingRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        public async Task<int> CreateRoomAsync(Room room)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspInsertRoom]"))
                    {
                        AddRoomParameters(command, room);

                        var id = command.Parameters.Add("@Id", SqlDbType.Int);
                        id.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return (int)id.Value;
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occured while creating the room.", ex);
            }
        }

        public async Task UpdateRoomAsync(Room room)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspUpdateRoom]"))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = room.Id;
                        AddRoomParameters(command, room);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException($"A database error occured while updating room with ID {room.Id}.", ex);
            }
        }

        public async Task<IEnumerable<Room>> GetAllRoomsAsync()
        {
            var rooms = new List<Room>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspGetAllRooms]"))
                    {
                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                rooms.Add(MapRoom(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while selecting rooms.", ex);
            }

            return rooms;
        }

        public async Task<int> CreateBookingAsync(RoomBooking booking)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspInsertBooking]"))
                    {
                        booking.Status = BookingStatus.Pending;
                        command.Parameters.Add("@RoomId", SqlDbType.Int).Value = booking.RoomId;
                        command.Parameters.Add("@MealPeriodId", SqlDbType.Int).Value = booking.MealPeriodId;
                        command.Parameters.Add("@From", SqlDbType.DateTime2).Value = booking.From;
                        command.Parameters.Add("@To", SqlDbType.DateTime2).Value = booking.To;
                        command.Parameters.Add("@CustomerName", SqlDbType.NVarChar, 120).Value = (object)booking.CustomerName ?? DBNull.Value;
                        command.Parameters.Add("@CustomerPhoneNumber", SqlDbType.NVarChar, 20).Value = (object)booking.CustomerPhoneNumber ?? DBNull.Value;
                        command.Parameters.Add("@NumberOfPax", SqlDbType.Int).Value = booking.NumberOfPax;
                        command.Parameters.Add("@Status", SqlDbType.VarChar, 50).Value = booking.Status.ToString();
                        command.Parameters.Add("@Remarks", SqlDbType.VarChar, 500).Value = (object)booking.Remarks ?? DBNull.Value;
                        command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = (object)booking.CustomerId ?? DBNull.Value;
                        command.Parameters.Add("@AdvancePayment", SqlDbType.Decimal).Value = booking.AdvancePayment;
                        command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = booking.CreatedBy;

                        var id = command.Parameters.Add("@Id", SqlDbType.Int);
                        id.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return (int)id.Value;
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 50000)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException($"A database error occurred while creating booking: {ex.Message}", ex);
            }
        }

        public async Task UpdateBookingAsync(RoomBooking booking)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    var sql = @"
                        UPDATE [Restaurant].[Bookings]
                        SET RoomId = @RoomId,
                            MealPeriodId = @MealPeriodId,
                            [From] = @From,
                            [To] = @To,
                            CustomerName = @CustomerName,
                            ContactNumber = @CustomerPhoneNumber,
                            NumberOfPax = @NumberOfPax,
                            Status = @Status,
                            Remarks = @Remarks,
                            CustomerId = @CustomerId,
                            AdvancePayment = @AdvancePayment
                        WHERE Id = @Id";

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandType = CommandType.Text;
                        command.CommandText = sql;

                        command.Parameters.Add("@Id", SqlDbType.Int).Value = booking.Id;
                        command.Parameters.Add("@RoomId", SqlDbType.Int).Value = booking.RoomId;
                        command.Parameters.Add("@MealPeriodId", SqlDbType.Int).Value = booking.MealPeriodId;
                        command.Parameters.Add("@From", SqlDbType.DateTime2).Value = booking.From;
                        command.Parameters.Add("@To", SqlDbType.DateTime2).Value = booking.To;
                        command.Parameters.Add("@CustomerName", SqlDbType.NVarChar, 120).Value = (object)booking.CustomerName ?? DBNull.Value;
                        command.Parameters.Add("@CustomerPhoneNumber", SqlDbType.NVarChar, 20).Value = (object)booking.CustomerPhoneNumber ?? DBNull.Value;
                        command.Parameters.Add("@NumberOfPax", SqlDbType.Int).Value = booking.NumberOfPax;
                        command.Parameters.Add("@Status", SqlDbType.VarChar, 50).Value = booking.Status.ToString();
                        command.Parameters.Add("@Remarks", SqlDbType.VarChar, 500).Value = (object)booking.Remarks ?? DBNull.Value;
                        command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = (object)booking.CustomerId ?? DBNull.Value;
                        command.Parameters.Add("@AdvancePayment", SqlDbType.Decimal).Value = booking.AdvancePayment;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating booking with ID {booking.Id}: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<RoomBooking>> GetAllBookingsAsync()
        {
            var bookings = new List<RoomBooking>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspGetAllBookings]"))
                    {
                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                bookings.Add(MapBooking(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while selecting bookings.", ex);
            }

            return bookings;
        }

        public async Task UpdateBookingStatusAsync(int bookingId, BookingStatus newStatus)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspUpdateBookingStatus]"))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = bookingId;
                        command.Parameters.Add("@Status", SqlDbType.VarChar, 50).Value = newStatus.ToString();

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 50000)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException($"A database error occurred while updating status for booking ID {bookingId}: {ex.Message}", ex);
            }
        }

        #region Private Methods
        private void AddRoomParameters(SqlCommand command, Room room)
        {
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = (object)room.Name ?? DBNull.Value;
            command.Parameters.Add("@MaxCapacity", SqlDbType.Int).Value = room.MaxCapacity;
            command.Parameters.Add("@Type", SqlDbType.NVarChar, 20).Value = (object)room.Type ?? DBNull.Value;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = room.IsActive;
            command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = (object)room.Status ?? DBNull.Value;
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 250).Value = (object)room.Description ?? DBNull.Value;
            command.Parameters.Add("@FloorNumber", SqlDbType.Int).Value = room.FloorNumber;
            command.Parameters.Add("@ImageUrl", SqlDbType.NVarChar, 1000).Value = (object)room.ImageUrl ?? DBNull.Value;
        }

        private Room MapRoom(IDataRecord record)
        {
            return new Room
            {
                Id = GetSafeInt(record, "Id"),
                Name = GetSafeString(record, "Name"),
                MaxCapacity = GetSafeInt(record, "MaxCapacity"),
                Type = GetSafeString(record, "Type"),
                IsActive = GetSafeBool(record, "IsActive", true),
                Status = GetSafeString(record, "Status"),
                Description = GetSafeString(record, "Description"),
                FloorNumber = GetSafeInt(record, "FloorNumber"),
                ImageUrl = GetSafeString(record, "ImageUrl")
            };
        }

        private RoomBooking MapBooking(IDataRecord record)
        {
            var statusStr = GetSafeString(record, "Status");
            Enum.TryParse<BookingStatus>(statusStr, true, out var status);

            return new RoomBooking
            {
                Id = GetValue<int>(record, "Id"),
                RoomId = GetSafeInt(record, "RoomID", GetSafeInt(record, "RoomId")),
                MealPeriodId = GetSafeInt(record, "MealPeriodId"),
                RoomName = GetSafeString(record, "RoomName"),
                MealPeriodName = GetSafeString(record, "MealPeriodName"),
                From = GetValue<DateTime>(record, "From"),
                To = GetValue<DateTime>(record, "To"),
                CustomerName = GetValue<string>(record, "CustomerName"),
                CustomerPhoneNumber = GetSafeString(record, "CustomerPhoneNumber", GetSafeString(record, "ContactNumber")),
                NumberOfPax = GetSafeInt(record, "NumberOfPax", 0),
                Status = status,
                Remarks = GetSafeString(record, "Remarks"),
                CustomerId = HasColumn(record, "CustomerId") ? GetValue<int?>(record, "CustomerId") : null,
                AdvancePayment = HasColumn(record, "AdvancePayment") ? GetValue<decimal>(record, "AdvancePayment") : 0m
            };
        }

        private int GetSafeInt(IDataRecord record, string columnName, int defaultValue = 0)
        {
            if (!HasColumn(record, columnName))
            {
                return defaultValue;
            }

            var value = record[columnName];

            if (value == null || value == DBNull.Value)
            {
                return defaultValue;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is byte byteValue)
            {
                return byteValue;
            }

            if (value is short shortValue)
            {
                return shortValue;
            }

            if (value is long longValue && longValue >= int.MinValue && longValue <= int.MaxValue)
            {
                return (int)longValue;
            }

            if (value is decimal decimalValue)
            {
                return decimal.ToInt32(decimal.Truncate(decimalValue));
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text))
            {
                return defaultValue;
            }

            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt) ||
                int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out parsedInt))
            {
                return parsedInt;
            }

            if (decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedDecimal) ||
                decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out parsedDecimal))
            {
                return decimal.ToInt32(decimal.Truncate(parsedDecimal));
            }

            return defaultValue;
        }

        private bool GetSafeBool(IDataRecord record, string columnName, bool defaultValue = false)
        {
            if (!HasColumn(record, columnName))
            {
                return defaultValue;
            }

            var value = record[columnName];

            if (value == null || value == DBNull.Value)
            {
                return defaultValue;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (value is byte byteValue)
            {
                return byteValue != 0;
            }

            if (value is short shortValue)
            {
                return shortValue != 0;
            }

            if (value is int intValue)
            {
                return intValue != 0;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text))
            {
                return defaultValue;
            }

            text = text.Trim();

            if (bool.TryParse(text, out var parsedBool))
            {
                return parsedBool;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt) ||
                int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsedInt))
            {
                return parsedInt != 0;
            }

            switch (text.ToUpperInvariant())
            {
                case "Y":
                case "YES":
                case "ACTIVE":
                case "ENABLED":
                    return true;
                case "N":
                case "NO":
                case "INACTIVE":
                case "DISABLED":
                    return false;
                default:
                    return defaultValue;
            }
        }

        private string GetSafeString(IDataRecord record, string columnName, string defaultValue = null)
        {
            if (!HasColumn(record, columnName))
            {
                return defaultValue;
            }

            var value = record[columnName];
            if (value == null || value == DBNull.Value)
            {
                return defaultValue;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private bool HasColumn(IDataRecord record, string columnName)
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
        #endregion
    }
}
