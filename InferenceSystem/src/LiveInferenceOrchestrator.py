# Live inference orchestrator

# stdlib
import argparse
import logging
import os
import sys
import time
import uuid
from datetime import datetime, timezone

# local
import spectrogram_visualizer

# third-party
import yaml
from azure.cosmos import CosmosClient
from azure.storage.blob import BlobServiceClient
from dotenv import load_dotenv
from model.inference import OrcaHelloSRKWDetectorV1
from model.types import DetectorInferenceConfig
from opencensus.ext.azure.log_exporter import AzureEventHandler, AzureLogHandler
from orcasound_hls import OrcasoundHLSClient
from pytz import timezone as pytz_tz

AZURE_STORAGE_ACCOUNT_NAME = "livemlaudiospecstorage"
AZURE_STORAGE_AUDIO_CONTAINER_NAME = "audiowavs"
AZURE_STORAGE_SPECTROGRAM_CONTAINER_NAME = "spectrogramspng"

COSMOSDB_ACCOUNT_NAME = "aifororcasmetadatastore"
COSMOSDB_DATABASE_NAME = "predictions"
COSMOSDB_CONTAINER_NAME = "metadata"

ORCASOUND_S3_BUCKET = "audio-orcasound-net"


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
        "whaleFoundConfidence": result.global_confidence
        * 100.0,  # DB assumes 0-100 here
        "location": source_guid_to_location[source_guid],
        "source_guid": source_guid,
        "predictions": prediction_list,
    }


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--orch_config",
        type=str,
        required=False,
        help="Path to orchestrator config YAML (default: /config/config.yml)",
    )
    parser.add_argument(
        "--max_live_iterations",
        type=int,
        default=None,
        help="Maximum number of LiveHLS poll cycles (ignored for DateRangeHLS)",
    )
    parser.add_argument(
        "--max_segments",
        type=int,
        default=None,
        help="Maximum number of segments to process (for DateRangeHLS testing)",
    )
    parser.add_argument(
        "--log-level",
        type=str,
        default="DEBUG",
        choices=["DEBUG", "INFO", "WARNING"],
        help="Log level (default: DEBUG)",
    )
    args, _ = parser.parse_known_args()

    if args.orch_config:
        print(
            f"Using orchestrator config from command line argument: {args.orch_config}"
        )
    else:
        args.orch_config = "/config/config.yml"
        print(f"Using orchestrator config from ConfigMap: {args.orch_config}")

    return args


def setup_logger(connection_string, log_level="DEBUG"):
    logging.basicConfig(
        level=logging.INFO,  # set here to avoid verbose third-party DEBUG logs
        format="%(asctime)s %(levelname)s %(message)s",
    )
    # Suppress noisy third-party INFO loggers
    logging.getLogger("azure").setLevel(logging.WARNING)
    logging.getLogger("urllib3").setLevel(logging.WARNING)
    logging.getLogger("botocore").setLevel(logging.WARNING)
    logging.getLogger("boto3").setLevel(logging.WARNING)
    logging.getLogger("matplotlib").setLevel(logging.WARNING)

    logger = logging.getLogger(__name__)
    # CLI specified log_level only applies to this module
    logger.setLevel(getattr(logging, log_level))
    if connection_string is not None:
        logger.addHandler(AzureLogHandler(connection_string=connection_string))
        logger.addHandler(AzureEventHandler(connection_string=connection_string))
    return logger


def apply_model_config_overrides(model_config, overrides, logger):
    """Apply nested overrides from orchestrator config onto the model config.

    Expects overrides as a dict like:
        {"inference": {"max_batch_size": 16}, "global_prediction": {"pred_global_threshold": 0.5}}
    """
    config_dict = model_config.as_dict()
    for section, values in overrides.items():
        if section not in config_dict:
            logger.warning(
                f"model_config_overrides: unknown section '{section}', skipping"
            )
            continue
        if not isinstance(values, dict):
            logger.warning(
                f"model_config_overrides: section '{section}' must be a dict, skipping"
            )
            continue
        for key, val in values.items():
            if key not in config_dict[section]:
                logger.warning(
                    f"model_config_overrides: unknown key '{section}.{key}', skipping"
                )
                continue
            config_dict[section][key] = val
    return DetectorInferenceConfig.from_dict(config_dict)


