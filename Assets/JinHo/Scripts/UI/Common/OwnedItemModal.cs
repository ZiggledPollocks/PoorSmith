using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class OwnedItemModal : MonoBehaviour
{
    static OwnedItemModal current;
    static int closedFrame = -1;
    public static bool CapturesInput => current != null || closedFrame == Time.frameCount;
    IDisposable activity;
    public UnityEngine.Component source;
    public static OwnedItemModal Attach(GameObject root)
    {
        current?.Close();
        var modal = root.AddComponent<OwnedItemModal>(); current = modal;
        modal.activity = GameUIController.Acquire(modal, modal.Close, true, true, 700);
        return modal;
    }
    public void Close()
    {
        closedFrame = Time.frameCount;
        if (current == this) current = null;
        activity?.Dispose(); activity = null;
        gameObject.SetActive(false); Destroy(gameObject);
    }
    void Update() { if (source==null || !source.gameObject.activeInHierarchy || Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close(); }
    void OnDestroy() { activity?.Dispose(); if (current == this) current = null; }
}
