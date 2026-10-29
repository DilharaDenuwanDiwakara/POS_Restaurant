param([string]$MSBuildPath)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
if (-not $MSBuildPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $MSBuildPath = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}
if (-not $MSBuildPath) { throw 'Visual Studio MSBuild is required.' }
$buildDirectory = Join-Path $env:TEMP ('pos-batch-production-checks-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildDirectory | Out-Null
& $MSBuildPath (Join-Path $workspace 'PointOfSale.sln') /t:Build /p:Configuration=Debug '/p:Platform=Any CPU' "/p:OutDir=$buildDirectory/" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }
$compiler = Join-Path (Split-Path $MSBuildPath) 'Roslyn/csc.exe'
$framework = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8'
$references = @('PresentationCore', 'PresentationFramework', 'WindowsBase', 'System.Xaml', 'UIAutomationProvider', 'UIAutomationTypes') |
    ForEach-Object { "/reference:$(Join-Path $framework ($_ + '.dll'))" }
$references += "/reference:$(Join-Path $buildDirectory 'PointOfSale.Core.dll')"
$references += "/reference:$(Join-Path $buildDirectory 'PointOfSale.UI.exe')"
$executable = Join-Path $buildDirectory 'BatchProductionChecks.exe'
& $compiler /nologo /target:exe "/out:$executable" @references (Join-Path $PSScriptRoot 'BatchProductionChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Regression check compilation failed.' }
Copy-Item -LiteralPath (Join-Path $buildDirectory 'PointOfSale.UI.exe.config') -Destination ($executable + '.config')
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }

