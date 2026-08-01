[CmdletBinding()]
param(
    [switch]$Start
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ApiUrl = if ($env:WORKOPS_API_URL) { $env:WORKOPS_API_URL } else { 'http://localhost:8080' }
$IdentityUrl = if ($env:WORKOPS_IDENTITY_URL) { $env:WORKOPS_IDENTITY_URL } else { 'http://localhost:8081' }
$ApiHostHeader = if ($env:WORKOPS_API_HOST_HEADER) { $env:WORKOPS_API_HOST_HEADER } else { '' }
$DemoPassword = if ($env:WORKOPS_DEMO_PASSWORD) { $env:WORKOPS_DEMO_PASSWORD } else { 'local-demo-only' }
$StateFile = if ($env:WORKOPS_DEMO_STATE) { $env:WORKOPS_DEMO_STATE } else { Join-Path $RepoRoot '.local/demo-state.json' }

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Write-Pass([string]$Message) {
    Write-Host "  [ok] $Message" -ForegroundColor Green
}

function Assert-Status($Response, [int]$Expected, [string]$Label) {
    if ($Response.Status -ne $Expected) {
        $safeBody = if ($Response.Body) { $Response.Body | ConvertTo-Json -Depth 10 } else { '<empty>' }
        throw "Expected HTTP $Expected but received $($Response.Status) for $Label. $safeBody"
    }
}

function Invoke-JsonApi(
    [string]$Method,
    [string]$Path,
    [string]$Token,
    [string]$WorkspaceId = '',
    $Payload = $null,
    [string]$IdempotencyKey = ''
) {
    $headers = @{ Authorization = "Bearer $Token" }
    if ($ApiHostHeader) { $headers['Host'] = $ApiHostHeader }
    if ($WorkspaceId) { $headers['X-Workspace-Id'] = $WorkspaceId }
    if ($IdempotencyKey) { $headers['Idempotency-Key'] = $IdempotencyKey }

    $parameters = @{
        Uri = "$ApiUrl$Path"
        Method = $Method
        Headers = $headers
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $Payload) {
        $parameters['ContentType'] = 'application/json'
        $parameters['Body'] = $Payload | ConvertTo-Json -Depth 10 -Compress
    }

    $response = Invoke-WebRequest @parameters
    $content = if ($response.Content -is [byte[]]) {
        [System.Text.Encoding]::UTF8.GetString($response.Content)
    }
    else {
        [string]$response.Content
    }
    $body = if ($content) { $content | ConvertFrom-Json } else { $null }
    [pscustomobject]@{
        Status = [int]$response.StatusCode
        Body = $body
        Headers = $response.Headers
    }
}

function Get-DemoToken([string]$Username) {
    $response = Invoke-RestMethod -Method Post `
        -Uri "$IdentityUrl/realms/workops/protocol/openid-connect/token" `
        -ContentType 'application/x-www-form-urlencoded' `
        -Body @{
            client_id = 'workops-cli'
            grant_type = 'password'
            scope = 'openid profile email'
            username = $Username
            password = $DemoPassword
        }
    $response.access_token
}

function Get-Subject([string]$Token) {
    $response = Invoke-RestMethod `
        -Uri "$IdentityUrl/realms/workops/protocol/openid-connect/userinfo" `
        -Headers @{ Authorization = "Bearer $Token" }
    $response.sub
}

function Wait-ForUrl(
    [string]$Url,
    [string]$Label,
    [hashtable]$Headers = @{},
    [int]$Attempts = 120
) {
    for ($index = 1; $index -le $Attempts; $index++) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Headers $Headers -SkipHttpErrorCheck
            if ([int]$response.StatusCode -lt 400) {
                Write-Pass "$Label is ready"
                return
            }
        }
        catch {
            # Keep waiting without printing provider details.
        }
        Start-Sleep -Seconds 1
    }
    throw "$Label did not become ready"
}

function Show-Summary([string]$WorkspaceId, [string]$ProjectId, [string]$WorkItemId) {
    Write-Host "`nGolden scenario complete"
    Write-Host "  Workspace: $WorkspaceId"
    Write-Host "  Project:   $ProjectId"
    Write-Host "  Work item: $WorkItemId"
    Write-Host '  Evidence:  authorization, tenant isolation, concurrency, audit, outbox notification'
    Write-Host '  Tokens:    held in memory only; never printed or written'
}

if ($Start) {
    Write-Step 'Starting the local stack'
    & docker compose --project-directory $RepoRoot up --detach --build
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose failed to start.' }
}

Write-Step 'Waiting for local services'
Wait-ForUrl "$IdentityUrl/realms/workops/.well-known/openid-configuration" 'Identity provider'
$apiHealthHeaders = @{}
if ($ApiHostHeader) { $apiHealthHeaders['Host'] = $ApiHostHeader }
Wait-ForUrl "$ApiUrl/health/ready" 'WorkOps API' $apiHealthHeaders

Write-Step 'Obtaining synthetic user tokens'
$ownerToken = Get-DemoToken 'demo-owner'
$contributorToken = Get-DemoToken 'demo-contributor'
$viewerToken = Get-DemoToken 'demo-viewer'
$outsiderToken = Get-DemoToken 'demo-outsider'
$contributorSubject = Get-Subject $contributorToken
$viewerSubject = Get-Subject $viewerToken
Write-Pass 'owner, contributor, viewer, and outsider authenticated'

if (Test-Path $StateFile) {
    $state = Get-Content -Raw $StateFile | ConvertFrom-Json
    if ($state.workspaceId -and $state.workItemId) {
        $current = Invoke-JsonApi GET "/api/v1/work-items/$($state.workItemId)" `
            $contributorToken $state.workspaceId
        if ($current.Status -eq 200) {
            Write-Step 'Reusing the saved idempotent demo state'
            Write-Pass 'existing work item is visible to its contributor'

            $stale = Invoke-JsonApi POST "/api/v1/work-items/$($state.workItemId)/transitions" `
                $contributorToken $state.workspaceId @{
                    targetStatus = 'Blocked'
                    expectedVersion = $state.staleVersion
                }
            Assert-Status $stale 409 'stale transition'
            if ($stale.Body.code -ne 'concurrency_conflict') { throw 'Unexpected stale-write problem code.' }
            Write-Pass 'stale transition remains a safe 409 Conflict'

            $outside = Invoke-JsonApi GET "/api/v1/work-items/$($state.workItemId)" `
                $outsiderToken $state.outsiderWorkspaceId
            Assert-Status $outside 404 'cross-workspace read'
            Write-Pass 'outsider still receives a non-disclosing 404'

            Show-Summary $state.workspaceId $state.projectId $state.workItemId
            exit 0
        }
    }
}

$runId = [DateTimeOffset]::UtcNow.ToString('yyyyMMddHHmmss')

Write-Step 'Creating two isolated workspaces'
$ownerWorkspace = Invoke-JsonApi POST '/api/v1/workspaces/' $ownerToken '' @{
    name = 'WorkOps Demo'
    slug = "workops-demo-$runId"
}
Assert-Status $ownerWorkspace 201 'owner workspace creation'
$workspaceId = $ownerWorkspace.Body.id

$outsiderWorkspace = Invoke-JsonApi POST '/api/v1/workspaces/' $outsiderToken '' @{
    name = 'Outsider Demo'
    slug = "outsider-demo-$runId"
}
Assert-Status $outsiderWorkspace 201 'outsider workspace creation'
$outsiderWorkspaceId = $outsiderWorkspace.Body.id
Write-Pass 'workspace ownership boundaries established'

Write-Step 'Inviting contributor and viewer'
$contributorInvite = Invoke-JsonApi POST "/api/v1/workspaces/$workspaceId/invitations" $ownerToken '' @{
    subject = $contributorSubject
    displayName = 'Demo Contributor'
    role = 'ProjectContributor'
}
Assert-Status $contributorInvite 201 'contributor invitation'
$contributorUserId = $contributorInvite.Body.userId

$viewerInvite = Invoke-JsonApi POST "/api/v1/workspaces/$workspaceId/invitations" $ownerToken '' @{
    subject = $viewerSubject
    displayName = 'Demo Viewer'
    role = 'Viewer'
}
Assert-Status $viewerInvite 201 'viewer invitation'
Write-Pass 'role-scoped memberships created'

Write-Step 'Creating an idempotent project'
$projectPayload = @{ name = 'Delivery Platform'; key = "demo-$runId" }
$idempotencyKey = "demo-project-$runId"
$project = Invoke-JsonApi POST '/api/v1/projects/' $ownerToken $workspaceId $projectPayload $idempotencyKey
Assert-Status $project 201 'project creation'
$projectId = $project.Body.id

$projectReplay = Invoke-JsonApi POST '/api/v1/projects/' $ownerToken $workspaceId $projectPayload $idempotencyKey
Assert-Status $projectReplay 201 'project replay'
if ($projectReplay.Headers['Idempotency-Replayed'] -notcontains 'true') {
    throw 'Project replay header was not returned.'
}
Write-Pass 'exact replay returned the original project'

Write-Step 'Proving viewer write denial'
$viewerWrite = Invoke-JsonApi POST '/api/v1/projects/' $viewerToken $workspaceId @{
    name = 'Forbidden Project'
    key = "forbidden-$runId"
}
Assert-Status $viewerWrite 403 'viewer project creation'
Write-Pass 'viewer write returned 403 Forbidden'

Write-Step 'Creating, updating, and transitioning a work item'
$workItem = Invoke-JsonApi POST "/api/v1/projects/$projectId/work-items" $contributorToken $workspaceId @{
    title = 'Deliver tenant-safe workflow'
    priority = 'High'
    assigneeUserId = $contributorUserId
    labels = @('backend', 'tenant-safe')
}
Assert-Status $workItem 201 'work-item creation'
$workItemId = $workItem.Body.id
$createdVersion = $workItem.Body.version

$updated = Invoke-JsonApi PATCH "/api/v1/work-items/$workItemId" $contributorToken $workspaceId @{
    title = 'Deliver secure tenant workflow'
    priority = 'Critical'
    assigneeUserId = $contributorUserId
    labels = @('api', 'security')
    expectedVersion = $createdVersion
}
Assert-Status $updated 200 'work-item update'
$updatedVersion = $updated.Body.version

$transitioned = Invoke-JsonApi POST "/api/v1/work-items/$workItemId/transitions" $contributorToken $workspaceId @{
    targetStatus = 'InProgress'
    expectedVersion = $updatedVersion
}
Assert-Status $transitioned 200 'work-item transition'
$currentVersion = $transitioned.Body.version
Write-Pass 'work item moved from Backlog to InProgress'

Write-Step 'Proving stale-write and tenant boundaries'
$staleWrite = Invoke-JsonApi POST "/api/v1/work-items/$workItemId/transitions" $contributorToken $workspaceId @{
    targetStatus = 'Blocked'
    expectedVersion = $updatedVersion
}
Assert-Status $staleWrite 409 'stale transition'
if ($staleWrite.Body.code -ne 'concurrency_conflict') { throw 'Unexpected stale-write problem code.' }
Write-Pass 'stale version returned 409 Conflict'

$outsideRead = Invoke-JsonApi GET "/api/v1/work-items/$workItemId" $outsiderToken $outsiderWorkspaceId
Assert-Status $outsideRead 404 'cross-workspace read'
Write-Pass 'cross-workspace read returned a non-disclosing 404'

Write-Step 'Waiting for audit and notification evidence'
$notificationCount = 0
for ($index = 1; $index -le 30; $index++) {
    $notifications = Invoke-JsonApi GET '/api/v1/notifications?page=1&pageSize=20' `
        $contributorToken $workspaceId
    Assert-Status $notifications 200 'notification list'
    $notificationCount = [int]$notifications.Body.totalCount
    if ($notificationCount -gt 0) { break }
    Start-Sleep -Seconds 1
}
if ($notificationCount -lt 1) { throw 'Notification was not delivered within 30 seconds.' }

$audit = Invoke-JsonApi GET `
    '/api/v1/audit-events?page=1&pageSize=20&action=work_item.transitioned&entityType=work_item' `
    $ownerToken $workspaceId
Assert-Status $audit 200 'audit list'
if ([int]$audit.Body.totalCount -lt 1) { throw 'Transition audit evidence was not found.' }
Write-Pass 'transactional audit and outbox notification are visible'

$stateDirectory = Split-Path -Parent $StateFile
New-Item -ItemType Directory -Force -Path $stateDirectory | Out-Null
@{
    workspaceId = $workspaceId
    outsiderWorkspaceId = $outsiderWorkspaceId
    projectId = $projectId
    workItemId = $workItemId
    staleVersion = $updatedVersion
    currentVersion = $currentVersion
} | ConvertTo-Json | Set-Content -Path $StateFile -Encoding utf8NoBOM

Show-Summary $workspaceId $projectId $workItemId
