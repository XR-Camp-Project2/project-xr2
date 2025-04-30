using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[System.Serializable]
public struct SFXPlayerEntry
{
    [SerializeField] private string name;
    public SFXPlayer sfxPlayer;
    [SerializeField] private List<AudioClip> clips;

    public string Name => sfxPlayer != null ? sfxPlayer.name : "Unnamed";
    public List<AudioClip> Clips => sfxPlayer != null ? new List<AudioClip>(sfxPlayer.audioClips) : new List<AudioClip>();
    
    public void UpdateEntry()
    {
        name = Name;
        clips = Clips;
    }
}

public class AudioManager : MonoBehaviour
{
    [Header("各種類的 SFX Player")]
    [Tooltip("Drag and drop SFX Player objects here. Each represents a different sound group.")]
    public List<SFXPlayerEntry> sfxPlayers = new List<SFXPlayerEntry>();
    
    private Dictionary<string, SFXPlayer> sfxPlayerDict = new Dictionary<string, SFXPlayer>();
    private Dictionary<string, List<AudioClip>> sfxClipDict = new Dictionary<string, List<AudioClip>>();
    private Dictionary<string, Dictionary<string, int>> clipNameToIndexDict = new Dictionary<string, Dictionary<string, int>>();

    private void Awake()
    {
        Initialize();
    }

    private void OnValidate()
    {
        Initialize();
    }

    private void Initialize()
    {
        sfxPlayerDict.Clear();
        sfxClipDict.Clear();
        clipNameToIndexDict.Clear();
        
        for (int i = 0; i < sfxPlayers.Count; i++)
        {
            SFXPlayerEntry entry = sfxPlayers[i];
            entry.UpdateEntry(); // Update the name and clips dynamically
            sfxPlayers[i] = entry;
            
            if (entry.sfxPlayer != null)
            {
                sfxPlayerDict[entry.sfxPlayer.name] = entry.sfxPlayer;
                sfxClipDict[entry.sfxPlayer.name] = entry.Clips;
                
                // Map clip names to indexes
                var clipIndexMap = new Dictionary<string, int>();
                for (int j = 0; j < entry.Clips.Count; j++)
                {
                    if (entry.Clips[j] != null)
                    {
                        clipIndexMap[entry.Clips[j].name] = j;
                    }
                }
                clipNameToIndexDict[entry.sfxPlayer.name] = clipIndexMap;
            }
        }
    }

    /// <summary>
    /// Plays a sound from a specific SFX Player instance using either an index or a clip name.
    /// </summary>
    public void Play(string sfxPlayerName, object clipReference, float volume = -1f, object source = null)
    {
        if (sfxPlayerDict.TryGetValue(sfxPlayerName, out SFXPlayer sfxPlayer))
        {
            int clipIndex = -1;
            if (clipReference is int index)
            {
                clipIndex = index;
            }
            else if (clipReference is string clipName && clipNameToIndexDict.ContainsKey(sfxPlayerName) && clipNameToIndexDict[sfxPlayerName].TryGetValue(clipName, out int nameIndex))
            {
                clipIndex = nameIndex;
            }

            if (clipIndex >= 0 && clipIndex < sfxPlayer.audioClips.Count)
            {
                int sourceIndex = source is int intSource ? intSource : sfxPlayer.FindSource();
                // Use specified volume if provided, otherwise use the default AudioSource volume
                if (volume>=0)
                {
                    sfxPlayer.Play(sourceIndex, clipIndex, volume);
                }
                else
                {
                    sfxPlayer.Play(sourceIndex, clipIndex, 1);
                }
            }
            else
            {
                Debug.LogWarning($"Invalid clip reference '{clipReference}' for SFX Player '{sfxPlayerName}'.");
            }
        }
        else
        {
            Debug.LogWarning($"SFX Player '{sfxPlayerName}' not found in Sound Manager.");
        }
    }

    /// <summary>
    /// Stops a sound from a specific SFX Player instance using either an index or a clip name.
    /// If no parameter is provided, stops audio source 0.
    /// </summary>
    public void Stop(string sfxPlayerName, object clipReference = null)
    {
        if (sfxPlayerDict.TryGetValue(sfxPlayerName, out SFXPlayer sfxPlayer))
        {
            int sourceIndex = 0; // Default to stopping audio source 0
            
            if (clipReference is int index)
            {
                sourceIndex = index;
            }
            else if (clipReference is string clipName)
            {
                sourceIndex = sfxPlayer.GetIndex(clipName);
            }
            
            if (sourceIndex >= 0)
            {
                sfxPlayer.Stop(sourceIndex);
            }
            else
            {
                Debug.LogWarning($"Invalid stop reference '{clipReference}' for SFX Player '{sfxPlayerName}'.");
            }
        }
        else
        {
            Debug.LogWarning($"SFX Player '{sfxPlayerName}' not found in Sound Manager.");
        }
    }
    
    /// <summary>
    /// Fades in a sound over time.
    /// </summary>
    public void FadeIn(string sfxPlayerName, object clipReference, float duration, float targetVolume = 1f, object source=null)
    {
        if (sfxPlayerDict.TryGetValue(sfxPlayerName, out SFXPlayer sfxPlayer))
        {
            int clipIndex = -1;
            if (clipReference is int index)
            {
                clipIndex = index;
            }
            else if (clipReference is string clipName && clipNameToIndexDict.ContainsKey(sfxPlayerName) && clipNameToIndexDict[sfxPlayerName].TryGetValue(clipName, out int nameIndex))
            {
                clipIndex = nameIndex;
            }

            int sourceIndex = source is int intSource ? intSource : sfxPlayer.FindSource();
            if (clipIndex >= 0 && clipIndex < sfxPlayer.audioClips.Count)
            {
                sfxPlayer.FadeIn(sourceIndex, clipIndex, duration, targetVolume);
            }
            else
            {
                Debug.LogWarning($"Invalid clip reference '{clipReference}' for SFX Player '{sfxPlayerName}'.");
            }
        }
        else
        {
            Debug.LogWarning($"SFX Player '{sfxPlayerName}' not found in Sound Manager.");
        }
    }

    public void FadeOut(string sfxPlayerName, object clipReference = null, float duration = 1.5f)
    {
        if (sfxPlayerDict.TryGetValue(sfxPlayerName, out SFXPlayer sfxPlayer))
        {
            int sourceIndex = 0; // Default to fading out source 0

            if (clipReference is int index)
            {
                sourceIndex = index;
            }
            else if (clipReference is string clipName)
            {
                sourceIndex = sfxPlayer.GetIndex(clipName);
            }

            if (sourceIndex >= 0)
            {
                sfxPlayer.FadeOut(sourceIndex, duration);
            }
            else
            {
                Debug.LogWarning($"No active source found playing clip '{clipReference}' in SFX Player '{sfxPlayerName}'.");
            }
        }
        else
        {
            Debug.LogWarning($"SFX Player '{sfxPlayerName}' not found in Sound Manager.");
        }
    }
}