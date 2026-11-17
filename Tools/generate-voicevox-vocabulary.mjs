import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const source = path.join(root, 'Assets/Resources/VocabularyData/Library.json');
const output = path.join(root, 'Assets/Resources/VocabularyAudio');
const endpoint = globalThis.process?.env?.VOICEVOX_URL ?? 'http://127.0.0.1:50021';
const voices = [
  { key: 'female', folder: 'Female', name: '四国めたん', style: 'ノーマル', speakerId: 2, credit: 'VOICEVOX:四国めたん' },
  { key: 'male', folder: 'Male', name: '玄野武宏', style: 'ノーマル', speakerId: 11, credit: 'VOICEVOX:玄野武宏' },
];
const speeds = [
  { percent: 100, scale: 1 },
  { percent: 75, scale: 0.75 },
  { percent: 50, scale: 0.5 },
];

async function request(route, options) {
  const response = await fetch(endpoint + route, options);
  if (!response.ok) throw new Error(`${route}: HTTP ${response.status}: ${await response.text()}`);
  return response;
}

const library = JSON.parse(await fs.readFile(source, 'utf8'));
const version = await (await request('/version')).json();
const available = await (await request('/speakers')).json();
for (const voice of voices) {
  const speaker = available.find(s => s.name === voice.name);
  if (!speaker?.styles.some(s => s.id === voice.speakerId && s.name === voice.style))
    throw new Error(`VoiceVox style unavailable: ${voice.name} ${voice.style} (${voice.speakerId})`);
  for (const speed of speeds)
    await fs.mkdir(path.join(output, voice.folder, speed.percent === 100 ? '' : String(speed.percent)), { recursive: true });
}

const entries = [];
for (const [index, item] of library.entries.entries()) {
  if (!/^[a-f0-9]{32}$/.test(item.id) || !item.reading || !item.japanese || /\?/.test(item.reading + item.japanese))
    throw new Error(`Incomplete vocabulary entry: ${item.name}`);
  const audio = {};
  for (const voice of voices) {
    let query;
    for (const speed of speeds) {
      const relative = `${voice.folder}/${speed.percent === 100 ? '' : speed.percent + '/'}${item.id}.wav`;
      const destination = path.join(output, relative);
      // Reuse existing clips on a resumed run. Delete a clip to regenerate it.
      let existing = null;
      try { existing = await fs.readFile(destination); } catch (error) { if (error.code !== 'ENOENT') throw error; }
      if (!existing || existing.toString('ascii', 0, 4) !== 'RIFF' || existing.toString('ascii', 8, 12) !== 'WAVE') {
        query ??= await (await request(`/audio_query?text=${encodeURIComponent(item.reading)}&speaker=${voice.speakerId}`, { method: 'POST' })).json();
        const wav = Buffer.from(await (await request(`/synthesis?speaker=${voice.speakerId}`, {
          method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ ...query, speedScale: speed.scale }),
        })).arrayBuffer());
        if (wav.toString('ascii', 0, 4) !== 'RIFF' || wav.toString('ascii', 8, 12) !== 'WAVE')
          throw new Error(`VoiceVox returned invalid WAV for ${item.name} / ${voice.key} / ${speed.percent}%`);
        await fs.writeFile(destination, wav);
      }
      audio[speed.percent === 100 ? voice.key : `${voice.key}${speed.percent}`] = relative;
    }
  }
  entries.push({ id: item.id, name: item.name, japanese: item.japanese, reading: item.reading, category: item.category, audio });
  if ((index + 1) % 20 === 0 || index + 1 === library.entries.length)
    console.log(`${index + 1}/${library.entries.length} vocabulary items`);
}

await fs.writeFile(path.join(output, 'manifest.json'), JSON.stringify({
  source: 'Assets/Resources/VocabularyData/Library.json', engineVersion: version, voices, speeds, entries,
}, null, 2) + '\n');
console.log(`Saved ${entries.length * voices.length * speeds.length} WAV clips to ${output}`);
