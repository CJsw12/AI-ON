"""Azure Korean professor clips. Dry runs need only the Python standard library.

uv run --with requests --with soundfile --with numpy --with pyloudnorm python \
    Tools/Voice/generate_azure.py --voice LeeMyeonghee --dry-run
Add --with faster-whisper to uv and --asr to this script for optional ASR QA.
"""
import argparse
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime
import importlib.util
import io
import json
import math
import os
from pathlib import Path
import re
import sys
import tempfile
import time
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
VOICE_TOOLS = ROOT / 'Tools' / 'Voice'

# Reuse the canonical SAY/spoken/name without loading CosyVoice dependencies.
_spec = importlib.util.spec_from_file_location('_aion_voice_rules', VOICE_TOOLS / 'generate.py')
_rules = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_rules)
name, spoken = _rules.name, _rules.spoken


def load_voices():
    data = json.loads((VOICE_TOOLS / 'azure' / 'voices.json').read_text(encoding='utf-8-sig'))
    voices = {k: v for k, v in data.items() if not k.startswith('_')}
    if 'ShimHanmoe' in voices:
        raise ValueError('ShimHanmoe는 기존 CosyVoice 경로만 사용합니다.')
    for voice_id, config in voices.items():
        if not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*', voice_id):
            raise ValueError('잘못된 Voice Id입니다.')
        if not re.fullmatch(r'ko-KR-[A-Za-z]+Neural', config['voice']):
            raise ValueError('한국어 신경망 음성을 지정해야 합니다.')
        for field, low, high in [('rate', -30, 30), ('pitch', -20, 20),
                                 ('volume', -30, 30), ('pause', 0, 1000)]:
            value = config[field]
            if type(value) not in (int, float) or not math.isfinite(value) or not low <= value <= high:
                raise ValueError(f'{voice_id}: {field} 범위가 잘못되었습니다.')
    return voices


def select_voices(voices, voice_id=None):
    if voice_id == 'ShimHanmoe':
        raise ValueError('ShimHanmoe는 기존 CosyVoice 경로만 사용합니다.')
    if voice_id is not None and voice_id not in voices:
        raise ValueError('설정에 없는 Voice Id입니다.')
    return {voice_id: voices[voice_id]} if voice_id else voices


def build_ssml(text, config):
    body = escape(spoken(text), {'"': '&quot;', "'": '&apos;'})
    pause = config['pause']
    if pause > 0:
        body = re.sub(r'([,.?!…])(?=\s)', rf"\1<break time='{pause:g}ms'/>", body)
    voice = escape(config['voice'], {"'": '&apos;', '"': '&quot;'})
    return ("<speak version='1.0' xml:lang='ko-KR'>"
            f"<voice name='{voice}'><prosody rate='{config['rate']:g}%' "
            f"pitch='{config['pitch']:g}%' volume='{config['volume']:+g}%'>"
            f'{body}</prosody></voice></speak>')


def environment_value(key):
    value = os.environ.get(key, '').strip()
    if not value and sys.platform == 'win32':
        import winreg
        try:
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, 'Environment') as registry:
                value = str(winreg.QueryValueEx(registry, key)[0]).strip()
        except OSError:
            pass
    return value


def retry_delay(header, attempt):
    if header:
        try:
            value = float(header)
            if math.isfinite(value):
                return max(0, value)
        except ValueError:
            try:
                when = parsedate_to_datetime(header)
                if when.tzinfo is None:
                    when = when.replace(tzinfo=timezone.utc)
                return max(0, (when - datetime.now(timezone.utc)).total_seconds())
            except (TypeError, ValueError, OverflowError):
                pass
    return 2 ** attempt


class AzureClient:
    def __init__(self, key, region, requests):
        if not re.fullmatch(r'[a-z0-9-]+', region):
            raise ValueError('AZURE_SPEECH_REGION 형식이 잘못되었습니다.')
        self.key, self.region, self.requests = key, region, requests
        self.last_start = None

    def synthesize(self, ssml):
        for attempt in range(4):  # Initial request plus at most three retries.
            if self.last_start is not None:
                time.sleep(max(0, 3.1 - (time.monotonic() - self.last_start)))
            self.last_start = time.monotonic()
            try:
                response = self.requests.post(
                    f'https://{self.region}.tts.speech.microsoft.com/cognitiveservices/v1',
                    headers={'Ocp-Apim-Subscription-Key': self.key,
                             'X-Microsoft-OutputFormat': 'riff-24khz-16bit-mono-pcm',
                             'User-Agent': 'aion-voice-gen',
                             'Content-Type': 'application/ssml+xml; charset=utf-8'},
                    data=ssml.encode('utf-8'), timeout=(10, 90), allow_redirects=False)
            except Exception:
                # Never propagate SDK/HTTP errors: they can contain request headers.
                raise RuntimeError('Azure 전송 실패(상세 요청 정보 생략).') from None
            status = response.status_code
            if status == 200:
                return response.content
            if (status == 429 or 500 <= status <= 599) and attempt < 3:
                time.sleep(retry_delay(response.headers.get('Retry-After'), attempt))
                continue
            raise RuntimeError(f'Azure HTTP {status}(응답 본문 생략).')


