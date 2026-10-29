"""Original modal bells, paper/ink texture and restrained low impact; no third-party samples."""
import math,wave,struct,random,json
from pathlib import Path
work=Path(__file__).resolve().parent;folder=work.parents[1]/'Assets/CinematicProof/M3/Audio';rate=44100
cues={'Awakening':(.85,[196,294],.13),'Charge':(1.48,[146.83,220,293.66],.08),'SealBreak':(.43,[1174.66,1567.98],.26),'Impact':(.72,[65.41,98,130.81],.28),'Common':(.72,[523.25,659.25],.018),'Rare':(1.05,[587.33,880,1174.66],.012),'Epic':(1.35,[392,493.88,659.25,987.77],.014),'Legendary':(1.95,[261.63,392,523.25,659.25,1046.5],.013),'Settle':(.43,[523.25,784],.015),'ResultTick':(.19,[1046.5,1567.98],.012)}
results=[]
for index,(name,(duration,freqs,noise)) in enumerate(cues.items()):
 target=folder/(name+'.wav')
 if target.exists():raise RuntimeError('Refusing overwrite '+str(target))
 rng=random.Random(3500+index);values=[];filtered=0
 for i in range(round(duration*rate)):
  t=i/rate;fade=min(1,(duration-t)/.09);attack=min(1,t/(.1 if name=='Charge' else .012));tone=0
  for j,f in enumerate(freqs):
   delay=j*.038 if name in ['Rare','Epic','Legendary'] else 0
   if t<delay:continue
   q=t-delay;env=(1-math.exp(-q*180))*math.exp(-q*(2.1+j*.35))
   tone+=(math.sin(math.tau*f*q+math.sin(math.tau*f*1.997*q)*.12)*.67+math.sin(math.tau*f*2.73*q)*.19+math.sin(math.tau*f*4.1*q)*.06)*env
  tone/=len(freqs);filtered=.94*filtered+.06*(rng.random()*2-1);grit=filtered*noise*math.exp(-t*(1.7 if name=='Charge' else 11))
  if name=='Impact':tone+=math.sin(math.tau*(48+38*math.exp(-t*18))*t)*math.exp(-t*7)*.32
  if name=='Charge':tone*=.28+.72*(t/duration)**1.2
  values.append((tone+grit)*attack*fade)
 gain=.50/max(abs(x) for x in values);left=[];right=[]
 for i,x in enumerate(values):
  other=values[max(0,i-132)] if name in ['Rare','Epic','Legendary'] else x
  left.append(round(x*gain*32767));right.append(round((x*.88+other*.12)*gain*32767))
 with wave.open(str(target),'wb') as w:w.setnchannels(2);w.setsampwidth(2);w.setframerate(rate);w.writeframes(b''.join(struct.pack('<hh',a,b) for a,b in zip(left,right)))
 all_values=[v/32768 for pair in zip(left,right) for v in pair];rms=(sum(v*v for v in all_values)/len(all_values))**.5
 results.append(dict(name=name,seconds=len(values)/rate,rate=rate,channels=2,peakDBFS=20*math.log10(max(abs(v) for v in all_values)),rmsDBFS=20*math.log10(rms),clipped=sum(abs(v)>=1 for v in all_values),license='Original mathematical synthesis, no third-party samples'))
(work/'Validation/audio-analysis.json').write_text(json.dumps(results,indent=2),encoding='utf-8');print('Generated',len(results),'original cues; all unclipped.')
