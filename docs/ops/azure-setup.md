# Azure demo environment: one-off setup

The `deploy` workflow (`.github/workflows/deploy.yml`) deploys every green `main` to a free Azure environment using
OpenID Connect (no stored cloud credentials). Do these steps once, in order. `<owner>` is the GitHub account or
organisation that owns the repository; pick your own names for the `<...>` placeholders.

Everything created is on a free tier: App Service plan F1 (Linux), Azure SQL free offer (serverless, auto-pause when
the monthly free allowance is exhausted). Estimated cost is zero (at most £1 per month).

## 1. Resource group
```bash
az login
az group create --name <resource-group> --location <region>
```

## 2. App registration and federated credentials
```bash
az ad app create --display-name cardlab-deploy
# note the appId (client id) and the app's object id
az ad sp create --id <app-id>
az ad app federated-credential create --id <app-object-id> --parameters '{
  "name": "cardlab-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<owner>/cardlab:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'
az ad app federated-credential create --id <app-object-id> --parameters '{
  "name": "cardlab-env-demo",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<owner>/cardlab:environment:demo",
  "audiences": ["api://AzureADTokenExchange"]
}'
```
The job runs in environment `demo`, so GitHub presents the environment subject; the `main` credential is kept per
design section 10.

## 3. Contributor on the resource group only
```bash
az role assignment create --assignee <app-id> --role Contributor \
  --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>
```
Do not grant anything at subscription scope.

## 4. Repository variables
In GitHub: Settings, Secrets and variables, Actions, Variables. These are identifiers, not secrets. Do not create any
repository secrets.

| Variable | Value |
|----------|-------|
| `AZURE_CLIENT_ID` | the app registration's client id |
| `AZURE_TENANT_ID` | your Entra tenant id |
| `AZURE_SUBSCRIPTION_ID` | the subscription id |
| `AZURE_RESOURCE_GROUP` | `<resource-group>` |
| `AZURE_WEBAPP_NAME` | globally unique web app name |
| `AZURE_SQL_SERVER_NAME` | globally unique SQL server name |
| `AZURE_SQL_ADMIN_LOGIN` | your Entra login (UPN) to administer the SQL server |
| `AZURE_SQL_ADMIN_OBJECT_ID` | that identity's object id (`az ad signed-in-user show --query id -o tsv`) |
| `DEMO_URL` | `https://<webapp-name>.azurewebsites.net` |
| `TICKET_KEY_PATTERN` | ticket key regex, e.g. `CL-\d+` |

## 5. Environment `demo`
Settings, Environments, New environment, name it exactly `demo`. Under *Deployment branches and tags* choose *Selected branches and tags* and add `main` only (required: the OIDC credential trusts this environment, so any other branch must not be able to use it).

## 6. First deploy
Actions, `deploy`, Run workflow on `main`. The Bicep step creates the plan, web app, SQL server and database. The
run will fail at the health wait (the database user and lookup key do not exist yet); that is expected. Continue with
steps 7 and 8, then re-run the workflow.

## 7. SQL user for the web app (managed identity)
The Bicep only allows Azure services through the SQL firewall, so first allow your own address:
```bash
az sql server firewall-rule create -g <resource-group> -s <sql-server-name> -n setup-client \
  --start-ip-address <your-ip> --end-ip-address <your-ip>
```
Connect to the `cardlab` database as the SQL Entra admin (for example with the Azure portal query editor or
`sqlcmd -G`) and run, replacing `<webapp-name>` with `AZURE_WEBAPP_NAME`:
```sql
CREATE USER [<webapp-name>] FROM EXTERNAL PROVIDER;
ALTER ROLE db_ddladmin ADD MEMBER [<webapp-name>];
ALTER ROLE db_datareader ADD MEMBER [<webapp-name>];
ALTER ROLE db_datawriter ADD MEMBER [<webapp-name>];
```
Then remove the temporary rule (incremental Bicep deploys do not remove it):
```bash
az sql server firewall-rule delete -g <resource-group> -s <sql-server-name> -n setup-client
```
`db_ddladmin` is needed because migrations run at startup. No password exists anywhere; the app connects with its
managed identity.

## 8. Lookup key (`CardLab__LookupKey`)
Generate a random 32-byte value and set it once as an App Service setting. It must never be committed or stored in
GitHub:
```bash
key=$(openssl rand -base64 32)
az webapp config appsettings set -g <resource-group> -n <webapp-name> --settings "CardLab__LookupKey=$key" --output none
```
The Bicep declares no app settings and `deploy.yml` only merges its own keys, so the value survives redeploys. After
the second deploy, confirm it is still present:
```bash
az webapp config appsettings list -g <resource-group> -n <webapp-name> --query "[?name=='CardLab__LookupKey'].name" -o tsv
```
It must print `CardLab__LookupKey`. Never set `CardLab__TestEndpoints` on demo, and leave `CardLab__Commit` unset: the health endpoint then reports the
commit stamped into the deployed build (`SourceRevisionId`), which is what the deploy's health wait compares.

Re-run the `deploy` workflow now; the health wait and the smoke project should pass.

## 9. Branch protection (DEC-21)
Settings, Branches, add a rule for `main` requiring pull requests and these status checks: `ci / backend`,
`ci / web`, `ci / e2e`, `ci / ticket-key`, and CodeQL. Enable *Require branches to be up to date before merging* so the commit that lands on `main` (and
deploys) is the one CI tested.

## 10. Secret scanning
Settings, Code security: keep secret scanning (and push protection) enabled. The repository must report no secrets,
and `deploy.yml` must contain no stored cloud credentials (OIDC only).

## Rollback
Open the last green `deploy` run in Actions and choose *Re-run all jobs*. A re-run uses that run's commit, and GitHub
allows re-runs for 30 days. Past that window, revert the bad commit on `main`. The database schema is forward-only.
