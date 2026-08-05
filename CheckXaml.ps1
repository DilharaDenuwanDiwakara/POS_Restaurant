$files = Get-ChildItem -Path 'PointOfSale.UI' -Filter '*.xaml' -Recurse
foreach ($f in $files) {
    try {
        $doc = New-Object System.Xml.XmlDocument
        $doc.Load($f.FullName)
    } catch {
        Write-Output "$($f.FullName): $($_.Exception.Message)"
    }
}
