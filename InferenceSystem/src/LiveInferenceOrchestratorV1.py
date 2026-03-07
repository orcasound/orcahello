# Live inference orchestrator V1
# Uses OrcaHelloSRKWDetectorV1 (model_v1) instead of FastAIModel.
# Additive: LiveInferenceOrchestrator.py (FastAI path) is untouched.

from model_v1.inference import OrcaHelloSRKWDetectorV1
from model_v1.types import DetectorInferenceConfig

from orca_hls_utils.DateRangeHLSStream import DateRangeHLSStream
from orca_hls_utils.HLSStream import HLSStream

import spectrogram_visualizer
from datetime import datetime
from datetime import timedelta
from pytz import timezone
from dotenv import load_dotenv

import argparse
import os
import uuid
import yaml

from azure.storage.blob import BlobServiceClient
from azure.cosmos import CosmosClient

import sys
import logging
from opencensus.ext.azure.log_exporter import AzureLogHandler
from opencensus.ext.azure.log_exporter import AzureEventHandler

load_dotenv()

AZURE_STORAGE_ACCOUNT_NAME = "livemlaudiospecstorage"
AZURE_STORAGE_AUDIO_CONTAINER_NAME = "audiowavs"
AZURE_STORAGE_SPECTROGRAM_CONTAINER_NAME = "spectrogramspng"

COSMOSDB_ACCOUNT_NAME = "aifororcasmetadatastore"
COSMOSDB_DATABASE_NAME = "predictions"
COSMOSDB_CONTAINER_NAME = "metadata"

# TODO: get this data from https://live.orcasound.net/api/json/feeds
ANDREWS_BAY_LOCATION = {"id": "rpi_andrews_bay", "name": "Andrews Bay", "longitude": -123.1666492, "latitude": 48.5500299}
BUSH_POINT_LOCATION = {"id": "rpi_bush_point", "name": "Bush Point", "longitude": -122.6040035, "latitude": 48.0336664}
MAST_CENTER_LOCATION = {"id": "rpi_mast_center", "name": "Mast Center", "longitude": -122.32512, "latitude": 47.34922}
NORTH_SAN_JUAN_CHANNEL_LOCATION = {"id": "rpi_north_sjc", "name": "North San Juan Channel", "longitude": -123.058779, "latitude": 48.591294}
ORCASOUND_LAB_LOCATION = {"id": "rpi_orcasound_lab", "name": "Orcasound Lab", "longitude": -123.1735774, "latitude": 48.5583362}
POINT_ROBINSON_LOCATION = {"id": "rpi_point_robinson", "name": "Point Robinson", "longitude": -122.37267, "latitude": 47.388383}
PORT_TOWNSEND_LOCATION = {"id": "rpi_port_townsend", "name": "Port Townsend", "longitude": -122.760614, "latitude": 48.135743}
SUNSET_BAY_LOCATION = {"id": "rpi_sunset_bay", "name": "Sunset Bay", "longitude": -122.33393605795372, "latitude": 47.86497296593844}

source_guid_to_location = {
    "rpi_andrews_bay": ANDREWS_BAY_LOCATION,
    "rpi_bush_point": BUSH_POINT_LOCATION,
    "rpi_mast_center": MAST_CENTER_LOCATION,
    "rpi_north_sjc": NORTH_SAN_JUAN_CHANNEL_LOCATION,
    "rpi_orcasound_lab": ORCASOUND_LAB_LOCATION,
    "rpi_point_robinson": POINT_ROBINSON_LOCATION,
    "rpi_port_townsend": PORT_TOWNSEND_LOCATION,
    "rpi_sunset_bay": SUNSET_BAY_LOCATION,
}


def assemble_blob_uri(container_name, item_name):
    return "https://{acct}.blob.core.windows.net/{cont}/{item}".format(
        acct=AZURE_STORAGE_ACCOUNT_NAME, cont=container_name, item=item_name
    )


