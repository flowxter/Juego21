<#
.SYNOPSIS
    Compila las dependencias del cliente y las copia al proyecto Unity.

.DESCRIPTION
    Unity no consume paquetes NuGet, así que el proyecto puente tools/UnityPlugins
    los publica y este script lleva las DLL a Assets/Plugins/Blackjack.

    Hay que ejecutarlo tras cambiar Blackjack.Core o Blackjack.Protocol: el
    cliente usa la versión compilada, no el código fuente, así que sin esto
    Unity se queda con las reglas antiguas.

.EXAMPLE
    pwsh tools/sync-unity-plugins.ps1
#>

[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$bridgeProject = Join-Path $repoRoot 'tools/UnityPlugins/UnityPlugins.csproj'
$publishDir = Join-Path $repoRoot 'tools/UnityPlugins/publish'
$pluginsDir = Join-Path $repoRoot 'client/BlackjackClient/Assets/Plugins/Blackjack'

# Se copia TODO el resultado de la publicación.
#
# Es tentador omitir System.Memory, System.Buffers o
# System.Runtime.CompilerServices.Unsafe dando por hecho que Unity ya las trae.
# No las expone a los plugins: al faltar, System.Text.Json no resuelve sus
# referencias y arrastra en cascada a SignalR y a los ensamblados propios, con
# el proyecto entero sin cargar.
#
# Si alguna vez Unity avisa de "Multiple precompiled assemblies with the same
# name", esa concreta —y solo esa— se añade aquí.
$excluded = @(
    # El proyecto puente no tiene código propio: solo existe para arrastrar
    # los paquetes NuGet hasta aquí.
    'UnityPlugins.dll'
)

Write-Host 'Publicando dependencias...' -ForegroundColor Cyan
& dotnet publish $bridgeProject -c $Configuration -o $publishDir | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Falló la publicación de UnityPlugins (código $LASTEXITCODE)." }

if (-not (Test-Path $pluginsDir)) {
    New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null
}

# Se limpian las DLL anteriores para que una dependencia eliminada no quede
# rezagada en Unity y siga cargándose.
Get-ChildItem -Path $pluginsDir -Filter '*.dll' -ErrorAction SilentlyContinue | Remove-Item -Force

$copied = 0
$skipped = 0

foreach ($dll in Get-ChildItem -Path $publishDir -Filter '*.dll') {
    if ($excluded -contains $dll.Name) {
        $skipped++
        Write-Host "  omitida: $($dll.Name)" -ForegroundColor DarkGray
        continue
    }

    Copy-Item -Path $dll.FullName -Destination $pluginsDir -Force
    $copied++
}

Write-Host ''
Write-Host "$copied DLL copiadas a Assets/Plugins/Blackjack ($skipped omitidas)." -ForegroundColor Green
Write-Host 'Vuelve a Unity para que recompile.' -ForegroundColor Green
