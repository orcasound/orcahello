import asyncio
import hmac
import ipaddress
import math
import os
import tempfile
import time
from collections import OrderedDict, deque
from pathlib import Path
from typing import BinaryIO

from starlette.responses import JSONResponse


class PayloadTooLarge(Exception):
    pass


class UnsupportedAudioFormat(Exception):
    pass


class InvalidAudio(Exception):
    pass


class AudioTooLong(Exception):
    pass


class AudioTooShort(Exception):
    pass


class InferenceBusy(Exception):
    pass


class SlidingWindowRateLimiter:
    """Process-local sliding-window limiter with bounded client-key storage."""

    def __init__(
        self,
        requests: int,
        window_seconds: float = 60.0,
        max_client_keys: int = 10_000,
        clock=None,
    ):
        self.requests = requests
        self.window_seconds = window_seconds
        self.max_client_keys = max_client_keys
        self._clock = clock or time.monotonic
        self._requests_by_key: OrderedDict[str, deque[float]] = OrderedDict()
        self._lock = asyncio.Lock()

    async def check(self, client_key: str) -> float | None:
        now = self._clock()
        cutoff = now - self.window_seconds
        async with self._lock:
            entries = self._requests_by_key.setdefault(client_key, deque())
            while entries and entries[0] <= cutoff:
                entries.popleft()

            self._requests_by_key.move_to_end(client_key)
            if len(entries) >= self.requests:
                return max(0.0, self.window_seconds - (now - entries[0]))

            entries.append(now)
            while len(self._requests_by_key) > self.max_client_keys:
                self._requests_by_key.popitem(last=False)
            return None


async def run_in_thread_until_complete(function, *args, **kwargs):
    """Wait for blocking work to finish before propagating cancellation."""
    worker = asyncio.create_task(asyncio.to_thread(function, *args, **kwargs))
    try:
        return await asyncio.shield(worker)
    except asyncio.CancelledError:
        while not worker.done():
            try:
                await asyncio.shield(worker)
            except asyncio.CancelledError:
                continue
            except Exception:
                break
        if worker.done() and not worker.cancelled():
            worker.exception()
        raise


def client_ip_for_scope(scope, trusted_proxy_cidrs: tuple[str, ...] = ()) -> str:
    client = scope.get("client")
    if not client:
        return "unknown"
    peer = str(client[0])
    try:
        peer_ip = ipaddress.ip_address(peer)
        trusted_networks = tuple(
            ipaddress.ip_network(cidr, strict=False) for cidr in trusted_proxy_cidrs
        )
    except ValueError:
        return peer

    if not any(peer_ip in network for network in trusted_networks):
        return peer

    forwarded_for = dict(scope.get("headers", [])).get(b"x-forwarded-for", b"")
    forwarded_ips = []
    for value in forwarded_for.decode("latin-1").split(","):
        try:
            forwarded_ips.append(ipaddress.ip_address(value.strip()))
        except ValueError:
            continue

    for address in reversed([*forwarded_ips, peer_ip]):
        if not any(address in network for network in trusted_networks):
            return str(address)
    return str(forwarded_ips[0]) if forwarded_ips else peer