def target_db(volume):
    """Preserve the assigned linear volume offset after loudness normalization."""
    return -20 + 20 * math.log10(1 + volume / 100)


def normalize_wav(wav, config):
    import numpy as np
    import soundfile as sf
    import pyloudnorm as pyln
    audio, rate = sf.read(io.BytesIO(wav), dtype='float64', always_2d=True)
    audio = audio.mean(axis=1)
    if rate != 24000 or not len(audio) or not np.isfinite(audio).all():
        raise ValueError('24kHz PCM 응답이 아니거나 오디오가 비어 있습니다.')
    rms = float(np.sqrt(np.mean(audio ** 2)))
    if rms > 0:
        loudness = float('nan')
        if len(audio) >= int(.4 * rate):
            try:
                loudness = pyln.Meter(rate).integrated_loudness(audio)
            except ValueError:
                pass
        # Short clips use the same volume-adjusted target in dBFS RMS, not LUFS.
        level = loudness if math.isfinite(loudness) else 20 * math.log10(rms)
        audio *= 10 ** ((target_db(config['volume']) - level) / 20)
    peak = float(np.max(np.abs(audio)))
    ceiling = 10 ** (-1 / 20)
    if peak > ceiling:
        audio *= ceiling / peak
    return audio


def atomic_text(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', newline='\n',
                                         dir=path.parent, delete=False) as f:
            temporary = Path(f.name)
            f.write(content)
        os.replace(temporary, path)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def save_clip(path, wav, config):
    import soundfile as sf
    audio = normalize_wav(wav, config)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(dir=path.parent, suffix='.tmp', delete=False) as f:
            temporary = Path(f.name)
        sf.write(str(temporary), audio, 24000, format='OGG', subtype='VORBIS')
        os.replace(temporary, path)  # Preserve existing audio until encoding succeeds.
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def generate_voice(voice_id, config, texts, out, client, force=False, log_path=None):
    out.mkdir(parents=True, exist_ok=True)
    generated = skipped = failed = 0
    failed_names = []
    started = time.monotonic()
    keep = {name(t) + '.ogg' for t in texts}
    remaining = sum(force or not (out / (name(t) + '.ogg')).exists() for t in texts)
    for index, text in enumerate(texts, 1):
        path = out / (name(text) + '.ogg')
        if path.exists() and not force:
            skipped += 1
        else:
            remaining -= 1
            try:
                save_clip(path, client.synthesize(build_ssml(text, config)), config)
                generated += 1
            except Exception:
                failed += 1
                failed_names.append(name(text))
                # No exception text, headers, response bodies, or credentials in logs.
                print(f'{voice_id}: 실패 {name(text)} (기존 파일 보존)', flush=True)
        elapsed = time.monotonic() - started
        average = max(3.1, elapsed / max(1, generated + failed))
        print(f'{voice_id} [{index}/{len(texts)}] 생성 {generated} / 건너뜀 {skipped} / '
              f'실패 {failed} / 예상 남은 시간 {remaining * average:.0f}초', flush=True)
    # All source lines stay in the manifest, including failures without an audio file.
    atomic_text(out / 'lines.txt', ''.join(f'{name(t)}\t{t}\n' for t in texts))
    for clip in out.glob('*.ogg'):
        if clip.name not in keep:
            clip.unlink()
            clip.with_name(clip.name + '.meta').unlink(missing_ok=True)
    summary = f'{voice_id}: 생성 {generated} / 건너뜀 {skipped} / 실패 {failed}'
    print(summary, flush=True)
    if log_path is not None:
        atomic_text(log_path, summary + '\n' + ''.join(f'FAILED\t{clip}\n' for clip in failed_names))
    return failed


