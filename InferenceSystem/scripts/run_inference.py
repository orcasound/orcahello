#!/usr/bin/env python
"""Run model_v1 inference on a local audio file or directory.

Usage:
    python scripts/run_inference.py                              # uses default WAV test file
    python scripts/run_inference.py path/to/audio.wav
    python scripts/run_inference.py path/to/audio.flac
    python scripts/run_inference.py path/to/audio.wav --output results.json
    python scripts/run_inference.py path/to/audio_dir/          # recurse over wav/flac/mp3
    python scripts/run_inference.py path/to/audio_dir/ --output path/to/out_dir/
    python scripts/run_inference.py path/to/audio.wav --config path/to/custom_config.yaml

    # Re-aggregate existing results with new config (no model inference):
    python scripts/run_inference.py results.json --reaggregate
    python scripts/run_inference.py results_dir/ --reaggregate
    python scripts/run_inference.py results_dir/ --reaggregate --config path/to/custom_config.yaml
"""
import argparse
import csv
import json
import os
import sys
from dataclasses import asdict
from pathlib import Path

import yaml

# Add src directory to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'src'))

from model_v1.inference import OrcaHelloSRKWDetectorV1, aggregate_predictions
from model_v1.types import SegmentPrediction, GlobalPredictionConfig

TEST_DATA_DIR = os.path.join(os.path.dirname(__file__), '..', 'tests', 'test_data')
DEFAULT_AUDIO_PATH = os.path.join(TEST_DATA_DIR, "rpi_sunset_bay_2025_09_18_01_12_06_PDT--f6b3fcd7-2036-433a-8a18-76a6b3b4f0c9.wav")

AUDIO_EXTENSIONS = {'.wav', '.flac', '.mp3'}


def find_audio_files(root: Path) -> list[Path]:
    """Recursively find all audio files under root."""
    return sorted(p for p in root.rglob('*') if p.suffix.lower() in AUDIO_EXTENSIONS)


def find_json_files(root: Path) -> list[Path]:
    """Recursively find all JSON result files under root (excluding summary.csv)."""
    return sorted(p for p in root.rglob('*.json') if p.name != 'summary.json')


def reaggregate_single(json_path: Path, gp_config: GlobalPredictionConfig, verbose: bool = True) -> dict:
    """Re-aggregate a single JSON result file with new config. Updates file in place."""
    with open(json_path) as f:
        data = json.load(f)

    # Reconstruct SegmentPrediction objects from saved data
    segment_preds = [
        SegmentPrediction(
            start_time_s=seg['start_time_s'],
            duration_s=seg['duration_s'],
            confidence=seg['confidence']
        )
        for seg in data['segment_predictions']
    ]

    # Re-run aggregation with new config
    local_predictions, global_prediction, global_confidence = aggregate_predictions(
        segment_preds, gp_config
    )

    # Update data
    old_global_pred = data['global_prediction']
    old_global_conf = data['global_confidence']
    data['local_predictions'] = local_predictions
    data['global_prediction'] = global_prediction
    data['global_confidence'] = global_confidence

    # Write back
    with open(json_path, 'w') as f:
        json.dump(data, f, indent=2)

    # Get original audio file path from metadata
    original_file_path = data.get('metadata', {}).get('wav_file_path', str(json_path))

    if verbose:
        print(f"Re-aggregated: {json_path}")
        print(f"  global_prediction: {old_global_pred} -> {global_prediction}")
        print(f"  global_confidence: {old_global_conf:.3f} -> {global_confidence:.3f}")

    return {
        'file_path': original_file_path,
        'global_prediction': global_prediction,
        'global_confidence': global_confidence,
        'num_positive_segments': sum(local_predictions),
        'num_total_segments': len(local_predictions),
    }


def reaggregate_directory(input_dir: Path, gp_config: GlobalPredictionConfig):
    """Re-aggregate all JSON result files in a directory."""
    csv_path = input_dir / 'summary.csv'
    if not csv_path.exists():
        print(f"No summary.csv found in {input_dir}")
        return

    # Read original CSV to get file paths and map to JSON files
    with open(csv_path, newline='') as f:
        reader = csv.DictReader(f)
        original_rows = list(reader)

    if not original_rows:
        print(f"No rows in {csv_path}")
        return

    print(f"Found {len(original_rows)} entries in {csv_path}")
    print(f"Aggregation strategy: {gp_config.aggregation_strategy}")
    print("=" * 60)

    summary_rows = []
    for i, orig_row in enumerate(original_rows):
        # Derive JSON path from original audio file path
        audio_rel = Path(orig_row['file_path'])
        json_path = input_dir / audio_rel.parent / (audio_rel.stem + '.json')

        print(f"\n[{i+1}/{len(original_rows)}] {orig_row['file_path']}")

        if not json_path.exists():
            print(f"  WARNING: JSON not found: {json_path}, skipping")
            continue

        row = reaggregate_single(json_path, gp_config, verbose=False)
        # Use original file_path from CSV
        row['file_path'] = orig_row['file_path']
        print(f"  global_prediction={row['global_prediction']}  "
              f"global_confidence={row['global_confidence']:.3f}  "
              f"positive_segments={row['num_positive_segments']}/{row['num_total_segments']}")
        summary_rows.append(row)

    # Write updated summary CSV
    with open(csv_path, 'w', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=[
            'file_path', 'global_prediction', 'global_confidence',
            'num_positive_segments', 'num_total_segments'
        ])
        writer.writeheader()
        writer.writerows(summary_rows)

    print("\n" + "=" * 60)
    print(f"Summary CSV updated: {csv_path}")
    num_detected = sum(1 for r in summary_rows if r['global_prediction'] == 1)
    print(f"Files with orca detected: {num_detected}/{len(summary_rows)}")


