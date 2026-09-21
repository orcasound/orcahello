## AI For Orcas - Notification System

The notification system is a set of Azure functions responsible for:
- Facilitating adding/removing moderators and subscribers
- Identifying changes in the database and sending alerts

## Architecture

### Update Orcasite

![post to Orcasite architecture](img/post-to-orcasite.png)

One Azure Function is used to notify the [Orcasite JSON API](https://live.orcasound.net/api/json/swaggerui) of any machine detections.

- A change in the Cosmos DB metadata store triggers the PostToOrcasite function
- The Orcasite feeds API is used to map an OrcaHello location id to an Orcasite feed id
- The function then calls the Orcasite Detection API to post a detection to Orcasite

### Update email list

![add email architecture](img/add-email.png)

There are two Azure Functions that update the email list.

- SubscribeToModeratorEmail is a REST API that writes to the email list
- SubscribeToSubscriberEmail is a REST API that writes to the email list
- Email list is implemented using Azure Tables, using either "Moderator" or "Subscriber" as the partition key

#### Sample REST calls

Add email to subscribers list:

```bash
curl -X POST -d '{"email": "sample@email.com"}' '<SubscriberEmailEndpoint>'
```

Delete email from subscribers list:

```bash
curl -X DELETE -d '{"email": "sample@email.com"}' '<SubscriberEmailEndpoint>'
```

Add email to moderators list:

```bash
curl -X POST -d '{"email": "sample@email.com"}' '<ModeratorEmailEndpoint>'
```

Delete email from moderators list:

```bash
curl -X DELETE -d '{"email": "sample@email.com"}' '<ModeratorEmailEndpoint>'
```

### Send email to moderators and subscribers

![send email architecture](img/send-email.png)

There are three other Azure Functions that make up the email notification system.

In the moderators flow:

- A change in the Cosmos DB metadata store triggers the SendModeratorEmail function
- If there is a newly detected orca call that requires a moderator to validate, the function fetches the relevant email list
- The function then calls AWS Simple Email Service to send emails to moderators

In the subscribers flow:

- A change in the Cosmos DB metadata store triggers the DbToQueue function
- If there is a new orca call that the moderator has validated, the function sends a message to a queue
- The SendSubscriberEmail function periodically checks the queue
- If there are items in the queue, the function fetches the relevant email list
- The function then calls AWS Simple Email Service to send emails to subscribers

## Get email list

![list email architecture](img/list-email.png)

There are two Azure Functions that query the email list.

- ListModeratorEmails is a REST API that lists all saved moderator emails
- ListSubscriberEmails is a REST API that lists all saved subscriber emails

### Sample REST calls

List all subscriber emails:

```bash
curl -X GET '<SubscriberEmailEndpoint>'
```

List all moderator emails:

```bash
curl -X GET '<ModeratorEmailEndpoint>'
```

## Prerequisites

- Access to the Orca Conservancy Azure subscription
- Development tools
    - If using Visual Studio, install Visual Studio 2026 (18.0 or later) with .NET 10 tooling, the "Azure development" workload, and [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
    - If using Visual Studio Code, install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local), and the C# and "Azure Functions" extensions
    - If using CLI, install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- If running locally, install and start the [Azurite storage emulator](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)

## Build

To build the functions locally:

1. Go to the `NotificationSystem/NotificationSystem` directory.
2. If building from the command line, run:

    ```bash
    dotnet build NotificationSystem.csproj
    ```

3. To run the existing test suites, return to the parent `NotificationSystem` directory and run:

    ```bash
    dotnet test NotificationSystem.Tests.Unit/NotificationSystem.Tests.Unit.csproj
    dotnet test NotificationSystem.Tests.Integration/NotificationSystem.Tests.Integration.csproj
    ```

4. If using Visual Studio 2026, open `NotificationSystem.slnx` and build as normal.

## Azure Resource Dependencies

All resources are located in resource group **LiveSRKWNotificationSystem**.

1. Storage account with queues, email template images and moderator/subscriber list: orcanotificationstorage
2. Metadata store (from which some functions are triggered): aifororcasmetadatastore
3. Azure function app: orcanotification

## Run Locally

Go to the `orcanotification` Function App, then **Settings > Configuration** to identify the required app settings. Use test resources where a function can send email, change an email list, post to Orcasite, or process queue/Cosmos events.

Create an ignored `local.settings.json` in `NotificationSystem/NotificationSystem` using the template below. Fill in valid local or test configuration values. Never commit or publish real credentials.

```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "OrcaNotificationStorageSetting": "<storage account connection string>",
        "aifororcasmetadatastore_DOCUMENTDB": "<cosmos db connection string>",
        "AWS_ACCESS_KEY_ID": "<AWS Access Key>",
        "AWS_SECRET_ACCESS_KEY": "<AWS Secret Key>",
        "SenderEmail": "<email address>",
        "SUBSCRIBER_EMAIL_COOLDOWN_MINUTES": "<minutes to wait before re-notifying subscribers for the same location; defaults to 15 if unset>",
        "ORCASITE_HOSTNAME": "live.orcasound.net",
        "ORCASITE_APIKEY": "<orcasite API key>",
        "CURRENT_EPOCH_START": "<timestamp of current epoch>"
    }
}
```

Start Azurite, then run the Functions host from `NotificationSystem/NotificationSystem`:

```bash
dotnet run
```

Confirm that the host starts and discovers the eight functions described in the Architecture and Get email list sections above. Use a valid test Cosmos DB connection for the Cosmos-triggered functions; listener errors caused by missing test services or credentials must be resolved before confirming runtime discovery for deployment.

## Local development with .NET Aspire (optional)

A [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) app host is provided in
`NotificationSystem.AppHost` to run the full detection→notification pipeline locally from a
single command — no live Azure resources or shared credentials required. It starts the
isolated-worker Functions host together with emulators for every backing service the
functions bind to, plus the Aspire dashboard (structured logs, distributed traces and
metrics):

- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) backs both
  the Functions host storage (`AzureWebJobsStorage`) and the app's own
  `OrcaNotificationStorageSetting` store. The `srkwfound` queue and `EmailList` table are
  created automatically on startup.
