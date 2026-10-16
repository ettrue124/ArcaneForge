"""Bounded JSON-lines subprocess transport for a real rules engine.

Wire contract: requests are {id, op, ...fields}; replies are
{id, ok: true, result: ...} or {id, ok: false, error: "..."}.
Only protocol replies belong on stdout; engine diagnostics belong on stderr.
This transport never manufactures cards or match outcomes.
"""
import collections
import json
import os
from pathlib import Path
import queue
import subprocess
import threading
import time
import uuid


class EngineError(RuntimeError):
    pass


class EngineTimeout(EngineError):
    pass


class EngineRejected(EngineError):
    """A correlated rules-engine rejection; the transport is still healthy."""


class Engine:
    MAX_LINE = 1024 * 1024

    @classmethod
    def from_config(cls, path):
        config_path = Path(path).resolve(strict=True)
        try:
            config = json.loads(config_path.read_text(encoding='utf-8'))
            command = config['command']
            cwd = Path(config['cwd'])
            if not cwd.is_absolute():
                cwd = config_path.parent / cwd
            return cls(command, cwd, default_timeout=config.get('defaultTimeout', 90), env=config.get('env'))
        except (KeyError, TypeError, ValueError, OSError) as exc:
            raise EngineError('Invalid engine launch configuration: ' + str(exc)) from exc

    def __init__(self, command, cwd, default_timeout=30, env=None):
        if not isinstance(command, (list, tuple)) or not command or any(not isinstance(x, str) or not x for x in command):
            raise ValueError('Engine command must be a nonempty argument list')
        executable = Path(command[0])
        if not executable.is_absolute():
            raise ValueError('Engine executable must have an absolute path')
        self.command = list(command)
        self.cwd = Path(cwd).resolve(strict=True)
        if not self.cwd.is_dir():
            raise ValueError('Engine working directory must be a directory')
        if not 0 < default_timeout <= 3600:
            raise ValueError('Invalid default engine timeout')
        self.default_timeout = default_timeout
        if env is not None and (not isinstance(env, dict) or any(
                not isinstance(k, str) or not k or '=' in k or '\0' in k or
                not isinstance(v, str) or '\0' in v for k, v in env.items())):
            raise ValueError('Engine environment must map valid string keys to string values')
        self.env = dict(env or {})
        self._lock = threading.RLock()
        self._process = None
        self._responses = None
        self._stderr = collections.deque(maxlen=32)
        self._reader_threads = []

    def start(self):
        with self._lock:
            if self._process is not None and self._process.poll() is None:
                return
            self._cleanup()
            responses = queue.Queue(maxsize=16)
            self._stderr.clear()
            try:
                process = subprocess.Popen(self.command, cwd=str(self.cwd), stdin=subprocess.PIPE,
                                           stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                           shell=False, bufsize=0,
                                           env={**os.environ, **self.env},
                                           creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
            except OSError as exc:
                raise EngineError('Could not launch rules engine: ' + str(exc)) from exc
            self._process, self._responses = process, responses
            threads = [threading.Thread(target=self._read_stdout, args=(process, responses), daemon=True),
                       threading.Thread(target=self._read_stderr, args=(process,), daemon=True)]
            self._reader_threads = threads
            for thread in threads:
                thread.start()

    def _read_stdout(self, process, responses):
        try:
            while True:
                line = process.stdout.readline(self.MAX_LINE + 1)
                if not line:
                    self._offer(responses, EngineError('Rules engine closed stdout'), process)
                    return
                if len(line) > self.MAX_LINE or not line.endswith(b'\n'):
                    self._offer(responses, EngineError('Engine reply exceeds the line limit or lacks a newline'), process)
                    self._kill(process)
                    return
                if not self._offer(responses, line, process):
                    return
        except (OSError, ValueError) as exc:
            self._offer(responses, EngineError('Engine output reader failed: ' + str(exc)), process)

    @staticmethod
    def _kill(process):
        if process is not None and process.poll() is None:
            try:
                process.kill()
            except OSError:
                pass

    @staticmethod
    def _offer(responses, item, process):
        try:
            responses.put_nowait(item)
            return True
        except queue.Full:
            Engine._kill(process)
            return False

    def _read_stderr(self, process):
        try:
            while True:
                line = process.stderr.readline(4096)
                if not line:
                    return
                self._stderr.append(line.decode('utf-8', errors='replace').rstrip())
        except (OSError, ValueError):
            pass

    @property
    def diagnostics(self):
        return list(self._stderr)

    @property
    def running(self):
        return self._process is not None and self._process.poll() is None

    def request(self, op, timeout=None, **fields):
        if not isinstance(op, str) or not op or 'id' in fields or 'op' in fields:
            raise ValueError('Invalid engine operation fields')
        timeout = self.default_timeout if timeout is None else timeout
        if not isinstance(timeout, (int, float)) or not 0 < timeout <= 3600:
            raise ValueError('Engine timeout must be greater than zero and at most 3600 seconds')
        request_id = uuid.uuid4().hex
        try:
            payload = (json.dumps({'id': request_id, 'op': op, **fields}, allow_nan=False, ensure_ascii=False) + '\n').encode('utf-8')
        except (ValueError, TypeError, UnicodeError) as exc:
            raise ValueError('Engine request must be valid JSON') from exc
        if len(payload) > self.MAX_LINE:
            raise ValueError('Engine request exceeds the line limit')
        deadline = time.monotonic() + timeout
        if not self._lock.acquire(timeout=timeout):
            raise EngineTimeout('Timed out waiting for the engine transport')
        timer = None
        rejected = False
        timed_out = threading.Event()
        try:
            self.start()
            process = self._process
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise EngineTimeout('Engine request timed out')
            # The timer also bounds a write if an engine stops reading stdin.
            def expire():
                timed_out.set()
                self._kill(process)
            timer = threading.Timer(remaining, expire)
            timer.daemon = True
            timer.start()
            try:
                pending = memoryview(payload)
                while pending:
                    written = process.stdin.write(pending[:65536])
                    if not written:
                        raise BrokenPipeError('Engine stdin closed')
                    pending = pending[written:]
                process.stdin.flush()
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise queue.Empty()
                reply = self._responses.get(timeout=remaining)
            except (OSError, ValueError, queue.Empty) as exc:
                if timed_out.is_set() or time.monotonic() >= deadline:
                    raise EngineTimeout('Rules engine request timed out') from exc
                raise EngineError('Rules engine transport failed: ' + str(exc)) from exc
            if isinstance(reply, EngineError):
                if timed_out.is_set():
                    raise EngineTimeout('Rules engine request timed out')
                raise reply
            try:
                result = json.loads(reply.decode('utf-8'))
            except (ValueError, UnicodeError) as exc:
                raise EngineError('Rules engine returned invalid JSON') from exc
            if not isinstance(result, dict) or result.get('id') != request_id or type(result.get('ok')) is not bool:
                raise EngineError('Rules engine returned an invalid response envelope')
            if not result['ok']:
                error = result.get('error')
                if not isinstance(error, str):
                    raise EngineError('Rules engine returned an invalid error response')
                # A valid, correlated rejection preserves transport synchronization.
                # Killing Forge here destroys the match on an ordinary stale click.
                rejected = True
                raise EngineRejected(error)
            if 'result' not in result:
                raise EngineError('Rules engine reply is missing its result')
            return result['result']
        except EngineError:
            # Never reuse a desynchronized or timed-out transport.
            if not rejected:
                self._cleanup()
            raise
        finally:
            if timer:
                timer.cancel()
            self._lock.release()

    def status(self, timeout=None):
        return self.request('status', timeout=timeout)

    def cards(self, timeout=None, **fields):
        return self.request('cards', timeout=timeout, **fields)

    def simulate(self, timeout=None, **fields):
        return self.request('simulate', timeout=timeout, **fields)

    def _cleanup(self):
        process = self._process
        self._kill(process)
        if process is not None:
            try:
                process.wait(timeout=2)
            except subprocess.TimeoutExpired:
                pass
            for pipe in (process.stdin, process.stdout, process.stderr):
                if pipe is not None:
                    pipe.close()
        for thread in self._reader_threads:
            thread.join(timeout=1)
        self._reader_threads = []
        self._process = None
        self._responses = None

    def close(self):
        # Interrupt an in-flight request before waiting for its serialization lock.
        self._kill(self._process)
        with self._lock:
            self._cleanup()

    def __enter__(self):
        self.start()
        return self

    def __exit__(self, *_):
        self.close()
