param([string]$MSBuildPath = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
$output = Join-Path $env:TEMP ('pos-bank-checkout-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
& $MSBuildPath (Join-Path $workspace 'PointOfSale.sln') /t:Build /p:Configuration=Debug "/p:OutDir=$output/" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$framework = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8'
$references = @('PresentationCore','PresentationFramework','WindowsBase','System.Xaml') | ForEach-Object { "/reference:$(Join-Path $framework ($_ + '.dll'))" }
& (Join-Path (Split-Path $MSBuildPath) 'Roslyn/csc.exe') /nologo /target:exe "/out:$output/BankCheckoutChecks.exe" @references /reference:System.Data.dll /reference:System.Configuration.dll "/reference:$output/PointOfSale.Core.dll" "/reference:$output/PointOfSale.UI.exe" (Join-Path $PSScriptRoot 'BankCheckoutChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& "$output/BankCheckoutChecks.exe"
if ($LASTEXITCODE -ne 0) { throw 'Checkout tests failed.' }

Add-Type -AssemblyName System.Data
$db = 'BankCheckoutChecks_' + [guid]::NewGuid().ToString('N')
$master = New-Object System.Data.SqlClient.SqlConnection('Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True')
function Invoke-Sql($Connection, [string]$Sql) {
    $command = $Connection.CreateCommand()
    try { $command.CommandText = $Sql; $command.CommandTimeout = 60; [void]$command.ExecuteNonQuery() }
    finally { $command.Dispose() }
}
function Assert-Sql($Connection, [string]$Condition, [string]$Message) {
    Invoke-Sql $Connection "IF NOT ($Condition) THROW 59000, '$Message', 1;"
}
$connection = $null
try {
    $master.Open()
    Invoke-Sql $master "CREATE DATABASE [$db];"
    $connection = New-Object System.Data.SqlClient.SqlConnection("Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=$db;Integrated Security=True")
    $connection.Open()
    $fixture = Get-Content (Join-Path $PSScriptRoot 'BankPromotionCheckoutFixture.sql') -Raw
    foreach ($batch in [regex]::Split($fixture, '(?m)^GO\s*$')) { if ($batch.Trim()) { Invoke-Sql $connection $batch } }
    $sqlDirectory = Join-Path $workspace 'PointOfSale.Infrastructure/Database/Sales'
    Invoke-Sql $connection (Get-Content "$sqlDirectory/BankPromotionSchema.sql" -Raw)
    Invoke-Sql $connection (Get-Content "$sqlDirectory/BankPromotionCheckoutSchema.sql" -Raw)
    Invoke-Sql $connection (Get-Content "$sqlDirectory/BankPromotionCheckoutSchema.sql" -Raw)
    Invoke-Sql $connection (Get-Content "$sqlDirectory/uspFinalizeSale.BankPromotion.sql" -Raw)
    Invoke-Sql $connection @'
INSERT INTO [Sales].[BankPromotion](BankId,PromoName,DiscountPercentage,IsActive,ValidFrom,ValidTo,RequiredCardType)
VALUES(1,'Test offer',10,1,DATEADD(DAY,-1,GETDATE()),DATEADD(DAY,1,GETDATE()),'CREDIT');
INSERT INTO [System].[BankBINMapping](BankId,BINPrefix,IsActive,CardType) VALUES(1,'012345',1,'CREDIT');
INSERT INTO [Sales].[BankPromoExcludedVariant] VALUES(1,102);
UPDATE [Sales].[Sales] SET PromoType='BANK',PromoBankId=1,BankPromotionId=1,BankDiscountAmount=10;
'@
    $cases = @(
        @{ Payments = "(1,'CARD',370,'1234')"; Error = 52031 },
        @{ Payments = "(1,'CARD',370,'0123456')"; Error = 52031 },
        @{ Payments = "(1,'CARD',370,'12a456')"; Error = 52031 },
        @{ Payments = "(1,'CARD',370,NULL)"; Error = 52031 },
        @{ Payments = ''; Error = 52030 },
        @{ Payments = "(1,'CARD',200,'012345'),(1,'CARD',170,'5678')"; Error = 52030 },
        @{ Payments = "(NULL,'CASH',370,NULL)"; Error = 52030 },
        @{ Payments = "(NULL,'CREDIT',370,NULL)"; Error = 52030 },
        @{ Payments = "(NULL,'BANK_TRANSFER',370,NULL)"; Error = 52030 },
        @{ Payments = "(1,NULL,370,'012345')"; Error = 52030 },
        @{ Payments = "(1,'CARD',369,'012345')"; Error = 52034 },
        @{ Payments = "(1,'CARD',371,'012345')"; Error = 52034 },
        @{ Payments = "(1,'CARD',370,'012345')"; Setup = 'UPDATE Sales.Sales SET PromoBankId=2'; Cleanup = 'UPDATE Sales.Sales SET PromoBankId=1'; Error = 52032 },
        @{ Payments = "(1,'CARD',370,'012345')"; Setup = 'UPDATE Sales.Sales SET PromoBankId=NULL'; Cleanup = 'UPDATE Sales.Sales SET PromoBankId=1'; Error = 52031 },
        @{ Payments = "(1,'CARD',370,'012345')"; Setup = 'UPDATE Sales.BankPromotion SET ValidTo=DATEADD(SECOND,-1,GETDATE())'; Cleanup = 'UPDATE Sales.BankPromotion SET ValidTo=DATEADD(DAY,1,GETDATE())'; Error = 52032 },
        @{ Payments = "(1,'CARD',370,'012345')"; Setup = 'UPDATE Sales.Sales SET DiscountAmount=15'; Cleanup = 'UPDATE Sales.Sales SET DiscountAmount=10'; Error = 52033 },
        @{ Payments = "(1,'CARD',370,'012345')"; Setup = 'UPDATE Sales.Sales SET BankDiscountAmount=20'; Cleanup = 'UPDATE Sales.Sales SET BankDiscountAmount=10'; Error = 52033 },
        @{ Payments = "(1,'CARD',370,'012345')"; Setup = "DELETE FROM System.AccountMapping WHERE MappingKey='BANK_PROMO_EXPENSE'"; Cleanup = "INSERT INTO System.AccountMapping VALUES('BANK_PROMO_EXPENSE',6)"; Error = 52036 }
    )
    foreach ($case in $cases) {
        if ($case.Setup) { Invoke-Sql $connection $case.Setup }
        $insert = if ($case.Payments) { 'INSERT INTO @p VALUES ' + $case.Payments + ';' } else { '' }
        try {
            Invoke-Sql $connection "DECLARE @p Sales.PaymentListType; $insert EXEC Sales.uspFinalizeSale @SalesId=1,@CreatedBy=1,@Payments=@p;"
            throw "Expected SQL rejection $($case.Error)"
        } catch [System.Data.SqlClient.SqlException] {
            if ($_.Exception.Number -ne $case.Error) { throw }
        } finally {
            if ($case.Cleanup) { Invoke-Sql $connection $case.Cleanup }
        }
        Assert-Sql $connection '(SELECT COUNT(*) FROM Sales.Payment)=0 AND (SELECT COUNT(*) FROM Accounts.JournalEntry)=0 AND (SELECT SUM(AvailableQuantity) FROM Inventory.LocationStock)=300' 'Rejected promotion must leave payments stock and ledger unchanged'
    }
    Invoke-Sql $connection "DECLARE @p Sales.PaymentListType; INSERT INTO @p VALUES(1,'CARD',370,'012345'); EXEC Sales.uspFinalizeSale @SalesId=1,@CreatedBy=1,@Payments=@p;"
    Assert-Sql $connection "(SELECT PaymentStatus FROM Sales.Sales WHERE Id=1)='PAID' AND (SELECT BalanceAmount FROM Sales.Sales WHERE Id=1)=0" 'Discounted invoice must be fully paid'
    Assert-Sql $connection '(SELECT COUNT(*) FROM Sales.Payment)=1 AND (SELECT SUM(AvailableQuantity) FROM Inventory.LocationStock)=297' 'Valid bank invoice must post exactly once'
    Assert-Sql $connection '(SELECT SUM(DebitAmount) FROM Accounts.JournalEntryLine WHERE AccountId=6)=10 AND (SELECT SUM(DebitAmount) FROM Accounts.JournalEntryLine WHERE AccountId=5)=20' 'Bank and item discounts must use separate accounts'
    Assert-Sql $connection "(SELECT COUNT(*) FROM Accounts.JournalEntryLine WHERE Memo LIKE 'System Auto-Balancing%')=0 AND (SELECT SUM(DebitAmount-CreditAmount) FROM Accounts.JournalEntryLine)=0" 'Bank journal must balance without hidden adjustments'
    try {
        Invoke-Sql $connection "DECLARE @p Sales.PaymentListType; INSERT INTO @p VALUES(1,'CARD',370,'012345'); EXEC Sales.uspFinalizeSale @SalesId=1,@CreatedBy=1,@Payments=@p;"
        throw 'Expected duplicate bank settlement rejection'
    } catch [System.Data.SqlClient.SqlException] { if ($_.Exception.Number -ne 52038) { throw } }
    Assert-Sql $connection '(SELECT COUNT(*) FROM Sales.Payment)=1 AND (SELECT SUM(AvailableQuantity) FROM Inventory.LocationStock)=297' 'Duplicate settlement must not change stock or payments'
    Invoke-Sql $connection @'
UPDATE Sales.BankPromotion SET DiscountPercentage=50 WHERE Id=1;
INSERT INTO Sales.Sales(Id,BranchId,InvoiceNumber,SubTotal,DiscountAmount,TaxAmount,ServiceCharge,PromoType,PromoBankId,BankPromotionId,BankDiscountAmount)
VALUES(2,1,'TEST-FRACTION',1.01,0.50,0,0,'BANK',1,1,0.50);
INSERT INTO Sales.SalesLine VALUES(2,101,1.005,1,0,0);
DECLARE @p Sales.PaymentListType; INSERT INTO @p VALUES(1,'CARD',0.51,'012345');
EXEC Sales.uspFinalizeSale @SalesId=2,@CreatedBy=1,@Payments=@p;
UPDATE Sales.BankPromotion SET DiscountPercentage=10 WHERE Id=1;
INSERT INTO Sales.Sales(Id,BranchId,InvoiceNumber,SubTotal,DiscountAmount,TaxAmount,ServiceCharge,PromoType)
VALUES(3,1,'TEST-CASH',100,10,0,0,'CASH');
INSERT INTO Sales.SalesLine VALUES(3,101,1,100,0,0);
DECLARE @cash Sales.PaymentListType; INSERT INTO @cash VALUES(NULL,'CASH',50,NULL),(1,'CARD',40,'012345');
EXEC Sales.uspFinalizeSale @SalesId=3,@CreatedBy=1,@Payments=@cash;
'@
    Assert-Sql $connection '(SELECT BalanceAmount FROM Sales.Sales WHERE Id=2)=0 AND (SELECT SUM(l.DebitAmount) FROM Accounts.JournalEntryLine l JOIN Accounts.JournalEntry j ON j.Id=l.JournalEntryId WHERE j.SourceId=2 AND l.AccountId=6)=0.50' 'Fractional eligible quantities must match CSharp rounding'
    Assert-Sql $connection '(SELECT BalanceAmount FROM Sales.Sales WHERE Id=3)=0 AND (SELECT COUNT(*) FROM Sales.Payment WHERE SalesId=3)=2' 'Ordinary invoices must retain split payments'
    & (Join-Path (Split-Path $MSBuildPath) 'Roslyn/csc.exe') /nologo /target:exe "/out:$output/BankCheckoutPersistenceChecks.exe" /reference:System.Data.dll /reference:System.Configuration.dll "/reference:$output/PointOfSale.Core.dll" "/reference:$output/PointOfSale.Infrastructure.dll" (Join-Path $PSScriptRoot 'BankCheckoutPersistenceChecks.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Persistence test compilation failed.' }
    & "$output/BankCheckoutPersistenceChecks.exe" $connection.ConnectionString
    if ($LASTEXITCODE -ne 0) { throw 'Persistence tests failed.' }
    Write-Output "Checkout SQL: $($cases.Count) rejection/rollback scenarios, full bank settlement, duplicate rejection, fractional rounding and ordinary split payments passed."
} finally {
    if ($connection) { $connection.Dispose() }
    if ($master.State -eq 'Open') {
        Invoke-Sql $master "IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END;"
    }
    $master.Dispose()
}
