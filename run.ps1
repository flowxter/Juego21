<#
.SYNOPSIS
    Levanta todo lo necesario para jugar: base de datos y servidor.

.DESCRIPTION
    Arranca PostgreSQL en Docker, espera a que acepte conexiones de verdad y
    lanza el servidor. Deja la consola ocupada mostrando los registros; para
    parar, Ctrl+C.

    Después, abre client/BlackjackClient en Unity y dale a Play.

.EXAMPLE
    pwsh run.ps1
#>

[CmdletBinding()]
param(
    [int]$Port = 5199
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

Write-Host 'Levantando PostgreSQL...' -ForegroundColor Cyan
Push-Location $repoRoot
try {
    & docker compose up -d db | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'No se pudo levantar la base. ¿Está Docker Desktop abierto?'
    }
}
finally {
    Pop-Location
}

# No basta con que el contenedor esté "running": durante la inicialización
# Postgres levanta un servidor temporal que aún no tiene creado el rol.
Write-Host 'Esperando a que la base acepte conexiones...' -ForegroundColor Cyan
$ready = $false
foreach ($attempt in 1..30) {
    & docker exec blackjack-db psql -U blackjack -d blackjack -c 'SELECT 1' *> $null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 2
}

if (-not $ready) {
    throw 'La base no respondió a tiempo. Revisa: docker logs blackjack-db'
}

Write-Host 'Base lista.' -ForegroundColor Green
Write-Host ''

# Se escucha en todas las interfaces, no solo en localhost: de lo contrario
# ningún otro equipo de la red podría entrar en la mesa.
$lanAddress = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
    Select-Object -First 1 -ExpandProperty IPAddress

Write-Host "Servidor local:  http://localhost:$Port" -ForegroundColor Green

if ($lanAddress) {
    Write-Host "Desde otro equipo de tu red: http://${lanAddress}:$Port" -ForegroundColor Yellow
    Write-Host '  (pon esa direccion en el campo "Servidor" de la pantalla de acceso)' -ForegroundColor DarkYellow
    Write-Host '  Si no conecta, permite el puerto en el Firewall de Windows:' -ForegroundColor DarkYellow
    Write-Host "  New-NetFirewallRule -DisplayName 'Blackjack' -Direction Inbound -LocalPort $Port -Protocol TCP -Action Allow" -ForegroundColor DarkGray
}

Write-Host ''
Write-Host 'Ctrl+C para parar.' -ForegroundColor Green
Write-Host ''

$env:ASPNETCORE_URLS = "http://0.0.0.0:$Port"
& dotnet run --project (Join-Path $repoRoot 'src/Blackjack.Server') --no-launch-profile
