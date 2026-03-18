# OrcaHello SRKW Detector

Southern Resident Killer Whale call detection model and inference system.

**Model on HuggingFace**: [orcasound/orcahello-srkw-detector-v1](https://huggingface.co/orcasound/orcahello-srkw-detector-v1)

## Quick Start

Prerequisites:
- python 3.11 with [uv installed](https://docs.astral.sh/uv/getting-started/installation/#installation-methods)
- appropriate `uv venv` [environment created](https://docs.astral.sh/uv/pip/environments/) and activated

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

The InferenceSystem is an umbrella term for all the code used to stream audio from Orcasound's S3 buckets, run AI inference on audio segments and upload positive detections to Azure. The entrypoint for the InferenceSystem is [src/LiveInferenceOrchestrator.py](src/LiveInferenceOrchestrator.py).

# How to run the InferenceSystem locally
## Setup

```bash
cd InferenceSystem
uv sync --group prod
```

The model is downloaded automatically from HuggingFace Hub on first use.


## Run inference orchestrator locally

```
cd InferenceSystem
uv run python src/LiveInferenceOrchestrator.py --orch_config tests/orch_configs/LiveHLS/LiveHLS_OrcasoundLab.yml --max_iterations 2
```

You should see the following logs in your terminal. Since this is a test `orch_config`, `upload_to_azure` is set to `false` and no updates are made to the backend (Azure blob Storage, CosmosDB). See [tests/orch_configs](tests/orch_configs) for more config examples.

```
2026-03-10 02:53:03,521 INFO [iter 6] Processing clip: rpi_orcasound_lab_2026_03_09_19_51_52_PDT.wav, start_timestamp=2026-03-10T02:51:52Z
2026-03-10 02:53:05,843 DEBUG Generated spectrogram: wav_dir/rpi_orcasound_lab_2026_03_09_19_51_52_PDT.png
/usr/src/venv/lib/python3.11/site-packages/torchaudio/functional/functional.py:582: UserWarning: At least one mel filterbank has all zero values. The value for `n_mels` (256) may be set too high. Or, the value for `n_freqs` (1281) may be set too low.
  warnings.warn(

=== Performance ===
File duration:   59.00s
Processing time: 4.90s
Realtime factor: 12.03x

=== Summary ===
0/29 segments predicted positive
global_confidence: 0.163
global_prediction: 0
2026-03-10 02:53:10,747 INFO [iter 6] Inference: prediction=0, confidence=0.163, positive_segments=0/29
2026-03-10 02:53:10,752 DEBUG Deleted local files: wav_dir/rpi_orcasound_lab_2026_03_09_19_51_52_PDT.wav, wav_dir/rpi_orcasound_lab_2026_03_09_19_51_52_PDT.png
2026-03-10 02:53:10,752 DEBUG [iter 6] Cursor advanced to 2026-03-10T02:53:52
2026-03-10 02:53:10,752 INFO 

-------------------- iter 7 --------------------
2026-03-10 02:53:10,752 INFO [iter 7] Fetching next clip: cursor=2026-03-10T02:53:52
```


### Test Azure upload locally

You will need to create an `InferenceSystem/.env` file with the appropriate Azure credentials. This can be completed in two ways.
1.  Ask an existing contributor for their .env file.
2.  Create one of your own.  This .env file should be created in the format below.

```
AZURE_COSMOSDB_PRIMARY_KEY=<key>
AZURE_STORAGE_CONNECTION_STRING=<string>
INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING=<string>
```

Use [DateRangeHLS_SunsetBay_AzureUploadTest.yml](tests/orch_configs/LocalDebug/DateRangeHLS_SunsetBay_AzureUploadTest.yml) as a reference.
> Note: Only if you know what you are doing :) Both `upload_to_azure` and `cleanup_azure_uploads` should be set to avoid permanent changes to the backend. 

### Azure Credentials

All credentials are found in the [Azure portal](https://portal.azure.com/) under the `"LiveSRKWNotificationSystem"` resource group:

- **`AZURE_STORAGE_CONNECTION_STRING`** — Go to the `"livemlaudiospecstorage"` storage account. See [these instructions](https://docs.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-python#copy-your-credentials-from-the-azure-portal) for getting the connection string.
- **`AZURE_COSMOSDB_PRIMARY_KEY`** — Go to the `"aifororcasmetadatastore"` CosmosDB account → "Keys" → copy the primary key.
- **`INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING`** — Go to the `"InferenceSystemInsights"` App Insights service → copy the connection string from "Essentials".

Set them as environment variables or add them to your `.env` file:

```bash
# Mac/Linux
export AZURE_STORAGE_CONNECTION_STRING="<value>"
export AZURE_COSMOSDB_PRIMARY_KEY="<value>"
export INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING="<value>"
```

```cmd
:: Windows
setx AZURE_STORAGE_CONNECTION_STRING "<value>"
setx AZURE_COSMOSDB_PRIMARY_KEY "<value>"
setx INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING "<value>"
```



# Running inference system in a local docker container

## Prerequisites

- **Docker**: installation instructions on [macOS](https://docs.docker.com/docker-for-mac/), [Windows](https://docs.docker.com/docker-for-windows/), and [Linux](https://docs.docker.com/engine/installation/#supported-platforms).



## Adding a new hydrophone

**Note:** With the new common container image approach, adding a new hydrophone is now much simpler and no longer requires building a separate Docker image.

1. Create a new ConfigMap file for the hydrophone in the deploy folder named `{namespace}-configmap.yaml` (e.g., `new-hydrophone-configmap.yaml`). Use an existing ConfigMap file as a template. The ConfigMap should be in the same namespace as the deployment and contain a single entry with the key `config.yml`.

2. Create a new deployment YAML under the [deploy](deploy) folder using the namespace as the filename (e.g., `new-hydrophone.yaml`). Use an existing deployment file as a template.

4. Follow the deployment steps in the "Deploying an updated docker build to Azure Kubernetes Service" section below to:
   - Create the namespace
   - Create the namespace-scoped ConfigMap with the hydrophone configuration
   - Create the secret
   - Apply the deployment

**Important:** The Docker container image is common across all hydrophones and does not include configuration files. Each hydrophone's configuration is stored in a namespace-scoped Kubernetes ConfigMap and mounted at `/config/`. The container detects which hydrophone it's serving by reading its Kubernetes namespace and loading the corresponding config. You no longer need to edit the Dockerfile or build separate images for each hydrophone location.

## Building the docker container for production

From the `InferenceSystem` directory, run the following command.
It will take a while (~2-3 minutes on macOS or Linux, ~10-20 minutes on Windows) the first time, but builds are cached, and it
should take a much shorter time in future builds.

```
docker build . -t live-inference-system -f ./Dockerfile
```

> NOTE: If building locally on an M-series Mac, prefix with `docker buildx build --platform linux/amd64` so the container works on cloud VMs.

## Running the docker container

From the `InferenceSystem` directory, mount an orchestrator config at `/config/config.yml`:

Linux:
```bash
docker run --rm -it --env-file .env \
  -v $PWD/tests/orch_configs/LiveHLS/LiveHLS_OrcasoundLab.yml:/config/config.yml \
  live-inference-system \
  --max_iterations 2
```

Windows:
```cmd
docker run --rm -it --env-file .env ^
  -v %cd%/tests/orch_configs/LiveHLS/LiveHLS_OrcasoundLab.yml:/config/config.yml ^
  live-inference-system ^
  --max_iterations 2
```

**Note:** When deployed to Kubernetes, the container automatically detects its namespace and loads the configuration from the ConfigMap.

# Pushing your image to Azure Container Registry

The GitHub repository contains a workflow (`.github/workflows/InferenceSystem-deploy.yaml`) that pushes the latest image build to ACR when the main branch is tagged with a tag of the form `InferenceSystem.v#.#.#`. For example:

```
git tag InferenceSystem.v2.0.0
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

