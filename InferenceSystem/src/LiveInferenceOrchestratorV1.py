# Live inference orchestrator V1
# Uses OrcaHelloSRKWDetectorV1 (model_v1) instead of FastAIModel.
# Additive: LiveInferenceOrchestrator.py (FastAI path) is untouched.

# stdlib
import argparse
import logging
import os
import sys
import uuid
from datetime import datetime, timedelta

# local
import spectrogram_visualizer

# third-party
import yaml
from azure.cosmos import CosmosClient
from azure.storage.blob import BlobServiceClient
from dotenv import load_dotenv
from model_v1.inference import OrcaHelloSRKWDetectorV1
from model_v1.types import DetectorInferenceConfig
from opencensus.ext.azure.log_exporter import AzureEventHandler, AzureLogHandler
from orca_hls_utils.DateRangeHLSStream import DateRangeHLSStream
from orca_hls_utils.HLSStream import HLSStream
from pytz import timezone

AZURE_STORAGE_ACCOUNT_NAME = "livemlaudiospecstorage"
AZURE_STORAGE_AUDIO_CONTAINER_NAME = "audiowavs"
AZURE_STORAGE_SPECTROGRAM_CONTAINER_NAME = "spectrogramspng"

COSMOSDB_DATABASE_NAME = "predictions"
COSMOSDB_CONTAINER_NAME = "metadata"

# TODO: get this data from https://live.orcasound.net/api/json/feeds
source_guid_to_location = {
    "rpi_andrews_bay": {
        "id": "rpi_andrews_bay",
        "name": "Andrews Bay",
        "longitude": -123.1666492,
        "latitude": 48.5500299,
    },
    "rpi_bush_point": {
        "id": "rpi_bush_point",
        "name": "Bush Point",
        "longitude": -122.6040035,
        "latitude": 48.0336664,
    },
    "rpi_mast_center": {
        "id": "rpi_mast_center",
        "name": "Mast Center",
        "longitude": -122.32512,
        "latitude": 47.34922,
    },
    "rpi_north_sjc": {
        "id": "rpi_north_sjc",
        "name": "North San Juan Channel",
        "longitude": -123.058779,
        "latitude": 48.591294,
    },
    "rpi_orcasound_lab": {
        "id": "rpi_orcasound_lab",
        "name": "Orcasound Lab",
        "longitude": -123.1735774,
        "latitude": 48.5583362,
    },
    "rpi_point_robinson": {
        "id": "rpi_point_robinson",
        "name": "Point Robinson",
        "longitude": -122.37267,
        "latitude": 47.388383,
    },
    "rpi_port_townsend": {
        "id": "rpi_port_townsend",
        "name": "Port Townsend",
        "longitude": -122.760614,
        "latitude": 48.135743,
    },
    "rpi_sunset_bay": {
        "id": "rpi_sunset_bay",
        "name": "Sunset Bay",
        "longitude": -122.33393605795372,
        "latitude": 47.86497296593844,
    },
}


def assemble_blob_uri(container_name, item_name):
    return "https://{acct}.blob.core.windows.net/{cont}/{item}".format(
        acct=AZURE_STORAGE_ACCOUNT_NAME, cont=container_name, item=item_name
    )


def build_cosmosdb_metadata(
    audio_uri, image_uri, result, timestamp_in_iso, source_guid, model_id
):
    """Build CosmosDB metadata dict from a DetectionResult.

    Uses actual segment start_time_s / duration_s from DetectionResult (no even-spacing
    approximation). Only positive segments (local_prediction == 1) are included.
    """
    prediction_list = []
    for id_num, (pred, seg) in enumerate(
        zip(result.local_predictions, result.segment_predictions)
    ):
        if pred == 1:
            prediction_list.append(
                {
                    "id": id_num,
                    "startTime": seg.start_time_s,
                    "duration": seg.duration_s,
                    "confidence": seg.confidence,
                }
            )

    return {
        "id": str(uuid.uuid4()),
        "modelId": model_id,
        "audioUri": audio_uri,
        "imageUri": image_uri,
        "reviewed": False,
        "timestamp": timestamp_in_iso,
        "whaleFoundConfidence": result.global_confidence,
        "location": source_guid_to_location[source_guid],
        "source_guid": source_guid,
        "predictions": prediction_list,
    }


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--config",
        type=str,
        required=False,
        help="Path to config YAML (default: /config/config.yml)",
    )
    parser.add_argument(
        "--max_iterations",
        type=int,
        default=None,
        help="Maximum number of clips to process",
    )
    args, _ = parser.parse_known_args()

    if args.config:
        print(f"Using config from command line argument: {args.config}")
    else:
        args.config = "/config/config.yml"
        print(f"Using config from ConfigMap: {args.config}")

    return args