def load_model(orch_config, logger):
    """Load OrcaHelloSRKWDetectorV1 from HuggingFace (or local cache if HF_HUB_OFFLINE=1), applying any model_config_overrides from orch_config."""
    model_config = DetectorInferenceConfig.from_yaml(orch_config["model_config_path"])

    overrides = orch_config.get("model_config_overrides")
    if overrides:
        logger.info(f"Applying model config overrides: {overrides}")
        model_config = apply_model_config_overrides(model_config, overrides, logger)

    repo_id = orch_config.get(
        "model_hf_repo_id", "orcasound/orcahello-srkw-detector-v1"
    )
    if os.getenv("HF_HUB_OFFLINE", "0") == "1":
        logger.debug(
            f"Loading model from local HuggingFace cache (HF_HUB_OFFLINE=1): {repo_id}"
        )
    else:
        logger.debug(f"Loading model from HuggingFace Hub: {repo_id}")

    model = OrcaHelloSRKWDetectorV1.from_pretrained(
        repo_id, config=model_config.as_dict()
    )
    logger.debug(f"Model device: {model._device}  |  Dtype: {model._dtype}")
    return model, model_config


def setup_azure_clients(orch_config):
    """Returns (blob_service_client, cosmos_client), or (None, None) if upload_to_azure is False."""
    if not orch_config["upload_to_azure"]:
        return None, None

    blob_service_client = BlobServiceClient.from_connection_string(
        os.getenv("AZURE_STORAGE_CONNECTION_STRING")
    )
    cosmos_client = CosmosClient(
        f"https://{COSMOSDB_ACCOUNT_NAME}.documents.azure.com:443/",
        os.getenv("AZURE_COSMOSDB_PRIMARY_KEY"),
    )
    return blob_service_client, cosmos_client


def build_orcasound_client(orch_config) -> OrcasoundHLSClient:
    """Return an ``OrcasoundHLSClient`` from orch_config."""
    return OrcasoundHLSClient(
        bucket=ORCASOUND_S3_BUCKET,
        hydrophone_id=orch_config["hls_hydrophone_id"],
    )


def upload_detection_to_azure(
    clip_path,
    spectrogram_path,
    result,
    start_timestamp,
    hls_hydrophone_id,
    model_id,
    blob_service_client,
    cosmos_client,
    logger,
):
    """Upload audio, spectrogram, and CosmosDB metadata for a positive detection."""
    audio_clip_name = os.path.basename(clip_path)
    audio_blob_client = blob_service_client.get_blob_client(
        container=AZURE_STORAGE_AUDIO_CONTAINER_NAME, blob=audio_clip_name
    )
    with open(clip_path, "rb") as data:
        audio_blob_client.upload_blob(data)
    audio_uri = assemble_blob_uri(AZURE_STORAGE_AUDIO_CONTAINER_NAME, audio_clip_name)
    logger.debug(f"Uploaded audio blob: {audio_clip_name}")

    spectrogram_name = os.path.basename(spectrogram_path)
    spectrogram_blob_client = blob_service_client.get_blob_client(
        container=AZURE_STORAGE_SPECTROGRAM_CONTAINER_NAME, blob=spectrogram_name
    )
    with open(spectrogram_path, "rb") as data:
        spectrogram_blob_client.upload_blob(data)
    spectrogram_uri = assemble_blob_uri(
        AZURE_STORAGE_SPECTROGRAM_CONTAINER_NAME, spectrogram_name
    )
    logger.debug(f"Uploaded spectrogram blob: {spectrogram_name}")

    metadata = build_cosmosdb_metadata(
        audio_uri, spectrogram_uri, result, start_timestamp, hls_hydrophone_id, model_id
    )
    database = cosmos_client.get_database_client(COSMOSDB_DATABASE_NAME)
    container = database.get_container_client(COSMOSDB_CONTAINER_NAME)
    container.create_item(body=metadata)
    logger.info(
        f"Uploaded detection to Azure: audio={audio_clip_name}, "
        f"spectrogram={spectrogram_name}, cosmos_id={metadata['id']}, "
        f"timestamp={start_timestamp}"
    )
    return audio_clip_name, spectrogram_name, metadata["id"]