def run_single(model, audio_path: Path, config: dict, output_path: Path | None, verbose: bool = True) -> dict:
    """Run inference on a single file. Returns a summary dict."""
    if verbose:
        print(f"Processing: {audio_path}")

    result = model.detect_srkw_from_file(str(audio_path), config)

    if verbose:
        result.print_summary(verbose=True)

    if output_path is not None:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        result_dict = asdict(result)
        result_dict['metadata']['realtime_factor'] = result.metadata.realtime_factor
        with open(output_path, 'w') as f:
            json.dump(result_dict, f, indent=2)
        if verbose:
            print(f"\nResults saved to: {output_path}")

    return {
        'file_path': str(audio_path),
        'global_prediction': result.global_prediction,
        'global_confidence': result.global_confidence,
        'num_positive_segments': sum(result.local_predictions),
        'num_total_segments': len(result.local_predictions),
    }


def run_directory(model, input_dir: Path, output_dir: Path, config: dict):
    """Run inference on all audio files in input_dir, mirroring structure to output_dir."""
    audio_files = find_audio_files(input_dir)
    if not audio_files:
        print(f"No audio files found in {input_dir}")
        return

    print(f"Found {len(audio_files)} audio file(s) in {input_dir}")
    print(f"Output directory: {output_dir}")
    print("=" * 60)

    summary_rows = []
    for audio_path in audio_files:
        rel = audio_path.relative_to(input_dir)
        output_json = output_dir / rel.parent / (rel.stem + '.json')
        print(f"\n[{audio_files.index(audio_path)+1}/{len(audio_files)}] {rel}")
        row = run_single(model, audio_path, config, output_json, verbose=False)
        row['file_path'] = str(rel)  # store relative path in CSV
        # Print compact per-file summary
        print(f"  global_prediction={row['global_prediction']}  "
              f"global_confidence={row['global_confidence']:.3f}  "
              f"positive_segments={row['num_positive_segments']}/{row['num_total_segments']}")
        summary_rows.append(row)

    # Write summary CSV
    csv_path = output_dir / 'summary.csv'
    output_dir.mkdir(parents=True, exist_ok=True)
    with open(csv_path, 'w', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=[
            'file_path', 'global_prediction', 'global_confidence',
            'num_positive_segments', 'num_total_segments'
        ])
        writer.writeheader()
        writer.writerows(summary_rows)

    print("\n" + "=" * 60)
    print(f"Summary CSV written to: {csv_path}")
    num_detected = sum(1 for r in summary_rows if r['global_prediction'] == 1)
    print(f"Files with orca detected: {num_detected}/{len(summary_rows)}")


def _from_dict(cls, d: dict):
    """Create dataclass from dict, ignoring unknown keys."""
    import dataclasses
    field_names = {f.name for f in dataclasses.fields(cls)}
    return cls(**{k: v for k, v in d.items() if k in field_names})


def main():
    # Default config path relative to InferenceSystem directory
    default_config_path = os.path.join(os.path.dirname(__file__), '..', 'model', 'config.yaml')
    
    parser = argparse.ArgumentParser(description="Run model_v1 inference on audio files or directories")
    parser.add_argument("path", nargs="?", default=DEFAULT_AUDIO_PATH,
                        help="Path to audio file/directory or JSON result file/directory")
    parser.add_argument("--output", "-o",
                        help="Output path: JSON file (single-file mode) or directory (directory mode)")
    parser.add_argument("--config", "-c", default=default_config_path,
                        help=f"Path to config YAML file (default: {default_config_path})")
    parser.add_argument("--reaggregate", action="store_true",
                        help="Re-aggregate existing JSON results with current config (no model inference)")
    args = parser.parse_args()

    input_path = Path(args.path)
    config_path = args.config

    with open(config_path, 'r') as f:
        config = yaml.safe_load(f)

    # Re-aggregation mode: just re-run aggregation on existing JSON results
    if args.reaggregate:
        print(f"Using config from: {config_path}")
        gp_config = _from_dict(GlobalPredictionConfig, config.get('global_prediction', {}))
        print(f"Aggregation strategy: {gp_config.aggregation_strategy}")
        print("-" * 60)

        if input_path.is_dir():
            reaggregate_directory(input_path, gp_config)
        elif input_path.suffix == '.json':
            reaggregate_single(input_path, gp_config, verbose=True)
        else:
            print(f"Error: --reaggregate requires a JSON file or directory, got: {input_path}")
            return 1
        return 0

    # Normal inference mode
    model_path = os.path.join(os.path.dirname(__file__), '..', 'model', 'model_v1.pt')

    print(f"Loading model from: {model_path}")
    print(f"Using config from:  {config_path}")
    print("-" * 60)

    model = OrcaHelloSRKWDetectorV1.from_pretrained("orcasound/orcahello-srkw-detector-v1", config=config)
    print(f"Device: {model._device}  |  Dtype: {model._dtype}")
    print("-" * 60)

    if input_path.is_dir():
        output_dir = Path(args.output) if args.output else input_path.parent / (input_path.name + '_inference_output')
        run_directory(model, input_path, output_dir, config)
    else:
        print(f"Testing audio file: {input_path}")
        print("-" * 60)
        output_path = Path(args.output) if args.output else None
        row = run_single(model, input_path, config, output_path, verbose=True)
        if row['global_prediction'] == 1:
            print("\n*** ORCA DETECTED! ***")
            return 0
        else:
            print("\nNo orca detected.")
            return 1


if __name__ == "__main__":
    sys.exit(main())
