# PowerShell Template for Batch Processing Multiple Databases
# Copy and customize this script for your environment

param(
    [string]$Environment = "production",
    [switch]$DryRun = $false,
    [switch]$AnalyzeOnly = $false,
    [switch]$ObfuscateOnly = $false
)

# Configuration
$BaseDir = Split-Path -Parent $PSScriptRoot
$AutoMappingGenerator = Join-Path $BaseDir "auto-mapping-generator"
$DataObfuscation = Join-Path $BaseDir "data-obfuscation"
$JsonDir = Join-Path $BaseDir "JSON"
$LogDir = Join-Path $BaseDir "logs"

# Ensure directories exist
New-Item -ItemType Directory -Force -Path $JsonDir | Out-Null
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

# Database list
$databases = @(
    @{
        Server = "server1.example.com"
        Database = "Database1"
        IntegratedSecurity = $true
    },
    @{
        Server = "server2.example.com"
        Database = "Database2"
        Username = "sa"
        Password = "YourPassword123!"
    }
)

# Logging function
function Write-Log {
    param($Message, $Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "$timestamp [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path "$LogDir\batch-process-$(Get-Date -Format 'yyyyMMdd').log" -Value $logMessage
}

Write-Log "Starting batch processing for $($databases.Count) databases"
Write-Log "Environment: $Environment"
Write-Log "Dry Run: $DryRun"

foreach ($db in $databases) {
    Write-Log "Processing database: $($db.Database) on $($db.Server)"
    
    # Build connection string
    if ($db.IntegratedSecurity) {
        $connectionString = "Server=$($db.Server);Database=$($db.Database);Integrated Security=true;TrustServerCertificate=true;"
    } else {
        $connectionString = "Server=$($db.Server);Database=$($db.Database);User Id=$($db.Username);Password=$($db.Password);TrustServerCertificate=true;"
    }
    
    # Step 1: Analyze Schema
    if (-not $ObfuscateOnly) {
        Write-Log "Analyzing schema for $($db.Database)"
        
        Push-Location $AutoMappingGenerator
        try {
            $analyzeCmd = "dotnet run -- `"$connectionString`""
            Write-Log "Executing: $analyzeCmd"
            
            $result = Invoke-Expression $analyzeCmd 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Log "Schema analysis failed for $($db.Database)" "ERROR"
                Write-Log $result "ERROR"
                continue
            }
            Write-Log "Schema analysis completed successfully"
        }
        finally {
            Pop-Location
        }
    }
    
    # Step 2: Obfuscate Data
    if (-not $AnalyzeOnly) {
        Write-Log "Starting obfuscation for $($db.Database)"
        
        $mappingFile = Join-Path $JsonDir "$($db.Database)-mapping.json"
        $configFile = Join-Path $JsonDir "$($db.Database)-config.json"
        
        if (-not (Test-Path $mappingFile) -or -not (Test-Path $configFile)) {
            Write-Log "Configuration files not found for $($db.Database)" "ERROR"
            continue
        }
        
        Push-Location $DataObfuscation
        try {
            $obfuscateCmd = "dotnet run -- `"$mappingFile`" `"$configFile`""
            if ($DryRun) {
                $obfuscateCmd += " --dry-run"
            }
            
            Write-Log "Executing: $obfuscateCmd"
            
            $result = Invoke-Expression $obfuscateCmd 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Log "Obfuscation failed for $($db.Database)" "ERROR"
                Write-Log $result "ERROR"
                continue
            }
            Write-Log "Obfuscation completed successfully"
        }
        finally {
            Pop-Location
        }
    }
    
    Write-Log "Completed processing for $($db.Database)"
    Write-Log "----------------------------------------"
}

Write-Log "Batch processing completed"

# Generate summary report
$summaryFile = Join-Path $LogDir "batch-summary-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
Write-Log "Generating summary report: $summaryFile"

# Create summary content
$summary = @"
Batch Processing Summary
========================
Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Environment: $Environment
Databases Processed: $($databases.Count)
Dry Run: $DryRun

Database Results:
"@

foreach ($db in $databases) {
    $reportFile = Join-Path $BaseDir "reports\$($db.Database)-obfuscation-*.json"
    if (Test-Path $reportFile) {
        $summary += "`n- $($db.Database): SUCCESS"
    } else {
        $summary += "`n- $($db.Database): FAILED or SKIPPED"
    }
}

Set-Content -Path $summaryFile -Value $summary
Write-Log "Summary report generated"