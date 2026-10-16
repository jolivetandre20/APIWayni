# Restores a mysqldump (.sql or .sql.gz) into the compose MySQL (PowerShell 7+). Default: the 1,000-debtor sample.
# Usage: .\scripts\restore-backup.ps1   (from the repository root, after `docker compose up -d mysql`)
param([string]$Backup = "db\backup\wayni_deudores_sample_backup.sql")
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
$vars = @{}
Get-Content .env | Where-Object { $_ -match '^\s*[^#].*=' } | ForEach-Object { $k, $v = $_ -split '=', 2; $vars[$k.Trim()] = $v.Trim() }
$sql = Resolve-Path $Backup
if ($Backup.EndsWith(".gz")) {
    $sql = Join-Path $env:TEMP "wayni_restore.sql"
    $in = [IO.File]::OpenRead((Resolve-Path $Backup)); $gz = New-Object IO.Compression.GZipStream($in, [IO.Compression.CompressionMode]::Decompress)
    $out = [IO.File]::Create($sql); $gz.CopyTo($out); $out.Dispose(); $gz.Dispose(); $in.Dispose()
}
Write-Host "Restoring $Backup ..."
Get-Content -Raw -Encoding UTF8 $sql | docker compose exec -T mysql mysql -uroot "-p$($vars['MYSQL_ROOT_PASSWORD'])" --default-character-set=utf8mb4
docker compose exec -T mysql mysql -uroot "-p$($vars['MYSQL_ROOT_PASSWORD'])" -e "GRANT ALL PRIVILEGES ON $($vars['MYSQL_DATABASE']).* TO '$($vars['MYSQL_USER'])'@'%'; SELECT COUNT(*) AS deudores FROM $($vars['MYSQL_DATABASE']).deudores;"
