<#
.SYNOPSIS
    Cleans all DynamoDB tables and deletes all Cognito users.
.DESCRIPTION
    Wipes all data from the database and removes registered Cognito users so you can
    re-seed and start testing from a completely clean slate.
.PARAMETER Region
    AWS Region (defaults to 'eu-north-1').
.PARAMETER UserPoolName
    Cognito User Pool Name (defaults to 'restaurant-backend-user-pool').
.PARAMETER Force
    Skip interactive confirmation.
.EXAMPLE
    .\clean-dynamodb.ps1
    .\clean-dynamodb.ps1 -Force
#>

param(
    [string]$Region = "eu-north-1",
    [string]$UserPoolName = "restaurant-backend-user-pool",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "===================================================" -ForegroundColor Red
Write-Host " Full Database & User Pool Cleaner                 " -ForegroundColor Red
Write-Host " Region: $Region                                   " -ForegroundColor Red
Write-Host "===================================================" -ForegroundColor Red

if (-not $Force) {
    $answer = Read-Host "WARNING: This will delete ALL data from ALL DynamoDB tables and ALL users from Cognito ($Region). Continue? (y/n)"
    if ($answer -notmatch "^[Yy]$") {
        Write-Host "Operation cancelled by user." -ForegroundColor Yellow
        exit 0
    }
}

# ----------------------------------------------------
# 1. Clean Cognito Users
# ----------------------------------------------------
Write-Host "`n[1/2] Cleaning Cognito Users ($UserPoolName)..." -ForegroundColor Cyan

try {
    $poolListRaw = aws cognito-idp list-user-pools --max-results 60 --region $Region 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) {
        $poolList = $poolListRaw | ConvertFrom-Json
        $pool = $poolList.UserPools | Where-Object { $_.Name -eq $UserPoolName } | Select-Object -First 1

        if ($pool) {
            $poolId = $pool.Id
            Write-Host "  Found User Pool '$UserPoolName' (ID: $poolId)" -ForegroundColor Gray

            $usersRaw = aws cognito-idp list-users --user-pool-id $poolId --region $Region 2>&1 | Out-String
            if ($LASTEXITCODE -eq 0) {
                $usersJson = $usersRaw | ConvertFrom-Json
                $users = @($usersJson.Users)

                if ($users.Count -gt 0) {
                    Write-Host "  Deleting $($users.Count) user(s) from Cognito..." -ForegroundColor Yellow
                    foreach ($u in $users) {
                        $username = $u.Username
                        $email = ($u.Attributes | Where-Object { $_.Name -eq "email" } | Select-Object -ExpandProperty Value)
                        aws cognito-idp admin-delete-user --user-pool-id $poolId --username $username --region $Region | Out-Null
                        Write-Host "    - Deleted: $username ($email)" -ForegroundColor Gray
                    }
                    Write-Host "  OK: All Cognito users deleted." -ForegroundColor Green
                } else {
                    Write-Host "  Cognito User Pool is already empty (0 users)." -ForegroundColor Gray
                }
            } else {
                Write-Warning "  Could not list Cognito users: $usersRaw"
            }
        } else {
            Write-Host "  User Pool '$UserPoolName' not found in $Region. Skipping Cognito cleanup." -ForegroundColor Gray
        }
    } else {
        Write-Warning "  Could not list Cognito User Pools: $poolListRaw"
    }
}
catch {
    Write-Warning "  Cognito cleanup failed: $_"
}

# ----------------------------------------------------
# 2. Clean DynamoDB Tables
# ----------------------------------------------------
Write-Host "`n[2/2] Cleaning DynamoDB Tables..." -ForegroundColor Cyan

$AllTables = @(
    @{ Name = "GuestSessions";     Keys = @("guestSessionId") },
    @{ Name = "GroupReservations"; Keys = @("groupReservationId") },
    @{ Name = "Orders";            Keys = @("orderId", "customerId") },
    @{ Name = "Notifications";     Keys = @("id") },
    @{ Name = "Reservations";      Keys = @("id") },
    @{ Name = "ReportsHistory";    Keys = @("id") },
    @{ Name = "Reports";           Keys = @("waiterId", "date") },
    @{ Name = "Waiters";           Keys = @("userId") },
    @{ Name = "Feedback";          Keys = @("reservationId", "createdAt") },
    @{ Name = "Tables";            Keys = @("id") },
    @{ Name = "Dishes";            Keys = @("id") },
    @{ Name = "Locations";         Keys = @("id") },
    @{ Name = "ProfileImages";     Keys = @("userId") }
)

function Clear-Table {
    param(
        [string]$TableName,
        [string[]]$KeyAttributes,
        [string]$TargetRegion
    )

    Write-Host "`n[*] Cleaning '$TableName'..." -ForegroundColor Yellow

    $proj = ($KeyAttributes -join ", ")
    $scanRaw = aws dynamodb scan --table-name $TableName --projection-expression $proj --region $TargetRegion 2>&1 | Out-String

    if ($LASTEXITCODE -ne 0) {
        if ($scanRaw -match "ResourceNotFoundException") {
            Write-Host "  Table '$TableName' does not exist in $TargetRegion. Skipping." -ForegroundColor Gray
            return
        }
        Write-Warning "  Scan error on '$TableName': $scanRaw"
        return
    }

    $scanJson = $scanRaw | ConvertFrom-Json
    $items = @($scanJson.Items)

    if ($items.Count -eq 0) {
        Write-Host "  Table '$TableName' is already empty (0 items)." -ForegroundColor Gray
        return
    }

    Write-Host "  Found $($items.Count) item(s) to delete..." -ForegroundColor Yellow

    $batchSize = 25
    for ($i = 0; $i -lt $items.Count; $i += $batchSize) {
        $end = [Math]::Min($i + $batchSize - 1, $items.Count - 1)
        $chunk = $items[$i..$end]

        $deleteRequests = @()
        foreach ($item in $chunk) {
            $keyMap = @{}
            foreach ($k in $KeyAttributes) {
                if ($item.PSObject.Properties[$k]) {
                    $attrObj = $item.$k
                    $typeName = $attrObj.PSObject.Properties.Name[0]
                    $typeVal = $attrObj.$typeName
                    $keyMap[$k] = @{ $typeName = $typeVal }
                }
            }
            $deleteRequests += @{
                DeleteRequest = @{
                    Key = $keyMap
                }
            }
        }

        $payload = @{
            $TableName = $deleteRequests
        }

        $tmpFile = [System.IO.Path]::GetTempFileName()
        try {
            $payload | ConvertTo-Json -Depth 6 | Set-Content -Path $tmpFile -Encoding UTF8
            aws dynamodb batch-write-item --request-items "file://$tmpFile" --region $TargetRegion 2>&1 | Out-Null
        }
        finally {
            if (Test-Path $tmpFile) {
                Remove-Item $tmpFile -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Write-Host "  OK: Deleted $($items.Count) item(s) from '$TableName'." -ForegroundColor Green
}

foreach ($t in $AllTables) {
    Clear-Table -TableName $t.Name -KeyAttributes $t.Keys -TargetRegion $Region
}

Write-Host "`n===================================================" -ForegroundColor Green
Write-Host " Full cleanup complete! All tables & users removed." -ForegroundColor Green
Write-Host " To re-populate seed data, run: .\seed-aws-cli.ps1 " -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Green
