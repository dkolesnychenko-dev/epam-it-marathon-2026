<#
.SYNOPSIS
    Seeds test users (Customer, Waiter, Admin) into AWS Cognito and DynamoDB.
.DESCRIPTION
    Creates standard test accounts in the Cognito User Pool, assigns appropriate
    roles/groups (Customer, Waiter, Admin), and links Waiter users to their respective
    restaurant location in the DynamoDB 'Waiters' table.
.PARAMETER Region
    AWS Region (defaults to 'eu-north-1').
.PARAMETER UserPoolName
    Cognito User Pool Name (defaults to 'restaurant-backend-user-pool').
.EXAMPLE
    .\seed-users.ps1
    .\seed-users.ps1 -Region eu-north-1
#>

param(
    [string]$Region = "eu-north-1",
    [string]$UserPoolName = "restaurant-backend-user-pool"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$UsersFile = Join-Path $ScriptDir "data\users.json"

Write-Host "===================================================" -ForegroundColor Cyan
Write-Host " Cognito & DynamoDB Test Users Seeder              " -ForegroundColor Cyan
Write-Host " Region: $Region                                   " -ForegroundColor Cyan
Write-Host " User Pool: $UserPoolName                          " -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

if (-not (Test-Path $UsersFile)) {
    Write-Error "Users seed file not found: $UsersFile"
    exit 1
}

# 1. Locate Cognito User Pool
Write-Host "`n[1/4] Finding Cognito User Pool '$UserPoolName'..." -ForegroundColor Yellow
$poolId = $null

try {
    $poolListRaw = aws cognito-idp list-user-pools --max-results 60 --region $Region --output json 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0 -and $poolListRaw.Trim().StartsWith("{")) {
        $poolList = $poolListRaw | ConvertFrom-Json
        $pool = $poolList.UserPools | Where-Object { $_.Name -eq $UserPoolName } | Select-Object -First 1
        if ($pool) {
            $poolId = $pool.Id
            Write-Host "  Found User Pool ID: $poolId" -ForegroundColor Green
        }
    }
}
catch {
    Write-Warning "Failed to query Cognito User Pools: $_"
}

if (-not $poolId) {
    Write-Error "Cognito User Pool '$UserPoolName' was not found in region '$Region'. Make sure infrastructure is deployed."
    exit 1
}

# 2. Ensure Cognito Groups exist
Write-Host "`n[2/4] Ensuring Cognito Groups (Customer, Waiter, Admin) exist..." -ForegroundColor Yellow
$requiredGroups = @("Customer", "Waiter", "Admin")

try {
    $existingGroupsRaw = aws cognito-idp list-groups --user-pool-id $poolId --region $Region --output json 2>&1 | Out-String
    $existingGroupNames = @()
    if ($LASTEXITCODE -eq 0 -and $existingGroupsRaw.Trim().StartsWith("{")) {
        $existingGroupsJson = $existingGroupsRaw | ConvertFrom-Json
        $existingGroupNames = @($existingGroupsJson.Groups | ForEach-Object { $_.GroupName })
    }

    foreach ($group in $requiredGroups) {
        if ($existingGroupNames -notcontains $group) {
            Write-Host "  Creating group: $group..." -ForegroundColor Gray
            aws cognito-idp create-group `
                --group-name $group `
                --user-pool-id $poolId `
                --description "Role group for $group" `
                --region $Region | Out-Null
            Write-Host "  OK: Group '$group' created" -ForegroundColor Green
        } else {
            Write-Host "  Group '$group' already exists" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Warning "Error checking/creating groups: $_"
}

# 3. Create Users
Write-Host "`n[3/4] Creating test users in Cognito..." -ForegroundColor Yellow
$users = Get-Content -Raw $UsersFile | ConvertFrom-Json

foreach ($user in $users) {
    $email = $user.email
    $pwd = $user.password
    $fn = $user.firstName
    $ln = $user.lastName
    $role = $user.role

    Write-Host "`n  Processing user: $email ($role)..." -ForegroundColor Cyan

    $userExists = $false
    try {
        $getUserRaw = aws cognito-idp admin-get-user --user-pool-id $poolId --username $email --region $Region --output json 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0 -and $getUserRaw.Trim().StartsWith("{")) {
            $userExists = $true
            Write-Host "    User already exists in Cognito." -ForegroundColor Gray
        }
    }
    catch {
        $userExists = $false
    }

    if (-not $userExists) {
        Write-Host "    Creating user in Cognito..." -ForegroundColor Gray
        aws cognito-idp admin-create-user `
            --user-pool-id $poolId `
            --username $email `
            --user-attributes Name=email,Value=$email Name=email_verified,Value=true Name=given_name,Value=$fn Name=family_name,Value=$ln `
            --message-action SUPPRESS `
            --region $Region | Out-Null

        Write-Host "    Setting permanent password..." -ForegroundColor Gray
        aws cognito-idp admin-set-user-password `
            --user-pool-id $poolId `
            --username $email `
            --password $pwd `
            --permanent `
            --region $Region | Out-Null

        Write-Host "    OK: User created." -ForegroundColor Green
    }

    # Assign group
    Write-Host "    Assigning group '$role'..." -ForegroundColor Gray
    aws cognito-idp admin-add-user-to-group `
        --user-pool-id $poolId `
        --username $email `
        --group-name $role `
        --region $Region 2>&1 | Out-Null

    # 4. If Waiter, register in DynamoDB Waiters table
    if ($role -eq "Waiter" -and $user.locationId) {
        Write-Host "    Syncing Waiter with DynamoDB 'Waiters' table..." -ForegroundColor Gray
        $getUserRaw = aws cognito-idp admin-get-user --user-pool-id $poolId --username $email --region $Region --output json | Out-String
        $userJson = $getUserRaw | ConvertFrom-Json
        $sub = ($userJson.UserAttributes | Where-Object { $_.Name -eq "sub" }).Value

        if ($sub) {
            $now = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            $dynamoItem = "{`"userId`":{`"S`":`"$sub`"},`"locationId`":{`"S`":`"$($user.locationId)`"},`"createdAt`":{`"S`":`"$now`"},`"reservations`":{`"L`":[]}}"

            aws dynamodb put-item `
                --table-name Waiters `
                --item $dynamoItem `
                --region $Region | Out-Null

            Write-Host "    OK: Waiter mapped in DynamoDB (sub: $sub -> location: $($user.locationId))" -ForegroundColor Green
        } else {
            Write-Warning "    Could not find 'sub' attribute for user $email"
        }
    }
}

Write-Host "`n===================================================" -ForegroundColor Cyan
Write-Host " User seeding completed successfully!              " -ForegroundColor Green
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "`nTest accounts ready to use:" -ForegroundColor Yellow
foreach ($user in $users) {
    Write-Host "  * $($user.role.ToUpper()): $($user.email) / $($user.password) ($($user.firstName) $($user.lastName))" -ForegroundColor White
}

