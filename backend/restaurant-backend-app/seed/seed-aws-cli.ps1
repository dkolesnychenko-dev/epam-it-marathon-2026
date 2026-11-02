<#
.SYNOPSIS
    Directly seeds DynamoDB tables using the AWS CLI and batch JSON files.
.DESCRIPTION
    Pushes Locations, Dishes, Tables, and Feedback into DynamoDB in eu-north-1.
.PARAMETER Region
    AWS Region (defaults to 'eu-north-1').
.EXAMPLE
    .\seed-aws-cli.ps1
    .\seed-aws-cli.ps1 -Region us-east-1
#>

param(
    [string]$Region = "eu-north-1"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BatchDir = Join-Path $ScriptDir "data\dynamodb-batch"

Write-Host "===================================================" -ForegroundColor Cyan
Write-Host " DynamoDB Direct Seeder (AWS CLI Batch-Write)     " -ForegroundColor Cyan
Write-Host " Region: $Region                                   " -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

function Write-Batch {
    param(
        [string]$Name,
        [string]$FileName
    )

    $filePath = Join-Path $BatchDir $FileName
    if (-not (Test-Path $filePath)) {
        Write-Error "File not found: $filePath"
        return
    }

    Write-Host "`n[+] Seeding $Name from $FileName..." -ForegroundColor Yellow
    $fileUri = "file://$filePath"
    
    try {
        $result = aws dynamodb batch-write-item --request-items $fileUri --region $Region | Out-String
        Write-Host "  OK: $Name" -ForegroundColor Green
        if ($result.Trim().Length -gt 0 -and $result -match "UnprocessedItems") {
            Write-Host "  Response: $result" -ForegroundColor Gray
        }
    }
    catch {
        Write-Error "  FAILED to batch write $Name : $_"
    }
}

# 1. Locations
Write-Batch -Name "Locations" -FileName "batch-locations.json"

# 2. Dishes
Write-Batch -Name "Dishes" -FileName "batch-dishes.json"

# 3. Tables
Write-Batch -Name "Tables" -FileName "batch-tables.json"

# 4. Feedbacks
Write-Batch -Name "Feedback" -FileName "batch-feedbacks.json"

Write-Host "`nDynamoDB Seeding finished!" -ForegroundColor Green
Write-Host "You can verify the data in AWS Console or via:" -ForegroundColor Cyan
Write-Host "  aws dynamodb scan --table-name Locations --region $Region" -ForegroundColor Gray
Write-Host "  aws dynamodb scan --table-name Dishes --region $Region" -ForegroundColor Gray