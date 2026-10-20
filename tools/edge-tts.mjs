import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { execFileSync, spawn } from 'node:child_process';
import { MsEdgeTTS, OUTPUT_FORMAT } from 'msedge-tts';
import ffmpegPath from 'ffmpeg-static';

function parseArgs(argv) {
  const args = { voice: 'en-US-AvaMultilingualNeural', rate: 8, out: '', text: '', engine: 'auto' };
  for (let i = 0; i < argv.length; i++) {
    const key = argv[i];
    const value = argv[i + 1];
    if (key === '--voice') { args.voice = value; i++; }
    else if (key === '--rate') { args.rate = Number(value); i++; }
    else if (key === '--out') { args.out = value; i++; }
    else if (key === '--text') { args.text = value; i++; }
    else if (key === '--engine') { args.engine = value; i++; }
  }
  return args;
}

function readStdin() {
  return new Promise((resolve) => {
    let data = '';
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (chunk) => { data += chunk; });
    process.stdin.on('end', () => resolve(data));
    process.stdin.on('error', () => resolve(data));
  });
}

function escapeXml(text) {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

function edgeRate(percent) {
  const n = Math.max(-50, Math.min(50, Number(percent) || 0));
  return (n >= 0 ? '+' : '') + n + '%';
}

async function synthesizeEdge(text, voice, rate, mp3Path) {
  const tts = new MsEdgeTTS({ connectionTimeoutMs: 15000, synthesisTimeoutMs: 22000 });
  try {
    await tts.setMetadata(voice, OUTPUT_FORMAT.AUDIO_24KHZ_48KBITRATE_MONO_MP3);
    const { audioStream } = tts.toStream(escapeXml(text), {
      rate: edgeRate(rate),
      pitch: '+0Hz',
    });

    const chunks = [];
    await new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('Edge TTS timed out')), 25000);
      audioStream.on('data', (chunk) => chunks.push(chunk));
      audioStream.once('end', () => { clearTimeout(timer); resolve(); });
      audioStream.once('error', (error) => { clearTimeout(timer); reject(error); });
    });

    const buffer = Buffer.concat(chunks);
    if (!buffer.length) throw new Error('Edge TTS returned no audio');
    fs.writeFileSync(mp3Path, buffer);
    return buffer.length;
  } finally {
    try { tts.close(); } catch { /* ignore */ }
  }
}

function convertToWav(mp3Path, wavPath) {
  execFileSync(ffmpegPath, [
    '-y',
    '-i', mp3Path,
    '-ar', '24000',
    '-ac', '1',
    '-sample_fmt', 's16',
    wavPath,
  ], { stdio: 'ignore', windowsHide: true });
}

function sapiVoice(voice) {
  const value = String(voice || '');
  if (value.toLowerCase().includes('andrew') || value.toLowerCase().includes('david')) {
    return 'Microsoft David Desktop';
  }
  if (value.toLowerCase().includes('zira')) {
    return 'Microsoft Zira Desktop';
  }
  return 'Microsoft Zira Desktop';
}

function synthesizeSapi(text, voice, rate, wavPath) {
  const base = path.join(os.tmpdir(), 'ultron-sapi-' + Date.now() + '-' + Math.random().toString(16).slice(2));
  const txtPath = base + '.txt';
  fs.writeFileSync(txtPath, text, 'utf8');

  const sapiRate = Math.max(-10, Math.min(10, Math.round((Number(rate) || 0) / 10)));
  const script = [
    'Add-Type -AssemblyName System.Speech',
    '$s = New-Object System.Speech.Synthesis.SpeechSynthesizer',
    'if ($env:ULTRON_SAPI_VOICE) { try { $s.SelectVoice($env:ULTRON_SAPI_VOICE) } catch {} }',
    '$r = 0; [void][int]::TryParse($env:ULTRON_SAPI_RATE, [ref]$r); $s.Rate = $r',
    '$s.SetOutputToWaveFile($env:ULTRON_WAV)',
    '$s.Speak([System.IO.File]::ReadAllText($env:ULTRON_TXT, [System.Text.Encoding]::UTF8))',
    '$s.Dispose()',
  ].join('; ');

  return new Promise((resolve, reject) => {
    const child = spawn('powershell.exe', [
      '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-Command', script,
    ], {
      env: {
        ...process.env,
        ULTRON_TXT: txtPath,
        ULTRON_WAV: wavPath,
        ULTRON_SAPI_VOICE: sapiVoice(voice),
        ULTRON_SAPI_RATE: String(sapiRate),
      },
      windowsHide: true,
    });

    let stderr = '';
    child.stderr.on('data', (data) => { stderr += data.toString(); });
    child.on('error', (error) => {
      cleanup();
      reject(error);
    });
    child.on('close', (code) => {
      cleanup();
      try {
        if (code !== 0) throw new Error('Windows SAPI failed (' + code + '): ' + stderr.slice(-300));
        if (!fs.existsSync(wavPath) || fs.statSync(wavPath).size < 1000) {
          throw new Error('Windows SAPI returned no audio');
        }
        resolve();
      } catch (error) {
        reject(error);
      }
    });

    function cleanup() {
      try { fs.unlinkSync(txtPath); } catch { /* ignore */ }
    }
  });
}

async function main() {
  const started = Date.now();
  const args = parseArgs(process.argv.slice(2));
  let text = args.text;
  if (!text) {
    text = await readStdin();
  }
  text = String(text || '').trim();
  if (!text) throw new Error('No text provided');
  if (!args.out) throw new Error('No output path provided');

  const outPath = path.resolve(args.out);
  fs.mkdirSync(path.dirname(outPath), { recursive: true });

  const wantsSapi = args.engine === 'sapi' || String(args.voice || '').toLowerCase().startsWith('sapi:');
  let edgeError = null;

  if (!wantsSapi) {
    const mp3Path = outPath + '.mp3';
    try {
      await synthesizeEdge(text, args.voice, args.rate, mp3Path);
      convertToWav(mp3Path, outPath);
      try { fs.unlinkSync(mp3Path); } catch { /* ignore */ }
      if (!fs.existsSync(outPath) || fs.statSync(outPath).size < 1000) {
        throw new Error('Converted WAV is empty');
      }
      console.log(JSON.stringify({ ok: true, engine: 'edge', wav: outPath, ms: Date.now() - started }));
      return;
    } catch (error) {
      edgeError = error;
      try { fs.unlinkSync(mp3Path); } catch { /* ignore */ }
    }
  }

  try {
    await synthesizeSapi(text, args.voice, args.rate, outPath);
    console.log(JSON.stringify({ ok: true, engine: 'sapi', wav: outPath, ms: Date.now() - started }));
  } catch (error) {
    const detail = edgeError ? 'Edge: ' + edgeError.message + ' | ' : '';
    console.log(JSON.stringify({
      ok: false,
      engine: 'none',
      error: detail + error.message,
      ms: Date.now() - started,
    }));
    process.exitCode = 1;
  }
}

main().catch((error) => {
  console.log(JSON.stringify({ ok: false, engine: 'none', error: String(error && error.message ? error.message : error) }));
  process.exitCode = 1;
});
