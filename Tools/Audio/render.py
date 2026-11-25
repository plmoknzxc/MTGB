"""Prepare MTGB's menu music, battle scores and short interface/table sounds.

Run with Python, numpy and ffmpeg. Finished menu cues ship with the project.
Use menu_scores.py to prepare an individual licensed menu loop.
"""
from pathlib import Path
import subprocess
import tempfile
import wave

import numpy as np

if __package__:
    from .menu_scores import moon_menu, tide_menu
else:
    from menu_scores import moon_menu, tide_menu


RATE = 44100
OUT = Path(__file__).resolve().parents[2] / "Assets/Resources/GameAudio"
TIDELIGHTS = Path(__file__).resolve().parents[2] / "Themes/Tidelights/audio"
TAU = 2 * np.pi


def clock(seconds):
    return np.arange(round(seconds * RATE)) / RATE


def voice(key, seconds, instrument):
    t = clock(seconds)
    freq = 440 * 2 ** ((key - 69) / 12)
    phase = TAU * freq * t
    if instrument == "felt":
        # A pair of slightly stretched strings with a fast-decaying hammer attack.
        signal = sum((np.sin(phase * h * (1 + .00012*h*h)) +
                      .65*np.sin(phase * h * 1.0013)) * weight *
                     np.exp(-t * decay) for h, weight, decay in
                     [(1, .55, 1.25), (2, .23, 1.95), (3, .12, 3.1), (5, .045, 6.8)])
        attack, release = .008, .22
    elif instrument == "bow":
        # Two desks, with independent vibrato, slow bow changes and no hard retrigger.
        signal = np.zeros_like(t)
        for ratio, rate, offset in [(0.9988, 4.27, .2), (1.0014, 4.65, 2.2)]:
            p = phase * ratio + .24 * np.sin(TAU*rate*t + offset)
            signal += sum(w*np.sin(h*p) for h,w in [(1,.39),(2,.18),(3,.13),(4,.045),(6,.035)])
        signal *= .8 + .16*np.sin(TAU*.64*t + key)
        attack, release = .34, .42
    elif instrument == "reed":
        p = phase + .19*np.sin(TAU*4.6*t)*np.minimum(t/.42,1)
        signal = (.78*np.sin(p)+.13*np.sin(2*p+.3)+.09*np.sin(3*p))*(.93+.07*np.sin(3.8*t))
        attack, release = .11, .20
    else:
        signal = np.sin(phase+.6*np.exp(-5*t)*np.sin(phase*2.01)) * np.exp(-2.3*t)
        attack, release = .012, .16
    env = np.minimum(t/attack,1)*np.minimum((seconds-t)/release,1)
    return signal * np.maximum(env,0)


def wav(path, samples):
    data=np.round(np.clip(samples,-1,1)*32767).astype("<i2")
    with wave.open(str(path),"wb") as output:
        output.setparams((1 if data.ndim==1 else data.shape[1],2,RATE,0,"NONE","not compressed"))
        output.writeframes(data.tobytes())


def save_loop(mix, path, peak):
    # Circular stereo room reflections retain the previous phrase at the seam.
    wet=mix.copy()
    for delay,gain in [(.039,.12),(.073,.105),(.133,.085),(.209,.07),(.317,.055),(.453,.045),(.59,.03),(.739,.019)]:
        wet+=np.roll(mix[:,::-1],round(delay*RATE),axis=0)*gain
    wet *= peak/np.max(np.abs(wet))
    path.parent.mkdir(exist_ok=True,parents=True)
    with tempfile.TemporaryDirectory() as tmp:
        master=Path(tmp)/"master.wav";wav(master,wet)
        subprocess.run(["ffmpeg","-hide_banner","-loglevel","error","-y","-i",str(master),
                        "-c:a","libvorbis","-q:a","6",str(path)],check=True)