def cleanup_azure_uploads(
    uploaded_items, blob_service_client, cosmos_client, source_guid, logger
):
    """Delete blobs and CosmosDB docs created during this run. For testing only."""
    database = cosmos_client.get_database_client(COSMOSDB_DATABASE_NAME)
    container = database.get_container_client(COSMOSDB_CONTAINER_NAME)
    for audio_name, spectrogram_name, cosmos_id in uploaded_items:
        print(
            f"\nAbout to delete: {audio_name}, {spectrogram_name}, CosmosDB id={cosmos_id}"
        )
        confirm = input("Confirm deletion? [y/N]: ").strip().lower()
        if confirm != "y":
            logger.debug(f"Skipped cleanup for cosmos_id={cosmos_id}")
            continue
        blob_service_client.get_blob_client(
            container=AZURE_STORAGE_AUDIO_CONTAINER_NAME, blob=audio_name
        ).delete_blob()
        logger.debug(f"Deleted audio blob: {audio_name}")
        blob_service_client.get_blob_client(
            container=AZURE_STORAGE_SPECTROGRAM_CONTAINER_NAME, blob=spectrogram_name
        ).delete_blob()
        logger.debug(f"Deleted spectrogram blob: {spectrogram_name}")
        container.delete_item(item=cosmos_id, partition_key=source_guid)
        logger.debug(f"Deleted CosmosDB doc: id={cosmos_id}")


def _process_segment(
    segment,
    model,
    model_config,
    orch_config,
    blob_service_client,
    cosmos_client,
    logger,
    model_id,
    local_dir,
):
    """Process a single HLS segment: download, inference, upload."""
    hls_hydrophone_id = orch_config["hls_hydrophone_id"]

    logger.info(
        f"Segment: folder={segment.folder_epoch}, "
        f"indices=[{segment.start_index}:{segment.end_index}), "
        f"start={segment.start_iso}, duration={segment.duration_s:.1f}s"
    )

    # --- Download and convert to WAV ---
    try:
        clip_path = segment.download_as_wav(local_dir)
    except Exception as e:
        logger.warning(f"Failed to download segment: {e}")
        return

    start_timestamp = segment.start_iso
    logger.info(
        f"Processing clip: {os.path.basename(clip_path)}, "
        f"start_timestamp={start_timestamp}"
    )

    # --- Run inference ---
    result = model.detect_srkw_from_file(clip_path, model_config)
    result.print_summary(verbose=False)

    logger.info(
        f"Inference: prediction={result.global_prediction}, "
        f"confidence={result.global_confidence:.3f}, "
        f"positive_segments={sum(result.local_predictions)}/{len(result.local_predictions)}",
        extra={"custom_dimensions": {"Hydrophone ID": hls_hydrophone_id}},
    )

    # Generate spectrogram only when it will be used.
    if result.global_prediction == 1 or not orch_config["delete_local_wavs"]:
        spectrogram_path = spectrogram_visualizer.write_spectrogram(clip_path)
        logger.debug(f"Generated spectrogram: {spectrogram_path}")
    else:
        spectrogram_path = None

    if result.global_prediction == 1:
        logger.info(
            f"Orca detected (confidence={result.global_confidence:.3f})",
            extra={"custom_dimensions": {"Hydrophone ID": hls_hydrophone_id}},
        )
        if orch_config["upload_to_azure"]:
            uploaded = upload_detection_to_azure(
                clip_path,
                spectrogram_path,
                result,
                start_timestamp,
                hls_hydrophone_id,
                model_id,
                blob_service_client,
                cosmos_client,
                logger,
            )
            if orch_config.get(
                "cleanup_azure_uploads", False
            ):  # Used for local testing only
                cleanup_azure_uploads(
                    [uploaded],
                    blob_service_client,
                    cosmos_client,
                    hls_hydrophone_id,
                    logger,
                )

    if orch_config["delete_local_wavs"]:
        os.remove(clip_path)
        deleted = [clip_path]
        if spectrogram_path is not None:
            os.remove(spectrogram_path)
            deleted.append(spectrogram_path)
        logger.debug(f"Deleted local files: {', '.join(deleted)}")


