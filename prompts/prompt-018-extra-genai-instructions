Extra instructions for Creating Azure infrastructure with Bicep and deployment script that properly configures Azure OpenAI integration with App Service using Managed Identity authentication.

Bear the below in mind when creating the bicep for the genai resources and endpoints:

CRITICAL REQUIREMENTS:

1. **Bicep Structure:**
   - Create app-service.bicep that creates the Managed Identity and App Service
   - Create genai.bicep that creates Azure OpenAI and grants the Managed Identity the "Cognitive Services OpenAI User" role
   - In genai.bicep, output: openAIEndpoint, openAIModelName, openAIName
   - In main.bicep, conditionally deploy genai.bicep and output the OpenAI configuration values

2. **Deployment Script MUST:**
   - Deploy the Bicep infrastructure first
   - After deployment completes, retrieve the OpenAI endpoint and model name from deployment outputs using:
     ```bash
     az deployment group show --query "properties.outputs.openAIEndpoint.value"
     ```
   - Configure the App Service settings with the retrieved values using:
     ```bash
     az webapp config appsettings set --settings \
         "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
         "OpenAI__DeploymentName=$OPENAI_MODEL_NAME"
     ```
   - This MUST happen AFTER infrastructure deployment, not during it

3. **Why this is necessary:**
   - Bicep cannot configure these settings directly due to circular dependency
   - App Service needs the Managed Identity first
   - GenAI resources need the Managed Identity ID for role assignment
   - App settings need the OpenAI endpoint (which doesn't exist until GenAI deployment completes)
   - The deployment script breaks this cycle by configuring settings post-deployment

4. **Application Code:**
   - Use Azure.Identity package with DefaultAzureCredential
   - Configure OpenAIClient with Managed Identity, not API keys
   - Read OpenAI:Endpoint and OpenAI:DeploymentName from configuration

Do NOT try to configure OpenAI__Endpoint or OpenAI__DeploymentName in the Bicep app settings. This will create a circular dependency that cannot be resolved in Bicep alone.
