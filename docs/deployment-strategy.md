# Deployment strategy

This explains how Gift of the Givers gets from a commit to the live Azure resources, how each part can be rolled back, how secrets are kept out of the code, and how database changes are made.

## What gets deployed

| Part | Azure resource | Plan |
|---|---|---|
| Web app (Razor Pages) | `Gift-of-the-givers-prototype` (App Service, South Africa North) | F1 Free |
| Tax certificate Function | `GiftOfTheGiversFunctions` (Function App, Windows) | Y1 Consumption |
| Database | `free-sql-db-0536742` on `gift-givers-relief-sql` | Serverless GP_S_Gen5, free offer, auto-pauses after 60 min |

## The pipeline

Everything is in `azure-pipelines.yml` in Azure Repos. A push to `main` runs every stage in order. A pull request only runs Build and Test, so nothing untested or unreviewed reaches Azure.

```
Build -> Test -> Deploy SQL schema -> Deploy Function App -> Deploy web app
```

1. **Build** restores packages (the Helpers library comes from our Azure Artifacts feed), builds the web app and the Function, and publishes three artifacts:
   - `webapp`: the zipped web app. The build number is stamped into the app's version, so the footer of the live site shows `Build <run number>`.
   - `functionapp`: the zipped Function App.
   - `sql`: the EF Core scripts (see the database section below).
   - `deploy`: the PowerShell scripts the deploy stages use.
2. **Test** runs the unit and integration tests and publishes the test results and code coverage.
3. **Deploy SQL schema** applies any new EF Core migrations to Azure SQL.
4. **Deploy Function App** checks the Function's app settings, zip-deploys it with the `AzureFunctionApp` task, and smoke-tests `GenerateTaxCertificate`.
5. **Deploy web app** checks the web app's settings, zip-deploys it with the `AzureWebApp` task, and smoke-tests the home page.

The database goes first on purpose. Our migrations only add things (for example an index), so the old app keeps working on the new schema while the new app is being deployed.

The deploy stages are deployment jobs that target the `production` environment, so Azure DevOps keeps a deployment history under Pipelines > Environments.

### Connecting to Azure

The pipeline signs in to Azure with the service connection `gotg-azure-wif`. It uses **workload identity federation**, so there is no password or client secret stored anywhere. Behind it is a user-assigned managed identity, `gotg-ado-deploy`, that only has the **Contributor** role on the `Gift-of-the-givers-prototype_group` resource group. It cannot touch anything else in the subscription.

## Secrets and configuration

- The SQL connection string lives only in the App Service app settings (`ConnectionStrings__DefaultConnection`) of the web app and the Function App. It is not in `appsettings.json`, not in the repo and not in a pipeline variable.
- When the database stage needs the connection string, it reads it from the web app's settings at run time through the service connection, and registers it as a secret straight away (`##vso[task.setsecret]`). Azure DevOps then masks it in every log.
- The Function key for the smoke test is fetched the same way (`az functionapp function keys list`) and masked. It is sent in the `x-functions-key` header, not in the URL, and never printed.
- Non-secret settings (resource names, URLs, the donation used for the smoke test) are in the variable group **gotg-deploy-config** under Pipelines > Library. It holds no secrets. If we ever need a secret pipeline variable, it would go in that group marked as secret, or in Azure Key Vault linked to the group.
- Before each deploy, the pipeline checks that the required app settings exist (`Assert-AppSettings.ps1`). It prints only the setting names and "present" or "MISSING". It also sets `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated` and `FUNCTIONS_EXTENSION_VERSION=~4` on the Function App, so those can't drift.

## Database changes (EF Core migrations)

The live database was first built by hand, so it had the tables but no EF Core migration history. The old migrations in the repo were left over from an earlier version of the app and no longer matched the model, so I replaced them with:

- `InitialBaseline`: the current model. It describes the tables that already exist.
- `AddDonationDonorDateIndex`: a real change. It adds the index `IX_Donations_UserId_DonationDate` on `Donations (UserId, DonationDate DESC)`. The donor dashboard loads a donor's donations newest first, and the Phase 2 load test showed the dashboard is one of the slower pages. Adding an index is safe for the running app.

How a migration reaches Azure:

1. The Build stage runs `dotnet ef migrations script --idempotent` and saves `migrate-idempotent.sql`. Each migration in that script only runs if it is not already in `__EFMigrationsHistory`, so it is safe to run on every deploy.
2. The Build stage also saves `rollback-latest-migration.sql` (`dotnet ef migrations script <newest> <previous>`), which undoes the newest migration.
3. The deploy stage first runs `baseline-existing-database.sql`. If the tables exist but the history table doesn't (our case, once), it creates the history table and records `InitialBaseline` as applied. After that it does nothing.
4. Then it runs `migrate-idempotent.sql` with `Invoke-Sqlcmd` and prints the migration history before and after, and checks that the new index exists.

**Serverless wake-up.** The database pauses after an hour without use. The deploy script tries to connect up to 10 times, 30 seconds apart, before it gives up.

