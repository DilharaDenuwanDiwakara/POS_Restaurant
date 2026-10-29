param([string]$MSBuildPath)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
if (-not $MSBuildPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $MSBuildPath = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}
if (-not $MSBuildPath) { throw 'Visual Studio MSBuild is required.' }
$buildDirectory = Join-Path $env:TEMP ('pos-bank-promo-checks-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildDirectory | Out-Null
& $MSBuildPath (Join-Path $workspace 'PointOfSale.sln') /t:Build /p:Configuration=Debug '/p:Platform=Any CPU' "/p:OutDir=$buildDirectory/" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }
$compiler = Join-Path (Split-Path $MSBuildPath) 'Roslyn/csc.exe'
$executable = Join-Path $buildDirectory 'BankPromotionChecks.exe'
& $compiler /nologo /target:exe "/out:$executable" /reference:System.Data.dll /reference:System.Configuration.dll "/reference:$buildDirectory/PointOfSale.Core.dll" "/reference:$buildDirectory/PointOfSale.Infrastructure.dll" (Join-Path $PSScriptRoot 'BankPromotionChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Regression check compilation failed.' }
& $executable (Join-Path $workspace 'PointOfSale.Infrastructure/Database/Sales/BankPromotionSchema.sql')
if ($LASTEXITCODE -ne 0) { throw 'Bank promotion checks failed.' }
$framework = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8'
$references = @('PresentationCore', 'PresentationFramework', 'WindowsBase', 'System.Xaml', 'UIAutomationProvider', 'UIAutomationTypes') |
    ForEach-Object { "/reference:$(Join-Path $framework ($_ + '.dll'))" }
$viewExecutable = Join-Path $buildDirectory 'BankPromotionViewChecks.exe'
& $compiler /nologo /target:exe "/out:$viewExecutable" @references "/reference:$buildDirectory/PointOfSale.Core.dll" "/reference:$buildDirectory/PointOfSale.UI.exe" (Join-Path $PSScriptRoot 'BankPromotionViewChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'ViewModel check compilation failed.' }
Copy-Item -LiteralPath (Join-Path $buildDirectory 'PointOfSale.UI.exe.config') -Destination ($viewExecutable + '.config')
& $viewExecutable (Join-Path $buildDirectory 'BankPromotionPreview.png')
if ($LASTEXITCODE -ne 0) { throw 'Bank promotion ViewModel checks failed.' }
