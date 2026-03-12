#!/usr/bin/env python
"""
Segment an audio file and save mel spectrogram images for each segment.

Supports WAV, FLAC, and any other ffmpeg-supported format. Loads the model
config from model/config.yaml and overrides input_pad_s to match the requested
segment duration so the full segment is preserved without padding/cropping.

Outputs:
  <output_dir>/
    ├── segment_01_0000s_0060s.wav
    ├── segment_02_0060s_0120s.wav
    └── spectrograms/
        ├── segment_01_0000s_0060s.png
        └── ...

Usage:
    cd InferenceSystem
    source model-v1-venv/bin/activate

    # Default WAV test file, 60s segments
    python scripts/run_audio_processing.py

    # FLAC file
    python scripts/run_audio_processing.py /path/to/audio.flac

    # WAV with custom segment duration
    python scripts/run_audio_processing.py /path/to/audio.wav --segment-duration 30

    # Custom output directory
    python scripts/run_audio_processing.py /path/to/audio.flac --output-dir /tmp/my_segments
"""
import argparse
import copy
import math
import os
import sys

import matplotlib.pyplot as plt
import numpy as np
import yaml
import soundfile as sf
import torch
from librosa import get_duration

# Add src directory to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'src'))

from model.audio_frontend import audio_segment_generator, prepare_waveform

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_DIR = os.path.join(SCRIPT_DIR, '..')
CONFIG_PATH = os.path.join(REPO_DIR, 'tests', 'test_config.yaml')
DEFAULT_AUDIO_PATH = os.path.join(
    REPO_DIR, 'tests', 'test_data',
    'rpi_sunset_bay_2025_09_18_01_12_06_PDT--f6b3fcd7-2036-433a-8a18-76a6b3b4f0c9.wav'
)
DEFAULT_OUTPUT_BASE = os.path.join(REPO_DIR, 'tmp')


def save_spectrogram(mel_spec, start_s, end_s, img_path, config):
    """Render mel spectrogram tensor to a PNG image."""
    spec_np = mel_spec.squeeze(0).numpy()  # (n_mels, n_frames)

    hop = config["spectrogram"]["hop_length"]
    sr = config["audio"]["resample_rate"]
    top_db = config["spectrogram"]["top_db"]
    n_frames = spec_np.shape[1]

    fig, ax = plt.subplots(figsize=(16, 4))
    img = ax.imshow(
        spec_np,
        aspect="auto",
        origin="lower",
        cmap="magma",
        vmin=spec_np.max() - top_db,
        vmax=spec_np.max(),
    )

    # X-axis ticks in wall-clock seconds from source file (~10 ticks)
    tick_step_s = max(1, int((end_s - start_s) / 10))
    tick_frames = np.arange(0, n_frames, tick_step_s * sr / hop)
    ax.set_xticks(tick_frames)
    ax.set_xticklabels([f"{start_s + t * hop / sr:.0f}" for t in tick_frames])

    ax.set_xlabel(f"Time (s)  [{start_s:.1f}s – {end_s:.1f}s of source]")
    ax.set_ylabel("Mel bin")
    ax.set_title(
        f"Mel spectrogram — {start_s:.1f}–{end_s:.1f}s  "
        f"(shape: {spec_np.shape[0]}×{n_frames},  "
        f"min={spec_np.min():.1f} dB  max={spec_np.max():.1f} dB)"
    )

    plt.colorbar(img, ax=ax, label="dB")
    plt.tight_layout()
    plt.savefig(img_path, dpi=100)
    plt.close(fig)


def main():
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        "audio_file", nargs="?", default=DEFAULT_AUDIO_PATH,
        help="Path to audio file (WAV, FLAC, etc.). Defaults to test WAV."
    )
    parser.add_argument(
        "--segment-duration", type=float, default=60.0, metavar="SECS",
        help="Segment duration in seconds (default: 60.0)"
    )
    parser.add_argument(
        "--output-dir", default=None, metavar="DIR",
        help="Output directory for WAV segments and spectrogram images. "
             "Defaults to claude-scratch/tmp/<stem>_segments/"
    )
    args = parser.parse_args()

    audio_path = os.path.abspath(args.audio_file)
    seg_dur = args.segment_duration

    if not os.path.exists(audio_path):
        print(f"ERROR: audio file not found: {audio_path}")
        sys.exit(1)

    # Default output dir: claude-scratch/tmp/<stem>_segments/
    if args.output_dir is None:
        stem = os.path.splitext(os.path.basename(audio_path))[0]
        output_dir = os.path.join(DEFAULT_OUTPUT_BASE, f"{stem}_segments")
    else:
        output_dir = os.path.abspath(args.output_dir)

    spec_dir = os.path.join(output_dir, 'spectrograms')
    os.makedirs(output_dir, exist_ok=True)
    os.makedirs(spec_dir, exist_ok=True)

    # Load config and override input_pad_s to match the requested segment duration
    with open(CONFIG_PATH, 'r') as f:
        config = yaml.safe_load(f)
    config_seg = copy.deepcopy(config)
    config_seg["model"]["input_pad_s"] = seg_dur

    duration = get_duration(path=audio_path)
    n_expected = math.ceil(duration / seg_dur)
    print(f"Audio file  : {audio_path}")
    print(f"Duration    : {duration:.3f}s")
    print(f"Segment dur : {seg_dur}s  ({n_expected} segment(s) expected)")
    print(f"Output dir  : {output_dir}")
    print()

    segments = []
    # strict_segments=False: allow a final partial segment when file duration < seg_dur
    for seg_path, start_s, end_s in audio_segment_generator(
        audio_path,
        segment_duration_s=seg_dur,
        segment_hop_s=seg_dur,
        output_dir=output_dir,
        strict_segments=False,
        process_waveform_config=config_seg["audio"],
    ):
        segments.append((seg_path, start_s, end_s))
        seg_num = len(segments)
        size_kb = os.path.getsize(seg_path) / 1024

        data, sample_rate = sf.read(seg_path, dtype="float32")
        waveform = torch.from_numpy(data.reshape(1, -1))
        mel_spec = prepare_waveform(waveform, sample_rate, config_seg)
        spec_np = mel_spec.squeeze(0).numpy()

        img_path = os.path.join(
            spec_dir, f"segment_{seg_num:02d}_{int(start_s):04d}s_{int(end_s):04d}s.png"
        )
        save_spectrogram(mel_spec, start_s, end_s, img_path, config)

        print(f"  [{seg_num}] {start_s:.1f}–{end_s:.1f}s  "
              f"wav={size_kb:.0f} KB  "
              f"spec={spec_np.shape[0]}×{spec_np.shape[1]}  "
              f"→ {os.path.basename(img_path)}")

    print(f"\nDone: {len(segments)} segment(s) processed.")
    print(f"WAV segments   : {output_dir}/")
    print(f"Spectrograms   : {spec_dir}/")


if __name__ == "__main__":
    main()
