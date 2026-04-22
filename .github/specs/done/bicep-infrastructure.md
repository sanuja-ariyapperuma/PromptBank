# Spec: Azure Infrastructure Provisioning with Bicep

**Status:** ✅ Done  
**Spec file:** `.github/specs/done/bicep-infrastructure.md`

---

## Problem Statement

The Prompt Bank app needs repeatable, version-controlled Azure infrastructure provisioning for two environments (dev and production). This spec defines the Bicep modules, parameter files, and GitHub Actions workflow required to provision and maintain the Azure resources.

This is a **demo app** — the database is SQLite, stored on the App Service persistent `/home` filesystem (backed by Azure Files). No Azure SQL or Key Vault is required.

---

## Target Architecture

```
Azure Resource Group (per environment)
├── App Service Plan              (B1 for dev, B2 for prod)
└── App Service (Web App)         (.NET 10, SQLite database on /home filesystem)
```

---

## Functional Requirements

### FR-1: Modular Bicep Structure
The infrastructure is split into focused, reusable modules:

```
infra/
  main.bicep                    # Orchestration entry point; accepts environment param
  modules/
    appServicePlan.bicep        # App Service Plan
    appService.bicep            # Web App + App Settings (SQLite connection string)
  parameters/
    dev.bicepparam              # Dev environment parameter values
    prod.bicepparam             # Production environment parameter values
```

### FR-2: Parameterised Environments
`main.bicep` accepts an `environment` parameter (`'dev'` or `'prod'`) that drives:
- Resource naming convention: `promptbank-{environment}` (e.g. `promptbank-dev`)
- App Service Plan SKU: `B1` for dev, `B2` for prod

### FR-3: SQLite on Persistent App Service Storage
- The SQLite database file is stored at `/home/data/promptbank.db`.
- The `/home` filesystem on Azure App Service is persistent across restarts (backed by Azure Files).
- On startup, `Program.cs` ensures the `/home/data/` directory exists before EF Core runs `MigrateAsync()`.
- The connection string `Data Source=/home/data/promptbank.db` is set via the `ConnectionStrings__DefaultConnection` app setting.

### FR-4: App Service Configuration
The App Service `appSettings` block sets:

| Setting | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` (dev) / `Production` (prod) |
| `ConnectionStrings__DefaultConnection` | `Data Source=/home/data/promptbank.db` |
| `WEBSITE_RUN_FROM_PACKAGE` | `1` |

### FR-5: Resource Naming Convention
All resources follow a consistent naming pattern:

| Resource | Name pattern |
|---|---|
| Resource Group | `rg-promptbank-{env}` |
| App Service Plan | `asp-promptbank-{env}` |
| App Service | `app-promptbank-{env}` |

### FR-6: GitHub Actions Workflow — Infrastructure Deployment
File: `.github/workflows/infra.yml`

**Trigger:**
- `push` to `main` branch when files under `infra/**` change
- `workflow_dispatch` (manual trigger) with `environment` input (`dev` or `prod`)

**Authentication:**
- Uses **OIDC / Workload Identity Federation** — no stored Azure credentials in GitHub secrets.
- Requires a GitHub secret `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

**Steps:**
1. Checkout code
2. Azure login via OIDC (`azure/login@v2`)
3. Run `az bicep build` to validate templates (lint step)
4. Deploy to dev: `az deployment group create` with `parameters/dev.bicepparam`
5. Deploy to prod: (manual trigger only, or protected environment gate)

### FR-7: Outputs
`main.bicep` outputs the following values (used by the app deployment workflow):

| Output | Description |
|---|---|
| `appServiceName` | Name of the Web App for `az webapp deploy` |
| `appServiceUrl` | Default hostname of the Web App |

---

## Non-Functional Requirements

### NFR-1: Idempotent Deployments
All Bicep modules are safe to re-run — repeated deployments do not duplicate or destroy resources.

### NFR-2: No Secrets Required
- No SQL admin password, no Key Vault, no managed identity SQL roles.
- The only GitHub secrets needed are the OIDC secrets (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`).

### NFR-3: Location Parameterised
Default location is `uksouth`. Overridable via parameter.

---

## Bicep Module Designs

### `main.bicep`
```bicep
@allowed(['dev', 'prod'])
param environment string

param location string = 'uksouth'

var appPlanSku = environment == 'prod' ? 'B2' : 'B1'
var aspnetEnvironment = environment == 'prod' ? 'Production' : 'Development'

module asp 'modules/appServicePlan.bicep' = { ... }
module app 'modules/appService.bicep'     = { ... }

output appServiceName string = app.outputs.name
output appServiceUrl  string = app.outputs.defaultHostName
```

### `modules/appServicePlan.bicep`
- Resource: `Microsoft.Web/serverfarms`
- `kind: 'app'`, `reserved: false` (Windows)
- SKU from environment parameter

### `modules/appService.bicep`
- Resource: `Microsoft.Web/sites`
- `siteConfig.netFrameworkVersion: 'v10.0'`
- App Settings block (see FR-4)
- SQLite connection string: `Data Source=/home/data/promptbank.db`

---

## Parameter Files

### `infra/parameters/dev.bicepparam`
```bicep
using '../main.bicep'

param environment = 'dev'
param location    = 'uksouth'
```

### `infra/parameters/prod.bicepparam`
```bicep
using '../main.bicep'

param environment = 'prod'
param location    = 'uksouth'
```

---

## GitHub Actions Workflow Outline

```yaml
# .github/workflows/infra.yml
name: Infrastructure Provisioning

on:
  push:
    branches: [main]
    paths: ['infra/**']
  workflow_dispatch:
    inputs:
      environment:
        description: 'Target environment'
        required: true
        default: 'dev'
        type: choice
        options: [dev, prod]

permissions:
  id-token: write   # Required for OIDC
  contents: read

jobs:
  provision:
    runs-on: ubuntu-latest
    environment: ${{ github.event.inputs.environment || 'dev' }}
    steps:
      - uses: actions/checkout@v4
      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - name: Validate Bicep
        run: az bicep build --file infra/main.bicep
      - name: Deploy infrastructure
        run: |
          az deployment group create \
            --resource-group rg-promptbank-${{ env.ENVIRONMENT }} \
            --template-file infra/main.bicep \
            --parameters infra/parameters/${{ env.ENVIRONMENT }}.bicepparam
```

---

## GitHub Secrets Required

| Secret | Description |
|---|---|
| `AZURE_CLIENT_ID` | Client ID of the GitHub OIDC app registration |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |

---

## Acceptance Criteria

| # | Criterion |
|---|---|
| AC-1 | `az bicep build --file infra/main.bicep` succeeds with no errors |
| AC-2 | Running the workflow provisions both resources (App Service Plan + App Service) in the correct resource group |
| AC-3 | App Service `ConnectionStrings__DefaultConnection` is set to `Data Source=/home/data/promptbank.db` |
| AC-4 | Re-running the workflow on an existing environment is idempotent (no errors, no duplicates) |
| AC-5 | Dev deployment uses B1 App Service Plan; prod uses B2 |
| AC-6 | No secrets appear in source control or workflow logs |
| AC-7 | `main.bicep` outputs are accessible after deployment for use by app deployment workflows |
