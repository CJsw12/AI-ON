$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$config = Get-Content -Raw -Encoding UTF8 (Join-Path $repo 'Tools/Voice/azure/voices.json') | ConvertFrom-Json
$voices = @($config.PSObject.Properties | Where-Object { -not $_.Name.StartsWith('_') })
if ($voices.Count -ne 31) { throw "Expected 31 voices, got $($voices.Count)" }
foreach ($v in $voices) {
    if ($v.Name -eq 'ShimHanmoe') { throw 'ShimHanmoe must use CosyVoice' }
    if (-not (Test-Path (Join-Path $repo "Assets/Resources/Voice/$($v.Name)"))) { throw "Missing voice directory: $($v.Name)" }
    if ($v.Value.voice -notlike 'ko-KR-*') { throw "Non-Korean voice: $($v.Name)" }
    foreach ($range in @(@('rate',-30,30),@('pitch',-20,20),@('volume',-30,30),@('pause',0,1000))) {
        $value = $v.Value.($range[0])
        if ($null -eq $value -or $value -is [string] -or $value -is [bool] -or $value -lt $range[1] -or $value -gt $range[2]) { throw "Invalid $($range[0]): $($v.Name)" }
    }
}
$python = Get-Command python -ErrorAction SilentlyContinue
if ($python) {
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & $python.Source --version 2>$null | Out-Null
    $available = $LASTEXITCODE -eq 0
    $ErrorActionPreference = $previousPreference
} else { $available = $false }
if ($available) {
    $code = @'
import importlib.util, pathlib, sys, socket, tempfile, json, io, contextlib
from unittest.mock import patch
import xml.etree.ElementTree as ET
root = pathlib.Path(sys.argv[1])
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('azure_generator', root / 'Tools/Voice/generate_azure.py')
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
# Any accidental real network use in these checks fails immediately.
socket.socket = lambda *a, **k: (_ for _ in ()).throw(AssertionError('Network forbidden'))
voices = m.load_voices()
for bad in ('ShimHanmoe', 'Unknown', '../ShimHanmoe'):
    try:
        m.select_voices(voices, bad)
    except ValueError:
        pass
    else:
        raise AssertionError('Invalid voice accepted: ' + bad)
fixture = next((root / 'Assets/Resources/Voice').glob('*/lines.txt'))
line = next(s for s in fixture.read_text(encoding='utf-8-sig').splitlines() if '\t' in s)
filename, text = line.split('\t', 1)
assert m.name(text) == filename.removesuffix('.ogg'), 'Existing clip hash changed'
assert m.name('hello') == '5d41402abc4b'
assert m.spoken('Git i++ LIFO예요') == '깃 아이 플러스 플러스 라이포예요'
c = dict(voice='ko-KR-SunHiNeural', rate=12, pitch=-2, volume=0, pause=250)
xml = m.build_ssml('안녕, 친구! < & > " \'', c)
assert '<break time=\'250ms\'/>' in xml and '&lt;' in xml and '&amp;' in xml
tree = ET.fromstring(xml)
assert tree.attrib['{http://www.w3.org/XML/1998/namespace}lang'] == 'ko-KR'
p = tree.find('voice/prosody')
assert p.attrib == {'rate':'12%', 'pitch':'-2%', 'volume':'+0%'}
assert len(p.findall('break')) == 2
assert '<break' not in m.build_ssml('안녕, 친구!', dict(c, pause=0))
assert len(ET.fromstring(m.build_ssml('점. 쉼, 물음? 느낌! 줄임… 다음', c)).findall('.//break')) == 5
assert m.cer('가 나', '가나') == 0 and m.cer('가나다라', '가나마라') == .25
assert m.target_db(0) == -20
assert abs(m.target_db(-8) - (-20.724243453088895)) < 1e-10
assert abs(m.target_db(15) - (-18.786043192927767)) < 1e-10
assert m.retry_delay('5', 0) == 5 and m.retry_delay(None, 2) == 4
class Clock:
    now = 0
    starts = []
    def sleep(self, seconds): self.now += seconds
    def monotonic(self): return self.now
class Response:
    def __init__(self, status): self.status_code=status; self.headers={'Retry-After':'1'}; self.content=b'wav'
class Requests:
    def __init__(self, statuses): self.statuses=iter(statuses); self.calls=[]
    def post(self, *a, **k):
        self.calls.append(k); clock.starts.append(clock.now)
        return Response(next(self.statuses))
clock=Clock()
fake=Requests([429,503,500,200,200])
with patch.object(m.time, 'monotonic', clock.monotonic), patch.object(m.time, 'sleep', clock.sleep):
    client=m.AzureClient('secret-do-not-print', 'koreacentral', fake)
    assert client.synthesize(xml) == b'wav'
    assert client.synthesize(xml) == b'wav'
assert len(fake.calls)==5 and all(b-a >= 3.1-1e-8 for a,b in zip(clock.starts,clock.starts[1:])), clock.starts
assert fake.calls[0]['headers']['X-Microsoft-OutputFormat']=='riff-24khz-16bit-mono-pcm'
clock=Clock(); fake=Requests([429]*4)
with patch.object(m.time, 'monotonic', clock.monotonic), patch.object(m.time, 'sleep', clock.sleep):
    try: m.AzureClient('secret-do-not-print', 'koreacentral', fake).synthesize(xml)
    except RuntimeError as e: assert 'secret-do-not-print' not in str(e)
    else: raise AssertionError('Retries did not stop')
assert len(fake.calls)==4
with tempfile.TemporaryDirectory() as d:
    out=pathlib.Path(d)
    old=out/(m.name('old')+'.ogg'); old.write_bytes(b'previous')
    stale=out/'stale.ogg'; stale.write_bytes(b'stale'); stale.with_name('stale.ogg.meta').write_text('meta')
    class Broken:
        def synthesize(self, ssml): raise RuntimeError('failed')
    assert m.generate_voice('Test', c, ['old','new'], out, Broken(), True) == 2
    assert old.read_bytes()==b'previous'
    assert not stale.exists() and not stale.with_name('stale.ogg.meta').exists()
    manifest=(out/'lines.txt').read_text(encoding='utf-8')
    assert manifest == m.name('old')+'\told\n'+m.name('new')+'\tnew\n'
    # A successful request followed by an encoder failure also preserves old audio.
    class Good:
        def synthesize(self, ssml): return b'new-wav'
    with patch.object(m, 'save_clip', side_effect=ValueError('encoder failed')):
        assert m.generate_voice('Test', c, ['old'], out, Good(), True, out/'run.log') == 1
    assert old.read_bytes()==b'previous'
    assert 'FAILED\t'+m.name('old') in (out/'run.log').read_text(encoding='utf-8')
    # Existing clips must not reach the request path without --force.
    assert m.generate_voice('Test', c, ['old'], out, Broken()) == 0
with tempfile.TemporaryDirectory() as d:
    base=pathlib.Path(d); tools=base/'Tools'/'Voice'; (tools/'azure').mkdir(parents=True)
    (tools/'azure'/'voices.json').write_text(json.dumps({'LeeMyeonghee': c}),encoding='utf-8')
    with patch.object(m, 'ROOT', base), patch.object(m, 'VOICE_TOOLS', tools):
        with contextlib.redirect_stderr(io.StringIO()):
            assert m.main(['--voice','LeeMyeonghee','--dry-run']) == 2
            assert m.main(['--voice','ShimHanmoe','--dry-run']) == 2
        (tools/'LeeMyeonghee.lines.txt').write_text('hello\nworld\n',encoding='utf-8')
        with patch.object(m, 'environment_value', side_effect=AssertionError('Dry run read credentials')):
            with contextlib.redirect_stdout(io.StringIO()) as output:
                assert m.main(['--voice','LeeMyeonghee','--dry-run']) == 0
        assert output.getvalue().count('<speak ') == 2
        assert not (base/'Assets').exists() and not (base/'Logs').exists()
print('Azure pure functions, pacing/retries, and failure preservation passed (offline)')
'@
    $previousEncoding = $OutputEncoding
    $OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    try { $code | & $python.Source - $repo }
    finally { $OutputEncoding = $previousEncoding }
    if ($LASTEXITCODE -ne 0) { throw "Azure Python tests failed: $LASTEXITCODE" }
} else { Write-Host 'Python 인터프리터가 없어 Python 테스트 건너뜀' }
Write-Host 'AzureVoice tests passed'
