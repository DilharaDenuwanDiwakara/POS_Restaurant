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
        public async Task<string> ImportOpeningStockAsync(string filePath, int userId)
        {
            EnsureFileSizeIsAllowed(filePath);
            EnsureFileIsNotLocked(filePath);

            var stockItems = new List<OpenStockItemDto>();
            var errors = new List<string>();

            using (var workbook = new XLWorkbook(filePath))
            {
                var ws = workbook.Worksheet(1);
                var rows = ws.RangeUsed().RowsUsed().Skip(1); // Skip Header

                int rowNum = 2; // Start from row 2 for error reporting

                foreach (var row in rows)
                {
                    // 1. Read Barcode (Column 1)
                    string barcode = (string)GetString(row.Cell(1));

                    // Skip empty rows
                    if (string.IsNullOrWhiteSpace(barcode))
                    {
                        rowNum++;
                        continue;
                    }

                    // 2. Read Values
                    decimal qty = GetDecimal(row.Cell(2));
                    decimal price = GetDecimal(row.Cell(3));
                    decimal cost = GetDecimal(row.Cell(4)); // Helper returns 0 if empty

                    // 3. Basic Validation
                    if (qty <= 0)
                    {
                        errors.Add($"Row {rowNum}: Quantity must be greater than 0.");
                    }
                    else if (price < 0)
                    {
                        errors.Add($"Row {rowNum}: Selling Price cannot be negative.");
                    }
                    else
                    {
                        // Add to list
                        stockItems.Add(new OpenStockItemDto
                        {
                            ProductCode = barcode,
                            Quantity = qty,
                            SellingPrice = price,
                            UnitCost = cost == 0 ? (decimal?)null : cost
                        });
                    }
                    rowNum++;
                }
            }

            // If Excel validation failed, return errors immediately
            if (errors.Any())
            {
                return "Import Failed with Validation Errors:\n" + string.Join("\n", errors.Take(10)); // Show top 10 errors
            }

            // If no data found
            if (!stockItems.Any())
            {
                return "No valid data found in the Excel file.";
            }

            // 4. Save to Database
            try
            {
                await _inventoryRepository.ImportOpeningStockAsync(stockItems, userId);
                return string.Empty; // Success
            }
            catch (Exception ex)
            {
                return $"Database Error: {ex.Message}";
            }
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
        #endregion

    }
}