**Firewall.** The SQL server has "Allow Azure services and resources to access this server" turned on (the `AllowAllWindowsAzureIps` rule). The Microsoft-hosted agent runs in Azure, so it can connect without adding its own IP address. The service connection is not given any rights on the SQL server's resource group. If that setting were ever turned off, the alternative is a step that adds the agent's IP as a firewall rule before the migration and removes it afterwards (`az sql server firewall-rule create/delete`). That would need the identity to have rights on the SQL server.

## Rollback

### Web app

The App Service plan is **F1 Free**, which has no deployment slots, so a slot swap isn't possible without upgrading to Standard. I didn't upgrade, to stay within the free credit. Instead:

1. **Smoke test.** After every deploy, the pipeline loads the home page and checks for HTTP 200 and that the footer shows the build number of the run that just deployed. It retries for up to 3 minutes while the app restarts.
2. **Automatic rollback.** If the deploy or the smoke test fails, the next two steps (which only run on failure) download the `webapp` artifact from the most recent **successful** run on `main` and deploy it again. The run still shows as failed so someone looks at it.
3. **Manual rollback to any earlier run.** In Azure DevOps, choose *Run pipeline* on `main` and set **rollbackRunId** to the ID of the run you want to go back to (the number in the run's URL, `buildId=...`). That run skips Build, Test and the normal deploys, and runs one Rollback stage instead. It downloads that run's `webapp` and `functionapp` artifacts, deploys them, and smoke-tests the home page (expecting that run's build number in the footer) and the Function. It also publishes the packages again so the rollback run counts as "last good" for the automatic rollback.
4. **Rolling forward again** is just a new push to `main`, or re-running the latest good run.

Artifacts are kept as long as the run is kept (Azure DevOps keeps the last runs by default), so recent builds can always be redeployed.

### Function App

The Function is deployed from the same pipeline, so it rolls back the same way: the manual rollback run redeploys the `functionapp` artifact from the chosen run, and the smoke test then calls `GenerateTaxCertificate` again.

### Database

App rollbacks do not touch the database. Our migrations are additive, so an older app still works on the newer schema.

If a migration itself has to be undone:

1. Download the `sql` artifact from the run that applied it.
2. Run `rollback-latest-migration.sql` against the database in the Azure portal query editor (or with `sqlcmd`). It drops the new index and removes that migration's row from `__EFMigrationsHistory`. For `AddDonationDonorDateIndex` it is:

   ```sql
   DROP INDEX [IX_Donations_UserId_DonationDate] ON [Donations];
   DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261003134722_AddDonationDonorDateIndex';
   ```
3. Remove the migration from the code (`dotnet ef migrations remove`, or revert the commit) before the next push, otherwise the pipeline will apply it again.

If data is damaged, use **point-in-time restore** (Azure SQL keeps 7 days of backups on this database):

1. In the Azure portal, open `free-sql-db-0536742` and choose **Restore**.
2. Pick **Point-in-time**, choose a time just before the bad change, and give the new database a name such as `free-sql-db-0536742-restored`.
3. When it is ready, check the data in the query editor.
4. Either copy the missing rows back, or point the app at the restored database by changing `ConnectionStrings__DefaultConnection` on the web app and the Function App (the database name in the connection string), then restart both apps.

The same restore from the command line:

```
az sql db restore -g VisualStudioOnline-2BCCE8B16A4F49DA9DBE18DFF45E32AC -s gift-givers-relief-sql \
  -n free-sql-db-0536742 --dest-name free-sql-db-0536742-restored --time "2026-10-03T12:00:00Z"
```

Note: a restored copy is a new database and is not covered by the free offer, so it should be deleted once it is no longer needed.

## The old GitHub deployment

Before this pipeline, the web app was deployed by GitHub Actions from the `master` branch of `OdirileMasemola/GiftOfTheGivers` (set up through the App Service Deployment Center). That workflow only runs on a push to `master`, and we no longer push to `master`, so it doesn't overwrite pipeline deployments. I left the Deployment Center connection in place. If GitHub `master` is ever pushed again, its workflow would deploy over the pipeline's version, so the Deployment Center source should be disconnected first (App Service > Deployment Center > Disconnect).

## Real pipeline runs (Phase 3)

| Run | Build id | What it did | Result |
|---|---|---|---|
| 20261006.1 | 19 | First full CD run after commit `c5f1b28`. Build, Test, Deploy SQL, Deploy Function, Deploy web app. Smoke tests passed. Live footer showed `Build 20261006.1`. | succeeded |
| 20261006.2 | 20 | Manual rollback demo: `rollbackRunId=19`. Redeployed the web app and Function packages from run 19, smoke-tested the home page and GenerateTaxCertificate, republished artifacts. Footer still `Build 20261006.1`. | succeeded |

SQL after run 19: `__EFMigrationsHistory` contains `InitialBaseline` and `AddDonationDonorDateIndex`. Index `IX_Donations_UserId_DonationDate` exists on `Donations (UserId, DonationDate DESC)`.

Service connection used: `gotg-azure-wif` (workload identity federation). Environment: `production`. Variable group: `gotg-deploy-config` (no secrets).
