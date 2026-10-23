using System;
using UnityEngine;

namespace Naispo.Overhead
{
    // Two short voices keep overlapping game events quiet without scheduling delayed sounds.
    public sealed class GameAudio : MonoBehaviour
    {
        const int SampleRate=22050;
        AudioSource bounceSource,feedbackSource;
        AudioClip bounceClip,correctClip,incorrectClip,celebrationClip;
        AudioListener ownedListener;
        bool muted,paused;
        float nextBounceAt,celebrationUntil;

        public bool Muted
        {
            get => muted;
            set
            {
                if(muted==value)return;
                muted=value;
                if(muted)ResetSounds();
            }
        }

        void Awake()
        {
            bounceSource=CreateSource();
            feedbackSource=CreateSource();
            bounceClip=CreateClip("Soft paddle pop",GameSoundCue.Bounce);
            correctClip=CreateClip("Happy delivery",GameSoundCue.Correct);
            incorrectClip=CreateClip("Gentle surprise",GameSoundCue.Incorrect);
            celebrationClip=CreateClip("Small celebration",GameSoundCue.Celebration);

            bool hasListener=false;
            foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if(listener.isActiveAndEnabled) { hasListener=true;break; }
            if(!hasListener)
            {
                var listenerObject=Camera.main!=null ? Camera.main.gameObject : gameObject;
                // A disabled listener may already occupy the camera. Do not add a duplicate there.
                if(listenerObject.GetComponent<AudioListener>()!=null)listenerObject=gameObject;
                if(listenerObject.GetComponent<AudioListener>()==null)
                    ownedListener=listenerObject.AddComponent<AudioListener>();
            }
        }

        AudioSource CreateSource()
        {
            var source=gameObject.AddComponent<AudioSource>();
            source.playOnAwake=false;
            source.loop=false;
            source.spatialBlend=0;
            source.volume=0.28f;
            source.dopplerLevel=0;
            source.reverbZoneMix=0;
            return source;
        }

        static AudioClip CreateClip(string name,GameSoundCue cue)
        {
            float[] samples=GameSoundSynthesis.Create(cue,SampleRate);
            var clip=AudioClip.Create(name,samples.Length,1,SampleRate,false);
            clip.hideFlags=HideFlags.DontSave;
            clip.SetData(samples,0);
            return clip;
        }

        bool CanPlay => isActiveAndEnabled&&!muted&&!paused;

        // horizontalPosition is normalized to the playfield: 0=left, 0.5=center, 1=right.
        public void PlayBounce(float horizontalPosition)
        {
            if(!CanPlay||bounceSource==null||Time.unscaledTime<nextBounceAt||bounceSource.isPlaying)return;
            nextBounceAt=Time.unscaledTime+0.08f;
            if(float.IsNaN(horizontalPosition)||float.IsInfinity(horizontalPosition))horizontalPosition=0.5f;
            bounceSource.panStereo=(Mathf.Clamp01(horizontalPosition)*2-1)*0.35f;
            bounceSource.clip=bounceClip;
            bounceSource.Play();
        }

        public void PlayDelivery(bool correct)
        {
            if(!CanPlay||feedbackSource==null||Time.unscaledTime<celebrationUntil)return;
            feedbackSource.Stop();
            feedbackSource.clip=correct ? correctClip : incorrectClip;
            feedbackSource.Play();
        }

        public void PlayCelebration()
        {
            if(!CanPlay||feedbackSource==null)return;
            feedbackSource.Stop();
            feedbackSource.clip=celebrationClip;
            celebrationUntil=Time.unscaledTime+celebrationClip.length;
            feedbackSource.Play();
        }

        public void SetPaused(bool paused)
        {
            if(this.paused==paused)return;
            this.paused=paused;
            // Stop rather than Pause: resuming the game must not replay an old event.
            ResetSounds();
        }

        public void ResetSounds()
        {
            if(bounceSource!=null)bounceSource.Stop();
            if(feedbackSource!=null)feedbackSource.Stop();
            nextBounceAt=0;
            celebrationUntil=0;
        }

