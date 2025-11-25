# Azure Services Architecture

This document describes the Azure services deployed by this solution and how they connect to each other.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                                    Azure Cloud                                       │
│                                                                                      │
│  ┌─────────────────────────────────────────────────────────────────────────────┐    │
│  │                        Resource Group (UK South)                             │    │
│  │                                                                              │    │
│  │   ┌─────────────────┐         ┌─────────────────┐                           │    │
│  │   │   User Assigned  │         │   App Service   │                           │    │
│  │   │ Managed Identity │◄───────►│   (ASP.NET 8)   │                           │    │
│  │   │                  │         │                 │                           │    │
│  │   │  mid-appmod...   │         │  app-expense... │                           │    │
│  │   └────────┬─────────┘         └────────┬────────┘                           │    │
│  │            │                            │                                     │    │
│  │            │    Managed Identity        │                                     │    │
│  │            │    Authentication          │                                     │    │
│  │            │                            │                                     │    │
│  │            ▼                            ▼                                     │    │
│  │   ┌─────────────────┐         ┌─────────────────┐                           │    │
│  │   │   Azure SQL     │◄───────►│    Northwind    │                           │    │
│  │   │    Server       │         │    Database     │                           │    │
│  │   │                 │         │                 │                           │    │
│  │   │ sql-expense...  │         │   (Basic Tier)  │                           │    │
│  │   └─────────────────┘         └─────────────────┘                           │    │
│  │                                                                              │    │
│  └─────────────────────────────────────────────────────────────────────────────┘    │
│                                                                                      │
│  ┌─────────────────────────────────────────────────────────────────────────────┐    │
│  │                     GenAI Resources (Sweden Central)                         │    │
│  │                        (Optional - deploy-with-chat.sh)                      │    │
│  │                                                                              │    │
│  │   ┌─────────────────┐         ┌─────────────────┐                           │    │
│  │   │  Azure OpenAI   │         │    AI Search    │                           │    │
│  │   │    Service      │         │    Service      │                           │    │
│  │   │                 │         │                 │                           │    │
│  │   │  aoai-expense...│         │ search-expense..│                           │    │
│  │   │                 │         │                 │                           │    │
│  │   │   GPT-4o Model  │         │   Basic Tier    │                           │    │
│  │   └────────┬────────┘         └────────┬────────┘                           │    │
│  │            │                           │                                     │    │
│  │            │   Cognitive Services      │    Search Index                    │    │
│  │            │   OpenAI User Role        │    Data Reader Role                │    │
│  │            │                           │                                     │    │
│  │            └───────────────────────────┴─────────────────────────────────────┤    │
│  │                                    │                                         │    │
│  │                           Managed Identity                                   │    │
│  │                           Authentication                                     │    │
│  │                                    │                                         │    │
│  │                                    ▼                                         │    │
│  │                           ┌───────────────────┐                              │    │
│  │                           │  User Assigned    │                              │    │
│  │                           │ Managed Identity  │                              │    │
│  │                           └───────────────────┘                              │    │
│  │                                                                              │    │
│  └─────────────────────────────────────────────────────────────────────────────┘    │
│                                                                                      │
└─────────────────────────────────────────────────────────────────────────────────────┘


                                        │
                                        │ HTTPS
                                        │
                                        ▼
                               ┌─────────────────┐
                               │     Users       │
                               │   (Browsers)    │
                               └─────────────────┘
```

## Service Connections

### Authentication Flow
1. **User Assigned Managed Identity** is created and assigned to the App Service
2. The same identity is granted roles on:
   - **Azure SQL**: db_datareader, db_datawriter, EXECUTE permissions
   - **Azure OpenAI**: Cognitive Services OpenAI User role
   - **AI Search**: Search Index Data Reader role

### Data Flow
1. Users access the **App Service** via HTTPS
2. App Service uses the **Managed Identity** to authenticate with Azure SQL
3. Stored procedures in the **Northwind Database** handle all data operations
4. Chat functionality uses the **Managed Identity** to call Azure OpenAI
5. AI Search provides RAG capabilities for context-aware responses

## Deployment Scripts

| Script | Description |
|--------|-------------|
| `deploy.sh` | Deploys App Service + SQL Database only |
| `deploy-with-chat.sh` | Deploys all services including GenAI |

## Resource Naming Convention

All resources use a unique suffix generated from the resource group ID:
- `app-expensemgmt-{suffix}` - App Service
- `sql-expensemgmt-{suffix}` - SQL Server
- `mid-appmodassist-{suffix}` - Managed Identity
- `aoai-expensemgmt-{suffix}` - Azure OpenAI
- `search-expensemgmt-{suffix}` - AI Search
