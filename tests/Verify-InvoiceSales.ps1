param([string]$ConnectionString = 'Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;Connect Timeout=15')

$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent

# Run the production SQL against table variables; no application data is read or changed.
$source = Get-Content (Join-Path $workspace 'PointOfSale.Infrastructure/Repositories/Sales/DashboardRepository.cs') -Raw
$sql = [regex]::Match($source, 'const string sql = @"([\s\S]*?)";').Groups[1].Value
$overview = ($sql -split '-- RS2:')[0]
$daily = (($sql -split '-- RS3:[^\r\n]*')[1] -split '-- RS4:')[0]
$hourly = (($sql -split '-- RS5:[^\r\n]*')[1] -split '-- RS6:')[0]
$queries = ($overview + $daily + $hourly).Replace('[Sales].[Sales]', '@Sales').Replace('[Restaurant].[Order]', '@Orders')
$fixture = @'
DECLARE @Sales TABLE (
    SalesDate datetime2, SubTotal decimal(18,2), NetAmount decimal(18,2),
    DiscountAmount decimal(18,2), Status varchar(20), BranchId int, OrderId bigint
);
DECLARE @Orders TABLE (Id bigint, OrderDate datetime2, GuestCount int);
INSERT INTO @Sales VALUES
('2026-10-27T13:23:00',800,800,0,'COMPLETED',1,NULL),
('2026-10-27T12:51:00',350,350,0,'COMPLETED',1,NULL),
('2026-10-27T12:50:00',12250,0,12250,'COMPLETED',1,NULL),
('2026-10-27T11:41:00',1270,1143,127,'COMPLETED',1,NULL),
('2026-10-27T00:00:00',1300,1300,0,'COMPLETED',1,NULL),
('2026-10-27T14:00:00',1000,900,100,'COMPLETED',2,NULL),
('2026-10-27T15:00:00',5000,5000,0,'HOLD',1,NULL),
('2026-10-27T16:00:00',5000,5000,0,'VOID',1,NULL),
('2026-10-28T00:00:00',9000,9000,0,'COMPLETED',1,NULL),
('2026-10-26T23:59:59.9999999',100,90,10,'COMPLETED',1,NULL);
'@

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message : expected $Expected, received $Actual" }
}

$connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
try {
    $connection.Open()
    foreach ($scenario in @(
        @{ Branch = 1; From = '2026-10-27'; To = '2026-10-27'; Total = 15970; Today = 15970; Count = 5 },
        @{ Branch = 1; From = '2026-10-26'; To = '2026-10-26'; Total = 100; Today = 15970; Count = 5 },
        @{ Branch = 1; From = '2026-10-25'; To = '2026-10-25'; Total = 0; Today = 15970; Count = 5 },
        @{ Branch = $null; From = '2026-10-27'; To = '2026-10-27'; Total = 16970; Today = 16970; Count = 6 },
        @{ Branch = 99; From = '2026-10-27'; To = '2026-10-27'; Total = 0; Today = 0; Count = 0 }
    )) {
        $command = $connection.CreateCommand()
        $command.CommandText = $fixture + $queries
        $command.Parameters.Add('@BranchId', [System.Data.SqlDbType]::Int).Value = $(if ($null -eq $scenario.Branch) { [DBNull]::Value } else { $scenario.Branch })
        $dates = @{
            '@FromDate' = [datetime]$scenario.From
            '@ToDate' = [datetime]$scenario.To
            '@ToDateExclusive' = ([datetime]$scenario.To).AddDays(1)
            '@Today' = [datetime]'2026-10-27'
            '@Tomorrow' = [datetime]'2026-10-28'
        }
        foreach ($name in $dates.Keys) {
            $command.Parameters.Add($name, [System.Data.SqlDbType]::DateTime2).Value = $dates[$name]
        }
        $reader = $command.ExecuteReader()
        try {
            if (-not $reader.Read()) { throw 'Missing dashboard overview' }
            Assert-Equal $reader['TotalSales'] $scenario.Total 'Period sales must use subtotals'
            Assert-Equal $reader['TodaySales'] $scenario.Today 'Today sales must include fully discounted invoices'
            Assert-Equal $reader['TodayOrderCount'] $scenario.Count 'Today order count'
            if (-not $reader.NextResult()) { throw 'Missing daily sales' }
            $dailyTotal = 0D
            while ($reader.Read()) { $dailyTotal += $reader['SalesAmount'] }
            Assert-Equal $dailyTotal $scenario.Total 'Daily chart must reconcile with period sales'
            if (-not $reader.NextResult()) { throw 'Missing hourly sales' }
            $hourlyTotal = 0D
            $hourlyOrders = 0
            $hours = 0
            while ($reader.Read()) {
                $hourlyTotal += $reader['SalesAmount']
                $hourlyOrders += $reader['OrderCount']
                $hours++
            }
            Assert-Equal $hourlyTotal $scenario.Today 'Hourly chart must reconcile with today sales'
            Assert-Equal $hourlyOrders $scenario.Count 'Hourly orders must reconcile with today orders'
            Assert-Equal $hours 24 'Hourly chart must contain every hour'
        } finally { $reader.Dispose(); $command.Dispose() }
    }
} finally { $connection.Dispose() }

[xml]$view = Get-Content (Join-Path $workspace 'PointOfSale.UI/Views/Sales/SalesListView.xaml') -Raw
$namespaces = New-Object System.Xml.XmlNamespaceManager $view.NameTable
$namespaces.AddNamespace('w', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
$namespaces.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
$grid = $view.SelectSingleNode('//w:DataGrid[@x:Name="InvoicesDataGrid"]', $namespaces)
$amountColumns = @($grid.SelectNodes('w:DataGrid.Columns/w:DataGridTextColumn', $namespaces) | Where-Object { $_.Header -in @('Sub Total', 'Discount', 'Tax Amount', 'Net Amount') })
Assert-Equal ($amountColumns.Header -join ',') 'Sub Total,Discount,Tax Amount,Net Amount' 'Invoice amount column order'
Assert-Equal $amountColumns[0].Binding "{Binding SubTotal, StringFormat='Rs. {0:N2}'}" 'Subtotal binding'
Write-Output 'Invoice columns and five dashboard SQL scenarios passed.'
