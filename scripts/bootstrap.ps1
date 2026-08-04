[CmdletBinding()]
param(
    [switch]$Cleanup
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ApiPort = if ($env:WORKOPS_HTTP_PORT) { [int]$env:WORKOPS_HTTP_PORT } else { 8080 }
$IdentityPort = if ($env:WORKOPS_IDENTITY_PORT) { [int]$env:WORKOPS_IDENTITY_PORT } else { 8081 }
$ApiUrl = if ($env:WORKOPS_API_URL) { $env:WORKOPS_API_URL } else { "http://localhost:$ApiPort" }
$IdentityUrl = if ($env:WORKOPS_IDENTITY_URL) { $env:WORKOPS_IDENTITY_URL } else { "http://localhost:$IdentityPort" }
$EvidenceDirectory = if ($env:WORKOPS_DEMO_EVIDENCE_DIR) {
    $env:WORKOPS_DEMO_EVIDENCE_DIR
}

if ($ApiPort -lt 1 -or $ApiPort -gt 65535) { throw 'API port must be an integer from 1 to 65535.' }
if ($IdentityPort -lt 1 -or $IdentityPort -gt 65535) { throw 'Identity port must be an integer from 1 to 65535.' }
else {
    Join-Path $RepoRoot 'artifacts/reviewer-demo'
}

function Write-Pass([string]$Message) {
    Write-Host "  [ok] $Message" -ForegroundColor Green
}

function Write-WarningMessage([string]$Message) {
    Write-Warning $Message
}

function Assert-Command([string]$Name, [string]$Message) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw $Message
    }
}

function Test-TcpPort([int]$Port) {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $task = $client.ConnectAsync('127.0.0.1', $Port)
        if (-not $task.Wait(500)) { return $false }
        return $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Test-ReviewerPort(
    [int]$Port,
    [string]$Service,
    [string]$Label,
    [string[]]$RunningServices
) {
    if (-not (Test-TcpPort $Port)) {
        Write-Pass "$Label port $Port is available"
        return
    }

    if ($RunningServices -contains $Service) {
        Write-Pass "$Label port $Port is already used by this Compose stack"
        return
    }

    throw "$Label port $Port is already in use. Set the matching WORKOPS_*_PORT override or stop the conflicting process."
}

Assert-Command docker 'Docker is required. Install and start Docker Desktop or a compatible Docker Engine.'

if ($Cleanup) {
    Write-Host 'Stopping the local WorkOps stack (named volumes are preserved)'
    & docker compose --project-directory $RepoRoot down --remove-orphans
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose cleanup failed.' }

    Write-Host "`nCleanup complete"
    Write-Host "  API URL:       $ApiUrl"
    Write-Host "  Identity URL:  $IdentityUrl"
    Write-Host '  Data volumes:  preserved'
    exit 0
}

Write-Host 'Validating reviewer prerequisites (no tools will be installed and no services will be started)'

$requiredFiles = @(
    'docker-compose.yml',
    'Dockerfile',
    'global.json',
    'scripts/demo.ps1'
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path (Join-Path $RepoRoot $requiredFile) -PathType Leaf)) {
        throw "Repository prerequisite is missing: $requiredFile"
    }
}
Write-Pass 'repository prerequisites are present'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'PowerShell 7 or later is required by scripts/demo.ps1.'
}
Write-Pass "PowerShell $($PSVersionTable.PSVersion) is available"

& docker info *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker is installed but its daemon is unavailable. Start Docker and retry.' }
& docker compose version *> $null
if ($LASTEXITCODE -ne 0) { throw 'The Docker Compose plugin is required (docker compose).' }

$dockerClient = (& docker version --format '{{.Client.Version}}').Trim()
$dockerServer = (& docker version --format '{{.Server.Version}}').Trim()
$composeVersion = (& docker compose version --short).Trim()
Write-Pass "Docker client and daemon are available ($dockerClient/$dockerServer)"
Write-Pass "Docker Compose is available ($composeVersion)"

$composeJson = (& docker compose --project-directory $RepoRoot config --format json | Out-String)
if ($LASTEXITCODE -ne 0) { throw 'docker-compose.yml did not validate.' }
$composeConfig = $composeJson | ConvertFrom-Json
foreach ($privateService in @('postgres', 'rabbitmq', 'redis')) {
    $serviceConfig = $composeConfig.services.PSObject.Properties[$privateService].Value
    if (@($serviceConfig.ports).Count -gt 0) {
        throw "$privateService must not publish a host port."
    }
}
foreach ($loopbackService in @('api', 'identity')) {
    $serviceConfig = $composeConfig.services.PSObject.Properties[$loopbackService].Value
    $publishedPorts = @($serviceConfig.ports)
    if ($publishedPorts.Count -eq 0 -or @($publishedPorts | Where-Object host_ip -ne '127.0.0.1').Count -gt 0) {
        throw "$loopbackService must publish only loopback host ports."
    }
}
Write-Pass 'Compose configuration and loopback-only host boundary passed'

$globalJson = Get-Content -Raw (Join-Path $RepoRoot 'global.json') | ConvertFrom-Json
$sdkVersion = [string]$globalJson.sdk.version
if ($sdkVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw 'global.json does not contain a valid sdk.version.'
}
$dockerfile = Get-Content -Raw (Join-Path $RepoRoot 'Dockerfile')
if (-not $dockerfile.Contains("mcr.microsoft.com/dotnet/sdk:$sdkVersion-")) {
    throw "Dockerfile build SDK does not match global.json ($sdkVersion)."
}
Write-Pass ".NET SDK metadata is consistent ($sdkVersion)"

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $installedSdks = @(& dotnet --list-sdks | ForEach-Object { ($_ -split '\s+')[0] })
    if ($installedSdks -contains $sdkVersion) {
        Write-Pass "optional local .NET SDK $sdkVersion is available"
    }
    else {
        Write-WarningMessage "Local .NET SDK $sdkVersion is not installed; the containerized reviewer path remains available."
    }
}
else {
    Write-WarningMessage 'dotnet is not installed; it is optional for the containerized reviewer path.'
}

$runningServices = @(& docker compose --project-directory $RepoRoot ps --services --status running 2>$null)
Test-ReviewerPort $ApiPort 'api' 'API' $runningServices
Test-ReviewerPort $IdentityPort 'identity' 'Identity' $runningServices

$dockerMemory = [long](& docker info --format '{{.MemTotal}}')
if ($dockerMemory -lt 4GB) {
    Write-WarningMessage 'Docker reports less than 4 GiB of memory; the five-service stack may start slowly or fail.'
}

Write-Host "`nReviewer environment ready"
Write-Host "  API URL:          $ApiUrl"
Write-Host "  Identity URL:     $IdentityUrl"
Write-Host '  Scenario status:  not started'
Write-Host "  Evidence path:    $EvidenceDirectory"
Write-Host '  Start command:    ./scripts/demo.ps1 -Start'
Write-Host '  Cleanup command:  ./scripts/bootstrap.ps1 -Cleanup'
Write-Host '  Credentials:      not printed or inspected'