def setup_logger(connection_string):
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    logger = logging.getLogger(__name__)
    if connection_string is not None:
        logger.addHandler(AzureLogHandler(connection_string=connection_string))
        logger.addHandler(AzureEventHandler(connection_string=connection_string))
    return logger


def load_model(config_params, logger):
    model_v1_config = DetectorInferenceConfig.from_yaml(
        config_params["model_v1_config_path"]
    )

    repo_id = config_params.get(
        "model_v1_repo_id", "orcasound/orcahello-srkw-detector-v1"
    )
    if os.getenv("HF_HUB_OFFLINE", "0") == "1":
        logger.info(f"Loading model from local HuggingFace cache (HF_HUB_OFFLINE=1): {repo_id}")
    else:
        logger.info(f"Loading model from HuggingFace Hub: {repo_id}")

    model = OrcaHelloSRKWDetectorV1.from_pretrained(
        repo_id, config=model_v1_config.as_dict()
    )
    logger.info(f"Model loaded. Device: {model._device}  |  Dtype: {model._dtype}")
    return model, model_v1_config


def setup_azure_clients(config_params):
    """Returns (blob_service_client, cosmos_client), or (None, None) if upload_to_azure is False."""
    if not config_params["upload_to_azure"]:
        return None, None

    blob_service_client = BlobServiceClient.from_connection_string(
        os.getenv("AZURE_STORAGE_CONNECTION_STRING")
    )
    cosmos_client = CosmosClient(
        "https://aifororcasmetadatastore.documents.azure.com:443/",
        os.getenv("AZURE_COSMOSDB_PRIMARY_KEY"),
    )
    return blob_service_client, cosmos_client


def build_hls_stream(config_params, local_dir, logger):
    hls_hydrophone_id = config_params["hls_hydrophone_id"]
    hls_polling_interval = config_params["hls_polling_interval"]
    hydrophone_stream_url = (
        "https://s3-us-west-2.amazonaws.com/audio-orcasound-net/" + hls_hydrophone_id
    )

    hls_stream_type = config_params["hls_stream_type"]
    if hls_stream_type == "LiveHLS":
        return HLSStream(hydrophone_stream_url, hls_polling_interval, local_dir)

    if hls_stream_type == "DateRangeHLS":
        hls_start_time_pst = config_params["hls_start_time_pst"]
        hls_end_time_pst = config_params["hls_end_time_pst"]

        start_dt = datetime.strptime(hls_start_time_pst, "%Y-%m-%d %H:%M")
        hls_start_time_unix = int(timezone("US/Pacific").localize(start_dt).timestamp())

        end_dt = datetime.strptime(hls_end_time_pst, "%Y-%m-%d %H:%M")
        hls_end_time_unix = int(timezone("US/Pacific").localize(end_dt).timestamp())

        try:
            return DateRangeHLSStream(
                hydrophone_stream_url,
                hls_polling_interval,
                hls_start_time_unix,
                hls_end_time_unix,
                local_dir,
                False,
            )
        except IndexError as e:
            logger.error(
                f"Failed to initialize DateRangeHLSStream. "
                f"S3 folder list may be malformed or unsorted. "
                f"Hydrophone: {hls_hydrophone_id}, "
                f"start: {hls_start_time_unix}, end: {hls_end_time_unix}. "
                f"Details: {e}"
            )
            sys.exit(0)

    raise ValueError("hls_stream_type should be one of LiveHLS or DateRangeHLS")


def upload_detection_to_azure(
    clip_path, spectrogram_path, result, start_timestamp,
    hls_hydrophone_id, model_id, blob_service_client, cosmos_client, logger
):
    """Upload audio, spectrogram, and CosmosDB metadata for a positive detection."""
    audio_clip_name = os.path.basename(clip_path)
    audio_blob_client = blob_service_client.get_blob_client(
        container=AZURE_STORAGE_AUDIO_CONTAINER_NAME, blob=audio_clip_name
    )
    with open(clip_path, "rb") as data:
        audio_blob_client.upload_blob(data)
    audio_uri = assemble_blob_uri(AZURE_STORAGE_AUDIO_CONTAINER_NAME, audio_clip_name)
    logger.info(f"Uploaded audio to Azure Storage: {audio_clip_name}")

    spectrogram_name = os.path.basename(spectrogram_path)
    spectrogram_blob_client = blob_service_client.get_blob_client(
        container=AZURE_STORAGE_SPECTROGRAM_CONTAINER_NAME, blob=spectrogram_name
    )
    with open(spectrogram_path, "rb") as data:
        spectrogram_blob_client.upload_blob(data)
    spectrogram_uri = assemble_blob_uri(AZURE_STORAGE_SPECTROGRAM_CONTAINER_NAME, spectrogram_name)
    logger.info(f"Uploaded spectrogram to Azure Storage: {spectrogram_name}")

    metadata = build_cosmosdb_metadata(
        audio_uri, spectrogram_uri, result, start_timestamp, hls_hydrophone_id, model_id
    )
    database = cosmos_client.get_database_client(COSMOSDB_DATABASE_NAME)
    container = database.get_container_client(COSMOSDB_CONTAINER_NAME)
    container.create_item(body=metadata)
    logger.info(f"Added metadata to CosmosDB: id={metadata['id']}")


