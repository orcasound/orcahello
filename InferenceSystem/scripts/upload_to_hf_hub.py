#!/usr/bin/env python3
"""Upload OrcaHelloSRKWDetectorV1 to HuggingFace Hub."""

import argparse
from pathlib import Path
import sys
import yaml
import os
import shutil
import tempfile

SCRIPT_DIR = Path(__file__).parent
ROOT_DIR = SCRIPT_DIR.parent
sys.path.insert(0, str(ROOT_DIR / "src"))

from model_v1 import OrcaHelloSRKWDetectorV1
from huggingface_hub import HfApi, upload_file

DEFAULT_REPO_ID = "orcasound/orcahello-srkw-detector-v1"
DEFAULT_MODEL_CARD = ROOT_DIR / "model/MODEL_CARD.md"
DEFAULT_LICENSE = ROOT_DIR / "model/LICENSE"
DEFAULT_HERO_IMAGE = ROOT_DIR / "model/img-orca_fin_waveform.jpg"
DEFAULT_CONFIG = ROOT_DIR / "model/config.yaml"

def main():
    # Check for HF_TOKEN environment variable
    hf_token = os.getenv("HF_TOKEN")
    if not hf_token:
        print("⚠️  Warning: HF_TOKEN environment variable not set.")
        print("   You need a HuggingFace token with WRITE permissions to upload.")
        print("   Get one at: https://huggingface.co/settings/tokens")
        print("   Then set it: export HF_TOKEN=hf_xxx...")
        print()
    
    parser = argparse.ArgumentParser(description="Upload OrcaHelloSRKWDetectorV1 to HuggingFace Hub")
    parser.add_argument("-m", "--commit-message", required=True, help="Commit message for upload")
    parser.add_argument("--repo-id", default=DEFAULT_REPO_ID, help=f"HuggingFace repo ID (default: {DEFAULT_REPO_ID})")
    parser.add_argument("--checkpoint", type=Path, default=ROOT_DIR / "model/model_v1.pt", help="Path to model checkpoint")
    parser.add_argument("--config", type=Path, default=DEFAULT_CONFIG, help="Path to config YAML")
    parser.add_argument("--model-card", type=Path, default=DEFAULT_MODEL_CARD, help="Path to model card (README.md)")
    parser.add_argument("--license-file", type=Path, default=DEFAULT_LICENSE, help="Path to LICENSE file")
    args = parser.parse_args()

    # Validate paths
    if not args.checkpoint.exists():
        print(f"Error: Checkpoint not found at {args.checkpoint}")
        sys.exit(1)

    if not args.config.exists():
        print(f"Error: Config not found at {args.config}")
        sys.exit(1)

    # Load config
    print(f"Loading config from {args.config}")
    with open(args.config) as f:
        config_dict = yaml.safe_load(f)

    # Load model
    print(f"Loading model from {args.checkpoint}")
    model = OrcaHelloSRKWDetectorV1.from_checkpoint(str(args.checkpoint), config_dict)

    # Create temporary directory for upload
    with tempfile.TemporaryDirectory() as tmpdir:
        tmpdir = Path(tmpdir)

        # Save model to temp directory
        print(f"Preparing model files...")
        model.save_pretrained(tmpdir)

        # Copy custom README if provided
        if args.model_card and args.model_card.exists():
            print(f"Adding custom model card from {args.model_card}")
            shutil.copy(args.model_card, tmpdir / "README.md")
        else:
            print("⚠️  No model card found, using default from PyTorchModelHubMixin")

        # Copy LICENSE file if provided
        if args.license_file and args.license_file.exists():
            print(f"Adding LICENSE file from {args.license_file}")
            shutil.copy(args.license_file, tmpdir / "LICENSE")
        else:
            print("⚠️  No LICENSE file found")
        
        # Copy config and hero image
        shutil.copy(args.config, tmpdir / "config.yaml")
        shutil.copy(DEFAULT_HERO_IMAGE, tmpdir / "img-orca_fin_waveform.jpg")

        # Upload everything to hub
        print(f"\nUploading to {args.repo_id}...")
        print(f"Commit message: {args.commit_message}")

        api = HfApi()
        api.upload_folder(
            folder_path=tmpdir,
            repo_id=args.repo_id,
            repo_type="model",
            commit_message=args.commit_message,
            token=hf_token,
            create_pr=False,
        )

    print(f"\n✓ Successfully uploaded to: https://huggingface.co/{args.repo_id}")
    print(f"\nFiles uploaded:")
    print(f"  - config.yaml (model configuration)")
    print(f"  - config.json (model configuration: HF serialized)")
    print(f"  - model.safetensors (model weights)")
    if args.model_card and args.model_card.exists():
        print(f"  - README.md (model card)")
    if args.license_file and args.license_file.exists():
        print(f"  - LICENSE (RAIL license)")
    print(f"\nTo load the model:")
    print(f"  from src.model_v1.inference import OrcaHelloSRKWDetectorV1")
    print(f"  model = OrcaHelloSRKWDetectorV1.from_pretrained('{args.repo_id}')")

if __name__ == "__main__":
    main()
