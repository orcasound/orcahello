# Short (rolling) HLS manifests: what breaks, and where to fix it

Status as of 2026-11-14. Covers every Orcasound repository found that reads
HLS manifests from `audio-orcasound-net`.

## Summary

Since commit `697c5b2` (2026-10-16) on orcanode's `node_2026` branch, a node
writes two manifests in each S3 session folder:

| file | contents | updated |
|---|---|---|
| `live.m3u8` | Rolling window: only the **last 10** `.ts` segments, with `EXT-X-MEDIA-SEQUENCE:<N>` and `EXT-X-PROGRAM-DATE-TIME` (PDT) on each segment. About 34 lines. | every segment (~10 s) |
| `archive.m3u8` | **Every** segment since the folder started: `MEDIA-SEQUENCE:0`, starts at `live000.ts`, PDT on each segment, no `EXT-X-ENDLIST` while recording. About 26,000 lines by end of day (8,640 segments × 3 lines). | at most every 5 min (`ARCHIVE_UPLOAD_INTERVAL = 300` in `upload_s3.py`) |

Almost every consumer assumes `live.m3u8` lists every segment from
`live000.ts`. With a rolling window, each consumer either finds nothing or
silently computes wrong times. No consumer reads `archive.m3u8` yet.

**Orcasound Lab has written short manifests since 2026-10-16 20:44 UTC.**
From that point on:

- **orcasite** (bouts, reports, spectrograms, bout export) ingests only 14
  segments per day, with wrong start times. Bouts at Orcasound Lab can't play
  audio older than ~100 s. Confirmed on production; see
  [orcasite](#orcasite-liveorcasoundnet).
- **OrcaHello** live inference, as deployed, gets 0 segments per poll for this
  node. Fixed in orcahello PR #698, not yet deployed.
- **PODS-AI** live inference vendors the same old code (git submodule), and its
  training-data downloads have their own copy of the broken logic.
- **orca-hls-utils** (used by ambient-sound-analysis) and **orca-shipnoise**
  playback break the same way.

Every node will move to `node_2026` eventually. Until then, every consumer has
to handle all the manifest kinds below.

## The manifest kinds a client will meet

| kind | written by | how to recognize it | segment *n*'s start time |
|---|---|---|---|
| **A. Legacy full** | orcanode `main` (`node/stream.sh`, ffmpeg segment muxer `-segment_list live.m3u8`) | `MEDIA-SEQUENCE:0`, no PDT, lists every segment | `folder_epoch + 2.0 s + Σ EXTINF before n` (the 2.0 s is `FOLDER_TO_AUDIO_OFFSET`, an estimate; see orcasite#1041) |
| **B. Rolling 5, no archive** | `node_2026` before May 2026 (`-hls_list_size 5`) | rolling window, no `archive.m3u8`. Most of these folders were rewritten as full manifests by `repair_manifests.py`, so they now look like C. | after repair: the PDT written by the repair script (approximate times end in `.000Z`) |
| **C. Full with PDT** | `node_2026` May–Oct 2026 (`-hls_list_size 0`) | `MEDIA-SEQUENCE:0`, PDT on segments, lists every segment | PDT |
| **D. Rolling 10 + archive** | `node_2026` since 2026-10-16 (`LIVE_LIST_SIZE=10`) | `live.m3u8` has `MEDIA-SEQUENCE:N>0` and only 10 entries; `archive.m3u8` exists | PDT (in either file). Sequence number = `MEDIA-SEQUENCE` + position in the list |

What the nodes were writing on 2026-11-14 (from `latest.txt` and each node's
current folder):

| node | `live.m3u8` | PDT | `archive.m3u8` | kind |
|---|---|---|---|---|
| `rpi_orcasound_lab` | 34 lines, `MEDIA-SEQUENCE:3876` | yes | yes | **D** |
| `rpi_sunset_bay` | full, `MEDIA-SEQUENCE:0` | yes | no | C |
| `rpi_andrews_bay`, `rpi_bush_point`, `rpi_north_sjc`, `rpi_point_robinson` | full, `MEDIA-SEQUENCE:0` | no | no | A |
| `rpi_port_townsend` | empty | – | no | – |
| `rpi_mast_center` | last folder 2026-07-16 | no | no | A |

A Sunset Bay upgrade to the current `node_2026` would move it to kind D, with
the same breakage Orcasound Lab has.

Things that hold for every kind:

- Folder name = Unix time the container started. Since `setup.sh`, nodes
  restart at local midnight, so there is one folder per day.
- Segment files are `live%03d.ts`: 3 digits padded, wider after 999
  (`live999.ts` → `live1000.ts`). The number is the HLS media sequence number,
  not a time.
- `.ts` files are never deleted from S3. Segments that miss the live upload are
  uploaded later by `catchup_s3.py`, so their S3 `LastModified` is not their
  recording time.
- PDT carries a UTC offset (e.g. `2026-11-14T10:46:14.595-0800`). Parse it as
  an absolute instant. ffmpeg derives it from the session start plus
  accumulated durations, so it removes the 2 s guess but doesn't correct
  sound-card clock drift.

## How a request for a time window should be resolved

Rule of thumb: **prefer `archive.m3u8` for anything in the past, and use
`live.m3u8` only for the live edge. Use PDT for the time when it's present.**

For each folder that could cover `[t0, t1)` (list S3 folders; one folder can
hold up to ~24 h):

1. Load `live.m3u8`. If it has PDT, give each entry
   `seq = MEDIA-SEQUENCE + index` and `start = PDT`.
2. **No PDT and `MEDIA-SEQUENCE:0` (kind A):**
   `start = folder_epoch + 2.0 + Σ EXTINF`. This is the old behavior, and it's
   still correct for these folders.
3. **Window inside the rolling window:** done. This is the cheapest case.
4. **Window starts up to a few minutes before the rolling window** (the
   normal live-inference case, with a 60 s delay buffer): build names
   `live<seq>.ts` for the missing sequence numbers. Time them by stepping back
   from the earliest PDT with the mean EXTINF, and HEAD-check that each one
   exists. Errors are well under 1 s for a few segments.
5. **Window further back:** load `archive.m3u8`. It lists every segment with
   exact PDT. It can be up to ~5 min behind `live.m3u8`, so fill any gap
   between the two with step 4.
6. Never build a clip across a missing sequence number. Gaps are real
   outages.

`archive.m3u8` alone covers most non-live uses (bouts, reports, training
downloads, playback of past audio). Because it starts at `live000.ts` with
`MEDIA-SEQUENCE:0`, even an old cumulative-EXTINF parser computes the right
order and roughly right times from it. Only callers within ~5 min of "now"
need step 4.

`OrcasoundHLSClient` in this repo (`src/orcasound_hls/client.py`, PR #698)
implements steps 1–6. Python consumers can import it rather than write their
own.

## What happens today, consumer by consumer

### orcahello (this repo)

| where | what it reads | short-manifest behavior | fix |
|---|---|---|---|
| `InferenceSystem/src/orcasound_hls/` (`OrcasoundHLSClient.get_segments`), used by `LiveInferenceOrchestrator.py` | `live.m3u8`, cumulative EXTINF from `live000` | **Before PR #698:** treats the first listed segment (e.g. `live3876.ts`) as `live000.ts`, so the 10 segments are placed at the start of the folder, which is hours too early (by however long the node has been recording since midnight). Every live poll logs `Got 0 segments` without an error. | **PR #698** implements the algorithm above, with 9 offline tests. Legacy folders give identical results. **Takes effect only after merge *and* a manual `InferenceSystem-deploy` run** for each hydrophone. |
| `ModeratorFrontEnd/.../DetectionService.cs`, `NotificationSystem/.../OrcasiteHelper.cs` | S3 folder list only | unaffected | – |

### orcasite (live.orcasound.net)

This is where most user-facing features (bouts, reports, spectrograms, bout
export) get their audio, and it's broken for kind D today.

**Ingestion path:** an S3 event for an object ending in `.m3u8` goes to SQS,
then `Orcasite.Radio.FeedStreamQueue`
(`server/lib/orcasite/radio/feed_stream_queue.ex`). It upserts a `FeedStream`
with `playlist_m3u8_path = …/live.m3u8` and enqueues the
`Workers.UpdateFeedSegments` job, which runs `FeedStream :update_segments`. That
calls `FeedStream.parse_manifest/3`
(`server/lib/orcasite/radio/feed_stream.ex`), which creates one `FeedSegment`
per manifest entry.

Problems with kind D:

1. **`archive.m3u8` is ignored.** `select_recent_timestamp/2` matches only
   `…/hls/<ts>/live.m3u8`, and `from_m3u8_path` requires that same pattern.
2. **`parse_manifest` ignores `EXT-X-MEDIA-SEQUENCE`.** It starts each manifest
   at `feed_stream.start_time` (the folder epoch) and adds up EXTINF from 0. Each
   rolling `live.m3u8` is therefore placed in the first ~100 s of the folder.
   PDT is recorded in `program_date_time` but deliberately not used (orcasite#1041).
3. **Ingestion stops after 14 segments per day.** Production for today's
   Orcasound Lab folder (`1794643213`): 14 FeedSegments (`live000`–`live013`),
   with `live013.ts` stored at `08:01:42Z` while its PDT is `08:02:24Z`. Every
   Orcasound Lab FeedStream since 2026-10-16 has `duration` ≈ 100 s (a full day
   is ≈ 86,390 s). The likely cause, not confirmed against server logs: once
   the window rolls, new segments get start times that collide with the unique
   identity `feed_segment_timestamp [:feed_id, :start_time]`. The bulk insert
   uses `stop_on_error?: true`, so every later batch fails.
4. **`:update_end_time_and_duration`** adds up the parsed manifest, so it
   records ~100 s.

Downstream effects for Orcasound Lab since 2026-10-16:

| feature | how it finds audio | effect |
|---|---|---|
| Bouts (`Preparations.FeedSegmentsForBout`, `:for_feed_range`) | FeedSegments overlapping the bout's time | no segments, except the first ~100 s after midnight |
| Bout export (`bout_export_script.ex`, `bout_export_json.ex`) | the bout's FeedSegments | empty or wrong export |
| Spectrograms (`AudioImage` → `spectrogram-renderer` Lambda) | FeedSegments, passed as presigned `.ts` URLs | no tiles. The Lambda itself is fine. |
| Bout playback (`ui/src/components/Player/BoutPlayer.tsx`) | plays `…/<folder>/live.m3u8` and seeks to `targetTime − folder_epoch` | only the last ~100 s can be played. The offset also assumes the playlist starts at `live000`. |
| Live player (`useTimestampFetcher.ts`, `Player.tsx`) | `latest.txt` → `live.m3u8` | works; this is what the short manifest is for |
| Listener reports (`DetectionDialog.tsx`) | stores `playlist_timestamp` + `player_offset` (video.js `currentTime`) | **to verify:** with a rolling live playlist, `currentTime` may not be seconds since the folder started |

Suggested orcasite changes:

- In `FeedStreamQueue` / `from_m3u8_path`, accept `archive.m3u8` and make it the
  FeedStream's `playlist_m3u8_path` when it exists. Either skip `live.m3u8`
  events for folders that have an archive, or keep them only to create the
  FeedStream promptly.
- In `parse_manifest`, either honor `EXT-X-MEDIA-SEQUENCE` or refuse to ingest
  a manifest with `MEDIA-SEQUENCE > 0`, so that wrong rows are never written.
  Whether to use PDT for `start_time` is the orcasite#1041 decision. Note that
  PDT drops the 2 s offset, so mixed feeds would be inconsistent until the
  legacy path also adds `FOLDER_TO_AUDIO_OFFSET`.
- In `BoutPlayer`, use `archive.m3u8` for past times when it exists. It has no
  `ENDLIST` while recording, so players treat it as live and allow seeking
  back over the whole list.
- Repair the data: delete the wrong Orcasound Lab FeedSegments since
  2026-10-16, reset `duration`/`end_time` on those FeedStreams, and re-run
  `:update_segments` against each folder's `archive.m3u8`.
- Ingest within ~5 min of "now" lags because the archive uploads every 5 min.
  That's fine for bouts; live features keep using `live.m3u8`.

Related history: orcasite#1035 (PDT tags broke the old parser; fixed by
PR #1045 `ex_m3u8` and PR #1046, which records PDT). That fix covered kind C,
not D.

### pods-ai

| where | what it reads | short-manifest behavior | fix |
|---|---|---|---|
| `src/LiveInferenceOrchestrator.py` (live inference) | imports `orcasound_hls` from the `external/orcahello` submodule. `LiveInferenceSystem/Dockerfile` copies it in. | the submodule is pinned to orcahello `0efa6a8` (2026-11-04), the old code: 0 segments for kind D | after PR #698 merges, bump the submodule and redeploy |
| `src/audio_utils.py` `download_audio_from_start_utc` (used by `download_60s_audio`, `process_false_positives.py`, `process_false_negatives.py`) | `live.m3u8`; index = time since folder ÷ mean EXTINF | with 10 entries, the computed index is far beyond the list, so it returns `No segments` / `None` | read `archive.m3u8` when present (index = sequence number, since the archive starts at 0), or call `OrcasoundHLSClient.get_segments` |
| `src/download_wavs.py` `download_audio_segment` (training/testing WAVs) | same pattern, via `load_m3u8_with_retry` | same failure for kind D folders | same |
| `src/mcp_server.py` `list_s3_recordings` (tool for AI agents) | returns `…/<epoch>/live.m3u8` URLs | gives agents a 10-segment playlist for kind D folders | also return `archive.m3u8` when it exists, and say which manifest is complete |
| `timestamps.md` | documentation | doesn't mention rolling manifests | add a pointer to this document |

### orca-hls-utils (and ambient-sound-analysis)

`src/orca_hls_utils/HLSStream.py` (live) and `DateRangeHLSStream.py` load
`live.m3u8`. They compute `segment_start_index = (time − folder − audio_offset)
÷ mean EXTINF` and index into `stream_obj.segments`. With 10 entries:

- `DateRangeHLSStream`: `segment_end_index > num_total_segments` is always
  true, so it skips to the next folder and returns `(None, None, None)`. No
  audio.
- `HLSStream`: the same index math runs past a 10-entry list.

`ambient-sound-analysis` depends on it (`requirements.txt`:
`orca-hls-utils@git+…`), so its PSD pipeline has no data for kind D folders.
**Fix:** read `archive.m3u8` when it exists. For the live edge, use the
sequence-number approach, or switch to `OrcasoundHLSClient`.

### orca-shipnoise

- `scripts/preprocess/get_latest_timestamp.py` lists `.ts` keys and uses S3
  `LastModified` as the start time. It never reads a manifest, so it's
  unaffected by kind D, but times are wrong for segments uploaded late by
  `catchup_s3.py`.
- `backend/api_server.py` `parse_hls_info` builds `…/<folder>/live.m3u8` plus a
  playback offset `seg_number × 10 s`. For kind D that playlist holds only the
  last 10 segments, so the frontend player (`DetectionsPlayer.tsx`) can't reach
  the clip. **Fix:** use `archive.m3u8` when it exists.

### Other repos

| repo / file | reads | affected? |
|---|---|---|
| `orcanode-monitor` `Core/Fetcher.cs` | `latest.txt`, then the second-to-last `.ts` in `live.m3u8` | **no.** It only needs the tail. |
| `ais-shipnoise` `web/hydrophones.html` | `latest.txt` → `live.m3u8` for live listening | no |
| `spectrogram-renderer` | one presigned `.ts` URL from orcasite | no, but it depends on orcasite FeedSegments |
| `orcanode` `node_2026/repair_manifests.py` | skips folders that have `archive.m3u8` | no |
| `orca-action-workflow` `orcasound_processing.py` | local copy of a folder; converts each `.ts` listed in `live.m3u8`, timed from the folder epoch | yes for kind D (only the last 10, wrong times). Currently points at `streaming-orcasound-net/rpi_bush_point`, so low priority. |
| `downloadFromS3`, `aifororcas-podcast-ml` | old copies of `DateRangeHLSStream` / index math on `live.m3u8` | yes, but these are legacy scripts |

## Priority

1. **orcasite**: ingestion, bouts, spectrograms and bout playback. It's
   user-facing, and it needs a data repair for Orcasound Lab back to
   2026-10-16.
2. **orcahello**: merge PR #698 and run `InferenceSystem-deploy` for
   orcasound-lab. Live detections for that node have been missing since
   2026-10-16.
3. **pods-ai**: bump the submodule (live inference) and fix
   `audio_utils.py` / `download_wavs.py` (training data).
4. **orca-hls-utils**, which also fixes ambient-sound-analysis.
5. **orca-shipnoise** playback, then `pods-ai` `mcp_server` URLs.

A node-side option that would reduce the client work: set `LIVE_LIST_SIZE=20`
in `stream_sync.sh`. The rolling window would then cover the whole
live-inference look-back (~150 s), so live clients never need step 4, and the
playlist would stay at ~64 lines. Past-time consumers still need
`archive.m3u8`.

## Checking a node by hand

```bash
B=https://s3-us-west-2.amazonaws.com/audio-orcasound-net
N=rpi_orcasound_lab
TS=$(curl -s $B/$N/latest.txt)
curl -s $B/$N/hls/$TS/live.m3u8 | head -8         # MEDIA-SEQUENCE > 0 and ~34 lines → kind D
curl -sI $B/$N/hls/$TS/archive.m3u8 | head -1     # 200 → archive present
curl -s $B/$N/hls/$TS/archive.m3u8 | tail -3      # newest archived segment (up to 5 min old)
```

orcasite's view of the same folder (public GraphQL; Orcasound Lab's feed ID is
`feed_02u8r4ELz2xkCjPTc4yLWE`):

```bash
curl -s https://live.orcasound.net/graphql -H 'content-type: application/json' -d '{"query":
 "{ feedStreams(feedId:\"feed_02u8r4ELz2xkCjPTc4yLWE\", sort:[{field:START_TIME, order:DESC}], limit:5)
    { results { playlistTimestamp startTime duration } } }"}'
```

A healthy day shows `duration` ≈ 86,390. Kind D folders currently show ≈ 100.

## Open questions

- Why orcasite ingestion stops at 14 segments: the unique-identity collision
  is the likely cause, but it should be confirmed in the Oban or server logs.
- What `player_offset` means for listener reports made from a rolling live
  playlist.
- Whether the final `archive.m3u8` upload after the midnight restart always
  happens (README: "the final version is uploaded after the midnight
  restart"). If it doesn't, the last ≤5 min of each day are only reachable by
  sequence number.
- PDT accuracy against wall-clock time over a full day. It's computed from
  accumulated segment durations, so it is exactly as good as the sound-card
  clock.