def run_loop(
    hls_stream,
    model,
    model_v1_config,
    config_params,
    blob_service_client,
    cosmos_client,
    logger,
    model_id,
    max_iterations,
):
    hls_stream_type = config_params["hls_stream_type"]
    hls_polling_interval = config_params["hls_polling_interval"]
    hls_hydrophone_id = config_params["hls_hydrophone_id"]

    # Cursor tracking where we are in the audio timeline.
    # Initialized slightly in the past so the first get_next_clip call fetches immediately.
    current_clip_end_time = datetime.utcnow() - timedelta(seconds=10)
    iteration_count = 0

    while not hls_stream.is_stream_over():
        if max_iterations is not None and iteration_count >= max_iterations:
            break
        iteration_count += 1

        # --- Phase 1: Fetch next audio clip ---
        try:
            clip_path, start_timestamp, next_clip_end_time = hls_stream.get_next_clip(
                current_clip_end_time
            )
        except (IndexError, ValueError) as e:
            time_range = (
                f" Time range: {config_params['hls_start_time_pst']} to {config_params['hls_end_time_pst']} PST."
                if hls_stream_type == "DateRangeHLS" else ""
            )
            logger.warning(
                f"Unable to retrieve audio clip — no audio may exist for this time range. "
                f"Hydrophone: {hls_hydrophone_id}.{time_range} "
                f"next_clip_end_time={next_clip_end_time}, current_clip_end_time={current_clip_end_time}. "
                f"{type(e).__name__}: {e}"
            )
            # Advance cursor and retry next iteration
            if next_clip_end_time is not None:
                current_clip_end_time = next_clip_end_time
            current_clip_end_time += timedelta(seconds=hls_polling_interval)
            continue

        # --- Phase 2: Run inference (clip_path is None if no audio was available) ---
        if clip_path:
            logger.info(f"Processing clip: {os.path.basename(clip_path)}")
            spectrogram_path = spectrogram_visualizer.write_spectrogram(clip_path)
            result = model.detect_srkw_from_file(clip_path, model_v1_config)
            result.print_summary(verbose=False)

            logger.info(
                f"Inference result: global_prediction={result.global_prediction}, "
                f"global_confidence={result.global_confidence:.3f}, "
                f"positive_segments={sum(result.local_predictions)}/{len(result.local_predictions)}",
                extra={"custom_dimensions": {"Hydrophone ID": hls_hydrophone_id}},
            )

            if result.global_prediction == 1:
                logger.info(
                    "Orca Found: ",
                    extra={"custom_dimensions": {"Hydrophone ID": hls_hydrophone_id}},
                )
                if config_params["upload_to_azure"]:
                    upload_detection_to_azure(
                        clip_path, spectrogram_path, result, start_timestamp,
                        hls_hydrophone_id, model_id, blob_service_client, cosmos_client, logger
                    )

            if config_params["delete_local_wavs"]:
                os.remove(clip_path)
                os.remove(spectrogram_path)

        # --- Phase 3: Advance the timeline cursor ---
        # Use next_clip_end_time if provided by the stream, then add polling interval
        # to ensure we always request the next non-overlapping window.
        if next_clip_end_time is not None:
            current_clip_end_time = next_clip_end_time
        current_clip_end_time += timedelta(seconds=hls_polling_interval)


if __name__ == "__main__":
    load_dotenv()

    args = parse_args()

    with open(args.config) as f:
        config_params = yaml.load(f, Loader=yaml.FullLoader)

    app_insights_connection_string = os.getenv(
        "INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING"
    )
    logger = setup_logger(app_insights_connection_string)
    logger.info(f"App Insights connection string present: {app_insights_connection_string is not None}")

    model_id = config_params.get("model_id", "OrcaHelloSRKWDetectorV1.v1_0")
    logger.info(f"Model ID: {model_id}")
    model, model_v1_config = load_model(config_params, logger)

    blob_service_client, cosmos_client = setup_azure_clients(config_params)
    logger.info(f"Azure upload enabled: {config_params['upload_to_azure']}")

    local_dir = "wav_dir"
    os.makedirs(local_dir, exist_ok=True)

    hls_stream = build_hls_stream(config_params, local_dir, logger)
    logger.info(
        f"Starting inference loop. Hydrophone: {config_params['hls_hydrophone_id']}, "
        f"stream type: {config_params['hls_stream_type']}"
    )

    run_loop(
        hls_stream,
        model,
        model_v1_config,
        config_params,
        blob_service_client,
        cosmos_client,
        logger,
        model_id,
        max_iterations=args.max_iterations,
    )