- The [Azure Cosmos DB emulator](https://learn.microsoft.com/azure/cosmos-db/emulator) backs
  the `aifororcasmetadatastore_DOCUMENTDB` connection, providing the `predictions/metadata`
  container that drives the change-feed triggers (the `leases` container is created on demand
  by the triggers).

The Functions app (`NotificationSystem`) is orchestrated as-is; no application code was
changed to add the app host — the emulator connection strings are supplied by the
orchestrator.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A container runtime (Docker Desktop or Podman) for the Azurite and Cosmos DB emulator
  containers (the Cosmos emulator image is ~2 GB and is pulled on first run)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (`func`) on your `PATH` — the Aspire Functions integration uses it to launch the Functions host

### Run

```bash
cd NotificationSystem/NotificationSystem.AppHost
dotnet run
```

Then open the Aspire dashboard URL printed in the console to view resources, logs and traces.

### Scope and follow-ups

The app host wires the Functions **host storage** (`AzureWebJobsStorage`) and the
application's own backing services to local emulators, so the full detection→notification
pipeline runs end-to-end with no live Azure:

- `OrcaNotificationStorageSetting` → Azurite (the `srkwfound` queue and `EmailList` table are
  auto-provisioned on startup)
- `aifororcasmetadatastore_DOCUMENTDB` → the Cosmos DB emulator (`predictions/metadata`
  change-feed triggers)

The only settings you still supply yourself are those that target a live external service
with no local emulator: the AWS SES credentials (`AWS_ACCESS_KEY_ID`,
`AWS_SECRET_ACCESS_KEY`) and `SenderEmail` used to send moderator/subscriber email, plus
the Orcasite settings (`ORCASITE_HOSTNAME`, `ORCASITE_APIKEY`). Set them on the
`NotificationSystem.AppHost` project via user-secrets (or environment variables) and the
app host forwards any that are present into the Functions process:

```bash
cd NotificationSystem/NotificationSystem.AppHost
dotnet user-secrets set "AWS_ACCESS_KEY_ID" "<key>"
dotnet user-secrets set "AWS_SECRET_ACCESS_KEY" "<secret>"
dotnet user-secrets set "SenderEmail" "<email address>"
```

Settings that are not set are simply omitted, so email/Orcasite calls fail only if you
actually exercise those code paths without supplying them.

### Telemetry (`ServiceDefaults`)

The `NotificationSystem.ServiceDefaults` project provides a shared `AddServiceDefaults()`
call (wired into the Functions app's host builder) that enables OpenTelemetry logging,
metrics and tracing, and fans the signals out to whichever backends the environment is
configured for:

- **OTLP** — when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (the Aspire app host injects it
  automatically), signals flow into the Aspire dashboard locally, or any OTLP collector.
- **Azure Monitor / Application Insights** — when `APPLICATIONINSIGHTS_CONNECTION_STRING`
  is set, signals are exported straight to App Insights.

Both can be active at once, so the same code lights up the dashboard locally and App
Insights when deployed. `host.json` sets `"telemetryMode": "OpenTelemetry"` so the Functions
host emits through this same OpenTelemetry pipeline rather than its legacy Application
Insights SDK — host and worker signals are correlated and not double-counted.

## Run on Azure

1. Go to the `orcanotification` Function App.
2. On the **Overview** tab, confirm that the Function App is in the **Running** state.
3. On the **Functions** tab, confirm that all eight functions are listed and check the logs to verify that their listeners start without errors.

### Updating the .NET version

Merges to `main` automatically deploy the NotificationSystem package through the GitHub Actions workflow, but updating the Function App's .NET stack is currently a manual Azure portal step for `orcanotification`. Follow Microsoft's [Update Language Versions in Azure Functions](https://learn.microsoft.com/azure/azure-functions/update-language-versions?tabs=azure-portal%2Cwindows&pivots=programming-language-csharp) guidance: deploy the updated application package before changing the stack.

## Directory structure

The directories in this system are organized as follows:

* img: Contains images used in this README
* NotificationSystem: Contains the source code for the Azure functions
* NotificationSystem.AppHost: .NET Aspire app host for running the Functions app locally (see [Local development with .NET Aspire](#local-development-with-net-aspire-optional))
* NotificationSystem.Tests.Unit: Contains unit tests
* NotificationSystem.Tests.Integration: Contains integration tests
* PostBackfillToOrcasite: Contains a console app to post the history of machine detections to the Orcasite detection API
* TestData: Contains data files used by the tests
