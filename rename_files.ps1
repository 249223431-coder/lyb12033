Get-ChildItem -Path "E:\team\team\static\uploads\MCS图纸\6号涂布机供风系统和引纸器电路图" -Filter "*.pdf" | ForEach-Object {
    $oldName = $_.Name
    $newName = $oldName -replace 'SGD31EH_', ''
    if ($newName -ne $oldName) {
        Rename-Item $_.FullName -NewName $newName
        Write-Host "重命名: $oldName -> $newName"
    }
}
Write-Host ""
Write-Host "完成！"