class Ensemble:
    """Beat-based stereo bus. Instruments that ring past the last bar wrap to the first."""
    def __init__(self, bars, meter, tempo, seed):
        self.beat = 60 / tempo
        self.mix = np.zeros((round(bars * meter * self.beat * RATE), 2), np.float32)
        self.rng = np.random.default_rng(seed)

    def play(self, at, sound, gain, pan):
        start = round(at * self.beat * RATE) % len(self.mix)
        stereo = sound[:, None] * gain * np.sqrt([(1-pan)/2, (1+pan)/2])
        first = min(len(sound), len(self.mix)-start)
        self.mix[start:start+first] += stereo[:first]
        self.mix[:len(sound)-first] += stereo[first:]

    def pluck(self, at, key, gain, pan, duration=2.1, bend=0):
        t = clock(duration)
        f = 440 * 2**((key-69)/12)
        # Pluck position carves notches in the spectrum. Bending behind the
        # bridge lifts a held string after the attack, then releases the tension.
        lift = np.exp(-((t-.43)/.22)**2) * bend
        phase = TAU * np.cumsum(f * 2**((lift + .12*np.exp(-24*t))/12)) / RATE
        pick = self.rng.uniform(.14,.23)
        y = np.zeros_like(t)
        for h in range(1,15):
            strength = np.sin(np.pi*h*pick)/(h**1.4)
            body = .7 + .5*np.exp(-((h*f-780)/430)**2)
            y += strength*body*np.sin(phase*h*(1+.000065*h*h))*np.exp(-t*(1.45+.58*h))
        y += .07*self.rng.standard_normal(len(t))*np.exp(-t*170)
        env = np.minimum(t/.003,1)*np.minimum((duration-t)/.065,1)
        self.play(at,y*env,gain,pan)

    def flute(self, at, key, beats, level):
        seconds = beats*self.beat
        t = clock(seconds)
        f = 440*2**((key-69)/12)
        vibrato = np.minimum(t/.58,1)**2 * .075*np.sin(TAU*self.rng.uniform(4.1,4.8)*t)
        phase = TAU*np.cumsum(f*2**((vibrato-.32*np.exp(-13*t))/12))/RATE
        env = np.minimum(t/.12,1)*np.minimum((seconds-t)/.19,1)
        breath = self.rng.standard_normal(len(t))
        air = np.convolve(breath,np.ones(8)/8,'same')-np.convolve(breath,np.ones(30)/30,'same')
        y = (.77*np.sin(phase)+.18*np.sin(2*phase)+.09*np.sin(3*phase+.35))*env
        y += air*(.17+.36*np.exp(-10*t))
        self.play(at, y*env*(.93+.07*np.sin(3.2*t)),level,.19)

    def skin(self, at, strength, rim=False):
        t = clock(.49 if rim else .88)
        freq = self.rng.uniform(177,188) if rim else self.rng.uniform(68,73)
        phase = TAU*freq*(t+.006*(1-np.exp(-27*t)))
        y = sum(w*np.sin(phase*r)*np.exp(-t*d) for r,w,d in
                [(1,.65,8),(1.52,.31,12),(2.04,.17,16),(2.41,.08,21)])
        stick = np.convolve(self.rng.normal(size=len(t)),np.ones(5)/5,'same')
        y += stick*np.exp(-t*65)*(.55 if rim else .22)
        self.play(at,y*np.minimum(t/.005,1)*np.minimum((t[-1]-t)/.055,1),
                  strength,.32 if rim else -.07)

    def bowed(self, at, key, beats, gain, pan):
        self.play(at,voice(key,beats*self.beat,"bow"),gain,pan)


def tide_battle():
    # Low, dry two-stroke drum replies in four. The B section thins to
    # pizzicato and the last eight bars bring the opening figure back in variation.
    ensemble = Ensemble(24,4,94,2312)
    roots = [38,38,34,36,38,41,43,33, 43,41,38,33,34,36,38,33, 38,34,36,38,43,41,33,38]
    phrases = [
        [(0,62,.7),(1,65,.45),(1.75,69,1.3)],[(.5,67,.7),(2,65,1.15)],
        [(0,62,.8),(1.5,65,.6),(2.5,70,1.1)],[(.75,67,1),(2.4,64,.75)],
        [(0,65,.55),(1,69,.55),(2,74,1.4)],[],[(.5,70,.6),(1.5,69,1.15)],[(0,64,1.8)],
        [],[(1,65,1.55)],[],[(0,64,1.35),(2.5,61,.75)],
        [(0,62,1.3),(2.25,65,.85)],[],[(.5,69,1.7)],[(2,64,1.25)],
        [(0,62,.7),(1,65,.45),(1.75,69,1.55)],[(.45,70,.7),(1.55,69,1.25)],
        [(0,67,.6),(1.25,64,1.15)],[(1.5,65,1.6)],[(0,67,.55),(1,70,.7),(2.3,69,1)],
        [(0,65,1.85)],[(.5,64,.75),(1.75,61,1.35)],[(0,62,2.15)]]
    for bar,(root,phrase) in enumerate(zip(roots,phrases)):
        downbeat = 4*bar
        sparse = 8 <= bar < 16
        for at,interval,gain in ([(0,0,.46),(1.5,7,.25),(2.75,12,.32)] if not sparse else [(0,0,.39),(2,7,.24)]):
            ensemble.pluck(downbeat+at+ensemble.rng.uniform(.002,.021),root+interval,gain,-.25,1.05)
        ensemble.skin(downbeat,.40 if not sparse else .25)
        if not sparse:
            ensemble.skin(downbeat+2.52,.22)
            if bar % 2 == 0:
                ensemble.skin(downbeat+3.28,.14,True)
        if bar%4==3:
            ensemble.skin(downbeat+3.52,.17,True)
        ensemble.bowed(downbeat+.10,root+12,3.65,.12,-.13)
        for at,key,length in phrase:
            ensemble.pluck(downbeat+at+.026,key,.37,.30,1.6,bend=.18 if length>1 else 0)
        if bar in (5,10,13,19,21):
            ensemble.flute(downbeat+.3, {5:72,10:69,13:67,19:69,21:72}[bar],2.4,.16)
    save_loop(ensemble.mix,TIDELIGHTS/"Tidewatch.ogg",.77)


