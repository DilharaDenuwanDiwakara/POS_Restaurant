$ErrorActionPreference = 'Stop'
Add-Type -Path "$PSScriptRoot/PointOfSale.Core.dll"
Add-Type -Path "$PSScriptRoot/PointOfSale.Infrastructure.dll"
$testDatabase = 'ItemCodeVerification_' + [Guid]::NewGuid().ToString('N')
$master = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Database=master;Connect Timeout=30')
$testConnection = $null
function Sql($connection, $sql) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
function Variant($id, $code, $price) {
    $variant = New-Object PointOfSale.Core.DTOs.VariantPriceDto
    $variant.VariantId = $id
    $variant.ItemCode = $code
    $variant.Price = $price
    return $variant
}
function Save($variants) {
    $repository.UpdateVariantPricesAsync([PointOfSale.Core.DTOs.VariantPriceDto[]]$variants).GetAwaiter().GetResult()
}
function Reject($variants, $message) {
    try { Save $variants } catch {
        if ($_.Exception.ToString().Contains($message)) { return }
        throw
    }
    throw "Expected rejection: $message"
}
try {
    $master.Open()
    Sql $master "CREATE DATABASE [$testDatabase]" | Out-Null
    $testConnection = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Database=$testDatabase")
    $testConnection.Open()
    Sql $testConnection 'CREATE SCHEMA Restaurant' | Out-Null
    Sql $testConnection 'CREATE TABLE Restaurant.Variant (Id int PRIMARY KEY, MenuItemId int, ItemCode nvarchar(50) COLLATE Latin1_General_CI_AS, Name nvarchar(100), Price decimal(18,2)); INSERT Restaurant.Variant VALUES (1,1,N''A'',N''Standard'',100), (2,1,N''B'',N''Large'',200), (3,2,N''TAKEN'',N''Standard'',300);' | Out-Null
    $databaseConnection = [System.Runtime.Serialization.FormatterServices]::GetUninitializedObject([PointOfSale.Infrastructure.DatabaseConnection])
    [PointOfSale.Infrastructure.DatabaseConnection].GetField('_connectionString', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($databaseConnection, $testConnection.ConnectionString)
    $repository = New-Object PointOfSale.Infrastructure.Repositories.Restaurant.MenuItemRepository($databaseConnection)
    $loaded = @($repository.GetVariantsByItemIdAsync(1).GetAwaiter().GetResult())
    if ($loaded.Count -ne 2 -or $loaded[0].ItemCode -ne 'A') { throw 'Load failed' }
    Save @((Variant 1 'A' 110))
    Save @((Variant 1 '  NEW  ' 120))
    if ((Sql $testConnection 'SELECT ItemCode FROM Restaurant.Variant WHERE Id=1') -cne 'NEW') { throw 'Trim/save failed' }
    Reject @((Variant 1 'TAKEN' 999)) 'another variant'
    Reject @((Variant 1 'taken' 999)) 'another variant'
    Reject @((Variant 1 ' ' 999)) 'required'
    Reject @((Variant 1 'X' 999), (Variant 2 ' x ' 999)) 'more than one variant'
    Reject @((Variant 1 'VALID' 999), (Variant 2 'TAKEN' 999)) 'another variant'
    if ((Sql $testConnection 'SELECT ItemCode FROM Restaurant.Variant WHERE Id=1') -cne 'NEW' -or (Sql $testConnection 'SELECT Price FROM Restaurant.Variant WHERE Id=1') -ne 120) { throw 'Rollback failed' }
    Reject @((Variant 999 'UNUSED' 100)) 'no longer exists'
    Write-Output 'PASS: load codes; unchanged own code; trim and save; duplicate across menu items; SQL case-insensitive duplicate; blank code; duplicate edited rows; full batch rollback; missing variant.'
} finally {
    if ($testConnection) { $testConnection.Dispose() }
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    if ($master.State -eq 'Open') {
        Sql $master "IF DB_ID(N'$testDatabase') IS NOT NULL BEGIN ALTER DATABASE [$testDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$testDatabase]; END" | Out-Null
    }
    $master.Dispose()
}
