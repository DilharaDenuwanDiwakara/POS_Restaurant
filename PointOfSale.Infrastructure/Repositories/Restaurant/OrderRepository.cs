using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;

namespace PointOfSale.Infrastructure.Repositories.Restaurant
{
    public class OrderRepository : BaseRepository, IOrderRepository
    {
        public OrderRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        public async Task<IEnumerable<ServedOrderDto>> GetServedOrdersAsync(int branchId)
        {
            var orders = new List<ServedOrderDto>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspGetServedOrders]"))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;

                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                orders.Add(MapServedOrder(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while fetching served orders.", ex);
            }

            return orders;
        }
        public async Task<IEnumerable<OrderItemDto>> GetOrderItemsAsync(long orderId)
        {
            var items = new List<OrderItemDto>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspGetOrderItemsForPayment]"))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        // Note: OrderId is BigInt in DB, so use SqlDbType.BigInt
                        command.Parameters.Add("@OrderId", SqlDbType.BigInt).Value = orderId;

                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                items.Add(MapOrderItem(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while fetching items for Order #{orderId}.", ex);
            }

            return items;
        }

        #region Private Helpers
        private ServedOrderDto MapServedOrder(IDataRecord record)
        {
            return new ServedOrderDto
            {
                // Use 'long' (Int64) for BigInt columns
                OpenAccountId = GetValue<long>(record, "OpenAccountId"),
                OrderId = GetValue<long>(record, "OrderId"),
                TableName = GetValue<string>(record, "TableName"),
                OrderNumber = GetValue<string>(record, "OrderNumber"),
                TotalAmount = GetValue<decimal>(record, "TotalAmount"),
                OrderDate = GetValue<DateTime>(record, "OrderDate")
            };
        }
        private OrderItemDto MapOrderItem(IDataRecord record)
        {
            return new OrderItemDto
            {
                OrderItemId = GetValue<long>(record, "OrderItemId"),
                VariantId = GetValue<int>(record, "VariantId"),
                ProductName = GetValue<string>(record, "ProductName"),
                VariantName = GetValue<string>(record, "VariantName"),
                Quantity = GetValue<int>(record, "Quantity"),
                UnitPrice = GetValue<decimal>(record, "UnitPrice"),
                DiscountAmount = GetValue<decimal>(record, "DiscountAmount"),
                Note = GetValue<string>(record, "Note"),
                OfferName = GetValue<string>(record, "OfferName"),
                IsFreeItem = GetValue<bool>(record, "IsFreeItem")
            };
        }
        #endregion
    }
}
