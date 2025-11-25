#!/bin/bash
# deploy-with-chat.sh - Deploy Expense Management System with GenAI services
# 
# Prerequisites:
# 1. Azure CLI installed and logged in (az login)
# 2. Subscription context set (az account set --subscription <subscription-id>)
#
# Usage: ./deploy-with-chat.sh

set -e

echo "=========================================="
echo "Expense Management System Deployment"
echo "       (with GenAI Services)"
echo "=========================================="
echo ""

# Configuration
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"

# Get the current user's details for SQL admin
echo "Getting current user details..."
ADMIN_LOGIN=$(az account show --query user.name -o tsv)
ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

echo "Admin Login: $ADMIN_LOGIN"
echo "Admin Object ID: $ADMIN_OBJECT_ID"
echo ""

# Create resource group if it doesn't exist
echo "Creating resource group: $RESOURCE_GROUP..."
az group create --name $RESOURCE_GROUP --location $LOCATION --output none

# Deploy infrastructure with GenAI
echo ""
echo "Deploying infrastructure (App Service, SQL Database, Azure OpenAI, AI Search)..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group $RESOURCE_GROUP \
    --template-file infrastructure/main.bicep \
    --parameters adminLogin="$ADMIN_LOGIN" adminObjectId="$ADMIN_OBJECT_ID" deployGenAI=true \
    --query "properties.outputs" \
    --output json)

# Extract outputs
APP_SERVICE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceName.value')
APP_SERVICE_HOST=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceHostName.value')
MANAGED_IDENTITY_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityClientId.value')
SQL_SERVER_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerName.value')
SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerFqdn.value')
DATABASE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.databaseName.value')
OPENAI_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIEndpoint.value')
OPENAI_MODEL_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIModelName.value')
SEARCH_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.searchEndpoint.value')

echo ""
echo "Deployment outputs:"
echo "  App Service: $APP_SERVICE_NAME"
echo "  App Service URL: https://$APP_SERVICE_HOST"
echo "  SQL Server: $SQL_SERVER_NAME"
echo "  Database: $DATABASE_NAME"
echo "  Managed Identity: $MANAGED_IDENTITY_NAME"
echo "  OpenAI Endpoint: $OPENAI_ENDPOINT"
echo "  OpenAI Model: $OPENAI_MODEL_NAME"
echo "  Search Endpoint: $SEARCH_ENDPOINT"
echo ""

# Configure App Service settings
echo "Configuring App Service settings..."
CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN},1433;Database=${DATABASE_NAME};Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};"

az webapp config appsettings set \
    --name $APP_SERVICE_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings "ConnectionStrings__DefaultConnection=$CONNECTION_STRING" \
               "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
               "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
               "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
               "Search__Endpoint=$SEARCH_ENDPOINT" \
               "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
    --output none

echo "App Service settings configured."
echo ""

# Wait for SQL Server to be fully ready
echo "Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30

# Add current IP to SQL firewall
echo "Adding current IP to SQL Server firewall..."
MY_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER_NAME \
    --name "DeploymentClient" \
    --start-ip-address $MY_IP \
    --end-ip-address $MY_IP \
    --output none 2>/dev/null || true

echo ""

# Install Python dependencies
echo "Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity

# Update Python scripts with actual server names
echo "Updating Python scripts with deployment values..."
sed -i.bak "s/sql-expensemgmt.database.windows.net/${SQL_SERVER_FQDN}/g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s/sql-expensemgmt.database.windows.net/${SQL_SERVER_FQDN}/g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s/sql-expensemgmt.database.windows.net/${SQL_SERVER_FQDN}/g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak

# Update script.sql with managed identity name
sed -i.bak "s/MANAGED-IDENTITY-NAME/${MANAGED_IDENTITY_NAME}/g" script.sql && rm -f script.sql.bak

# Import database schema
echo ""
echo "Importing database schema..."
python3 run-sql.py

# Grant managed identity access
echo ""
echo "Granting managed identity database access..."
python3 run-sql-dbrole.py

# Create stored procedures
echo ""
echo "Creating stored procedures..."
python3 run-sql-stored-procs.py

# Build and package the application
echo ""
echo "Building application..."
cd src/ExpenseManagement
dotnet restore
dotnet publish -c Release -o ./publish

# Create zip file with correct structure
echo "Creating deployment package..."
cd publish
zip -r ../../../app.zip ./*
cd ../../..

# Deploy the application
echo ""
echo "Deploying application to App Service..."
az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $APP_SERVICE_NAME \
    --src-path ./app.zip \
    --type zip

echo ""
echo "=========================================="
echo "Deployment Complete!"
echo "=========================================="
echo ""
echo "Application URL: https://${APP_SERVICE_HOST}/Index"
echo ""
echo "Features enabled:"
echo "  ✓ Expense Management Dashboard"
echo "  ✓ Create/View/Submit Expenses"
echo "  ✓ Approve/Reject Expenses (Manager)"
echo "  ✓ AI-Powered Chat Assistant"
echo "  ✓ REST API with Swagger Documentation"
echo ""
echo "NOTE: Navigate to /Index to view the application"
echo "      API documentation available at /swagger"
echo "      Chat assistant available at /Chat"
echo ""
