param(
    [string]$Username,
    [string]$Namespace,
    [string]$Repository = "frpc-manager",
    [string]$Description = "FrpC web management platform for frpc tunnels, with setup wizard, audit logs, health checks, backup/restore, SQLite and MySQL support.",
    [string]$ReadmePath = "dockerhub-description.md"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Username)) {
    $Username = Read-Host "Enter Docker Hub username"
}

if ([string]::IsNullOrWhiteSpace($Namespace)) {
    $Namespace = Read-Host "Enter Docker Hub namespace/repository owner"
}

if ([string]::IsNullOrWhiteSpace($Repository)) {
    $Repository = Read-Host "Enter Docker Hub repository name"
}

if ($Namespace -match "[@\\/:]") {
    throw "Invalid namespace '$Namespace'. Use Docker Hub ID/namespace, not an email address."
}

if ($Repository -match "[@\\/:]") {
    throw "Invalid repository '$Repository'. Use only the repository name, for example frpc-manager."
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$readmeFullPath = Join-Path $root $ReadmePath
if (-not (Test-Path $readmeFullPath)) {
    throw "Docker Hub README file not found: $readmeFullPath"
}

$tokenSecret = Read-Host "Enter Docker Hub password or access token" -AsSecureString
$tokenPtr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($tokenSecret)

try {
    $passwordOrToken = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($tokenPtr)
    $loginBody = @{
        username = $Username
        password = $passwordOrToken
    } | ConvertTo-Json

    Write-Host "Logging in to Docker Hub API..."
    $loginResponse = Invoke-RestMethod `
        -Method Post `
        -Uri "https://hub.docker.com/v2/users/login/" `
        -ContentType "application/json" `
        -Body $loginBody

    $jwt = $loginResponse.token
    if ([string]::IsNullOrWhiteSpace($jwt)) {
        throw "Docker Hub API did not return a token."
    }

    $fullDescription = Get-Content $readmeFullPath -Raw -Encoding utf8
    $updateBody = @{
        description = $Description
        full_description = $fullDescription
    } | ConvertTo-Json

    Write-Host "Updating Docker Hub repository: $Namespace/$Repository"
    Invoke-RestMethod `
        -Method Patch `
        -Uri "https://hub.docker.com/v2/repositories/$Namespace/$Repository/" `
        -ContentType "application/json" `
        -Headers @{ Authorization = "JWT $jwt" } `
        -Body $updateBody | Out-Null

    Write-Host "Done. Docker Hub description updated: $Namespace/$Repository"
}
finally {
    if ($tokenPtr -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPtr)
    }
}
