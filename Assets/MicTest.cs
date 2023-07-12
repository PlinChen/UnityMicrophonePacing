using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MicTest : MonoBehaviour
{
    public Toggle BgmToggle;
    public Toggle MicToggle;
    public Toggle MicLoopToggle;
    public Toggle MicMuteToggle;
    public Toggle KeepPaceToggle;
    
    public AudioSource BgmAudioSource;
    public AudioSource MicAudioSource;
    
    public TextMeshProUGUI MicNameText;
    
    private int _micIndex = 0;
    public Button MicIndexButton;
    public TextMeshProUGUI MicIndexText;
    
    [FormerlySerializedAs("micPositionSlider")] public Slider micAudioPositionSlider;
    [FormerlySerializedAs("micPositionText")] public TextMeshProUGUI micAudioPositionText;
    
    public Slider micDevicePositionSlider;
    public TextMeshProUGUI micADevicePositionText;
    
    private string micDeviceName;
    
    private int audioOutputSampleRate;
    private int halfAudioOutputSampleRate;
    private Coroutine _keepPaceCoroutine;
    // Start is called before the first frame update
    void Start()
    {
        audioOutputSampleRate = AudioSettings.outputSampleRate;
        halfAudioOutputSampleRate = audioOutputSampleRate / 2;
        BgmToggle.onValueChanged.AddListener((value) =>
        {
            if (value)
            {
                BgmAudioSource.mute = false;
                BgmAudioSource.loop = true;
                BgmAudioSource.Play();
            }
            else
            {
                BgmAudioSource.Stop();
            }
        });
        
        MicToggle.onValueChanged.AddListener(ToggleMic);
        
        MicLoopToggle.onValueChanged.AddListener((value) =>
        {
            MicAudioSource.loop = value;
        });
        
        MicMuteToggle.onValueChanged.AddListener((value) =>
        {
            MicAudioSource.mute = value;
        });
        
        MicIndexButton.onClick.AddListener(() =>
        {
            if (MicAudioSource.isPlaying) return;
            _micIndex++;
            MicIndexText.text = _micIndex.ToString();
        });
        
        KeepPaceToggle.onValueChanged.AddListener(OnKeepPaceToggle);
    }

    private void ToggleMic(bool isOn)
    {
        micAudioPositionSlider.gameObject.SetActive(isOn);
        micDevicePositionSlider.gameObject.SetActive(isOn);
        if (!isOn)
        {
            MicAudioSource.Stop();
            MicAudioSource.clip = null;
            MicNameText.text = "Mic Disabled";
            OnKeepPaceToggle(false);
            return;
        }
        
        // Get microphone devices
        var devices = Microphone.devices;
        if (devices.Length == 0)
        {
            MicNameText.text = "No Mic Device";
            micAudioPositionSlider.gameObject.SetActive(false);
            micDevicePositionSlider.gameObject.SetActive(false);
            MicToggle.SetIsOnWithoutNotify(false);
            return;
        }

        _micIndex %= devices.Length;
        MicIndexText.text = _micIndex.ToString();

        MicNameText.text = string.Join(";\n", devices.Select((d, i) => $"[{i}] {d}"));
        
        // Use first microphone device as default
        var device = devices[_micIndex];
        MicNameText.text += "\n\n" + device;
        
        // get microphone device caps
        var minFreq = 0;
        var maxFreq = 0;
        Microphone.GetDeviceCaps(device, out minFreq, out maxFreq);
        var useFreq = maxFreq > 0 ? maxFreq : AudioSettings.outputSampleRate;
        MicNameText.text += $"... minFreq:{minFreq}, maxFreq:{maxFreq}, useFreq:{useFreq}";
        
        // start microphone recording
        var clip = Microphone.Start(device, true, 1, useFreq);
        MicAudioSource.clip = clip;
        MicAudioSource.loop = MicLoopToggle.isOn;
        MicAudioSource.mute = MicMuteToggle.isOn;

        while (!(Microphone.GetPosition(device) >= 480)) {} // 10ms * 48000 / 1000ms = 480
        MicAudioSource.Play();
        MicAudioSource.timeSamples = 0;
        micDeviceName = device;
        
        OnKeepPaceToggle(KeepPaceToggle.isOn);
        StartCoroutine(ShowPatch());
    }
    
    private void OnKeepPaceToggle(bool isOn)
    {
        if (isOn)
        {
            _keepPaceCoroutine = StartCoroutine(KeepPace());
        }
        else if (_keepPaceCoroutine != null)
        {
            StopCoroutine(_keepPaceCoroutine);
            _keepPaceCoroutine = null;
        }
    }

    private IEnumerator KeepPace()
    {
        while (MicAudioSource.isPlaying)
        {
            var micAudioPosition = MicAudioSource.timeSamples;
            var micDevicePosition = Microphone.GetPosition(micDeviceName);
            micAudioPositionSlider.value = micAudioPosition;
            var diff = (micDevicePosition - micAudioPosition + audioOutputSampleRate) % audioOutputSampleRate;
            if (diff < halfAudioOutputSampleRate) // device faster
            {
                // skip if diff is too large
                // replay if diff is too small
                if (diff is > 2000 or < 240)
                {
                    MicAudioSource.timeSamples = (micDevicePosition - 480+ audioOutputSampleRate) % audioOutputSampleRate;
                }
            }
            else // device slower than audio output
            {
                MicAudioSource.timeSamples = (micDevicePosition - 480+ audioOutputSampleRate) % audioOutputSampleRate;
            }
            yield return new WaitForSeconds(0.3f);
        }
    }

    private IEnumerator ShowPatch()
    {
        var lastMicAudioPosition = 0;
        var lastMicDevicePosition = 0;
        while (MicAudioSource.isPlaying)
        {
            var micAudioPosition = MicAudioSource.timeSamples;
            micAudioPositionSlider.value = micAudioPosition;
            micAudioPositionText.text = micAudioPosition.ToString();
        
            var micDevicePosition = Microphone.GetPosition(micDeviceName);
            micDevicePositionSlider.value = micDevicePosition;
            micADevicePositionText.text = micDevicePosition.ToString();
            
            if (KeepPaceToggle.isOn)
            {
                yield return new WaitForSeconds(0.3f);
                continue;
            }
            // compute diff info
            var audioPositionDiff = (micAudioPosition - lastMicAudioPosition + audioOutputSampleRate) % audioOutputSampleRate;
            var devicePositionDiff = (micDevicePosition - lastMicDevicePosition + audioOutputSampleRate) % audioOutputSampleRate;
            var time = Time.deltaTime;
            var rightDiff = time * audioOutputSampleRate;

            var audioDiffRate = 100f * (audioPositionDiff - rightDiff) / rightDiff;
            var deviceDiffRate = 100f * (devicePositionDiff - rightDiff) / rightDiff;
            Debug.Log("audioDiffRate:" + audioDiffRate + ", deviceDiffRate:" + deviceDiffRate);

            // set last position
            lastMicAudioPosition = micAudioPosition;
            lastMicDevicePosition = micDevicePosition;
            
            yield return null; //
        }
    }
}
