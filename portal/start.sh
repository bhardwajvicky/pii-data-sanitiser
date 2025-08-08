#!/bin/bash

# PII Obfuscation Portal Startup Script
echo "Starting PII Obfuscation Portal..."

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "Docker is not running. Please start Docker first."
    exit 1
fi

# Navigate to the portal directory
cd "$(dirname "$0")"

# Start the services using Docker Compose
echo "Starting services with Docker Compose..."
docker-compose up -d

# Wait a moment for services to start
sleep 10

# Check if services are running
echo "Checking service status..."
docker-compose ps

echo ""
echo "Application URLs:"
echo "  Web Portal: http://localhost:7002"
echo "  API Documentation: http://localhost:7001/swagger"
echo "  Database: localhost:1433 (sa/YourStrong@Passw0rd)"

echo ""
echo "To stop the application, run: docker-compose down"
