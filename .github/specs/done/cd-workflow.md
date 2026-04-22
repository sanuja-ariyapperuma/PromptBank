# Spec: CD Workflow

**Status:** ✅ Done  
**Spec file:** `.github/specs/cd-workflow.md`

---

## Problem Statement

The repository has no continuous deployment workflow. After CI validates a change, there is no
automated path to get the built application into Azure App Service. This spec defines a GitHub
Actions CD workflow that deploys the app to the **dev** environment when a PR's CI passes, and
to the **prod** environment when a PR is merged to `main`.

---

## Architecture Overview

```
PR opened
  └─▶ CI (ci.yml) runs tests + publishes artifact
        └─▶ CD (cd.yml) deploys to dev GitHub Environment
              └─▶ Manual approval gate (prod GitHub Environment) → deploy to prod

PR merged to main
  └─▶ CI (ci.yml) runs tests + publishes artifact
        └─▶ CD (cd.yml) deploys to prod GitHub Environment (requires reviewer approval)
```

---

## Functional Requirements

### FR-1: Trigger
CD is triggered by the `workflow_run` event on successful completion of the `CI` workflow:
```yaml
on:
  workflow_run:
    workflows: ["CI"]
    types: [completed]
```

### FR-2: Deploy to Dev on PR
When CI completes successfully on a `pull_request` event, deploy to the `dev` Azure App Service:
- App Service: `app-promptbank-dev`
- Resource group: `rg-promptbank-dev`
- GitHub Environment: `dev`

### FR-3: Deploy to Prod on Merge
When CI completes successfully on a `push` event (i.e., PR merged to `main`), deploy to the
`prod` Azure App Service — gated by a required reviewer in the `prod` GitHub Environment:
- App Service: `app-promptbank-prod`
- Resource group: `rg-promptbank-prod`
- GitHub Environment: `prod`

### FR-4: Skip on CI Failure
If CI did not conclude with `success`, the CD workflow exits without deploying to either
environment.

### FR-5: Build Artifact — CI Changes
Add the following steps to `ci.yml` after the unit test step, so the published output is
available to CD:
```yaml
- name: Publish
  run: dotnet publish PromptBank --no-build --configuration Release --output ./publish

- name: Upload artifact
  uses: actions/upload-artifact@v4
  with:
    name: promptbank-app
    path: ./publish
    retention-days: 7
```

### FR-6: Download Artifact from CI Run
CD downloads the published artifact uploaded by CI using `actions/download-artifact@v4`,
referencing the triggering workflow run's ID so the artifact is fetched across workflow runs:
```yaml
- name: Download artifact
  uses: actions/download-artifact@v4
  with:
    name: promptbank-app
    path: ./publish
    run-id: ${{ github.event.workflow_run.id }}
    github-token: ${{ secrets.GITHUB_TOKEN }}

- name: Zip artifact for deployment
  run: zip -r app.zip ./publish
```

### FR-7: Deploy via az webapp deploy
CD deploys the zipped artifact to the App Service using the zip deployment method
(compatible with `WEBSITE_RUN_FROM_PACKAGE=1`):
```bash
az webapp deploy \
  --name app-promptbank-${ENVIRONMENT} \
  --resource-group rg-promptbank-${ENVIRONMENT} \
  --type zip \
  --src-path app.zip
```

### FR-8: Authentication
Use OIDC / Workload Identity Federation (same as `infra.yml`). Required GitHub secrets:
- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

### FR-9: Permissions
```yaml
permissions:
  id-token: write     # OIDC token exchange
  contents: read
  actions: read       # Required to download artifacts from the CI workflow run
```

### FR-10: Workflow Style
Follow the same header comment style and structure as `.github/workflows/infra.yml`.

### FR-11: Workflow File Location
```
.github/workflows/cd.yml
```

---

## Non-Functional Requirements

### NFR-1: GitHub Environment Approval Gate
The `prod` GitHub Environment must be configured in the repository settings with at least one
required reviewer before the workflow is useful. This is a one-time manual step in GitHub UI
(Settings → Environments → prod → Required reviewers). The `dev` environment requires no
approval.

### NFR-2: No Duplicate Secrets
CD reuses the same OIDC secrets as `infra.yml`. No additional secrets are required.

### NFR-3: Idempotent
Re-running CD (e.g., via "Re-run jobs") redeploys the same artifact with the same result.

---

## File Changes

| File | Change |
|---|---|
| `.github/workflows/cd.yml` | New — CD workflow |
| `.github/workflows/ci.yml` | Add `dotnet publish` and `upload-artifact` steps |

---

## ⚠️ Warnings

### WARNING-1: Seed Users Created in Production
`Program.cs` seeds `alice`, `bob`, and `carol` user accounts unconditionally on every startup
in every environment. These test accounts will be created in production with known passwords.

**Recommendation:** Gate seed user creation behind `IsDevelopment()` or a configuration flag.

### WARNING-2: GitHub Environments Must Be Configured
The `dev` and `prod` GitHub Environments and their protection rules must exist in the repository
settings for the `environment:` key in the workflow to work. If they don't exist, the workflow
will still run but deployment protection rules will not be enforced for prod.

---

## Acceptance Criteria

| # | Criterion |
|---|---|
| AC-1 | Opening a PR triggers CI, which on success triggers CD deploy to dev |
| AC-2 | Merging a PR to main triggers CI, which on success triggers CD deploy to prod (after approval) |
| AC-3 | A failed CI run prevents any CD deployment |
| AC-4 | The `prod` deployment is gated by a required reviewer in the GitHub Environment |
| AC-5 | Azure deployment uses OIDC — no stored credentials |
| AC-6 | Artifact is uploaded by CI and downloaded by CD (no rebuild in CD) |
| AC-7 | The app starts correctly on Azure App Service after deployment |
