using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class SalesRepository : BaseRepository, ISalesRepository
    {
        public SalesRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region Public method
        public async Task<long> CreateAsync(Sale sale)
        {
            if (sale.Lines != null && sale.Lines.Any())
            {
                sale.TaxAmount = sale.Lines.Sum(l => l.TaxAmount);

                // If you also added SSCL property to your Sale object, sum it here too:
                // sale.SsclAmount = sale.Lines.Sum(l => l.SsclAmount); 
            }
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "[Sales].[uspInsertSales]";

                AddMainParameters(command, sale);
                AddLineItemsParameter(command, sale.Lines);
                AddPaymentsParameter(command, sale.Payments);
                AddOutputParameter(command);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                var salesIdValue = command.Parameters["@SalesId"].Value;
                if (salesIdValue == DBNull.Value || Convert.ToInt64(salesIdValue) <= 0)
                    throw new InvalidOperationException("The sale was not saved. The database did not return a valid sales id.");

                return Convert.ToInt64(salesIdValue);
            }
        }

        public DataTable GetInvoiceData(long salesId)
        {
            DataTable dt = new DataTable();
            using (var conn = GetConnection())
            using (var cmd = CreateCommand(conn, "[Sales].[uspGetSalesInvoice]"))
            {
                cmd.Parameters.Add("@SalesId", SqlDbType.BigInt).Value = salesId;
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    dt.Load(reader);
                }
            }
            return dt;
        }

        public async Task<List<SalesListDto>> GetSalesListAsync(DateTime from, DateTime to, int? branchId, string paymentType)
        {
            var list = new List<SalesListDto>();

            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "[Sales].[uspGetSalesList]";

                // Parameters
                command.Parameters.AddWithValue("@FromDate", from);
                command.Parameters.AddWithValue("@ToDate", to);
                command.Parameters.AddWithValue("@BranchId", (object)branchId ?? DBNull.Value);

                // Handle "All" or null logic for PaymentType
                object pType = string.IsNullOrEmpty(paymentType) || paymentType == "All"
                               ? (object)DBNull.Value
                               : paymentType;
                command.Parameters.AddWithValue("@PaymentType", pType);

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    var salesIdOrdinal = reader.GetOrdinal("SalesId");
                    var invoiceNumberOrdinal = reader.GetOrdinal("InvoiceNumber");
                    var salesDateOrdinal = reader.GetOrdinal("SalesDate");
                    var customerNameOrdinal = reader.GetOrdinal("CustomerName");
                    var salesPersonOrdinal = reader.GetOrdinal("SalesPerson");
                    var totalAmountOrdinal = reader.GetOrdinal("TotalAmount");
                    var discountOrdinal = reader.GetOrdinal("Discount");
                    var netAmountOrdinal = reader.GetOrdinal("NetAmount");
                    var cashOrdinal = reader.GetOrdinal("Cash");
                    var creditAmountOrdinal = reader.GetOrdinal("CreditAmount");
                    var paymentStatusOrdinal = reader.GetOrdinal("PaymentStatus");

                    while (await reader.ReadAsync())
                    {
                        list.Add(new SalesListDto
                        {
                            SalesId = Convert.ToInt64(reader.GetValue(salesIdOrdinal)),
                            InvoiceNumber = GetStringOrDefault(reader, invoiceNumberOrdinal),
                            SalesDate = reader.GetDateTime(salesDateOrdinal),
                            CustomerName = GetStringOrDefault(reader, customerNameOrdinal, "Walk-in"),
                            SalesPerson = GetStringOrDefault(reader, salesPersonOrdinal),
                            TotalAmount = GetDecimalOrDefault(reader, totalAmountOrdinal),
                            Discount = GetDecimalOrDefault(reader, discountOrdinal),
                            NetAmount = GetDecimalOrDefault(reader, netAmountOrdinal),
                            Cash = GetDecimalOrDefault(reader, cashOrdinal),
                            CreditAmount = GetDecimalOrDefault(reader, creditAmountOrdinal),
                            PaymentStatus = GetStringOrDefault(reader, paymentStatusOrdinal)
                        });
                    }
                }
            }

            return list;
        }

        public async Task<List<SalesLineItemDto>> GetSalesLineItemsAsync(long salesId)
        {
            var list = new List<SalesLineItemDto>();

            const string sql = @"
SELECT
    COALESCE(
        NULLIF(
            LTRIM(RTRIM(
                CASE
                    WHEN ISNULL(v.[Name], '') <> '' AND ISNULL(mi.[Name], '') <> ''
                        THEN mi.[Name] + ' - ' + v.[Name]
                    ELSE ISNULL(mi.[Name], '')
                END)), ''),
        NULLIF(p.[Name], ''),
        CONCAT('Item #', CAST(sl.[ProductId] AS NVARCHAR(20)))) AS ItemName,
    CAST(sl.[Quantity] AS DECIMAL(18, 3)) AS Quantity,
    CAST(sl.[UnitPrice] AS DECIMAL(18, 2)) AS UnitPrice,
    CAST(ISNULL(sl.[LineTotal], (sl.[UnitPrice] * sl.[Quantity]) - ISNULL(sl.[DiscountAmount], 0)) AS DECIMAL(18, 2)) AS LineTotal
FROM [Sales].[SalesLine] sl
LEFT JOIN [Restaurant].[Variant] v ON v.[Id] = sl.[ProductId]
LEFT JOIN [Restaurant].[MenuItem] mi ON mi.[Id] = v.[MenuItemId]
LEFT JOIN [Inventory].[Product] p ON p.[Id] = sl.[ProductId]
WHERE sl.[SalesId] = @SalesId
ORDER BY sl.[Id];";

            using (var connection = GetConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.CommandType = CommandType.Text;
                command.Parameters.Add("@SalesId", SqlDbType.BigInt).Value = salesId;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new SalesLineItemDto
                        {
                            ItemName = GetValue<string>(reader, "ItemName"),
                            Quantity = GetValue<decimal>(reader, "Quantity"),
                            UnitPrice = GetValue<decimal>(reader, "UnitPrice"),
                            LineTotal = GetValue<decimal>(reader, "LineTotal")
                        });
                    }
                }
            }

            return list;
        }

        #endregion

        #region Private method
        private void AddMainParameters(SqlCommand command, Sale sale)
        {
            command.Parameters.AddWithValue("@BranchId", sale.BranchId);
            command.Parameters.AddWithValue("@CustomerId", (object)sale.CustomerId ?? DBNull.Value);
            command.Parameters.AddWithValue("@TotalAmount", sale.TotalAmount);
            command.Parameters.AddWithValue("@Discount", sale.Discount);
            command.Parameters.AddWithValue("@CashGiven", sale.CashGiven);
            command.Parameters.AddWithValue("@IsTaxInvoice", sale.IsTaxInvoice);
            command.Parameters.AddWithValue("@TaxInvoiceNumber", (object)sale.TaxInvoiceNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("@CreatedBy", sale.CreatedBy);

            command.Parameters.AddWithValue("@OrderId", (object)sale.OrderId ?? DBNull.Value);
            command.Parameters.AddWithValue("@TaxAmount", sale.TaxAmount);
            command.Parameters.AddWithValue("@ServiceChargeAmount", sale.ServiceChargeAmount);
            command.Parameters.AddWithValue("@ShiftId", (object)sale.ShiftId ?? DBNull.Value);
            // command.Parameters.AddWithValue("@RestaurantOrderId", (object)sale.LinkedOrderId ?? DBNull.Value);
        }

        private void AddPaymentsParameter(SqlCommand command, IEnumerable<PaymentDetail> payments)
        {
            var dataTable = new DataTable();

            var paymentTerminalIdColumn = dataTable.Columns.Add("PaymentTerminalId", typeof(int));
            paymentTerminalIdColumn.AllowDBNull = true;

            var paymentMethodColumn = dataTable.Columns.Add("PaymentMethod", typeof(string));
            paymentMethodColumn.AllowDBNull = false;

            var amountColumn = dataTable.Columns.Add("Amount", typeof(decimal));
            amountColumn.AllowDBNull = false;

            var referenceNumberColumn = dataTable.Columns.Add("ReferenceNumber", typeof(string));
            referenceNumberColumn.AllowDBNull = true;

            foreach (var payment in payments ?? new List<PaymentDetail>())
            {
                dataTable.Rows.Add(
                    (object)payment.PaymentTerminalId ?? DBNull.Value,
                    payment.PaymentMethod,
                    payment.Amount,
                    (object)payment.ReferenceNumber ?? DBNull.Value);
            }

            var param = command.Parameters.AddWithValue("@Payments", dataTable);
            param.SqlDbType = SqlDbType.Structured;
            param.TypeName = "[Sales].[PaymentListType]";
        }

        private void AddLineItemsParameter(SqlCommand command, IEnumerable<SalesLine> lines)
        {
            var table = new DataTable();
            // MATCH THIS EXACTLY TO YOUR SQL TYPE [Sales].[SalesLineType]
            table.Columns.Add("ProductId", typeof(int));
            table.Columns.Add("Quantity", typeof(decimal));
            table.Columns.Add("UnitPrice", typeof(decimal));
            table.Columns.Add("DiscountAmount", typeof(decimal));
            table.Columns.Add("TaxAmount", typeof(decimal));

            foreach (var line in lines)
            {
                table.Rows.Add(
                    line.ProductId,       // This is the VariantId
                    line.Quantity,
                    line.UnitPrice,
                    line.LineDiscount,    // Mapped to DiscountAmount
                    line.TaxAmount
                );
            }

            var param = command.Parameters.AddWithValue("@SalesLines", table);
            param.SqlDbType = SqlDbType.Structured;
            param.TypeName = "[Sales].[SalesLineType]";
        }
        private void AddOutputParameter(SqlCommand command)
        {
            var outputParam = new SqlParameter("@SalesId", SqlDbType.BigInt)
            {
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(outputParam);
        }

        private static decimal GetDecimalOrDefault(SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? 0m
                : Convert.ToDecimal(reader.GetValue(ordinal));
        }

        private static string GetStringOrDefault(SqlDataReader reader, int ordinal, string defaultValue = "")
        {
            return reader.IsDBNull(ordinal)
                ? defaultValue
                : reader.GetValue(ordinal).ToString();
        }
        #endregion
    }
}
