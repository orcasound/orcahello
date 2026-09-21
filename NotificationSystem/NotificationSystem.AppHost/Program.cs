// Aspire AppHost for the OrcaHello NotificationSystem.
//
// A single `aspire run` (or `dotnet run`) starts the existing isolated-worker Azure
// Functions app together with local emulators for every backing service it binds to —
// so the full notification pipeline runs on a laptop with no live Azure resources or
// shared credentials:
//
//   * Azurite (Azure Storage emulator) backs both the Functions host storage and the
//     app's own "srkwfound" queue + "EmailList" table, exposed through the
//     "OrcaNotificationStorageSetting" connection the functions expect.
//   * The Azure Cosmos DB emulator backs the "predictions/metadata" change-feed triggers
//     through the "aifororcasmetadatastore_DOCUMENTDB" connection.
//
// The Functions app source is left completely unchanged — only configuration
// (connection strings) is supplied by the orchestrator.

var builder = DistributedApplication.CreateBuilder(args);

// --- Azure Storage (Azurite emulator) -------------------------------------------------
// Used both as the Functions host storage and as the app's own queue/table store.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

// The functions read a single account-style connection string ("OrcaNotificationStorageSetting")
// for BOTH the "srkwfound" queue (new QueueClient(...)) and the "EmailList" table
// ([TableInput]). Aspire's per-service child connection strings only carry one endpoint
// each, so compose the full Azurite account connection string (all endpoints) here.
// devstoreaccount1 + key below are Azurite's fixed, well-known emulator credentials.
const string AzuriteAccountKey =
    "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

var blobEndpoint = storage.GetEndpoint("blob");
var queueEndpoint = storage.GetEndpoint("queue");
var tableEndpoint = storage.GetEndpoint("table");

var storageConnection = ReferenceExpression.Create(
    $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey={AzuriteAccountKey};BlobEndpoint={blobEndpoint}/devstoreaccount1;QueueEndpoint={queueEndpoint}/devstoreaccount1;TableEndpoint={tableEndpoint}/devstoreaccount1;");

// --- Azure Cosmos DB (emulator) -------------------------------------------------------
// The change-feed triggers watch database "predictions", container "metadata"
// (partition key "/source_guid"); the "leases" container is created on demand by the
// triggers (CreateLeaseContainerIfNotExists = true). The preview (Linux vNext) emulator
// starts faster and is lighter than the classic emulator; its gateway still presents a
// self-signed TLS certificate, handled on the connection string below.
#pragma warning disable ASPIRECOSMOSDB001 // preview (vNext) Cosmos emulator is an evaluation API
var cosmos = builder.AddAzureCosmosDB("aifororcasmetadatastore")
    .RunAsPreviewEmulator();
#pragma warning restore ASPIRECOSMOSDB001

var predictions = cosmos.AddCosmosDatabase("predictions");
var metadata = predictions.AddContainer("metadata", "/source_guid");

// The emulator's gateway presents a self-signed TLS certificate that the host machine
// does not trust, so the Functions Cosmos change-feed listener must be told to skip
// certificate validation (honored by the Microsoft.Azure.Cosmos SDK when present in the
// connection string). Without this the triggers never create their lease container.
var cosmosConnection = ReferenceExpression.Create(
    $"{cosmos.Resource.ConnectionStringExpression};DisableServerCertificateValidation=True");

// --- Functions app --------------------------------------------------------------------
// The Cosmos and storage connections are supplied as plain environment strings rather
// than WithReference(...), so Aspire has no implicit dependency edge to the emulators.
// Wait for both explicitly: the Cosmos change-feed listener does not reliably recover if
// the Functions host starts before the emulator gateway is accepting connections, which
// leaves the "leases" container (and therefore every trigger) uncreated. Wait on the
// "metadata" container rather than the Cosmos emulator resource so the host only starts
// after the "predictions" database and "metadata" container have been provisioned —
// otherwise the change-feed triggers can register against a database that does not exist
// yet.
var functions = builder.AddAzureFunctionsProject<Projects.NotificationSystem>("notificationsystem")
    .WithHostStorage(storage)
    .WithEnvironment("OrcaNotificationStorageSetting", storageConnection)
    .WithEnvironment("aifororcasmetadatastore_DOCUMENTDB", cosmosConnection)
    .WaitFor(storage)
    .WaitFor(metadata);

// Forward the optional live-service settings (AWS SES + Orcasite) into the Functions
// process when supplied to the app host through user-secrets or environment variables.
// These target external services with no local emulator, so they are only forwarded when
// present; without this the app host would only inject the two emulator connection
// strings and the README's user-secrets guidance would never reach the worker.
foreach (var key in new[]
{
    "AWS_ACCESS_KEY_ID",
    "AWS_SECRET_ACCESS_KEY",
    "SenderEmail",
    "ORCASITE_HOSTNAME",
    "ORCASITE_APIKEY",
})
{
    var value = builder.Configuration[key];
    if (!string.IsNullOrEmpty(value))
    {
        functions.WithEnvironment(key, value);
    }
}

// Provision the app's own storage objects in Azurite once the emulator is ready. The
// queue/table bindings connect to but do not create these, and (unlike Cosmos databases
// and blob containers/queues) Aspire 13.5.3 has no declarative "add table" API — so
// create both the "srkwfound" queue and the "EmailList" table here, using the very same
// connection string the functions consume.
builder.Eventing.Subscribe<ResourceReadyEvent>(storage.Resource, async (@event, cancellationToken) =>
{
    var connectionString = await storageConnection.GetValueAsync(cancellationToken);
    if (connectionString is null)
    {
        return;
    }

    await new Azure.Storage.Queues.QueueServiceClient(connectionString)
        .GetQueueClient("srkwfound")
        .CreateIfNotExistsAsync(cancellationToken: cancellationToken);

    await new Azure.Data.Tables.TableServiceClient(connectionString)
        .CreateTableIfNotExistsAsync("EmailList", cancellationToken);
});

// AWS SES credentials, SenderEmail and the Orcasite settings target live external
// services with no local emulator; they are forwarded above from the app host's
// user-secrets / environment configuration when present.

builder.Build().Run();
