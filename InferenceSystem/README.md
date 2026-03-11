# OrcaHello SRKW Detector

Southern Resident Killer Whale call detection model and inference system.

**Model on HuggingFace**: [orcasound/orcahello-srkw-detector-v1](https://huggingface.co/orcasound/orcahello-srkw-detector-v1)

## Quick Start

```bash
cd InferenceSystem
uv sync
```

```python
from src.model_v1 import OrcaHelloSRKWDetectorV1

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

This document describes the following steps
1. How to run the InferenceSystem locally.
2. Deploying an updated docker build to Azure Container Instances.

Note: We use Python 3, specifically tested with Python 3.11.3

**Important:** Python 3.11 or later is now supported. The dependencies have been updated to support Python 3.11, including updated versions of torch, torchaudio, librosa, numpy, and other packages.

# How to run the InferenceSystem locally
## Create a virtual environment

1. In your working directory, run `python -m venv inference-venv` using Python 3.11 or later. This creates a directory `inference-venv` with relevant files/scripts. 
2. On Mac or Linux, activate this environment with `source inference-venv/bin/activate` and when you're done, `deactivate`

    On Windows, activate with `.\inference-venv\Scripts\activate.bat` and `.\inference-venv\Scripts\deactivate.bat` when done
3. In an active environment, cd to `/InferenceSystem` and run `python -m pip install --upgrade pip && pip install -r requirements.txt`
4. **Python 3.11+ Compatibility Patch**: After installing dependencies, you need to patch the fastai_audio package for Python 3.11+ compatibility:
   
   On Mac or Linux:
   ```bash
   bash patch_fastai_audio.sh
   ```
   
   On Windows:
   ```cmd
   patch_fastai_audio.bat
   ```
   
   This patch fixes a dataclass compatibility issue with Python 3.11+ where mutable default values are not allowed without using `field(default_factory=...)`.

**Troubleshooting:** If you encounter dependency conflicts with standard `pip install`, you can use `uv` which is better at resolving package version conflicts:

```bash
# Install uv (on Mac/Linux)
curl -LsSf https://astral.sh/uv/install.sh | sh

# On Windows, use pip to install uv first
pip install uv

# Then install dependencies using uv pip
uv pip install -r requirements.txt
``` 

## Model download

1.  Download the current production model from [this link.](https://trainedproductionmodels.blob.core.windows.net/dnnmodel/11-15-20.FastAI.R1-12.zip)
2.  Unzip *.zip and extract to `InferenceSystem/model` using `unzip 11-15-20.FastAI.R1-12.zip`
3.  Check the contents of `InferenceSystem/model`.
There should be 1 file
    * model.pkl

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
python src/LiveInferenceOrchestrator.py --config ./config/Test/FastAI_LiveHLS_OrcasoundLab.yml
```

You should see the following logs in your terminal. Since this is a Test config, no audio is uploaded to Azure and no metadata is written to CosmosDB.

```
Listening to location https://s3-us-west-2.amazonaws.com/audio-orcasound-net/rpi_orcasound_lab
Downloading live879.ts
live879.ts: 205kB [00:00, 1.17MB/s]
Downloading live880.ts
live880.ts: 205kB [00:00, 1.11MB/s]
Downloading live881.ts
live881.ts: 205kB [00:00, 948kB/s]
Downloading live882.ts
live882.ts: 205kB [00:00, 1.14MB/s]
Downloading live883.ts
live883.ts: 205kB [00:00, 1.07MB/s]
Downloading live884.ts
live884.ts: 205kB [00:00, 1.04MB/s]
rpi_orcasound_lab_2021_10_13_15_11_18_PDT.wav
Length of Audio Clip:60.010666666666665
Preprocessing: Downmixing to Mono
Preprocessing: Resampling to 200009/59 00:00<00:00]
```

# Running inference system in a local docker container

To deploy to production we use Azure Container Instances. To enable deploying to production, you need to first build the docker image for the inference system locally.

## Prerequisites

- **Docker**: To complete this step, you need Docker installed locally.  Docker provides packages that configure the Docker
environment on 
[macOS](https://docs.docker.com/docker-for-mac/),
[Windows](https://docs.docker.com/docker-for-windows/), and
[Linux](https://docs.docker.com/engine/installation/#supported-platforms).

- **model.zip**: Download model from 
[this link](https://trainedproductionmodels.blob.core.windows.net/dnnmodel/11-15-20.FastAI.R1-12.zip).
Rename the `*.zip` to `model.zip` and place it in `InferenceSystem/model.zip`.

- **Environment Variable File**: Create/get an environment variable file.  This should be a file called `inference-system/.env`.
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

2. Update [src/globals.py](src/globals.py) to add variables for the new hydrophone location.

3. Create a new deployment YAML under the [deploy](deploy) folder using the namespace as the filename (e.g., `new-hydrophone.yaml`). Use an existing deployment file as a template.

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

From the `InferenceSystem` directory, run the following command. You need to specify the `--config` argument when running locally since the container won't be in a Kubernetes environment with namespace detection.

Linux:
```
docker run --rm -it --env-file .env -v $PWD/config:/config live-inference-system python3 -u ./src/LiveInferenceOrchestrator.py --config /config/Test/FastAI_LiveHLS_OrcasoundLab.yml
```

Windows:
```
docker run --rm -it --env-file .env -v %cd%/config:/config live-inference-system python3 -u ./src/LiveInferenceOrchestrator.py --config /config/Test/FastAI_LiveHLS_OrcasoundLab.yml
```

**Note:** When deployed to Kubernetes, the container automatically detects its namespace and loads the configuration from the ConfigMap. The `--config` argument is only needed for local testing.

In addition, you should see something similar to the following in your console.

```
Listening to location https://s3-us-west-2.amazonaws.com/audio-orcasound-net/rpi_orcasound_lab
Downloading live879.ts
live879.ts: 205kB [00:00, 1.17MB/s]                                             
Downloading live880.ts
live880.ts: 205kB [00:00, 1.11MB/s]                                             
Downloading live881.ts
live881.ts: 205kB [00:00, 948kB/s]                                              
Downloading live882.ts
live882.ts: 205kB [00:00, 1.14MB/s]                                             
Downloading live883.ts
live883.ts: 205kB [00:00, 1.07MB/s]                                             
Downloading live884.ts
live884.ts: 205kB [00:00, 1.04MB/s]                                             
rpi_orcasound_lab_2021_10_13_15_11_18_PDT.wav
Length of Audio Clip:60.010666666666665
Preprocessing: Downmixing to Mono
Preprocessing: Resampling to 200009/59 00:00<00:00]
```

# Pushing your image to Azure Container Registry

This step pushes your local container to the Azure Container Registry (ACR).  If you would like more information, this
documentation is adapted from 
[this tutorial](https://docs.microsoft.com/en-us/azure/container-instances/container-instances-tutorial-prepare-acr).

The github repository contains a workflow that can push the latest image
build from the `main` branch to ACR.  This is done when the main branch is
tagged with a tag of the form "InferenceSystem.v#.#.#" where `#.#.#` is a
semantic version number like "0.9.1" or "1.0.0".  For example:

```
git tag InferenceSystem.v0.9.1
git push --tags
```

The same operations can be done manually as follows, but this is not recommended:

1. Login to the shared azure directory from the Azure CLI.

```
az login --tenant adminorcasound.onmicrosoft.com
```

2. We will be using the orcaconservancycr ACR in the LiveSRKWNotificationSystem Resource Group. Log in to the container registry.

```
az acr login --name orcaconservancycr
```

You should receive something similar to `Login succeeded`.

3. Tag your docker container with the version number. We use the following versioning scheme (note: hydrophone location is no longer part of the tag since we now use a common image).

```
docker tag live-inference-system orcaconservancycr.azurecr.io/live-inference-system:<date-of-deployment>.<model-type>.<Rounds-trained-on>.v<Major>
```

So, for example your command may look like

```
docker tag live-inference-system orcaconservancycr.azurecr.io/live-inference-system:11-15-20.FastAI.R1-12.v0
```

4. Push your image to Azure Container Registry once. This single image will be used by all hydrophone deployments.

```
docker push orcaconservancycr.azurecr.io/live-inference-system:<date-of-deployment>.<model-type>.<Rounds-trained-on>.v<Major>
```

**Note:** You only need to build and push one container image now, regardless of the number of hydrophones. Each hydrophone deployment references the same image but runs in a different namespace, which the container detects to load the appropriate configuration.

## Migration from Hydrophone-Specific Images

If you're currently using the old hydrophone-specific container images (e.g., tags like `09-19-24.FastAI.R1-12.BushPoint.v0`), you can migrate to the common container image approach:

1. **Build and push a new common container image** using the instructions above (note the new tag format without hydrophone location).

2. **Update each deployment YAML file** in the [deploy](./deploy/) folder to reference the new common image tag. For example:
   ```yaml
   image: orcaconservancycr.azurecr.io/live-inference-system:11-15-20.FastAI.R1-12.v0
   ```
   (Note: no `.BushPoint`, `.OrcasoundLab`, etc. suffix)

3. **Apply the updated deployment** for each hydrophone:
   ```bash
   kubectl apply -f deploy/bush-point.yaml
   kubectl apply -f deploy/orcasound-lab.yaml
   # etc. for other hydrophones
   ```

The container will automatically detect which namespace it's running in and load the appropriate configuration. The old hydrophone-specific images will continue to work, but using the common image approach will significantly reduce build times and simplify maintenance.

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

# No changes made to deploy-aci.yaml?

I purposefully told git to ignore all futher changes to the file with this command: `git update-index --assume-unchanged deploy-aci.yaml`.  This is to prevent people from checking in their credentials into the repository.  If you want a change to be tracked, you can turn off this feature with `git update-index --no-assume-unchanged deploy-aci.yaml`


# Running the automatic annotation data upload script PrepareDataForPredictionExplorer.py

This script processes audio from a segment of data and uploads it to the annotation website [https://aifororcas-podcast.azurewebsites.net/](https://aifororcas-podcast.azurewebsites.net/).

To run the script, find the connection string for blob storage account `"mldevdatastorage"` and run the following 

### Windows

-------

```
setx PODCAST_AZURE_STORAGE_CONNECTION_STRING "<yourconnectionstring>"
```

### Mac or Linux

-------

```
export PODCAST_AZURE_STORAGE_CONNECTION_STRING="<copied-connection-string>"
```

Call the script as follows, substituting appropriate values.

```
python PrepareDataForPredictionExplorer.py --start_time "2020-07-25 19:15" --end_time "2020-07-25 20:15" --s3_stream https://s3-us-west-2.amazonaws.com/audio-orcasound-net/rpi_orcasound_lab --model_path <folder> --annotation_threshold 0.4 --round_id round5 --dataset_folder <path-to-folder>
```