class RequestGuardMiddleware:
    """Apply IP rate limiting and request-body caps before multipart parsing."""

    def __init__(
        self,
        app,
        limiter: SlidingWindowRateLimiter,
        max_body_bytes: int,
        trusted_proxy_cidrs: tuple[str, ...] = (),
        expected_api_key: str | None = None,
    ):
        self.app = app
        self.limiter = limiter
        self.max_body_bytes = max_body_bytes
        self.trusted_proxy_cidrs = trusted_proxy_cidrs
        self.expected_api_key = expected_api_key

    async def __call__(self, scope, receive, send):
        if (
            scope["type"] != "http"
            or scope.get("method") != "POST"
            or scope.get("path") != "/analyze"
        ):
            await self.app(scope, receive, send)
            return

        headers = dict(scope.get("headers", []))
        provided_api_key = headers.get(b"x-api-key", b"").decode("latin-1") or None
        if not api_key_is_valid(provided_api_key, self.expected_api_key):
            response = JSONResponse(
                status_code=401,
                content={"detail": "invalid or missing API key"},
                headers={"WWW-Authenticate": "ApiKey"},
            )
            await response(scope, receive, send)
            return

        client_key = client_ip_for_scope(scope, self.trusted_proxy_cidrs)
        retry_after = await self.limiter.check(client_key)
        if retry_after is not None:
            response = JSONResponse(
                status_code=429,
                content={"detail": "rate limit exceeded"},
                headers={"Retry-After": str(max(1, math.ceil(retry_after)))},
            )
            await response(scope, receive, send)
            return

        content_length = headers.get(b"content-length", b"")
        if content_length.isdigit() and int(content_length) > self.max_body_bytes:
            await self._too_large(scope, receive, send)
            return

        bytes_received = 0

        async def limited_receive():
            nonlocal bytes_received
            message = await receive()
            if message["type"] == "http.request":
                bytes_received += len(message.get("body", b""))
                if bytes_received > self.max_body_bytes:
                    raise PayloadTooLarge
            return message

        response_started = False

        async def tracked_send(message):
            nonlocal response_started
            if message["type"] == "http.response.start":
                response_started = True
            await send(message)

        try:
            await self.app(scope, limited_receive, tracked_send)
        except PayloadTooLarge:
            if not response_started:
                await self._too_large(scope, receive, send)

    async def _too_large(self, scope, receive, send):
        response = JSONResponse(
            status_code=413, content={"detail": "request body too large"}
        )
        await response(scope, receive, send)


def allowed_audio_suffix(filename: str | None) -> str:
    """Return a normalized supported suffix without trusting the filename path."""
    if not filename:
        raise UnsupportedAudioFormat
    basename = filename.replace("\\", "/").rsplit("/", 1)[-1]
    suffix = Path(basename).suffix.lower()
    if suffix not in {".wav", ".flac"}:
        raise UnsupportedAudioFormat
    return suffix


def _copy_bounded(source: BinaryIO, max_bytes: int, temp_path: str) -> str:
    source.seek(0)
    copied = 0
    with open(temp_path, "wb") as destination:
        while chunk := source.read(64 * 1024):
            copied += len(chunk)
            if copied > max_bytes:
                raise PayloadTooLarge
            destination.write(chunk)
    return temp_path


async def copy_upload_to_temp(source: BinaryIO, max_bytes: int, suffix: str) -> str:
    fd, temp_path = tempfile.mkstemp(prefix="orcahello-", suffix=suffix)
    os.close(fd)
    try:
        return await run_in_thread_until_complete(
            _copy_bounded, source, max_bytes, temp_path
        )
    except BaseException:
        Path(temp_path).unlink(missing_ok=True)
        raise


def inspect_audio(
    file_path: str | Path,
    expected_suffix: str,
    min_duration_seconds: float,
    max_duration_seconds: float,
) -> float:
    try:
        import soundfile as sf

        info = sf.info(str(file_path))
    except Exception as exc:
        raise InvalidAudio from exc

    if (
        not math.isfinite(min_duration_seconds)
        or not math.isfinite(max_duration_seconds)
        or min_duration_seconds < 0
        or min_duration_seconds >= max_duration_seconds
    ):
        raise ValueError("audio duration limits are invalid")

    audio_format = info.format.upper()
    suffix_for_format = {"WAV": ".wav", "FLAC": ".flac"}.get(audio_format)
    if suffix_for_format is None or suffix_for_format != expected_suffix:
        raise UnsupportedAudioFormat
    if info.frames <= 0 or info.samplerate <= 0 or info.channels <= 0:
        raise InvalidAudio

    duration = info.frames / info.samplerate
    if not math.isfinite(duration):
        raise InvalidAudio
    if duration > max_duration_seconds:
        raise AudioTooLong
    if duration < min_duration_seconds:
        raise AudioTooShort

    return duration


def api_key_is_valid(provided: str | None, expected: str | None) -> bool:
    if not expected:
        return True
    if provided is None:
        return False
    return hmac.compare_digest(
        provided.encode("utf-8", errors="replace"),
        expected.encode("utf-8", errors="replace"),
    )
