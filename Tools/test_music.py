"""Regression checks for ROM extraction, arrangements and imported music references."""
import unittest,json,hashlib,wave,sys,re
from pathlib import Path
from recover_music import ROOT,decode,SONGS
sys.path.insert(0,str(ROOT.parent/'.audio-tools/python'))
import numpy as np
class MusicTests(unittest.TestCase):
 def test_rom_provenance(self):
  rom=(ROOT/'Analysis/OriginalROM.bin').read_bytes()
  manifest=json.loads((ROOT/'Analysis/Music/ExtractionManifest.json').read_text())
  self.assertEqual(hashlib.sha256(rom).hexdigest(),manifest['rom_sha256'])
  for entry in manifest['songs']:
   b=(ROOT/'Analysis/Music'/entry['file']).read_bytes(); at=int(entry['rom_offset'],16)
   self.assertEqual(b,rom[at:at+entry['length']]); self.assertEqual(hashlib.sha256(b).hexdigest(),entry['sha256'])
 def test_opening_score_loop_alignment(self):
  off,size,name=SONGS[0]; song=decode((ROOT/'Analysis/Music'/f'{name}.smy').read_bytes(),off,name)
  active=[t for t in song['tracks'] if t['notes']]
  self.assertEqual(len(active),6)
  self.assertEqual({t['ticks'] for t in active},{35200})
  self.assertEqual({t['loop_start'] for t in active},{0})
  self.assertAlmostEqual(song['tick_seconds']*35200,159.25796155656654,places=5)
 def test_victory_is_finite(self):
  off,size,name=SONGS[5]; song=decode((ROOT/'Analysis/Music'/f'{name}.smy').read_bytes(),off,name)
  self.assertTrue(all(t['loop_start'] is None for t in song['tracks']))
  self.assertEqual(max(t['ticks'] for t in song['tracks']),872)
 def test_audio_masters(self):
  for p in (ROOT/'Assets/GainGround/Editable/Audio').glob('*.wav'):
   with wave.open(str(p)) as w:
    self.assertEqual((w.getframerate(),w.getnchannels(),w.getsampwidth()),(48000,2,2))
    samples=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').reshape(-1,2)/32768
   peak=np.max(np.abs(samples)); rms=np.sqrt(np.mean(samples*samples))
   self.assertLess(peak,.9); self.assertGreater(peak,.8); self.assertGreater(rms,.07)
   self.assertGreater(np.mean((samples[:,0]-samples[:,1])**2),.00001)
   if 'Genesis' in p.name: self.assertLess(np.max(np.abs(samples[0]-samples[-1])),.005)
   else: self.assertLess(np.max(np.abs(samples[-2400:])),.001)
 def test_stage_asset_bindings(self):
  guids={re.search(r'guid: (\w+)',p.read_text()).group(1) for p in (ROOT/'Assets/GainGround/Editable/Audio').glob('*.meta')}
  stages=list((ROOT/'Assets/GainGround/Editable/OriginalCampaign').glob('Stage_*.asset'))
  self.assertEqual(len(stages),5)
  for p in stages:
   for field in ('stageMusic','victoryMusic'):
    guid=re.search(field+r': .*guid: (\w+)',p.read_text()).group(1); self.assertIn(guid,guids)
if __name__=='__main__': unittest.main()
