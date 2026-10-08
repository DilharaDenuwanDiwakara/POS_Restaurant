using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using PointOfSale.Core.Common;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Exception;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.Infrastructure.Service
{
    public class ExcelService : IExcelService
    {
        private readonly IProductRepository _productRepository;
        private readonly IInventoryRepository _inventoryRepository;

        public ExcelService(IProductRepository productRepository, IInventoryRepository inventoryRepository)
        {
            _productRepository = productRepository;
            _inventoryRepository = inventoryRepository;
        }

        #region Public
        public async Task<List<OpeningStockItemDto>> ImportOpeningStockAsync(string filePath, int userId)
        {
            EnsureFileSizeIsAllowed(filePath);
            EnsureFileIsNotLocked(filePath);

            var stockItems = new List<OpeningStockItemDto>();
            var errors = new List<string>();
            var activeProducts = (await _productRepository.GetAllAsync())
                .Where(p => p.IsActive && !string.IsNullOrWhiteSpace(p.ProductCode))
                .GroupBy(p => p.ProductCode.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            using (var workbook = new XLWorkbook(filePath))
            {
                var ws = workbook.Worksheet(1);
                var range = ws.RangeUsed();

                if (range == null)
                {
                    throw new InvalidOperationException("The Excel file does not contain any data.");
                }

                var headerRow = range.FirstRowUsed();
                var headerMap = headerRow.CellsUsed()
                    .Select(cell => new
                    {
                        Header = Convert.ToString(GetString(cell)),
                        ColumnNumber = cell.Address.ColumnNumber
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Header))
                    .GroupBy(x => x.Header.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().ColumnNumber, StringComparer.OrdinalIgnoreCase);

                var productCodeColumn = GetRequiredColumn(headerMap, "Product Code");
                var standardCostColumn = GetRequiredColumn(headerMap, "Standard Cost");
                var availableQuantityColumn = GetRequiredColumn(headerMap, "Available Quantity");

                var rows = range.RowsUsed().Skip(1);

                foreach (var row in rows)
                {
                    var rowNum = row.RowNumber();
                    var openingQuantity = GetDecimal(row.Cell(availableQuantityColumn));

                    if (openingQuantity <= 0)
                    {
                        continue;
                    }

                    var productCode = Convert.ToString(GetString(row.Cell(productCodeColumn)));
                    productCode = productCode == null ? string.Empty : productCode.Trim();

                    if (string.IsNullOrWhiteSpace(productCode))
                    {
                        errors.Add($"Row {rowNum}: Product Code is required when Available Quantity is greater than 0.");
                        continue;
                    }

                    Product product;
                    if (!activeProducts.TryGetValue(productCode, out product))
                    {
                        errors.Add($"Row {rowNum}: Product Code '{productCode}' was not found as an active product.");
                        continue;
                    }

                    stockItems.Add(new OpeningStockItemDto
                    {
                        ProductId = product.ProductId,
                        ProductCode = product.ProductCode,
                        ProductName = product.ProductName,
                        UnitCost = GetDecimal(row.Cell(standardCostColumn)),
                        OpeningQuantity = openingQuantity,
                        Uom = string.IsNullOrWhiteSpace(product.UnitMeasureName)
                            ? product.UnitMeasureCode
                            : product.UnitMeasureName
                    });
                }
            }

            if (errors.Any())
            {
                throw new InvalidOperationException(
                    "Opening stock import failed with validation errors:\n" + string.Join("\n", errors.Take(20)));
            }

            if (!stockItems.Any())
            {
                throw new InvalidOperationException("No opening stock rows were found. Only rows with Available Quantity greater than 0 are imported.");
            }

            return stockItems;
        }

        public void ExportSuppliers(IEnumerable<Supplier> suppliers, string filePath)
        {
            // Make sure the destination file isn't locked if they are overwriting an existing open file
            if (File.Exists(filePath))
            {
                EnsureFileIsNotLocked(filePath);
            }

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Suppliers");

                // 1. Create Headers
                string[] headers = {
                    "Supplier Code", "Name", "Tax Registration No", "Business Registration Number", "Primary Contact", "Primary Phone", "Primary Email",
                    "Address", "Payment Method", "Bank Name", "Bank Branch", "Account Number", "Account Name", "Is Credit", "Credit Limit", "Credit Period Days", "Active"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                // 2. Fill Data
                int row = 2;
                foreach (var supplier in suppliers)
                {
                    var primaryContact = supplier.Contacts?.FirstOrDefault(c => c.IsPrimary)
                        ?? supplier.Contacts?.FirstOrDefault();

                    worksheet.Cell(row, 1).Value = supplier.SupplierCode ?? "";
                    worksheet.Cell(row, 2).Value = supplier.SupplierName ?? "";
                    worksheet.Cell(row, 3).Value = supplier.TaxRegistrationNumber ?? "";
                    worksheet.Cell(row, 4).Value = supplier.BusinessRegistrationNumber ?? "";
                    worksheet.Cell(row, 5).Value = primaryContact?.ContactName ?? "";

                    // Format phone numbers as text so Excel doesn't remove leading zeros
                    worksheet.Cell(row, 6).Value = primaryContact?.PhoneNumber ?? "";

                    worksheet.Cell(row, 7).Value = primaryContact?.EmailAddress ?? "";
                    worksheet.Cell(row, 8).Value = supplier.Address ?? "";
                    worksheet.Cell(row, 9).Value = supplier.DefaultPaymentMethod?.ToString() ?? "";
                    worksheet.Cell(row, 10).Value = supplier.BankNameDisplay ?? "";
                    worksheet.Cell(row, 11).Value = supplier.BranchNameDisplay ?? "";
                    worksheet.Cell(row, 12).Value = supplier.AccountNumber ?? "";
                    worksheet.Cell(row, 13).Value = supplier.AccountName ?? "";

                    worksheet.Cell(row, 14).Value = supplier.IsCredit ? "Yes" : "No";
                    worksheet.Cell(row, 15).Value = supplier.CreditLimit.HasValue ? supplier.CreditLimit.Value.ToString("F2") : "N/A";
                    worksheet.Cell(row, 16).Value = supplier.CreditPeriodDays.HasValue ? supplier.CreditPeriodDays.Value.ToString() : "N/A";
                    worksheet.Cell(row, 17).Value = supplier.IsActive ? "Yes" : "No";

                    row++;
                }

                // 3. Auto-fit columns for a clean look
                worksheet.Columns().AdjustToContents();

                // 4. Save the file
                workbook.SaveAs(filePath);
            }
        }

        public void ExportProducts(IEnumerable<Product> products, string filePath)
        {
            products = products ?? Enumerable.Empty<Product>();

            if (File.Exists(filePath))
            {
                EnsureFileIsNotLocked(filePath);
            }

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Products");

                string[] headers =
                {
                    "Product Code", "Barcode", "Product Name", "Category", "Item Type", "Unit Measure",
                    "Standard Cost", "Available Quantity", "Reorder Point", "Additional Stock Quantity",
                    "Wastage %", "Purchasable", "Track Expiry", "Taxable", "Active"
                };

                for (var i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                var row = 2;
                foreach (var product in products)
                {
                    worksheet.Cell(row, 1).Value = product.ProductCode ?? string.Empty;
                    worksheet.Cell(row, 2).Value = product.Barcode ?? string.Empty;
                    worksheet.Cell(row, 3).Value = product.ProductName ?? string.Empty;
                    worksheet.Cell(row, 4).Value = product.CategoryName ?? string.Empty;
                    worksheet.Cell(row, 5).Value = product.ItemTypeName ?? string.Empty;
                    worksheet.Cell(row, 6).Value = product.UnitMeasureName ?? string.Empty;
                    worksheet.Cell(row, 7).Value = product.StandardCost;
                    worksheet.Cell(row, 8).Value = product.AvailableQuantity;
                    worksheet.Cell(row, 9).Value = product.ReorderPoint;
                    worksheet.Cell(row, 10).Value = product.AdditionalStockQuantity;
                    worksheet.Cell(row, 11).Value = product.WastagePercentage;
                    worksheet.Cell(row, 12).Value = product.IsPurchasable ? "Yes" : "No";
                    worksheet.Cell(row, 13).Value = product.TrackExpiry ? "Yes" : "No";
                    worksheet.Cell(row, 14).Value = product.IsTaxApplicable ? "Yes" : "No";
                    worksheet.Cell(row, 15).Value = product.IsActive ? "Yes" : "No";

                    row++;
                }

                worksheet.Range(1, 1, Math.Max(row - 1, 1), headers.Length).SetAutoFilter();
                worksheet.SheetView.FreezeRows(1);
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }
        }

        public void ExportMenuProfitability(IEnumerable<MenuProfitabilityDto> items, string filePath)
        {
            items = items ?? Enumerable.Empty<MenuProfitabilityDto>();

            if (File.Exists(filePath))
            {
                EnsureFileIsNotLocked(filePath);
            }

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Menu Profitability");

                string[] headers =
                {
                    "Category", "Item Code", "Menu Item", "Variant", "BOM Cost",
                    "Selling Price", "Gross Profit", "Food Cost %", "BOM Status"
                };

                for (var i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                var row = 2;
                foreach (var item in items)
                {
                    worksheet.Cell(row, 1).Value = item.CategoryName ?? string.Empty;
                    worksheet.Cell(row, 2).Value = item.ItemCode ?? string.Empty;
                    worksheet.Cell(row, 3).Value = item.MenuItemName ?? string.Empty;
                    worksheet.Cell(row, 4).Value = item.VariantName ?? string.Empty;
                    worksheet.Cell(row, 5).Value = item.TotalBOMCost;
                    worksheet.Cell(row, 6).Value = item.SellingPrice;
                    worksheet.Cell(row, 7).Value = item.GrossProfit;
                    worksheet.Cell(row, 8).Value = item.FoodCostPercentage / 100m;
                    worksheet.Cell(row, 9).Value = item.BOMStatus ?? string.Empty;

                    row++;
                }

                worksheet.Columns(5, 7).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Column(8).Style.NumberFormat.Format = "0.00%";
                worksheet.Range(1, 1, Math.Max(row - 1, 1), headers.Length).SetAutoFilter();
                worksheet.SheetView.FreezeRows(1);
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }
        }

        public void ExportSalesSummaryReport(DataTable data, string companyName, DateTime fromDate, DateTime toDate, string filePath)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (File.Exists(filePath))
            {
                EnsureFileIsNotLocked(filePath);
            }

            var categoryColumn = FindColumn(data, "ParentCategoryName", "ParentCategory", "CategoryName", "MenuCategoryName", "Category");
            var codeColumn = FindColumn(data, "Code", "ItemCode", "ProductCode", "MenuItemCode");
            var descriptionColumn = FindColumn(data, "Description", "ItemName", "ProductName", "MenuItemName", "Name");
            var packSizeColumn = FindColumn(data, "PackSize", "Pack", "PortionSize", "UnitMeasureName", "UOM", "UomName");
            var qtyColumn = FindColumn(data, "TotalQuantity", "ToDateQuantity", "ToDateQty", "SalesQuantity", "SalesQty", "TotalQty", "Qty", "Quantity");
            var qtyKgColumn = FindColumn(data, "TotalQuantityKg", "ToDateQuantityKg", "ToDateQtyKg", "QtyKg", "QtyKG", "QuantityKg", "QuantityKG", "QtyInKg");
            var amountColumn = FindColumn(data, "TotalAmount", "ToDateTotal", "SalesAmount", "Amount", "NetAmount");
            var vatColumn = FindColumn(data, "VatAmount", "VAT", "Vat", "TaxAmount");
            var averageColumn = FindColumn(data, "Average", "Avg", "AveragePrice", "AvgPrice");

            var returnQtyColumn = FindColumn(data, "CategoryToDateReturnQty", "CategoryReturnQty", "ReturnQty", "ToDateReturnQty");
            var returnQtyKgColumn = FindColumn(data, "CategoryToDateReturnQtyKg", "CategoryToDateReturnKg", "ReturnQtyKg", "ReturnQuantityKg");
            var returnTotalColumn = FindColumn(data, "CategoryToDateReturnTotal", "CategoryReturnTotal", "ReturnTotal", "ToDateReturnTotal");
            var returnVatColumn = FindColumn(data, "CategoryToDateReturnVat", "CategoryToDateReturnVAT", "CategoryReturnVat", "ReturnVat", "ReturnVAT");

            var cashColumn = FindColumn(data, "PayModeCashTotal", "CashTotal", "CashAmount");
            var cardColumn = FindColumn(data, "PayModeCardTotal", "CardTotal", "CardAmount");
            var bankTransferColumn = FindColumn(data, "PayModeBankTransferTotal", "BankTransferTotal", "BankTransferAmount");

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Sales Summary");
                var row = 1;

                worksheet.Range(row, 1, row, 8).Merge();
                worksheet.Cell(row, 1).Value = "SALES SUMMARY REPORT";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                worksheet.Cell(row, 1).Style.Font.FontSize = 14;
                worksheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                row++;

                worksheet.Range(row, 1, row, 8).Merge();
                worksheet.Cell(row, 1).Value = string.IsNullOrWhiteSpace(companyName) ? "Nexora" : companyName;
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                worksheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                row++;

                worksheet.Range(row, 1, row, 8).Merge();
                worksheet.Cell(row, 1).Value = $"From: {fromDate:dd/MM/yyyy}   To: {toDate:dd/MM/yyyy}";
                worksheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                row += 2;

                var summaries = new List<SalesCategoryExportSummary>();
                var groups = data.Rows.Cast<DataRow>()
                    .GroupBy(r => string.IsNullOrWhiteSpace(GetString(r, categoryColumn)) ? "UNCATEGORIZED" : GetString(r, categoryColumn))
                    .OrderBy(g => g.Key);

                foreach (var group in groups)
                {
                    var firstRow = row;
                    worksheet.Range(row, 1, row, 8).Merge();
                    worksheet.Cell(row, 1).Value = group.Key.ToUpperInvariant();
                    StyleSectionHeader(worksheet.Range(row, 1, row, 8));
                    row++;

                    WriteRow(worksheet, row, "CODE", "DESCRIPTION", "PACK SIZE", "QTY", "QTY(Kg)", "AMOUNT", "VAT", "AVERAGE");
                    StyleColumnHeader(worksheet.Range(row, 1, row, 8));
                    row++;

                    foreach (var item in group)
                    {
                        var qty = GetDecimal(item, qtyColumn);
                        var amount = GetDecimal(item, amountColumn);
                        var averageValue = GetDecimal(item, averageColumn);
                        var average = qty != 0m
                            ? amount / qty
                            : averageValue;

                        worksheet.Cell(row, 1).Value = GetString(item, codeColumn);
                        worksheet.Cell(row, 2).Value = GetString(item, descriptionColumn);
                        worksheet.Cell(row, 3).Value = GetString(item, packSizeColumn);
                        worksheet.Cell(row, 4).Value = qty;
                        worksheet.Cell(row, 5).Value = GetDecimal(item, qtyKgColumn);
                        worksheet.Cell(row, 6).Value = amount;
                        worksheet.Cell(row, 7).Value = GetDecimal(item, vatColumn);
                        worksheet.Cell(row, 8).Value = average;
                        row++;
                    }

                    var totalQty = group.Sum(item => GetDecimal(item, qtyColumn));
                    var totalQtyKg = group.Sum(item => GetDecimal(item, qtyKgColumn));
                    var totalAmount = group.Sum(item => GetDecimal(item, amountColumn));
                    var totalVat = group.Sum(item => GetDecimal(item, vatColumn));
                    var returnQty = GetFirstDecimal(group, returnQtyColumn);
                    var returnQtyKg = GetFirstDecimal(group, returnQtyKgColumn);
                    var returnAmount = GetFirstDecimal(group, returnTotalColumn);
                    var returnVat = GetFirstDecimal(group, returnVatColumn);
                    var netQty = totalQty - returnQty;
                    var netQtyKg = totalQtyKg - returnQtyKg;
                    var netAmount = totalAmount - returnAmount;
                    var netVat = totalVat - returnVat;

                    WriteSummaryRow(worksheet, row++, "TOTAL", totalQty, totalQtyKg, totalAmount, totalVat, totalQty == 0m ? 0m : totalAmount / totalQty, XLColor.FromHtml("#EEF3F8"));
                    WriteSummaryRow(worksheet, row++, "SALES RETURN", returnQty, returnQtyKg, returnAmount, returnVat, 0m, XLColor.FromHtml("#FFF4E5"));
                    WriteSummaryRow(worksheet, row++, "NET AMOUNT", netQty, netQtyKg, netAmount, netVat, netQty == 0m ? 0m : netAmount / netQty, XLColor.FromHtml("#EAF7EE"));

                    ApplyTableBorder(worksheet.Range(firstRow, 1, row - 1, 8));
                    row++;

                    summaries.Add(new SalesCategoryExportSummary
                    {
                        CategoryName = group.Key,
                        NetQty = netQty,
                        NetQtyKg = netQtyKg,
                        NetAmount = netAmount,
                        NetVat = netVat,
                        NetExclude = netAmount - netVat
                    });
                }

                row++;
                worksheet.Range(row, 1, row, 6).Merge();
                worksheet.Cell(row, 1).Value = "TOTAL AMOUNT IN ALL CATEGORIES";
                StyleSectionHeader(worksheet.Range(row, 1, row, 6));
                row++;

                var grandTotalFirstRow = row;
                WriteRow(worksheet, row, "CATEGORY", "QTY", "QTY(Kg)", "AMOUNT", "VAT", "NET EXCLUDE");
                StyleColumnHeader(worksheet.Range(row, 1, row, 6));
                row++;

                foreach (var summary in summaries)
                {
                    worksheet.Cell(row, 1).Value = summary.CategoryName;
                    worksheet.Cell(row, 2).Value = summary.NetQty;
                    worksheet.Cell(row, 3).Value = summary.NetQtyKg;
                    worksheet.Cell(row, 4).Value = summary.NetAmount;
                    worksheet.Cell(row, 5).Value = summary.NetVat;
                    worksheet.Cell(row, 6).Value = summary.NetExclude;
                    row++;
                }

                worksheet.Cell(row, 1).Value = "GRAND TOTAL";
                worksheet.Cell(row, 2).Value = summaries.Sum(s => s.NetQty);
                worksheet.Cell(row, 3).Value = summaries.Sum(s => s.NetQtyKg);
                worksheet.Cell(row, 4).Value = summaries.Sum(s => s.NetAmount);
                worksheet.Cell(row, 5).Value = summaries.Sum(s => s.NetVat);
                worksheet.Cell(row, 6).Value = summaries.Sum(s => s.NetExclude);
                worksheet.Range(row, 1, row, 6).Style.Font.Bold = true;
                worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF3F8");
                ApplyTableBorder(worksheet.Range(grandTotalFirstRow, 1, row, 6));
                row += 2;

                worksheet.Range(row, 1, row, 2).Merge();
                worksheet.Cell(row, 1).Value = "PAY MODE WISE TOTAL";
                StyleSectionHeader(worksheet.Range(row, 1, row, 2));
                row++;

                var payModeFirstRow = row;
                WritePayModeRow(worksheet, row++, "CASH", GetFirstDecimal(data.Rows.Cast<DataRow>(), cashColumn));
                WritePayModeRow(worksheet, row++, "CARD", GetFirstDecimal(data.Rows.Cast<DataRow>(), cardColumn));
                WritePayModeRow(worksheet, row++, "BANK TRANSFER", GetFirstDecimal(data.Rows.Cast<DataRow>(), bankTransferColumn));
                ApplyTableBorder(worksheet.Range(payModeFirstRow, 1, row - 1, 2));

                worksheet.Columns(4, 8).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Columns(2, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Column(4).Style.NumberFormat.Format = "#,##0.000";
                worksheet.Column(5).Style.NumberFormat.Format = "#,##0.000";
                worksheet.SheetView.FreezeRows(4);
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }
        }
        #endregion

        #region Private
        private void GenerateErrorExcel(string originalFile, List<ProductImportError> errors)
        {
            string errorFile = Path.Combine(
                Path.GetDirectoryName(originalFile),
                $"Product_Import_Errors_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Errors");

                ws.Cell(1, 1).Value = "RowNumber";
                ws.Cell(1, 2).Value = "ProductCode";
                ws.Cell(1, 3).Value = "Error";

                for (int i = 0; i < errors.Count; i++)
                {
                    ws.Cell(i + 2, 1).Value = errors[i].RowNumber;
                    ws.Cell(i + 2, 2).Value = errors[i].ProductCode;
                    ws.Cell(i + 2, 3).Value = errors[i].Error;
                }

                wb.SaveAs(errorFile);
            }
        }
        private void EnsureFileIsNotLocked(string filePath)
        {
            try
            {
                using (var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None))
                {
                }
            }
            catch (IOException)
            {
                throw new InvalidOperationException(
                    "The Excel file is currently open or locked. Please close it and try again.");
            }
        }

        private static void EnsureFileSizeIsAllowed(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > FileUploadConstraints.MaxFileSizeBytes)
            {
                throw new InvalidOperationException(FileUploadConstraints.BuildFileTooLargeMessage(fileInfo.Name));
            }
        }
        private static int GetRequiredColumn(Dictionary<string, int> headerMap, string headerName)
        {
            int columnNumber;
            if (headerMap.TryGetValue(headerName, out columnNumber))
            {
                return columnNumber;
            }

            throw new InvalidOperationException($"The Excel file is missing the required '{headerName}' column.");
        }
        private DataTable CreateProductDataTable()
        {
            var dt = new DataTable();

            dt.Columns.Add("CategoryId", typeof(int));
            dt.Columns.Add("BrandId", typeof(int));
            dt.Columns.Add("ProductCode", typeof(string));
            dt.Columns.Add("Barcode", typeof(string));
            dt.Columns.Add("ProductName", typeof(string));
            dt.Columns.Add("GenericName", typeof(string));
            dt.Columns.Add("UnitMeasureId", typeof(int));
            dt.Columns.Add("StandardCost", typeof(decimal));
            dt.Columns.Add("SellingPrice", typeof(decimal));
            dt.Columns.Add("ReorderPoint", typeof(int));
            dt.Columns.Add("MaxStockQuantity", typeof(int));
            dt.Columns.Add("ExpiryReminderDays", typeof(int));
            dt.Columns.Add("IsService", typeof(bool));
            dt.Columns.Add("IsActive", typeof(bool));

            return dt;
        }
        private decimal GetDecimal(IXLCell cell)
        {
            if (cell.IsEmpty()) return 0m;

            string value = cell.GetFormattedString().Trim();

            if (decimal.TryParse(value, out decimal result))
            {
                return result;
            }
            return 0m; // Fallback to 0 or throw specific error if strictly required
        }
        private int GetInt(IXLCell cell)
        {
            if (cell.IsEmpty()) return 0;

            string value = cell.GetFormattedString().Trim();

            if (int.TryParse(value, out int result))
            {
                return result;
            }
            return 0;
        }
        private bool GetBool(IXLCell cell)
        {
            if (cell.IsEmpty()) return false;
            // Handle "Yes"/"No", "1"/"0", "True"/"False"
            string val = cell.GetValue<string>().Trim();
            return val.Equals("1") || val.Equals("true", StringComparison.OrdinalIgnoreCase) || val.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
        private object GetString(IXLCell cell)
        {
            if (cell.IsEmpty()) return DBNull.Value;

            string value = cell.GetValue<string>().Trim();

            // If the cell had text but it was just spaces, return DBNull
            if (string.IsNullOrWhiteSpace(value))
            {
                return DBNull.Value;
            }

            return value;
        }

        private static string FindColumn(DataTable table, params string[] candidates)
        {
            if (table == null)
                return null;

            foreach (var candidate in candidates)
            {
                foreach (DataColumn column in table.Columns)
                {
                    if (string.Equals(column.ColumnName, candidate, StringComparison.OrdinalIgnoreCase))
                        return column.ColumnName;
                }
            }

            return null;
        }

        private static string GetString(DataRow row, string columnName)
        {
            if (row == null || string.IsNullOrEmpty(columnName) || row[columnName] == DBNull.Value)
                return string.Empty;

            return Convert.ToString(row[columnName])?.Trim() ?? string.Empty;
        }

        private static decimal GetDecimal(DataRow row, string columnName)
        {
            if (row == null || string.IsNullOrEmpty(columnName) || row[columnName] == DBNull.Value)
                return 0m;

            decimal value;
            return decimal.TryParse(Convert.ToString(row[columnName]), out value) ? value : 0m;
        }

        private static decimal GetFirstDecimal(IEnumerable<DataRow> rows, string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
                return 0m;

            foreach (var row in rows)
            {
                var value = GetDecimal(row, columnName);
                if (value != 0m)
                    return value;
            }

            return 0m;
        }

        private static void WriteRow(IXLWorksheet worksheet, int row, params object[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                worksheet.Cell(row, i + 1).Value = values[i]?.ToString() ?? string.Empty;
            }
        }

        private static void WriteSummaryRow(IXLWorksheet worksheet, int row, string label, decimal qty, decimal qtyKg, decimal amount, decimal vat, decimal average, XLColor fill)
        {
            worksheet.Range(row, 1, row, 3).Merge();
            worksheet.Cell(row, 1).Value = label;
            worksheet.Cell(row, 4).Value = qty;
            worksheet.Cell(row, 5).Value = qtyKg;
            worksheet.Cell(row, 6).Value = amount;
            worksheet.Cell(row, 7).Value = vat;
            worksheet.Cell(row, 8).Value = average;
            worksheet.Range(row, 1, row, 8).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 8).Style.Fill.BackgroundColor = fill;
        }

        private static void WritePayModeRow(IXLWorksheet worksheet, int row, string payMode, decimal amount)
        {
            worksheet.Cell(row, 1).Value = payMode;
            worksheet.Cell(row, 2).Value = amount;
            worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        }

        private static void StyleSectionHeader(IXLRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#243B6B");
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        private static void StyleColumnHeader(IXLRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#E9EDF5");
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        private static void ApplyTableBorder(IXLRange range)
        {
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private class SalesCategoryExportSummary
        {
            public string CategoryName { get; set; }
            public decimal NetQty { get; set; }
            public decimal NetQtyKg { get; set; }
            public decimal NetAmount { get; set; }
            public decimal NetVat { get; set; }
            public decimal NetExclude { get; set; }
        }
        #endregion

    }
}
