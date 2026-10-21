<#
.SYNOPSIS
    Builds fresh images from the current code and (re)starts the CV in Docker:
    Kotlin http://localhost:8280, .NET http://localhost:8281.

.DESCRIPTION
    Every run compiles the code in each image's build stage (for .NET: Release, warnings are errors) and replaces
    the running containers with ones from the new images. The data stays in each backend's PostgreSQL volume.

    Both backends serve the same pages and run the same SQL migrations from cv-shared, each against its own
    database (Flyway and DbUp keep separate journals).

    On the first run of a backend, when its .env does not exist yet, the .env is made from .env.example with a
    random database password. It is not made when the backend's database volume already exists: the volume
    keeps the password it was created with, so that .env has to be restored by hand.

.PARAMETER Backend
    kotlin, dotnet or all (the default).

.PARAMETER NoCache
    Builds every layer again, the dependency restore included, instead of reusing the unchanged ones.

.PARAMETER Logs
    Stays attached to the application's log after starting it (Ctrl+C leaves the containers running).
    Needs a single backend.
#>
param(
    [ValidateSet('all', 'kotlin', 'dotnet')]
    [string]$Backend = 'all',
    [switch]$NoCache,
    [switch]$Logs
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$backends = @(
    [pscustomobject]@{ Key = 'kotlin'; Name = 'Kotlin + Spring Boot'; Directory = 'cv-spring-kotlin'; Project = 'cv-spring-kotlin'; Url = 'http://localhost:8280' }
    [pscustomobject]@{ Key = 'dotnet'; Name = 'C# + ASP.NET Core'; Directory = 'cv-asp.net'; Project = 'cv-asp-net'; Url = 'http://localhost:8281' }
) | Where-Object { $Backend -eq 'all' -or $_.Key -eq $Backend }

if ($Logs -and @($backends).Count -gt 1) { throw '-Logs follows one log: choose -Backend kotlin or -Backend dotnet.' }

# Runs docker and returns its exit code. Docker writes its progress to stderr, which Windows PowerShell turns
# into error records once the script's output is redirected; under 'Stop' those would end the script, so here
# they only get printed and the exit code alone decides.
function Invoke-Docker {
    $ErrorActionPreference = 'Continue'
    & docker @args | Out-Host
    $LASTEXITCODE
}

function Test-DockerRunning {
    $ErrorActionPreference = 'Continue'
    docker version --format '{{.Server.Version}}' 2>&1 | Out-Null
    $LASTEXITCODE -eq 0
}

if (-not (Test-DockerRunning)) { throw 'Docker is not running.' }

# A random password of letters and digits (safe in a connection string and in .env without quoting).
function New-Password([int]$Length = 24) {
    $alphabet = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    # Bytes at or above the last whole multiple of the alphabet are skipped, so that every character is equally likely.
    $limit = 256 - (256 % $alphabet.Length)
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $byte = New-Object byte[] 1
    $password = ''
    while ($password.Length -lt $Length) {
        $random.GetBytes($byte)
        if ($byte[0] -lt $limit) { $password += $alphabet[$byte[0] % $alphabet.Length] }
    }
    $password
}

function Initialize-EnvFile($target) {
    $envFile = Join-Path $target.Directory '.env'
    if (Test-Path $envFile) { return }

    $volume = "$($target.Project)_pgdata"
    if (docker volume ls --quiet --filter "name=^$volume$") {
        throw "$envFile is missing but the database volume $volume exists: restore $envFile with the password the volume was created with, or remove the volume (docker volume rm $volume) to start with an empty database."
    }

    Write-Host "== First run: creating $envFile with a random database password" -ForegroundColor Cyan
    $password = New-Password
    $content = (Get-Content (Join-Path $target.Directory '.env.example') -Raw) `
        -replace 'your_db_user', 'cvuser' `
        -replace 'your_db_password', $password
    [System.IO.File]::WriteAllText((Join-Path $PSScriptRoot $envFile), $content)
}

function Wait-Healthy($target, [int]$TimeoutSeconds = 180) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            if ((Invoke-RestMethod -Uri "$($target.Url)/actuator/health" -TimeoutSec 5).status -eq 'UP') { return }
        }
        catch {
            # Not listening yet, or the database is not ready: try again.
        }
        Start-Sleep -Seconds 2
    }
    throw "$($target.Name) did not report UP within $TimeoutSeconds s - see: docker compose --file $($target.Directory)/docker-compose.yml logs app"
}

# A new id every run invalidates the compile layer: the code is compiled even when it did not change.
$env:BUILD_ID = Get-Date -Format 'yyyyMMddHHmmss'

foreach ($target in $backends) {
    $compose = @('compose', '--file', "$($target.Directory)/docker-compose.yml")
    Initialize-EnvFile $target

    Write-Host "== $($target.Name): building the image from the current code" -ForegroundColor Cyan
    $build = $compose + @('build', '--pull')
    if ($NoCache) { $build += '--no-cache' }
    if ((Invoke-Docker @build) -ne 0) { throw "The $($target.Name) image did not build - see the compiler output above." }

    Write-Host "== $($target.Name): starting new containers" -ForegroundColor Cyan
    $up = $compose + @('up', '--detach', '--force-recreate')
    if ((Invoke-Docker @up) -ne 0) { throw "The $($target.Name) containers did not start." }

    Write-Host "== $($target.Name): waiting for /actuator/health" -ForegroundColor Cyan
    Wait-Healthy $target
    Write-Host "Ready: $($target.Url)" -ForegroundColor Green
}

if ($Logs) {
    $follow = @('compose', '--file', "$($backends.Directory)/docker-compose.yml", 'logs', '--follow', 'app')
    Invoke-Docker @follow | Out-Null
}
