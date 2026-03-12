# OrcaHello SRKW Detector

Southern Resident Killer Whale call detection model and inference system.

**Model on HuggingFace**: [orcasound/orcahello-srkw-detector-v1](https://huggingface.co/orcasound/orcahello-srkw-detector-v1)

## Quick Start

```bash
cd InferenceSystem
uv sync
```

```python
from src.model import OrcaHelloSRKWDetectorV1

model = OrcaHelloSRKWDetectorV1.from_pretrained("orcasound/orcahello-srkw-detector-v1")
result = model.detect_srkw_from_file("audio.wav")

print(f"Orca detected: {result.global_prediction}")
print(f"Confidence: {result.global_confidence:.2f}")
```

See [MODEL_CARD.md](model/MODEL_CARD.md) for detailed usage, configuration, and API reference.

## Development

For local scripts, testing, and contributing: [DEVELOPMENT.md](DEVELOPMENT.md)

---

# Working with the InferenceSystem

The InferenceSystem is an umbrella term for all the code used to stream audio from Orcasound's S3 buckets, perform inference on audio segments using the deep learning model and upload positive detections to Azure. The entrypoint for the InferenceSystem is [src/LiveInferenceOrchestrator.py](src/LiveInferenceOrchestrator.py).

# How to run the InferenceSystem locally
## Setup

```bash
cd InferenceSystem
uv sync --group prod
```

The model is downloaded automatically from HuggingFace Hub on first use.

## Get connection string for interface with Azure Storage
To be able to upload detections to Azure, you will need a connection string.

Go to [Azure portal](https://portal.azure.com/) and find the `"LiveSRKWNotificationSystem"` resource group. Within that go to the `"livemlaudiospecstorage"` storage account. Refer to [this page](https://docs.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-python#copy-your-credentials-from-the-azure-portal) to see how to get the connection string.

### Windows

-------

```
setx AZURE_STORAGE_CONNECTION_STRING "<yourconnectionstring>"
```

### Mac or Linux

-------

```
export AZURE_STORAGE_CONNECTION_STRING="<copied-connection-string>"
```

## Get primary key for interface with CosmosDB

Go to the [Azure portal](https://portal.azure.com/)

Go to the `"LiveSRKWNotificationSystem"` resource group and within that go to the `"aifororcasmetadatastore"` CosmosDB account.

Go to "Keys" and look up the primary key

### Windows

-------

```
setx AZURE_COSMOSDB_PRIMARY_KEY "<yourprimarykey>"
```

### Mac or Linux

-------

```
export AZURE_COSMOSDB_PRIMARY_KEY="<yourprimarykey>"
```

## Get connection string for interface with App Insights

Go to the [Azure portal](https://portal.azure.com/)

Go to the `"LiveSRKWNotificationSystem"` resource group and within that go to the `"InferenceSystemInsights"` App Insights service

Look up the connection key from 'Essentials'

### Windows

-------

```
setx INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING "<yourconnectionstring>"
```

### Mac or Linux

-------

```
export INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING="<yourconnectionstring>"
```

## Run live inference locally

```
cd InferenceSystem
uv run python src/LiveInferenceOrchestrator.py --orch_config tests/orch_configs/LiveHLS/LiveHLS_OrcasoundLab.yml --max_iterations 2
```

# Running inference system in a local docker container

## Prerequisites

- **Docker**: installation instructions on [macOS](https://docs.docker.com/docker-for-mac/), [Windows](https://docs.docker.com/docker-for-windows/), and [Linux](https://docs.docker.com/engine/installation/#supported-platforms).

- **Environment Variable File**: Create/get an environment variable file `inference-system/.env`.
This can be completed in two ways.
    1.  Ask an existing contributor for their .env file.
    2.  Create one of your own.  This .env file should be created in the format below.

        `<key>` and `<string>` should be filled in with the Azure Storage Connection String and the Azure CosmosDB Primary Key above.

        ```
        AZURE_COSMOSDB_PRIMARY_KEY=<key>
        AZURE_STORAGE_CONNECTION_STRING=<string>
        INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING=<string>
        ```

## Adding a new hydrophone

**Note:** With the new common container image approach, adding a new hydrophone is now much simpler and no longer requires building a separate Docker image.

1. Create a new ConfigMap file for the hydrophone in the deploy folder named `{namespace}-configmap.yaml` (e.g., `new-hydrophone-configmap.yaml`). Use an existing ConfigMap file as a template. The ConfigMap should be in the same namespace as the deployment and contain a single entry with the key `config.yml`.

2. Create a new deployment YAML under the [deploy](deploy) folder using the namespace as the filename (e.g., `new-hydrophone.yaml`). Use an existing deployment file as a template.

4. Follow the deployment steps in the "Deploying an updated docker build to Azure Kubernetes Service" section below to:
   - Create the namespace
   - Create the namespace-scoped ConfigMap with the hydrophone configuration
   - Create the secret
   - Apply the deployment

**Important:** The container image is now common across all hydrophones. Configuration files are stored in a Kubernetes ConfigMap and mounted into the container at `/config/`. The container reads the namespace and loads the corresponding config file (e.g., namespace `bush-point` loads `/config/config.yml`).

## Building the docker container for production

From the `InferenceSystem` directory, run the following command.
It will take a while (~2-3 minutes on macOS or Linux, ~10-20 minutes on Windows) the first time, but builds are cached, and it
should take a much shorter time in future builds.

```
docker build . -t live-inference-system -f ./Dockerfile
```

**Important:** The Docker container is now common across all hydrophones and does not include configuration files. The container automatically detects which hydrophone it's serving by reading the Kubernetes namespace and loading the configuration from a ConfigMap mounted at `/config/`. You no longer need to edit the Dockerfile or build separate images for each hydrophone location.


## Running the docker container

From the `InferenceSystem` directory, mount an orchestrator config at `/config/config.yml`:

Linux:
```
docker run --rm -it --env-file .env \
  -v $PWD/tests/orch_configs/LiveHLS/LiveHLS_OrcasoundLab.yml:/config/config.yml \
  live-inference-system \
  /usr/src/venv/bin/python3 -u ./src/LiveInferenceOrchestrator.py --max_iterations 2
```

Windows:
```
docker run --rm -it --env-file .env ^
  -v %cd%/tests/orch_configs/LiveHLS/LiveHLS_OrcasoundLab.yml:/config/config.yml ^
  live-inference-system ^
  /usr/src/venv/bin/python3 -u ./src/LiveInferenceOrchestrator.py --max_iterations 2
```

**Note:** When deployed to Kubernetes, the container automatically detects its namespace and loads the configuration from the ConfigMap.

# Pushing your image to Azure Container Registry

The GitHub repository contains a workflow (`.github/workflows/InferenceSystem-deploy.yaml`) that pushes the latest image build to ACR when the main branch is tagged with a tag of the form `InferenceSystem.v#.#.#`. For example:

```
git tag InferenceSystem.v1.0.0
git push --tags
```

To push manually:

1. Login to the Azure CLI: `az login --tenant adminorcasound.onmicrosoft.com`
2. Login to ACR: `az acr login --name orcaconservancycr`
3. Tag and push:
```
docker tag live-inference-system orcaconservancycr.azurecr.io/live-inference-system:<date>.<version>
docker push orcaconservancycr.azurecr.io/live-inference-system:<date>.<version>
```

# Deploying an updated docker build to Azure Kubernetes Service

We are deploying one hydrophone per namespace. The container automatically detects its namespace and loads the configuration from a ConfigMap at runtime. To deploy a hydrophone, the following Kubernetes resources need to be created:

1. Namespace: used to group resources and identify which hydrophone configuration to use
2. ConfigMap: holds the configuration files for all hydrophones (shared across namespaces)
3. Secret: holds connection strings used by inference system
4. Deployment: forces one instance of inference system to remain running at all times

**Important:** Configuration files are stored in a Kubernetes ConfigMap and mounted at `/config/` in the container. The container reads the namespace (e.g., `bush-point`) and loads the corresponding config file (e.g., `/config/bush-point.yml`).

## Prerequisites

- You must have completed all of the steps above and should have a working container image pushed to ACR.
- az cli: installation instructions [here](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- kubectl cli: if you don't have this, you can run `az aks install-cli` or install it using instructions [here](https://kubernetes.io/docs/tasks/tools/)

1. Log into az cli

```bash
az login
```

2. Log into Kubernetes cluster. The current cluster is called inference-system-AKS in the LiveSRKWNotificationSystem resource group.

```bash
# replace "inference-system-AKS" with cluster name and "LiveSRKWNotificationSystem" with resource group
az aks get-credentials -g LiveSRKWNotificationSystem -n inference-system-AKS
```

Verify it is successful. You should see a list of VM names and no error message.

```bash
kubectl get nodes
```

3. If deploying a new hydrophone, create the namespace first.

```bash
# replace "bush-point" with hydrophone identifier
kubectl create namespace bush-point
```

4. Create or update the namespace-scoped ConfigMap for the hydrophone. Each namespace has its own ConfigMap.

```bash
# replace "bush-point" with hydrophone identifier
kubectl apply -f deploy/bush-point-configmap.yaml
```

**Important:** The ConfigMap must be in the same namespace as the deployment. Each ConfigMap contains only the configuration for that specific hydrophone. See [deploy/bush-point-configmap.yaml](deploy/bush-point-configmap.yaml) for an example.

5. If deploying a new hydrophone, create the secret in the namespace. Skip this step if the secret already exists.

```bash
# replace "bush-point" with hydrophone identifier
kubectl create secret generic inference-system -n bush-point \
    --from-literal=AZURE_COSMOSDB_PRIMARY_KEY='<cosmos_primary_key>' \
    --from-literal=AZURE_STORAGE_CONNECTION_STRING='<storage_connection_string>`' \
    --from-literal=INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING='<appinsights_connection_string>'
```

6. Create or update deployment. Use file for hydrophone under [deploy](./deploy/) folder, or create and commit a new one.

```bash
kubectl apply -f deploy/bush-point.yaml
```

**Note:** All deployment files now reference the same container image and mount the namespace-scoped ConfigMap at `/config/`. The container determines which hydrophone it's serving based on the namespace and loads the corresponding config file from the ConfigMap.

6. To verify that the container is running, check logs:

```bash
# get pod name
kubectl get pods -n bush-point

# replace pod name (likely will have different alphanumeric string at the end)
kubectl logs -n bush-point inference-system-6d4845c5bc-tfsbw
```

<details>
  <summary>Deployment to Azure Container Instances (deprecated)</summary>
# Deploying an updated docker build to Azure Container Instances
# This method has been deprecated

## Prerequisites

- You must have completed all of the steps above and should have a 
container that is working locally that you wish to deploy live to production.

- **Azure CLI**: You must have Azure CLI version 2.0.29 or later installed on your local computer. Run `az --version` to find the 
version. If you need to install or upgrade, see 
[Install the Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli).

## Deploying your updated container to Azure Container Instances

Ask an existing maintainer for the file `deploy-aci-with-creds.yaml` or change strings in `deploy-aci.yaml`.  There are three sensitive strings that must be filled in before deployment can
happen.

**NOTE** - Make sure you change these back after running the build - don't commit them to the repository!

1.  `<cosmos_primary_key>` - Replace this with the AZURE_COSMOSDB_PRIMARY_KEY from your .env file (or found above).
2.  `<storage_connection_string>` - Replace this with the AZURE_STORAGE_CONNECTION_STRING from your .env file (or found above).
3.  `<appinsights_connection_string>` - Replace this with the INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING from your .env file (or found above).
4.  `<image_registry_password>` - Replace this with the password for the orcaconservancycr container registry.  It can be found at
[this link](https://portal.azure.com/#@OrcaConservancy778.onmicrosoft.com/resource/subscriptions/9ffa543e-3596-43aa-b82c-8f41dfbf03cc/resourcegroups/LiveSRKWNotificationSystem/providers/Microsoft.ContainerRegistry/registries/orcaconservancycr/accessKey)
under the name `password`.

Then, run this command from the `InferenceSystem` directory.  It will take a while to complete.  Once complete, make sure to check your work below.

```
az container create -g LiveSRKWNotificationSystem -f .\deploy-aci.yaml
```

## Checking your work

View the container logs with the following command.  The logs should be similar to the logs created when you run the container locally (above).

```
az container attach --resource-group LiveSRKWNotificationSystem --name live-inference-system-aci-3gb-new
```

</details>

