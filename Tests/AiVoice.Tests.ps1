$ErrorActionPreference = 'Stop'
$OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$repo = Split-Path $PSScriptRoot -Parent
$worker = Get-Content (Join-Path $repo 'Server/ai-proxy/worker.js') -Raw -Encoding UTF8
$canonical = Get-Content (Join-Path $repo 'Tools/Voice/azure/voices.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$copyPath = Join-Path $repo 'Server/ai-proxy/voices.json'
if (!(Test-Path $copyPath)) { throw 'TTS voice configuration is missing' }
$copy = Get-Content $copyPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (($canonical | ConvertTo-Json -Depth 8 -Compress) -cne ($copy | ConvertTo-Json -Depth 8 -Compress)) { throw 'Voice configurations differ' }
$rules = Get-Content (Join-Path $repo 'Tools/Voice/generate.py') -Raw -Encoding UTF8
$pythonRules = [regex]::Matches($rules, '\(r"([^"]+)", "([^"]+)"\)')
$jsRules = [regex]::Matches($worker, '\[String\.raw`([^`]+)`, "([^"]+)"\]')
if ($pythonRules.Count -ne $jsRules.Count -or $jsRules.Count -eq 0) { throw 'SAY count differs' }
for ($i=0; $i -lt $pythonRules.Count; $i++) {
    if ($pythonRules[$i].Groups[1].Value -cne $jsRules[$i].Groups[1].Value -or $pythonRules[$i].Groups[2].Value -cne $jsRules[$i].Groups[2].Value) { throw "SAY differs at $i" }
}
$cs = Get-Content (Join-Path $repo 'Assets/Scripts/AiProfessorVoice.cs') -Raw -Encoding UTF8
if ($cs -notmatch 'ShimHanmoe' -or $cs -notmatch 'catch' -or $cs -notmatch 'return null' -or $cs -notmatch 'AudioClip.Create') { throw 'Missing silent subtitle fallback or WAV decoding' }
if (!(Test-Path (Join-Path $repo 'Assets/Scripts/AiProfessorVoice.cs.meta'))) { throw 'Missing meta' }
$stubs = @'
public static class AiProfessor {
 public static bool Supported = true;
 public const string ProxyEndpoint = "https://offline";
 public static readonly System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
 public static string Json(string text) { return text; }
}
namespace UnityEngine {
 public class Object { public static void Destroy(Object o) {} }
 public class AudioClip : Object {
  public static AudioClip Create(string n,int s,int c,int h,bool stream) { return new AudioClip(); }
  public bool SetData(float[] samples,int offset) { return true; }
 }
}
public static class WavChecks {
 public static void Run() {
  var stream = new System.IO.MemoryStream();
  var writer = new System.IO.BinaryWriter(stream);
  writer.Write(0x46464952u); writer.Write(44u); writer.Write(0x45564157u);
  writer.Write(0x20746d66u); writer.Write(16u); writer.Write((ushort)1); writer.Write((ushort)1);
  writer.Write(24000u); writer.Write(48000u); writer.Write((ushort)2); writer.Write((ushort)16);
  writer.Write(0x61746164u); writer.Write(8u);
  writer.Write((short)1000); writer.Write((short)-1000); writer.Write((short)1000); writer.Write((short)-1000);
  byte[] wav = stream.ToArray();
  var speech = AiProfessorVoice.Decode(wav,0);
  if(speech==null || speech.Samples.Length!=4 || Math.Abs(speech.Samples[0]-.1f)>.00001) throw new Exception("RMS normalization failed");
  var louder = AiProfessorVoice.Decode(wav,10);
  if(Math.Abs(louder.Samples[0]-.11f)>.00001) throw new Exception("Professor volume failed");
  wav[44]=255; wav[45]=127;
  foreach(float f in AiProfessorVoice.Decode(wav,30).Samples) if(Math.Abs(f)>Math.Pow(10,-1.0/20)+1e-6) throw new Exception("Peak limit failed");
  if(AiProfessorVoice.CanSpeak("ShimHanmoe") || AiProfessorVoice.CanSpeak(null)) throw new Exception("Excluded voice accepted");
  wav[22]=2;
  if(AiProfessorVoice.Decode(wav,0)!=null) throw new Exception("Stereo accepted");
  wav[22]=1; wav[40]=255;
  if(AiProfessorVoice.Decode(wav,0)!=null || AiProfessorVoice.Decode(new byte[3],0)!=null) throw new Exception("Truncated WAV accepted");
 }
}
'@
Add-Type -TypeDefinition ($cs + "`n" + $stubs) -ReferencedAssemblies System.Net.Http
[WavChecks]::Run()
Write-Host 'C# WAV parsing, normalization and excluded voice checks passed (Unity API stubbed)'
$node = Get-Command node -ErrorAction SilentlyContinue
if ($node) {
    $script = @'
const fs = require('node:fs');
const assert = require('node:assert/strict');
global.fetch = async () => { throw new Error('Unexpected network call'); };
(async () => {
const voices = JSON.parse(fs.readFileSync('Server/ai-proxy/voices.json', 'utf8').replace(/^\uFEFF/, ''));
let source = fs.readFileSync('Server/ai-proxy/worker.js', 'utf8')
  .replace(/import \{ DurableObject \} from "cloudflare:workers";/, 'class DurableObject { constructor(ctx, env) { this.ctx=ctx; this.env=env; } }')
  .replace(/import voices from "\.\/voices.json"[^;]*;/, `const voices = ${JSON.stringify(voices)};`);
const mod = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
const request = body => new Request('https://offline/tts', {method:'POST', headers:{'X-AION-Client':'dugeun-campus'}, body:JSON.stringify(body)});
for (const body of [{voice:'ShimHanmoe',text:'test'}, {voice:'unknown',text:'test'}, {voice:'LeeMyeonghee',text:'a'.repeat(301)}, {voice:'LeeMyeonghee',text:' '}]) {
 assert.equal((await mod.default.fetch(request(body), {})).status,400);
}
assert.equal((await mod.default.fetch(request({voice:'LeeMyeonghee',text:'hello'}), {LIMITER:{limit:async()=>({success:false})}})).status,429);
const fixtures = process.env.AION_SSML_FIXTURES;
if (fixtures) for (const item of JSON.parse(fixtures)) assert.equal(mod.buildSsml(item.text,item.config),item.ssml);
// Exercise the real relay with in-memory storage and a network stub only.
const storage = new Map();
storage.put = (key,value) => storage.set(key,value);
storage.transaction = async fn => fn(storage);
let calls = 0;
global.fetch = async (_url, options) => { calls++; assert.equal(options.headers['X-Microsoft-OutputFormat'],'riff-24khz-16bit-mono-pcm'); return new Response(new Uint8Array([1,2]), {status:200}); };
const relay = new mod.GeminiRelay({storage}, {AZURE_SPEECH_KEY:' fake ',AZURE_SPEECH_REGION:'koreacentral',TTS_DAILY_LIMIT:'1'});
assert.equal((await relay.fetch(request({voice:'LeeMyeonghee',text:'hello'}))).headers.get('X-AION-Volume'),'0');
assert.equal((await relay.fetch(request({voice:'LeeMyeonghee',text:'hello'}))).status,429);
assert.equal(calls,1);
const noKey = new mod.GeminiRelay({storage}, {});
assert.equal((await noKey.fetch(request({voice:'LeeMyeonghee',text:'hello'}))).status,503);
const disabled = new mod.GeminiRelay({storage}, {AZURE_SPEECH_KEY:'fake',AZURE_SPEECH_REGION:'koreacentral',TTS_DAILY_LIMIT:'0'});
assert.equal((await disabled.fetch(request({voice:'LeeMyeonghee',text:'hello'}))).status,429);
global.fetch = async () => { calls++; return new Response('private Azure diagnostic', {status:503}); };
const failed = new mod.GeminiRelay({storage}, {AZURE_SPEECH_KEY:'fake',AZURE_SPEECH_REGION:'koreacentral',TTS_DAILY_LIMIT:'10'});
const failure = await failed.fetch(request({voice:'LeeMyeonghee',text:'hello'}));
assert.equal(failure.status,503);
assert.equal((await failure.text()).includes('private Azure diagnostic'),false);
assert.equal(calls,2); // Never retry Azure failures.
const forbidden = new Request('https://offline/tts',{method:'POST',body:'{}'});
assert.equal((await mod.default.fetch(forbidden,{})).status,403);
console.log('Offline TTS route, quota and SSML checks passed');
})().catch(e=>{console.error(e.message);process.exit(1)});
'@
    $python = Get-Command python -ErrorAction SilentlyContinue
    $available = $false
    if ($python) {
        $previousPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & $python.Source --version 2>$null | Out-Null
        $available = $LASTEXITCODE -eq 0
        $ErrorActionPreference = $previousPreference
    }
    try {
        if ($available) {
            $py = @'
import importlib.util,json
s=importlib.util.spec_from_file_location('azure','Tools/Voice/generate_azure.py')
m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
v=m.load_voices()
cases=[('LeeMyeonghee','안녕, 좋아요! <a & b> "말" \'쉼\''),('ChoiYuNa','쉼, 있어요. 다음? 좋아요!'),('OhMyeongHwan','… Git Count OT LIFO예요 FIFO target k번째 10진수 2진수 1이 i++ 한Git')]
print(json.dumps([dict(text=t,config=v[k],ssml=m.build_ssml(t,v[k])) for k,t in cases],ensure_ascii=True))
'@
            $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($py))
            $env:AION_SSML_FIXTURES = (& $python.Source -c "import base64;exec(base64.b64decode('$encoded'))") -join "`n"
            if ($LASTEXITCODE -ne 0) { throw 'Python SSML fixtures failed' }
        } else { Write-Host 'Python unavailable: SSML parity skipped' }
        Push-Location $repo
        try { $script | & $node.Source; if ($LASTEXITCODE -ne 0) { throw 'Node offline TTS tests failed' } }
        finally { Pop-Location }
    } finally { Remove-Item Env:AION_SSML_FIXTURES -ErrorAction SilentlyContinue }
} else { Write-Host 'Node unavailable: runtime checks skipped' }
Write-Host 'AiVoice tests passed'
