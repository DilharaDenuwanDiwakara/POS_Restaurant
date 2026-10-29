param(
    [Parameter(Mandatory = $true)]
    [string]$ConfigPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

# Use the deployed application's ActiveConnection, rather than a hard-coded database.
[xml]$configuration = Get-Content -LiteralPath (Resolve-Path -LiteralPath $ConfigPath).Path
$connectionName = ($configuration.configuration.appSettings.add | Where-Object key -eq 'ActiveConnection').value
if ([string]::IsNullOrWhiteSpace($connectionName)) { $connectionName = 'ActiveConnection' }
$setting = $configuration.configuration.connectionStrings.add | Where-Object name -eq $connectionName
if (-not $setting) { throw "Connection setting '$connectionName' was not found." }

$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($setting.connectionString)
Write-Output "Updating bank promotion schema: $connectionName / $($builder.InitialCatalog)"
$schemaPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'PointOfSale.Infrastructure/Database/Sales/BankPromotionSchema.sql'
$connection = New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 60
    # The idempotent SQL script wraps the upgrade in a transaction.
    $command.CommandText = [System.IO.File]::ReadAllText($schemaPath)
    [void]$command.ExecuteNonQuery()
    $command.CommandText = "SELECT COUNT(*) FROM [System].[BankBINMapping] WHERE BankId IS NOT NULL;"
    $count = $command.ExecuteScalar()
    Write-Output "Bank-level BIN schema ready. Saved BIN mappings: $count"
} finally {
    $connection.Dispose()
}