def cer(expected, actual):
    expected, actual = re.sub(r'\s+', '', expected), re.sub(r'\s+', '', actual)
    row = list(range(len(actual) + 1))
    for i, a in enumerate(expected, 1):
        new = [i]
        for j, b in enumerate(actual, 1):
            new.append(min(new[-1] + 1, row[j] + 1, row[j-1] + (a != b)))
        row = new
    return row[-1] / max(1, len(expected))


def asr_report(model, voice_id, texts, out):
    rows = ['file\texpected\tactual\tcer\tflag\n']
    errors = 0
    for text in texts:
        path = out / (name(text) + '.ogg')
        if not path.exists():
            rows.append(f'{name(text)}\t{spoken(text)}\t\t\tMISSING\n')
            errors += 1
            continue
        try:
            segments, _ = model.transcribe(str(path), language='ko')
            actual = ' '.join(s.text for s in segments).replace('\t', ' ').replace('\n', ' ')
            expected = spoken(text)
            error = cer(expected, actual)
            flag = 'REVIEW' if error > .25 else ''
            rows.append(f'{name(text)}\t{expected}\t{actual}\t{error:.4f}\t{flag}\n')
            if flag:
                print(f'{voice_id}: ASR 확인 필요 {name(text)} CER={error:.3f}', flush=True)
        except Exception:
            errors += 1
            rows.append(f'{name(text)}\t{spoken(text)}\t\t\tERROR\n')
    atomic_text(ROOT / 'Logs' / 'Voice' / f'azure-asr-{voice_id}.tsv', ''.join(rows))
    return errors


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    target = parser.add_mutually_exclusive_group(required=True)
    target.add_argument('--voice')
    target.add_argument('--all', action='store_true')
    parser.add_argument('--force', action='store_true')
    parser.add_argument('--dry-run', action='store_true')
    parser.add_argument('--asr', action='store_true')
    args = parser.parse_args(argv)
    try:
        voices = select_voices(load_voices(), args.voice)
    except (ValueError, KeyError, OSError):
        print('음성 설정 오류: ShimHanmoe는 제외하며 voices.json의 Id만 사용합니다.', file=sys.stderr)
        return 2
    sources = {}
    for voice_id in voices:
        source = VOICE_TOOLS / f'{voice_id}.lines.txt'
        if not source.is_file():
            print(f'대사 원본이 없습니다. powershell -NoProfile -ExecutionPolicy Bypass '
                  f'-File Tools/Voice/export_lines.ps1 -Voice {voice_id}', file=sys.stderr)
            return 2
        texts = [line.strip() for line in source.read_text(encoding='utf-8-sig').splitlines() if line.strip()]
        sources[voice_id] = list(dict.fromkeys(texts))
    if args.dry_run:
        for voice_id, config in voices.items():
            texts = sources[voice_id]
            out = ROOT / 'Assets' / 'Resources' / 'Voice' / voice_id
            todo = sum(args.force or not (out / (name(t) + '.ogg')).exists() for t in texts)
            print(f'{voice_id}: 대상 {len(texts)} / 새로 만들 수 {todo} / 최소 예상 {todo * 3.1:.0f}초')
            for text in texts[:3]:
                print(build_ssml(text, config))
        return 0
    key, region = environment_value('AZURE_SPEECH_KEY'), environment_value('AZURE_SPEECH_REGION')
    if not key or not region:
        print('AZURE_SPEECH_KEY와 AZURE_SPEECH_REGION 환경 변수가 필요합니다.', file=sys.stderr)
        return 2
    try:
        import requests
        import numpy, soundfile, pyloudnorm  # Check dependencies before changing any outputs.
        client = AzureClient(key, region, requests)
        model = None
        if args.asr:
            from faster_whisper import WhisperModel
            model = WhisperModel('small', device='cpu', compute_type='int8')
    except Exception:
        print('의존성/환경 초기화 실패. uv의 --with 옵션과 지역 설정을 확인하세요.', file=sys.stderr)
        return 2
    failures = 0
    for voice_id, config in voices.items():
        out = ROOT / 'Assets' / 'Resources' / 'Voice' / voice_id
        failed = generate_voice(voice_id, config, sources[voice_id], out, client, args.force,
                                ROOT / 'Logs' / 'Voice' / f'azure-{voice_id}.log')
        failures += failed
        if model is not None:
            failures += asr_report(model, voice_id, sources[voice_id], out)
    return 1 if failures else 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except Exception:
        # Top-level I/O/third-party failures must not leak any request/credential text.
        print('생성 중 오류가 발생했습니다(상세 정보 생략). 기존 오디오를 확인하세요.', file=sys.stderr)
        sys.exit(1)