def table_actions():
    """Cut the physical gesture and contact separately for the battlefield animation."""
    masters = Path(__file__).resolve().parent / "Masters" / "Kenney"

    def take(name, start, end, lowpass, speed=1):
        filters = (f"atrim=start={start}:end={end},aresample={RATE},"
                   f"asetrate={round(RATE * speed)},aresample={RATE},"
                   f"highpass=f=55,lowpass=f={lowpass}")
        raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(masters / (name + ".ogg")),
                                       "-af", filters, "-ac", "1", "-f", "f32le", "-"])
        samples = np.frombuffer(raw, dtype="<f4").copy()
        samples /= np.max(np.abs(samples))
        samples[:88] *= np.linspace(0, 1, 88)
        samples[-661:] *= np.linspace(1, 0, 661)
        return samples

    def cue(name, peak, layers):
        offsets = [(round(delay * RATE), gain, sound) for delay, gain, sound in layers]
        mix = np.zeros(max(offset + len(sound) for offset, _, sound in offsets))
        for offset, gain, sound in offsets:
            mix[offset:offset + len(sound)] += gain * sound
        wav(OUT / (name + ".wav"), mix * (peak / np.max(np.abs(mix))))

    cue("Draw", .45, [(0, 1, take("card-slide-6", .019, .277, 6500))])
    cue("Land", .56, [(0, 1, take("card-place-1", .080, .285, 5400))])
    cue("Attack", .54, [(0, 1, take("cloth2", .038, .291, 4600, 1.18)),
                        (.012, .36, take("knifeSlice", .110, .365, 5100, 1.22))])
    cue("Block", .58, [(0, 1, take("bookPlace3", .008, .221, 4700)),
                       (.012, .26, take("metalClick", .075, .350, 4000))])
    cue("Impact", .69, [(0, .73, take("chop", .022, .211, 4400)),
                        (.008, 1, take("bookClose", .046, .229, 2200, .76))])


def effects():
    rng=np.random.default_rng(32)
    t=clock(.095)
    grain=rng.normal(size=len(t))
    tick=(.5*np.diff(grain,prepend=0)*np.exp(-t*150)+np.sin(TAU*(740*t+35*t*t))*np.exp(-t*110))
    t2=clock(.48)
    brush=rng.normal(size=len(t2));brush=np.convolve(brush,np.ones(12)/12,"same")
    sweep=brush*np.exp(-((t2-.12)/.055)**2)+.19*np.sin(TAU*(410*t2+320*t2**2))*np.exp(-((t2-.2)/.095)**2)
    ring=np.zeros_like(t2);hit=voice(83,.29,"bell");ring[-len(hit):]=hit*.24
    def write(name,a,peak):
        t=np.arange(len(a))/RATE
        a=a*np.minimum(t/.004,1)*np.minimum((len(a)/RATE-t)/.018,1)
        wav(OUT/(name+".wav"),a*(peak/np.max(abs(a))))
    write("Press",tick,.51)
    write("Cast",sweep+ring,.62)
    for name,notes in [("Victory",[62,69,74,77]),("Defeat",[65,64,57,50])]:
        out=np.zeros(round(2.55*RATE))
        for n,k in enumerate(notes):
            v=voice(k,1.35,"felt");p=round(n*.225*RATE);out[p:p+len(v)]+=v
        pad=voice(notes[-1]-12,1.85,"bow");out[round(.55*RATE):round(.55*RATE)+len(pad)]+=.30*pad
        write(name,out,.64)


if __name__=="__main__":
    OUT.mkdir(parents=True,exist_ok=True)
    moon_menu()
    tide_menu()
    tide_battle()
    effects()
    table_actions()
