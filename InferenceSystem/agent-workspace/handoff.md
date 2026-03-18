# Agent Workspace: HLS Timestamp Fix (#430, #457)

Branch: `claude/debug-timestamp-issue-5iZAA`
Issues: [#430](https://github.com/orcasound/orcahello/issues/430), [#457](https://github.com/orcasound/orcahello/issues/457)

## What was done

Replaced `orca-hls-utils==0.0.4` with a new internal `src/orcasound_hls/` module that fixes timestamp drift and expensive S3 listing.

### Root cause (#430)

The old `orca_hls_utils.HLSStream.get_next_clip()` computed `clip_start_time` as `end_time - hardcoded_60s`, where `end_time` was derived from `segment_end_index * rounded_average_duration + folder_epoch`. Two problems:

1. **Rounding compounds** — the average segment duration is slightly off; multiplied by hundreds of segments, drift grows to minutes over long-running streams.
2. **Hardcoded 60s** — actual clip duration depends on `polling_interval` and segment boundaries, not a constant.

Additionally, the old `DateRangeHLSStream.get_next_clip()` accidentally entered "demo mode" when the orchestrator passed its cursor datetime, overwriting the deterministic timestamp with the system-time-based cursor.

### New module: `src/orcasound_hls/`

| File | Purpose |
|------|---------|
| `segment.py` | `HLSSegment` frozen dataclass. Metadata + `download_as_wav()` / `download_as_flac()`. |
| `iterators.py` | `date_range_segments()` and `live_segments()` — stateless generators yielding `HLSSegment`. |
| `hls_locator.py` | Prefix-filtered S3 folder lookup (bisect, not full scan). |
| `__init__.py` | Re-exports `HLSSegment`, `date_range_segments`, `live_segments`. |

Key design decisions:
- **No stateful classes** — generators manage iteration, `HLSSegment` is immutable
- **Deterministic timestamps** — derived from `folder_epoch + cumulative_segment_durations[index] + audio_offset`
- **Prefix-filtered S3 listing** — uses first 4 digits of unix timestamp as S3 prefix, reducing API calls from ~1000+ folders to ~20

### Orchestrator changes (`src/LiveInferenceOrchestrator.py`)

- `build_hls_iterator()` replaces `build_hls_stream()` — returns a generator
- `run_loop()` simplified to `for segment in hls_segments:` pattern
- Removed `timedelta`/`UTC` imports, cursor management, `is_stream_over()` checks
- `HLSSegment.start_iso` used directly for CosmosDB `timestamp` field

### Dependency changes (`pyproject.toml`)

- Removed: `orca-hls-utils==0.0.4`
- Added: `ffmpeg-python==0.2.0`, `m3u8==6.0.0`, `boto3>=1.35.0` (were transitive deps of orca-hls-utils, now direct)

## What still needs to be done

### Must-do before merge

1. **Run full orchestrator integration tests** (`pytest tests/test_orchestrator.py -v`). These require HuggingFace Hub access to download the model, which was blocked by a proxy in the dev environment. All DateRangeHLS positive/negative/fail tests and the LiveHLS smoke test need to pass.

2. **Verify `live_segments()` end-to-end** against actual live hydrophone. The `date_range_segments()` path is well-tested (17/17 tests pass via `tests/test_orcasound_hls.py`), but `live_segments()` hasn't been exercised against a real live feed yet. Quickest way:
   ```bash
   cd InferenceSystem
   uv run python src/LiveInferenceOrchestrator.py \
     --orch_config tests/orch_configs/LiveHLS/LiveHLS_OrcasoundLab.yml \
     --max_iterations 2
   ```

3. **Docker build test** — verify the Dockerfile still works since `orca-hls-utils` was removed and new deps were added.

### Should-do

4. **M3U8 caching in `date_range_segments()`** — currently re-loads the M3U8 playlist for every clip within the same folder. The playlist is static for historical data, so it could be loaded once per folder. Easy optimisation in `iterators.py`.

5. **`audio_offset` per-hydrophone** — the 2-second constant is a guess. Could be made configurable via orch_config or auto-detected from the first few segments.

6. **Expose `HLSSegment` metadata in CosmosDB** — `build_cosmosdb_metadata()` could store `folder_epoch`, `start_index`, `end_index` for full provenance. This directly addresses the "seek location metadata" goal of #457.

7. **Consider contributing back** to `orca-hls-utils` per #457's goal.

## How to run tests

```bash
cd InferenceSystem
uv sync --group dev

# Module-level tests (no model needed, hits real S3)
uv run pytest tests/test_orcasound_hls.py -v

# Full orchestrator tests (needs HuggingFace access)
uv run pytest tests/test_orchestrator.py -v
```

## Key files to read

- `src/orcasound_hls/segment.py` — the `HLSSegment` dataclass (start here)
- `src/orcasound_hls/iterators.py` — generator functions
- `src/orcasound_hls/hls_locator.py` — S3 folder lookup
- `src/LiveInferenceOrchestrator.py` — `build_hls_iterator()` and `run_loop()`
- `tests/test_orcasound_hls.py` — 17 tests covering locator, dataclass, iteration, download
