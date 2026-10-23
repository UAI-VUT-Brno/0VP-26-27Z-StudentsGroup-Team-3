
$ErrorActionPreference = "Stop"

# Resolve paths relative to the repository root.
$root = [System.IO.Path]::GetFullPath(
    (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).ProviderPath
)

$apiDll = Join-Path $root (
    "src\UAI.ZeroVP.API\bin\Release\net10.0\UAI.ZeroVP.API.dll"
)

if (-not (Test-Path $apiDll)) {
    throw "Release API not found. Run dotnet build -c Release first."
}

$testDirectory = Join-Path $env:TEMP (
    "ZeroVP-MultiProcess-" + [Guid]::NewGuid().ToString("N")
)

New-Item -ItemType Directory -Path $testDirectory -Force |
    Out-Null

$jsonFile = Join-Path $testDirectory "todos.json"

# Allocate two different available localhost ports.
$listenerA = [Net.Sockets.TcpListener]::new(
    [Net.IPAddress]::Loopback, 0)

$listenerB = [Net.Sockets.TcpListener]::new(
    [Net.IPAddress]::Loopback, 0)

try {
    $listenerA.Start()
    $listenerB.Start()

    $portA = $listenerA.LocalEndpoint.Port
    $portB = $listenerB.LocalEndpoint.Port
}
finally {
    $listenerA.Stop()
    $listenerB.Stop()
}

$urlA = "http://127.0.0.1:$portA"
$urlB = "http://127.0.0.1:$portB"

# Save the existing process environment.
$oldStorage = $env:TodoStorage__FilePath
$oldSeed = $env:TodoStorage__SeedSampleData
$oldAspEnvironment = $env:ASPNETCORE_ENVIRONMENT
$oldDotnetEnvironment = $env:DOTNET_ENVIRONMENT

$env:TodoStorage__FilePath = $jsonFile
$env:TodoStorage__SeedSampleData = "false"
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:DOTNET_ENVIRONMENT = "Production"

$processA = $null
$processB = $null
$jobs = @()
$passed = $false

function Start-TestApi {
    param(
        [int]$Port,
        [string]$Name
    )

    $stdout = Join-Path $testDirectory "$Name.stdout.log"
    $stderr = Join-Path $testDirectory "$Name.stderr.log"

    Start-Process `
        -FilePath "dotnet" `
        -ArgumentList "`"$apiDll`" --urls http://127.0.0.1:$Port" `
        -WorkingDirectory (Split-Path $apiDll) `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -WindowStyle Hidden `
        -PassThru
}

function Wait-TestApi {
    param(
        [string]$Url,
        [System.Diagnostics.Process]$Process
    )

    for ($i = 0; $i -lt 100; $i++) {
        if ($Process.HasExited) {
            throw "API process exited unexpectedly: $Url"
        }

        try {
            return Invoke-RestMethod `
                -Uri "$Url/api/info" `
                -TimeoutSec 2
        }
        catch {
            Start-Sleep -Milliseconds 200
        }
    }

    throw "API did not start: $Url"
}

