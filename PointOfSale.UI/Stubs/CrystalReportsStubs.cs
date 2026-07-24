using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace CrystalDecisions.Shared
{
    public enum ExportFormatType
    {
        PortableDocFormat = 0
    }

    public sealed class ExportOptions
    {
    }

    public interface IParameterField
    {
    }

    public sealed class ConnectionInfo
    {
        public string ServerName { get; set; }
        public string DatabaseName { get; set; }
        public bool IntegratedSecurity { get; set; }
        public string UserID { get; set; }
        public string Password { get; set; }
    }

    public sealed class TableLogOnInfo
    {
        public ConnectionInfo ConnectionInfo { get; set; } = new ConnectionInfo();
    }

    public static class CachedReportConstants
    {
        public static readonly TimeSpan DEFAULT_TIMEOUT = TimeSpan.FromMinutes(5);
    }
}

namespace CrystalDecisions.ReportSource
{
    using CrystalDecisions.CrystalReports.Engine;

    public interface ICachedReport
    {
        ReportDocument CreateReport();
        string GetCustomizedCacheKey(RequestContext request);
    }

    public sealed class RequestContext
    {
    }
}

namespace CrystalDecisions.CrystalReports.Engine
{
    using CrystalDecisions.Shared;

    internal static class CrystalReportsStubSupport
    {
        public const string MissingCrystalReportsMessage =
            "Crystal Reports is not installed on this machine. Install the SAP Crystal Reports for .NET runtime or developer components to enable report export and printing.";
    }

    public class ReportDocument : Component
    {
        private readonly ParameterFieldDefinitions _parameterFields = new ParameterFieldDefinitions();

        public ReportDocument()
        {
            PrintOptions = new PrintOptions();
            Database = new Database();
            Subreports = new ReportDocumentCollection();
            DataDefinition = new DataDefinition(_parameterFields);
            ReportDefinition = new ReportDefinition();
        }

        public virtual string ResourceName { get; set; }

        public virtual bool NewGenerator { get; set; }

        public virtual string FullResourceName { get; set; }

        public PrintOptions PrintOptions { get; }

        public Database Database { get; }

        public ReportDocumentCollection Subreports { get; }

        public DataDefinition DataDefinition { get; }

        public ReportDefinition ReportDefinition { get; }

        public ParameterFieldDefinitions ParameterFields => _parameterFields;

        public virtual void Load(string reportPath)
        {
        }

        public virtual void SetDataSource(object dataSource)
        {
        }

        public virtual void SetParameterValue(string parameterName, object value)
        {
        }

        public virtual void ExportToDisk(ExportFormatType formatType, string filePath)
        {
            throw new CrystalReportsException(CrystalReportsStubSupport.MissingCrystalReportsMessage);
        }

        public virtual void PrintToPrinter(int nCopies, bool collated, int startPageN, int endPageN)
        {
            throw new CrystalReportsException(CrystalReportsStubSupport.MissingCrystalReportsMessage);
        }

        public virtual void Close()
        {
        }
    }

    public class ReportClass : ReportDocument
    {
    }

    public class CrystalReportsException : Exception
    {
        public CrystalReportsException(string message) : base(message)
        {
        }
    }

    public sealed class PrintOptions
    {
        public string PrinterName { get; set; }
    }

    public sealed class Database
    {
        public TableCollection Tables { get; } = new TableCollection();
    }

    public sealed class TableCollection : IEnumerable<Table>
    {
        private readonly List<Table> _tables = new List<Table>();

        public IEnumerator<Table> GetEnumerator()
        {
            return _tables.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class Table
    {
        public TableLogOnInfo LogOnInfo { get; } = new TableLogOnInfo();

        public void ApplyLogOnInfo(TableLogOnInfo logOnInfo)
        {
        }
    }

    public sealed class ReportDocumentCollection : IEnumerable<ReportDocument>
    {
        private readonly List<ReportDocument> _reports = new List<ReportDocument>();

        public IEnumerator<ReportDocument> GetEnumerator()
        {
            return _reports.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class DataDefinition
    {
        public DataDefinition(ParameterFieldDefinitions parameterFields)
        {
            ParameterFields = parameterFields;
        }

        public ParameterFieldDefinitions ParameterFields { get; }
    }

    public sealed class ReportDefinition
    {
        public SectionCollection Sections { get; } = new SectionCollection();
    }

    public sealed class SectionCollection
    {
        private readonly List<Section> _sections = new List<Section>();

        public Section this[int index]
        {
            get
            {
                while (_sections.Count <= index)
                {
                    _sections.Add(new Section());
                }

                return _sections[index];
            }
        }
    }

    public sealed class Section
    {
    }

    public sealed class ParameterFieldDefinitions : IEnumerable<ParameterFieldDefinition>
    {
        private readonly List<ParameterFieldDefinition> _fields = new List<ParameterFieldDefinition>();

        public ParameterFieldDefinition this[int index]
        {
            get
            {
                while (_fields.Count <= index)
                {
                    _fields.Add(new ParameterFieldDefinition($"Parameter{_fields.Count + 1}"));
                }

                return _fields[index];
            }
        }

        public ParameterFieldDefinition this[string name]
        {
            get
            {
                foreach (var field in _fields)
                {
                    if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return field;
                    }
                }

                return null;
            }
        }

        public IEnumerator<ParameterFieldDefinition> GetEnumerator()
        {
            return _fields.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class ParameterFieldDefinition : IParameterField
    {
        public ParameterFieldDefinition(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
