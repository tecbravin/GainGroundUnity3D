"""Render recovered ROM notes with GeneralUser GS sampled instruments at 48 kHz.
Run recover_music.py first. Dependencies live in workspace .audio-tools/python.
"""
from pathlib import Path
import sys,json,hashlib,wave
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT.parent/'.audio-tools/python'))
import numpy as np
import tinysoundfont, mido
from scipy.signal import fftconvolve, butter, sosfilt
RATE=48000
SF=ROOT.parent/'.audio-tools/GeneralUser-GS/GeneralUser-GS.sf2'
OUT=ROOT/'Assets/GainGround/Editable/Audio'
PROGRAMS={0:61,1:48,2:73,3:4,5:33,6:28,7:4}
def program(song,tr,n):
 if tr['kind']=='drums': return 0
 if song['name']=='06_Victory': return {1:56,2:60,3:48,4:56,5:42,6:10,7:10}[tr['channel']]
 if tr['channel']==5 and n['ins']==5: return 27
 return PROGRAMS[n['ins']]
def render(song):
 loop=song['name']=='01_Genesis'
 seconds=max(t['ticks'] for t in song['tracks'])*song['tick_seconds']
 frames=round(seconds*RATE); tail=RATE*3
 mix=np.zeros((frames+tail,2),np.float32)
 mid=mido.MidiFile(ticks_per_beat=480)
 conductor=mido.MidiTrack(); mid.tracks.append(conductor); conductor.append(mido.MetaMessage('set_tempo',tempo=500000))
 info=[]
 for tr in song['tracks']:
  if not tr['notes']: continue
  synth=tinysoundfont.Synth(gain=-12,samplerate=RATE); sfid=synth.sfload(str(SF))
  drum=tr['kind']=='drums'; channel=9 if drum else tr['channel']-1
  events=[]; midi_events=[]; names=set()
  for n in tr['notes']:
   start=round(n['t']*song['tick_seconds']*RATE); end=round((n['t']+n['d'])*song['tick_seconds']*RATE)
   prog=program(song,tr,n); bank=128 if drum else 0
   velocity=90 if drum else max(45,min(110,110-n['vol']))
   if tr['channel'] in (3,4,5,7): velocity=int(velocity*.77)
   if song['name']=='06_Victory' and tr['kind']=='psg': velocity=58
   pan=64 if drum or (tr['channel']==2 and prog==33) else int(64+(n['pan']-64)*.65)
   events.extend([(start,1,n['key'],velocity,prog,bank,pan),(end,0,n['key'],0,prog,bank,pan)])
   names.add(synth.sfpreset_name(sfid,bank,prog))
  events.sort(key=lambda e:(e[0],e[1]))
  audio=np.zeros_like(mix); pos=0
  mt=mido.MidiTrack(); mid.tracks.append(mt); mt.append(mido.MetaMessage('track_name',name=f"ROM channel {tr['channel']}: {', '.join(sorted(names))}"))
  midi_pos=0; current=None
  for at,on,key,vel,prog,bank,pan in events:
   if at>pos: audio[pos:at]=np.frombuffer(synth.generate_simple(at-pos),np.float32).reshape(-1,2); pos=at
   midi_at=round(at/RATE*960); delta=midi_at-midi_pos; midi_pos=midi_at
   if on:
    if current!=prog:
     synth.program_select(channel,sfid,bank,prog); current=prog
     mt.append(mido.Message('program_change',channel=channel,program=prog,time=delta)); delta=0
    synth.control_change(channel,10,pan)
    mt.append(mido.Message('control_change',channel=channel,control=10,value=pan,time=delta)); delta=0
    synth.noteon(channel,key,vel)
   else: synth.noteoff(channel,key)
   mt.append(mido.Message('note_on' if on else 'note_off',channel=channel,note=key,velocity=vel,time=delta))
  audio[pos:]=np.frombuffer(synth.generate_simple(len(audio)-pos),np.float32).reshape(-1,2)
  mix+=audio
  info.append(dict(channel=tr['channel'],instruments=sorted(names),notes=len(tr['notes'])))
  print(song['name'],info[-1],flush=True)
 # Subtle stereo room tail, high-pass/DC removal, then conservative peak normalization.
 mix=sosfilt(butter(2,35,fs=RATE,btype='highpass',output='sos'),mix,axis=0)
 rng=np.random.default_rng(4036)
 irlen=round(.85*RATE); times=np.arange(irlen)/RATE
 for ch in range(2):
  ir=rng.normal(0,1,irlen)*np.exp(-times*9)
  ir[:round(.023*RATE)]=0
  ir=sosfilt(butter(2,5200,fs=RATE,output='sos'),ir)
  ir*=.12/max(np.sqrt((ir*ir).sum()),1e-9)
  mix[:,ch]+=fftconvolve(mix[:,ch],ir)[:len(mix)]*.23
 if loop:
  # Fold the release/reverb tail into the first samples for a seamless full-song loop.
  mix[:tail]+=mix[frames:frames+tail]; mix=mix[:frames]
 else:
  mix[-RATE:]*=np.linspace(1,0,RATE)[:,None]
 peak=float(np.abs(mix).max()); mix*=10**(-1.2/20)/peak
 # 16-bit stereo master with triangular dither; Unity imports without sample-rate reduction.
 dither=(rng.random(mix.shape)-rng.random(mix.shape))/65536
 pcm=np.clip(np.rint((mix+dither)*32767),-32768,32767).astype('<i2')
 path=OUT/(song['name']+'_Remastered.wav')
 with wave.open(str(path),'wb') as w: w.setnchannels(2); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(pcm.tobytes())
 mid.save(str(ROOT/'Analysis/Music'/(song['name']+'_Arrangement.mid')))
 report=dict(name=song['name'],seconds=len(mix)/RATE,sample_rate=RATE,channels=2,loop=loop,peak_dbfs=float(20*np.log10(np.abs(mix).max())),rms_dbfs=float(20*np.log10(np.sqrt(np.mean(mix*mix)))),loop_boundary_delta=float(np.max(np.abs(mix[0]-mix[-1]))) if loop else None,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),tracks=info)
 (ROOT/'Analysis/Music'/(song['name']+'_Render.json')).write_text(json.dumps(report,indent=2))
 print(report,flush=True)
if __name__=='__main__':
 OUT.mkdir(exist_ok=True)
 for name in ('01_Genesis','06_Victory'): render(json.loads((ROOT/'Analysis/Music'/(name+'.json')).read_text()))