try {
    Write-Host "Starting two independent API processes..."

    $processA = Start-TestApi -Port $portA -Name "api-a"
    $processB = Start-TestApi -Port $portB -Name "api-b"

    $infoA = Wait-TestApi -Url $urlA -Process $processA
    $infoB = Wait-TestApi -Url $urlB -Process $processB

    if (-not $infoA.instanceId -or -not $infoB.instanceId) {
        throw "InstanceId is missing."
    }

    if ($infoA.instanceId -eq $infoB.instanceId) {
        throw "API instances have identical InstanceId."
    }

    Write-Host "[PASS] Unique instance IDs"

    # Create through A and immediately read through B.
    $created = Invoke-RestMethod `
        -Uri "$urlA/api/todos" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{
            title = "Created by API A"
            priority = "Normal"
        } | ConvertTo-Json -Compress)

    $id = $created.id

    $readB = Invoke-RestMethod `
        -Uri "$urlB/api/todos/$id"

    if ($readB.title -ne "Created by API A") {
        throw "API B cannot see the item created by API A."
    }

    Write-Host "[PASS] Immediate cross-process visibility"

    # A updates the task using the current version.
    $updated = Invoke-RestMethod `
        -Uri "$urlA/api/todos/$id" `
        -Method Put `
        -ContentType "application/json" `
        -Body (@{
            title = "Updated by API A"
            priority = "Normal"
            description = "Concurrency check"
            isCompleted = $false
            version = [long]$created.version
        } | ConvertTo-Json -Compress)

    if ($updated.version -ne 2) {
        throw "Version was not incremented to 2."
    }

    # B attempts to overwrite the update using stale version 1.
    $staleStatus = 0

    try {
        $null = Invoke-WebRequest `
            -Uri "$urlB/api/todos/$id" `
            -Method Put `
            -UseBasicParsing `
            -ContentType "application/json" `
            -Body (@{
                title = "Stale update by API B"
                priority = "High"
                description = "Should be rejected"
                isCompleted = $false
                version = [long]$created.version
            } | ConvertTo-Json -Compress)

        $staleStatus = 200
    }
    catch {
        if ($_.Exception.Response) {
            $staleStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    if ($staleStatus -ne 409) {
        throw "Expected HTTP 409, received HTTP $staleStatus."
    }

    $latest = Invoke-RestMethod `
        -Uri "$urlB/api/todos/$id"

    if ($latest.title -ne "Updated by API A" -or
        $latest.version -ne 2) {
        throw "Stale update changed the persisted data."
    }

    Write-Host "[PASS] HTTP 409 and stale-write protection"

    # Independent PowerShell jobs send writes to both processes.
    # This creates genuine concurrent cross-process activity.
    foreach ($url in @($urlA, $urlB)) {
        $jobs += Start-Job -ArgumentList $url -ScriptBlock {
            param($baseUrl)

            $ErrorActionPreference = "Stop"

            for ($i = 1; $i -le 20; $i++) {
                $body = @{
                    title = "Concurrent $PID task $i"
                    priority = "Normal"
                } | ConvertTo-Json -Compress

                Invoke-RestMethod `
                    -Uri "$baseUrl/api/todos" `
                    -Method Post `
                    -ContentType "application/json" `
                    -Body $body | Out-Null
            }

            20
        }
    }

    foreach ($job in $jobs) {
        $null = Wait-Job -Job $job -Timeout 120

        if ($job.State -ne "Completed") {
            throw "Concurrent writer job failed or timed out."
        }

        $count = Receive-Job -Job $job -ErrorAction Stop

        if ($count -ne 20) {
            throw "Concurrent writer did not finish all requests."
        }
    }

    $itemsA = Invoke-RestMethod -Uri "$urlA/api/todos"
    $itemsB = Invoke-RestMethod -Uri "$urlB/api/todos"

    if ($itemsA.Count -ne 41 -or $itemsB.Count -ne 41) {
        throw (
            "Lost updates: A=$($itemsA.Count), " +
            "B=$($itemsB.Count), expected 41."
        )
    }

    $parsedJson = Get-Content $jsonFile -Raw |
        ConvertFrom-Json

    if ($parsedJson.Count -ne 41) {
        throw "Shared JSON contains an unexpected item count."
    }

    Write-Host "[PASS] 40 concurrent writes without data loss"
    Write-Host "[PASS] Valid shared JSON storage"

    $passed = $true

    Write-Host ""
    Write-Host "ALL MULTI-PROCESS TESTS PASSED"
}
finally {
    foreach ($job in $jobs) {
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    }

    foreach ($process in @($processA, $processB)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force `
                -ErrorAction SilentlyContinue

            $process.WaitForExit()
        }
    }

    $env:TodoStorage__FilePath = $oldStorage
    $env:TodoStorage__SeedSampleData = $oldSeed
    $env:ASPNETCORE_ENVIRONMENT = $oldAspEnvironment
    $env:DOTNET_ENVIRONMENT = $oldDotnetEnvironment

    if ($passed) {
        Remove-Item $testDirectory -Recurse -Force
    }
    else {
        Write-Host "Diagnostic files: $testDirectory"
    }
}