def run_loop(
    orcasound_client,
    model,
    model_config,
    orch_config,
    blob_service_client,
    cosmos_client,
    logger,
    model_id,
    max_live_iterations=None,
    max_segments=None,
):
    """Main inference loop: fetch HLS segments via orcasound_client, run model, upload detections."""
    local_dir = "wav_dir"
    os.makedirs(local_dir, exist_ok=True)

    hls_stream_type = orch_config["hls_stream_type"]
    segment_size = orch_config.get("inference_segment_size", 60.0)
    live_delay_buffer = orch_config.get("hls_live_delay_buffer", 60.0)

    if hls_stream_type == "DateRangeHLS":
        hls_start_time_pst = orch_config["hls_start_time_pst"]
        hls_end_time_pst = orch_config["hls_end_time_pst"]

        start_dt = datetime.strptime(hls_start_time_pst, "%Y-%m-%d %H:%M")
        start_unix = int(pytz_tz("US/Pacific").localize(start_dt).timestamp())

        end_dt = datetime.strptime(hls_end_time_pst, "%Y-%m-%d %H:%M")
        end_unix = int(pytz_tz("US/Pacific").localize(end_dt).timestamp())

        logger.debug(
            f"DateRange: hydrophone={orcasound_client.hydrophone_id}, "
            f"start_unix={start_unix}, end_unix={end_unix}, "
            f"start_pst={hls_start_time_pst}, end_pst={hls_end_time_pst}"
        )

        logger.info(
            f"Fetching DateRange segments: start_unix={start_unix}, "
            f"end_unix={end_unix}, segment_size={segment_size}"
        )
        segments = orcasound_client.get_segments(
            start_unix=start_unix,
            end_unix=end_unix,
            segment_size=segment_size,
        )
        if max_segments is not None:
            segments = segments[:max_segments]
        logger.info(f"Got {len(segments)} segments from date range")
        for segment in segments:
            _process_segment(
                segment,
                model,
                model_config,
                orch_config,
                blob_service_client,
                cosmos_client,
                logger,
                model_id,
                local_dir,
            )

    elif hls_stream_type == "LiveHLS":

        def _next_aligned_time(now: float, interval: float) -> float:
            """Next wall-clock boundary (e.g. XX:01:00 if now is XX:00:27 with 60s interval)."""
            return _align(now, interval) + interval

        def _align(ts: float, interval: float) -> float:
            """Round down to the nearest `interval` boundary."""
            return (ts // interval) * interval

        live_iteration_count = 0
        while True:
            now = _align(datetime.now(timezone.utc).timestamp(), segment_size)
            end_unix = now - live_delay_buffer
            start_unix = end_unix - segment_size

            logger.info(
                f"--- [iter {live_iteration_count}] LiveHLS poll: fetching segments in "
                f"[{start_unix:.0f}, {end_unix:.0f}] "
                f"(now={now:.0f}, delay={live_delay_buffer}s)"
            )
            segments = orcasound_client.get_segments(
                start_unix=start_unix,
                end_unix=end_unix,
                segment_size=segment_size,
            )
            logger.info(
                f"[iter {live_iteration_count}] LiveHLS poll: got {len(segments)} segments"
            )
            for segment in segments:
                _process_segment(
                    segment,
                    model,
                    model_config,
                    orch_config,
                    blob_service_client,
                    cosmos_client,
                    logger,
                    model_id,
                    local_dir,
                )

            live_iteration_count += 1
            if max_live_iterations is not None and live_iteration_count >= max_live_iterations:
                break

            # Sleep until the next wall-clock-aligned boundary
            sleep_time = _next_aligned_time(now, segment_size) - time.time()
            logger.debug(
                f"Sleeping for {sleep_time:.1f}s until {_next_aligned_time(now, segment_size):.0f}"
            )
            if sleep_time > 0:
                time.sleep(sleep_time)

    else:
        raise ValueError("hls_stream_type should be one of LiveHLS or DateRangeHLS")


if __name__ == "__main__":
    load_dotenv()  # for local development. in prod, env vars set by Kubernetes Secret

    args = parse_args()

    with open(args.orch_config) as f:
        orch_config = yaml.safe_load(f)

    app_insights_connection_string = os.getenv(
        "INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING"
    )
    logger = setup_logger(app_insights_connection_string, log_level=args.log_level)
    logger.debug(f"Orchestrator config: {args.orch_config}")
    logger.debug(
        f"App Insights connection string present: {app_insights_connection_string is not None}"
    )

    model_id = orch_config.get("model_id", "orcasound/orcahello-srkw-detector-v1")
    logger.info(f"Model ID: {model_id}")
    model, model_config = load_model(orch_config, logger)
    logger.info("Model loaded")

    blob_service_client, cosmos_client = setup_azure_clients(orch_config)
    logger.info(f"Azure upload: {orch_config['upload_to_azure']}")

    orcasound_client = build_orcasound_client(orch_config)
    logger.info(
        f"Starting inference loop: hydrophone={orch_config['hls_hydrophone_id']}, "
        f"stream_type={orch_config['hls_stream_type']}"
    )

    run_loop(
        orcasound_client,
        model,
        model_config,
        orch_config,
        blob_service_client,
        cosmos_client,
        logger,
        model_id,
        max_live_iterations=args.max_live_iterations,
        max_segments=args.max_segments,
    )
