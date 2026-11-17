# VoiceVox vocabulary audio

The 142 vocabulary entries have Japanese WAV clips for two voices at three speech speeds
(852 total) under `Assets/Resources/VocabularyAudio/Female` and
`Assets/Resources/VocabularyAudio/Male`. Each voice's root folder holds the 100% clips;
its `75` and `50` subfolders hold slower versions. `manifest.json` maps each stable
vocabulary ID to all six clips, the English name, Japanese text, reading, category,
and VoiceVox style.

The clips were synthesized from the `reading` field in
`Assets/Resources/VocabularyData/Library.json`. The female voice is
VOICEVOX:四国めたん (Shikoku Metan), normal style, speaker ID 2; the male voice is
VOICEVOX:玄野武宏 (Kurono Takehiro), normal style, speaker ID 11. The WAVs are
24 kHz mono PCM. VOICEVOX `speedScale` is 1, 0.75, or 0.5 while its pitch
setting stays unchanged. Neither the VoiceVox engine nor the voice models are bundled.

To regenerate, run `node Tools/generate-voicevox-vocabulary.mjs` while the
desktop VoiceVox app or engine is running at `http://127.0.0.1:50021`.
The script resumes by reusing existing valid WAVs. Delete a specific WAV before
running it again to regenerate that clip after changing its reading.
The manifest is rewritten to reflect the current library.

The main menu displays both credits when the app opens. Check the current VoiceVox software and voice
library terms for the intended distribution:

- https://voicevox.hiroshiba.jp/term/
- https://voicevox.hiroshiba.jp/qa/
- https://voicevox.hiroshiba.jp/product/shikoku_metan/
- https://voicevox.hiroshiba.jp/product/kurono_takehiro/

The generated pronunciations and accent patterns should be reviewed by a
Japanese speaker before using the audio as authoritative teaching material.
