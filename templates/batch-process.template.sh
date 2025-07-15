#!/bin/bash
# Bash Template for Batch Processing Multiple Databases
# Copy and customize this script for your environment

# Default values
ENVIRONMENT="production"
DRY_RUN=false
ANALYZE_ONLY=false
OBFUSCATE_ONLY=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -d|--dry-run)
            DRY_RUN=true
            shift
            ;;
        -a|--analyze-only)
            ANALYZE_ONLY=true
            shift
            ;;
        -o|--obfuscate-only)
            OBFUSCATE_ONLY=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Configuration
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
BASE_DIR="$(dirname "$SCRIPT_DIR")"
AUTO_MAPPING_GENERATOR="$BASE_DIR/auto-mapping-generator"
DATA_OBFUSCATION="$BASE_DIR/data-obfuscation"
JSON_DIR="$BASE_DIR/JSON"
LOG_DIR="$BASE_DIR/logs"

# Create directories if they don't exist
mkdir -p "$JSON_DIR" "$LOG_DIR"

# Database configuration
declare -A databases
databases["Database1"]="Server=server1.example.com;Database=Database1;Integrated Security=true;TrustServerCertificate=true;"
databases["Database2"]="Server=server2.example.com;Database=Database2;User Id=sa;Password=YourPassword123!;TrustServerCertificate=true;"

# Logging function
log() {
    local level=$1
    shift
    local message="$@"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    echo "$timestamp [$level] $message"
    echo "$timestamp [$level] $message" >> "$LOG_DIR/batch-process-$(date +%Y%m%d).log"
}

log "INFO" "Starting batch processing for ${#databases[@]} databases"
log "INFO" "Environment: $ENVIRONMENT"
log "INFO" "Dry Run: $DRY_RUN"

# Process each database
for db_name in "${!databases[@]}"; do
    connection_string="${databases[$db_name]}"
    log "INFO" "Processing database: $db_name"
    
    # Step 1: Analyze Schema
    if [[ "$OBFUSCATE_ONLY" != "true" ]]; then
        log "INFO" "Analyzing schema for $db_name"
        
        cd "$AUTO_MAPPING_GENERATOR"
        if dotnet run -- "$connection_string"; then
            log "INFO" "Schema analysis completed successfully for $db_name"
        else
            log "ERROR" "Schema analysis failed for $db_name"
            continue
        fi
    fi
    
    # Step 2: Obfuscate Data
    if [[ "$ANALYZE_ONLY" != "true" ]]; then
        log "INFO" "Starting obfuscation for $db_name"
        
        mapping_file="$JSON_DIR/${db_name}-mapping.json"
        config_file="$JSON_DIR/${db_name}-config.json"
        
        if [[ ! -f "$mapping_file" ]] || [[ ! -f "$config_file" ]]; then
            log "ERROR" "Configuration files not found for $db_name"
            continue
        fi
        
        cd "$DATA_OBFUSCATION"
        cmd="dotnet run -- \"$mapping_file\" \"$config_file\""
        
        if [[ "$DRY_RUN" == "true" ]]; then
            cmd="$cmd --dry-run"
        fi
        
        log "INFO" "Executing: $cmd"
        
        if eval $cmd; then
            log "INFO" "Obfuscation completed successfully for $db_name"
        else
            log "ERROR" "Obfuscation failed for $db_name"
            continue
        fi
    fi
    
    log "INFO" "Completed processing for $db_name"
    log "INFO" "----------------------------------------"
done

log "INFO" "Batch processing completed"

# Generate summary report
summary_file="$LOG_DIR/batch-summary-$(date +%Y%m%d-%H%M%S).txt"
log "INFO" "Generating summary report: $summary_file"

cat > "$summary_file" << EOF
Batch Processing Summary
========================
Date: $(date '+%Y-%m-%d %H:%M:%S')
Environment: $ENVIRONMENT
Databases Processed: ${#databases[@]}
Dry Run: $DRY_RUN

Database Results:
EOF

for db_name in "${!databases[@]}"; do
    if ls "$BASE_DIR/reports/${db_name}-obfuscation-"*.json 2>/dev/null | grep -q .; then
        echo "- $db_name: SUCCESS" >> "$summary_file"
    else
        echo "- $db_name: FAILED or SKIPPED" >> "$summary_file"
    fi
done

log "INFO" "Summary report generated"