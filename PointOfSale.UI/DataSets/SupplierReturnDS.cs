using System;
using System.Data;

namespace PointOfSale.UI.DataSets
{
    public class SupplierReturnDS : DataSet
    {
        public SupplierReturnDS()
        {
            DataSetName = "SupplierReturnDS";
            Namespace = "http://tempuri.org/SupplierReturnDS.xsd";
            SchemaSerializationMode = SchemaSerializationMode.IncludeSchema;

            rptGetSupplierReturnNote = CreateSupplierReturnReportTable();
            Tables.Add(rptGetSupplierReturnNote);
        }

        public DataTable rptGetSupplierReturnNote { get; private set; }

        private static DataTable CreateSupplierReturnReportTable()
        {
            var table = new DataTable("rptGetSupplierReturnNote");

            AddColumn(table, "ReportTitle", typeof(string));
            AddColumn(table, "CompanyName", typeof(string));
            AddColumn(table, "CompanyRegisterNumber", typeof(string));
            AddColumn(table, "CompanyTaxRegistrationNumber", typeof(string));
            AddColumn(table, "CompanyAddress", typeof(string));
            AddColumn(table, "CompanyContactNumber", typeof(string));
            AddColumn(table, "CompanyEmail", typeof(string));
            AddColumn(table, "CompanyLogo", typeof(byte[]));

            AddColumn(table, "SupplierReturnId", typeof(int));
            AddColumn(table, "SupplierReturnNumber", typeof(string));
            AddColumn(table, "CreditNoteNumber", typeof(string));
            AddColumn(table, "OriginalInvoiceNumber", typeof(string));
            AddColumn(table, "OriginalInvoiceDate", typeof(DateTime));
            AddColumn(table, "ReturnedDate", typeof(DateTime));
            AddColumn(table, "CreatedDate", typeof(DateTime));
            AddColumn(table, "ApprovedAt", typeof(DateTime));
            AddColumn(table, "ReturnedBy", typeof(string));
            AddColumn(table, "CreatedByName", typeof(string));
            AddColumn(table, "ApprovedByName", typeof(string));
            AddColumn(table, "StorekeeperNote", typeof(string));
            AddColumn(table, "Status", typeof(string));

            AddColumn(table, "ReturnSubTotal", typeof(decimal));
            AddColumn(table, "ReturnDiscountAmount", typeof(decimal));
            AddColumn(table, "ReturnTaxAmount", typeof(decimal));
            AddColumn(table, "ReturnTotalAmount", typeof(decimal));

            AddColumn(table, "SupplierName", typeof(string));
            AddColumn(table, "SupplierRegisterNumber", typeof(string));
            AddColumn(table, "SupplierTaxRegistrationNumber", typeof(string));
            AddColumn(table, "SupplierAddress", typeof(string));
            AddColumn(table, "SupplierContactNumber", typeof(string));
            AddColumn(table, "SupplierEmail", typeof(string));

            AddColumn(table, "LineNo", typeof(long));
            AddColumn(table, "ProductCode", typeof(string));
            AddColumn(table, "ProductName", typeof(string));
            AddColumn(table, "UnitMeasureCode", typeof(string));
            AddColumn(table, "UnitMeasureName", typeof(string));
            AddColumn(table, "BatchId", typeof(long));
            AddColumn(table, "QuantityReturn", typeof(decimal));
            AddColumn(table, "UnitPrice", typeof(decimal));
            AddColumn(table, "LineDiscount", typeof(decimal));
            AddColumn(table, "LineTaxAmount", typeof(decimal));
            AddColumn(table, "ReturnReason", typeof(string));
            AddColumn(table, "LineTotal", typeof(decimal));

            return table;
        }

        private static void AddColumn(DataTable table, string columnName, Type dataType)
        {
            table.Columns.Add(new DataColumn(columnName, dataType)
            {
                AllowDBNull = true
            });
        }
    }
}
