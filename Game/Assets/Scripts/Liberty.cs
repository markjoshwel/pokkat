using System;
using PokkatCore;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class Liberty : MonoBehaviour
{
    [SerializeField] private ARTrackedImageManager trackedImageManager;

    private void Awake()
    {
        Logkat.Dev($"Liberty: manager={trackedImageManager}");
    }
    
    private void OnEnable()
    {
        // subscribe to ar foundation's trackables changed event
        // (this fires whenever images are added, updated, or removed from tracking)
        trackedImageManager.trackablesChanged.AddListener(TriggerG);
    }

    private void OnDisable()
    {
        // unsubscribe from the event to prevent memory leaks and null reference errors
        trackedImageManager.trackablesChanged.RemoveListener(TriggerG);
    }

    private void TriggerG<T>(ARTrackablesChangedEventArgs<T> args) where T : ARTrackable
    {
        Logkat.Dev("Liberty.TriggerG");
    }

    public void Trigger()
    {
        Logkat.Dev("Liberty.Trigger");
    }
}