def build_cosmosdb_metadata(audio_uri, image_uri, result, timestamp_in_iso, source_guid, model_id):
    """Build CosmosDB metadata dict from a DetectionResult.

    Uses actual segment start_time_s / duration_s from DetectionResult (no even-spacing
    approximation). Only positive segments (local_prediction == 1) are included.
    """
    data = {}
    data["id"] = str(uuid.uuid4())
    print("===================")
    print(data["id"])

    data["modelId"] = model_id
    data["audioUri"] = audio_uri
    data["imageUri"] = image_uri
    data["reviewed"] = False
    data["timestamp"] = timestamp_in_iso
    data["whaleFoundConfidence"] = result.global_confidence
    data["location"] = source_guid_to_location[source_guid]
    data["source_guid"] = source_guid

    prediction_list = []
    id_num = 0
    for pred, seg in zip(result.local_predictions, result.segment_predictions):
        if pred == 1:
            prediction_list.append({
                "id": id_num,
                "startTime": seg.start_time_s,
                "duration": seg.duration_s,
                "confidence": seg.confidence,
            })
            id_num += 1

    data["predictions"] = prediction_list
    return data


def get_config_path():
    """
    Determine the config file path.
    
    Priority:
    1. Command line --config argument (for local testing)
    2. Well-known path (for production deployments)
    """
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", type=str, help="config.yml", required=False)
    parser.add_argument("--max_iterations", type=int, help="maximum number of clips to process", default=None)
    args, _ = parser.parse_known_args()

    if args.config:
        print(f"Using config from command line argument: {args.config}")
        return args.config, args

    config_path = "/config/config.yml"
    print(f"Using config from ConfigMap: {config_path}")
    return config_path, args


