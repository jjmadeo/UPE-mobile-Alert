<#
.SYNOPSIS
  Dispara una alerta real contra el backend (POST /api/alerts), simulando
  al backend PROPIO de un cuartel — equivalente Windows de send-alert.sh,
  sin depender de Node.

.DESCRIPTION
  Requiere: backend/ corriendo (`docker compose up -d` en backend/) y la
  app logueada al menos una vez contra ESE backend (para que el device
  token quede registrado — la URL configurada en la app, campo "Servidor"
  del login, tiene que apuntar a la misma IP:puerto que -BackendUrl acá).

.EXAMPLE
  .\send-alert.ps1

.EXAMPLE
  .\send-alert.ps1 -Title "Incendio" -Message "Depósito de químicos" -Address "Av. Siempre Viva 742"

.EXAMPLE
  .\send-alert.ps1 -FirefighterIds 1,2

.EXAMPLE
  .\send-alert.ps1 -Lat -32.89 -Lng -68.84

.EXAMPLE
  .\send-alert.ps1 -BackendUrl "http://192.168.0.28:5080"
#>
param(
    [string]$BackendUrl = $(if ($env:BACKEND_URL) { $env:BACKEND_URL } else { "http://localhost:5080" }),
    [string]$ApiKey = "demo-central-CAMBIAR-EN-SERIO-esto-es-solo-para-dev",
    [string]$InstitutionCode = "BOMBEROS-CENTRAL",
    [string]$Username = "juan",
    [string]$Password = "1234",
    [string]$Title = "Incendio estructural",
    [string]$Message = "Se solicita apoyo urgente.",
    [string]$Address = "Av. Siempre Viva 742",
    [Nullable[double]]$Lat = $null,
    [Nullable[double]]$Lng = $null,
    [int[]]$FirefighterIds = $null,
    [string]$CorrelationId = ""
)

$ErrorActionPreference = "Stop"

# Sin -FirefighterIds, resuelve el id del usuario de prueba logueándose —
# así el script sigue andando aunque se resetee la base (los ids
# autoincrementales pueden cambiar).
if (-not $FirefighterIds) {
    $loginBody = @{
        institutionCode = $InstitutionCode
        username        = $Username
        password        = $Password
    } | ConvertTo-Json

    try {
        $loginResponse = Invoke-RestMethod -Method Post -Uri "$BackendUrl/api/auth/login" `
            -ContentType "application/json" -Body $loginBody
    } catch {
        Write-Error "No se pudo conectar a $BackendUrl. ¿Está corriendo backend/ (docker compose up -d)? $_"
        exit 1
    }

    # firefighter.id viaja como string en el JSON del login (FirefighterDto.Id
    # es string, ver AuthService.cs) — CreateAlertRequestDto.FirefighterIds
    # necesita number, por eso se convierte acá.
    $FirefighterIds = @([int]$loginResponse.firefighter.id)
}

if (-not $CorrelationId) {
    $CorrelationId = [guid]::NewGuid().ToString()
}

$body = @{
    correlationId  = $CorrelationId
    title          = $Title
    message        = $Message
    address        = $Address
    latitude       = $Lat
    longitude      = $Lng
    firefighterIds = $FirefighterIds
} | ConvertTo-Json

Write-Host "POST $BackendUrl/api/alerts"
Write-Host "firefighterIds: $($FirefighterIds -join ',')"
Write-Host "correlationId: $CorrelationId"
Write-Host ""

try {
    $response = Invoke-WebRequest -Method Post -Uri "$BackendUrl/api/alerts" `
        -ContentType "application/json" -Headers @{ "X-Api-Key" = $ApiKey } -Body $body
    $statusCode = $response.StatusCode
    $responseBody = $response.Content
} catch {
    # Invoke-WebRequest tira una excepción para cualquier status >= 400 en
    # vez de devolverla — la desenvolvemos así igual se ve el body (el
    # backend siempre manda detalle útil en el JSON, no solo el código).
    $statusCode = $_.Exception.Response.StatusCode.value__
    $responseBody = $_.ErrorDetails.Message
}

Write-Host "status: $statusCode"
Write-Host $responseBody

$parsed = $responseBody | ConvertFrom-Json -ErrorAction SilentlyContinue
if ($parsed -and $parsed.unknownFirefighterIds -and $parsed.unknownFirefighterIds.Count -gt 0) {
    Write-Warning "hay ids que no existen en la institución: $($parsed.unknownFirefighterIds -join ',')"
}
if ($parsed -and $parsed.firefightersWithoutDevice -and $parsed.firefightersWithoutDevice.Count -gt 0) {
    Write-Warning "hay ids sin ningún device token registrado: $($parsed.firefightersWithoutDevice -join ',')"
}