        void OnDisable() => ResetSounds();

        void OnDestroy()
        {
            ResetSounds();
            if(bounceClip!=null)Destroy(bounceClip);
            if(correctClip!=null)Destroy(correctClip);
            if(incorrectClip!=null)Destroy(incorrectClip);
            if(celebrationClip!=null)Destroy(celebrationClip);
            if(bounceSource!=null)Destroy(bounceSource);
            if(feedbackSource!=null)Destroy(feedbackSource);
            if(ownedListener!=null)Destroy(ownedListener);
        }
    }

    internal enum GameSoundCue { Bounce,Correct,Incorrect,Celebration }

    // Pure PCM generation: no Unity state, randomness, files, or audio-device dependency.
    internal static class GameSoundSynthesis
    {
        public static float[] Create(GameSoundCue cue,int sampleRate)
        {
            if(sampleRate<8000||sampleRate>96000)throw new ArgumentOutOfRangeException(nameof(sampleRate));
            double duration;
            switch(cue)
            {
                case GameSoundCue.Bounce:duration=0.13;break;
                case GameSoundCue.Correct:duration=0.36;break;
                case GameSoundCue.Incorrect:duration=0.25;break;
                case GameSoundCue.Celebration:duration=0.9;break;
                default:throw new ArgumentOutOfRangeException(nameof(cue));
            }
            var samples=new float[(int)Math.Ceiling(duration*sampleRate)+1];
            switch(cue)
            {
                case GameSoundCue.Bounce:
                    AddNote(samples,sampleRate,0,0.13,330,185,0.43);
                    break;
                case GameSoundCue.Correct:
                    AddNote(samples,sampleRate,0,0.19,523.25,523.25,0.35);
                    AddNote(samples,sampleRate,0.12,0.24,659.25,659.25,0.39);
                    break;
                case GameSoundCue.Incorrect:
                    AddNote(samples,sampleRate,0,0.25,392,370,0.30);
                    break;
                case GameSoundCue.Celebration:
                    AddNote(samples,sampleRate,0,0.21,523.25,523.25,0.34);
                    AddNote(samples,sampleRate,0.16,0.23,659.25,659.25,0.35);
                    AddNote(samples,sampleRate,0.32,0.26,783.99,783.99,0.36);
                    AddNote(samples,sampleRate,0.50,0.40,1046.50,1046.50,0.38);
                    break;
            }
            double peak=0;
            foreach(float sample in samples)
            {
                if(float.IsNaN(sample)||float.IsInfinity(sample))throw new InvalidOperationException("Non-finite audio sample");
                peak=Math.Max(peak,Math.Abs(sample));
            }
            // Even both sources playing together leave substantial output headroom.
            double gain=peak>0.48 ? 0.48/peak : 1;
            for(int i=0;i<samples.Length;i++)samples[i]=(float)(samples[i]*gain);
            samples[0]=samples[samples.Length-1]=0;
            return samples;
        }

        static void AddNote(float[] samples,int sampleRate,double start,double duration,
            double startFrequency,double endFrequency,double amplitude)
        {
            int first=(int)Math.Round(start*sampleRate);
            int count=(int)Math.Ceiling(duration*sampleRate);
            for(int i=0;i<=count&&first+i<samples.Length;i++)
            {
                double t=(double)i/sampleRate;
                double attack=Smooth01(t/0.009);
                double release=Smooth01((duration-t)/0.04);
                double envelope=attack*release*Math.Exp(-2.3*t/duration);
                double phase=2*Math.PI*(startFrequency*t+(endFrequency-startFrequency)*t*t/(2*duration));
                double tone=(Math.Sin(phase)+0.10*Math.Sin(2*phase))/1.10;
                samples[first+i]+=(float)(amplitude*envelope*tone);
            }
        }

        static double Smooth01(double value)
        {
            double t=Math.Max(0,Math.Min(1,value));
            return t*t*(3-2*t);
        }
    }
}
