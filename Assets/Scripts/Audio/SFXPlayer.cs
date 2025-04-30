using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class SFXPlayer : MonoBehaviour
{
    [Header("音效列表")]
    public List<AudioClip> audioClips;
    private List<AudioSource> audioSources = new List<AudioSource>(); 
    public bool fadeIn = false;
    public float fadeDuration = 1.5f;
    private Coroutine current = null;

    private void Awake()
    {        
        audioSources.AddRange(GetComponents<AudioSource>());

        if (audioSources.Count == 0)
        {
            AddSource(); 
        }

        if(fadeIn == true)
        {
            FadeIn(0,0,fadeDuration,1);
        }
    }

    private void Check(int index)
    {
        while (audioSources.Count <= index)
        {
            AddSource();
        }
    }

    private void AddSource()
    {
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        newSource.spatialBlend = 1f;
        audioSources.Add(newSource);
    }

    public void Play(int sourceIndex, int clipIndex, float volume)
    {
        Check(sourceIndex);
        audioSources[sourceIndex].clip = audioClips[clipIndex];
        audioSources[sourceIndex].volume = Mathf.Clamp01(volume);
        audioSources[sourceIndex].Play();
    }

    public void Stop(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= audioSources.Count)
        {
            Debug.LogWarning($"Invalid audio source index: {sourceIndex}.");
            return;
        }

        if (audioSources[sourceIndex].isPlaying)
        {
            audioSources[sourceIndex].Stop();
        }
    }

    public void Pause(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= audioSources.Count)
        {
            Debug.LogWarning($"Invalid audio source index: {sourceIndex}.");
            return;
        }

        if (audioSources[sourceIndex].isPlaying)
        {
            audioSources[sourceIndex].Pause();
        }
    }

    public void Resume(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= audioSources.Count)
        {
            Debug.LogWarning($"Invalid audio source index: {sourceIndex}.");
            return;
        }

        if (!audioSources[sourceIndex].isPlaying)
        {
            audioSources[sourceIndex].UnPause();
        }
    }


    public void PlayOneShot(int sourceIndex, int clipIndex, float volume)
    {
        Check(sourceIndex);

        if (clipIndex < 0 || clipIndex >= audioClips.Count)
        {
            Debug.LogWarning($"Invalid audio clip index: {clipIndex}.");
            return;
        }

        audioSources[sourceIndex].PlayOneShot(audioClips[clipIndex], Mathf.Clamp01(volume));
    }


    public void FadeIn(int sourceIndex, int clipIndex, float duration, float targetVolume = 1f)
    {
         Check(sourceIndex);

        if (clipIndex < 0 || clipIndex >= audioClips.Count)
        {
            Debug.LogWarning($"Invalid audio clip index: {clipIndex}.");
            return;
        }

        AudioSource source = audioSources[sourceIndex];
        source.clip = audioClips[clipIndex];
        source.volume = 0;
        source.Play();

        if (current != null)
        {
            StopCoroutine(current);
        }
        current = StartCoroutine(FadeAudio(source, duration, targetVolume));
    }

    public void FadeOut(int sourceIndex, float duration)
    {
        if (sourceIndex < 0 || sourceIndex >= audioSources.Count)
        {
            Debug.LogWarning($"Invalid audio source index: {sourceIndex}.");
            return;
        }
        if (current != null)
        {
            StopCoroutine(current);
        }
        current = StartCoroutine(FadeAudio(audioSources[sourceIndex], duration, 0, stopAfterFade: true));
    }

    private IEnumerator FadeAudio(AudioSource source, float duration, float targetVolume, bool stopAfterFade = false)
    {
        float startVolume = source.volume;
        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            source.volume = Mathf.Lerp(startVolume, targetVolume, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        source.volume = targetVolume;

        if (stopAfterFade)
        {
            source.Stop();
        }
    }

    public int GetIndex(string clipName)
    {
        for (int i = 0; i < audioSources.Count; i++)
        {
            if (audioSources[i].isPlaying && 
                audioSources[i].clip != null && 
                audioSources[i].clip.name == clipName)
            {
                return i; // Return the first found source playing the clip
            }
        }
        return -1; // Return -1 if no matching clip is found
    }
    
    public int FindSource()
    {
        for (int i = 0; i < audioSources.Count; i++)
        {
            if (!audioSources[i].isPlaying)
            {
                return i;
            }
        }
        
        // No available source, add a new one
        AddSource();
        return audioSources.Count - 1;
    }

}