if __name__ == "__main__":
    config_path, args = get_config_path()

    with open(config_path) as f:
        config_params = yaml.load(f, Loader=yaml.FullLoader)

    # Logger to App Insights
    app_insights_connection_string = os.getenv('INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING')
    print("INSTRUMENTATION KEY: ", app_insights_connection_string)
    logger = logging.getLogger(__name__)
    if app_insights_connection_string is not None:
        logger.addHandler(AzureLogHandler(connection_string=app_insights_connection_string))
        logger.addHandler(AzureEventHandler(connection_string=app_insights_connection_string))
        logger.setLevel(logging.INFO)

    model_id = config_params.get("model_id", "OrcaHelloSRKWDetectorV1.v1_0")

    # Load model_v1 inference config
    model_v1_config_path = config_params["model_v1_config_path"]
    model_v1_config = DetectorInferenceConfig.from_yaml(model_v1_config_path)

    # Load model weights
    hf_hub_offline = os.getenv("HF_HUB_OFFLINE", "0") == "1"
    repo_id = config_params.get("model_v1_repo_id", "orcasound/orcahello-srkw-detector-v1")
    if hf_hub_offline:
        print(f"Loading model from local HuggingFace cache (HF_HUB_OFFLINE=1): {repo_id}")
    else:
        print(f"Loading model from HuggingFace Hub: {repo_id}")
    model = OrcaHelloSRKWDetectorV1.from_pretrained(repo_id, config=model_v1_config.as_dict())

    print(f"Device: {model._device}  |  Dtype: {model._dtype}")

    if config_params["upload_to_azure"]:
        connect_str = os.getenv('AZURE_STORAGE_CONNECTION_STRING')
        blob_service_client = BlobServiceClient.from_connection_string(connect_str)

        cosmos_db_endpoint = "https://aifororcasmetadatastore.documents.azure.com:443/"
        cosmos_db_primary_key = os.getenv('AZURE_COSMOSDB_PRIMARY_KEY')
        cosmos_client = CosmosClient(cosmos_db_endpoint, cosmos_db_primary_key)

    local_dir = "wav_dir"
    if not os.path.exists(local_dir):
        os.makedirs(local_dir)

    hls_stream_type = config_params["hls_stream_type"]
    hls_polling_interval = config_params["hls_polling_interval"]
    hls_hydrophone_id = config_params["hls_hydrophone_id"]
    hydrophone_stream_url = 'https://s3-us-west-2.amazonaws.com/audio-orcasound-net/' + hls_hydrophone_id

    if hls_stream_type == "LiveHLS":
        hls_stream = HLSStream(hydrophone_stream_url, hls_polling_interval, local_dir)
    elif hls_stream_type == "DateRangeHLS":
        hls_start_time_pst = config_params["hls_start_time_pst"]
        hls_end_time_pst = config_params["hls_end_time_pst"]

        start_dt = datetime.strptime(hls_start_time_pst, '%Y-%m-%d %H:%M')
        start_dt_aware = timezone('US/Pacific').localize(start_dt)
        hls_start_time_unix = int(start_dt_aware.timestamp())

        end_dt = datetime.strptime(hls_end_time_pst, '%Y-%m-%d %H:%M')
        end_dt_aware = timezone('US/Pacific').localize(end_dt)
        hls_end_time_unix = int(end_dt_aware.timestamp())

        try:
            hls_stream = DateRangeHLSStream(
                hydrophone_stream_url, hls_polling_interval,
                hls_start_time_unix, hls_end_time_unix, local_dir, False
            )
        except IndexError as e:
            print("\nERROR: Failed to initialize DateRangeHLSStream.")
            print("This usually means the S3 folder list is malformed or unsorted.")
            print(f"Details: {e}")
            print(f"Hydrophone: {hls_hydrophone_id}")
            print(f"Start time (unix): {hls_start_time_unix}")
            print(f"End time (unix)  : {hls_end_time_unix}")
            sys.exit(0)
    else:
        raise ValueError("hls_stream_type should be one of LiveHLS or DateRangeHLS")

    current_clip_end_time = datetime.utcnow() - timedelta(0, 10)

    max_iterations = args.max_iterations
    iteration_count = 0
    while not hls_stream.is_stream_over():
        if max_iterations is not None and iteration_count >= max_iterations:
            break
        iteration_count += 1

        try:
            clip_path, start_timestamp, next_clip_end_time = hls_stream.get_next_clip(current_clip_end_time)
        except (IndexError, ValueError) as e:
            print("\nWarning: Unable to retrieve audio clip. This may occur when no audio files exist for the specified time range.")
            print(f"Error details: {type(e).__name__}: {str(e)}")
            print(f"Hydrophone: {hls_hydrophone_id}")
            if hls_stream_type == "DateRangeHLS":
                print(f"Time range: {hls_start_time_pst} to {hls_end_time_pst} PST")
            print(f"next_clip_end_time    {next_clip_end_time!s}")
            print(f"current_clip_end_time {current_clip_end_time!s}")
            if next_clip_end_time is not None:
                current_clip_end_time = next_clip_end_time
            current_clip_end_time = current_clip_end_time + timedelta(0, hls_polling_interval)
            continue

        if clip_path:
            spectrogram_path = spectrogram_visualizer.write_spectrogram(clip_path)
            result = model.detect_srkw_from_file(clip_path, model_v1_config)
            result.print_summary(verbose=False)

            if result.global_prediction == 1:
                print("FOUND!!!!")

                properties = {'custom_dimensions': {'Hydrophone ID': hls_hydrophone_id}}
                logger.info('Orca Found: ', extra=properties)

                if config_params["upload_to_azure"]:
                    audio_clip_name = os.path.basename(clip_path)
                    audio_blob_client = blob_service_client.get_blob_client(
                        container=AZURE_STORAGE_AUDIO_CONTAINER_NAME, blob=audio_clip_name
                    )
                    with open(clip_path, "rb") as data:
                        audio_blob_client.upload_blob(data)
                    audio_uri = assemble_blob_uri(AZURE_STORAGE_AUDIO_CONTAINER_NAME, audio_clip_name)
                    print("Uploaded audio to Azure Storage")

                    spectrogram_name = os.path.basename(spectrogram_path)
                    spectrogram_blob_client = blob_service_client.get_blob_client(
                        container=AZURE_STORAGE_SPECTROGRAM_CONTAINER_NAME, blob=spectrogram_name
                    )
                    with open(spectrogram_path, "rb") as data:
                        spectrogram_blob_client.upload_blob(data)
                    spectrogram_uri = assemble_blob_uri(AZURE_STORAGE_SPECTROGRAM_CONTAINER_NAME, spectrogram_name)
                    print("Uploaded spectrogram to Azure Storage")

                    metadata = build_cosmosdb_metadata(
                        audio_uri, spectrogram_uri, result, start_timestamp, hls_hydrophone_id, model_id
                    )
                    database = cosmos_client.get_database_client(COSMOSDB_DATABASE_NAME)
                    container = database.get_container_client(COSMOSDB_CONTAINER_NAME)
                    container.create_item(body=metadata)
                    print("Added metadata to Azure CosmosDB")

            if config_params["delete_local_wavs"]:
                os.remove(clip_path)
                os.remove(spectrogram_path)

        if next_clip_end_time is not None:
            current_clip_end_time = next_clip_end_time
        current_clip_end_time = current_clip_end_time + timedelta(0, hls_polling_interval)
