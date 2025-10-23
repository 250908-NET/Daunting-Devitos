#!/bin/bash

# Configuration
CONTAINER_NAME="mssql-express"
SA_PASSWORD="YourStrong@Passw0rd"
MSSQL_PORT=1433

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${YELLOW}Starting SQL Server Express in Docker...${NC}"

# Check if container already exists
if [ "$(docker ps -a -q -f name=$CONTAINER_NAME)" ]; then
    echo -e "${YELLOW}Container '$CONTAINER_NAME' already exists.${NC}"
    
    # Check if it's running
    if [ "$(docker ps -q -f name=$CONTAINER_NAME)" ]; then
        echo -e "${GREEN}Container is already running.${NC}"
    else
        echo -e "${YELLOW}Starting existing container...${NC}"
        docker start $CONTAINER_NAME
        echo -e "${GREEN}Container started successfully!${NC}"
    fi
else
    echo -e "${YELLOW}Creating new SQL Server container...${NC}"
    
    # Run new container
    docker run -e "ACCEPT_EULA=Y" \
        -e "MSSQL_SA_PASSWORD=$SA_PASSWORD" \
        -e "MSSQL_PID=Express" \
        -p $MSSQL_PORT:1433 \
        --name $CONTAINER_NAME \
        --hostname mssql-express \
        -d \
        mcr.microsoft.com/mssql/server:2022-latest
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}Container created and started successfully!${NC}"
        echo -e "${YELLOW}Waiting for SQL Server to be ready...${NC}"
        sleep 15
    else
        echo -e "${RED}Failed to create container.${NC}"
        exit 1
    fi
fi

# Display connection information
echo ""
echo -e "${GREEN}==================================${NC}"
echo -e "${GREEN}SQL Server Express is running!${NC}"
echo -e "${GREEN}==================================${NC}"
echo -e "Server: ${YELLOW}localhost,$MSSQL_PORT${NC}"
echo -e "Username: ${YELLOW}sa${NC}"
echo -e "Password: ${YELLOW}$SA_PASSWORD${NC}"
echo -e "Connection String: ${YELLOW}Server=localhost,$MSSQL_PORT;User Id=sa;Password=$SA_PASSWORD;TrustServerCertificate=True;${NC}"
echo ""
echo -e "To stop: ${YELLOW}docker stop $CONTAINER_NAME${NC}"
echo -e "To remove: ${YELLOW}docker rm $CONTAINER_NAME${NC}"
echo -e "To view logs: ${YELLOW}docker logs $CONTAINER_NAME${NC}"
echo ""