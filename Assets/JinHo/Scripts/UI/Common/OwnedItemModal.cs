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
    public Behaviour owner;
    public static OwnedItemModal Attach(GameObject root)
    {
        current?.Close();
        var modal = root.AddComponent<OwnedItemModal>(); current = modal;
        modal.activity = GameUIController.Acquire(modal, modal.Close, true, true, 700);
        UIKeyboardSurface.Bind(modal,root.transform,"OwnedModal/"+root.name,modal.Close,23000);
        return modal;
    }
    public void Close()
    {
        closedFrame = Time.frameCount;
        if (current == this) current = null;
        activity?.Dispose(); activity = null;
        gameObject.SetActive(false); Destroy(gameObject);
    }
    void Update() { if(source==null||!source.gameObject.activeInHierarchy||owner!=null&&!owner.isActiveAndEnabled){Close();return;}if(GameUIController.CancelPressed)GameUIController.HandleEscape(); }
    void OnDisable() { closedFrame = Time.frameCount; activity?.Dispose(); activity=null; if(current==this)current=null; }
    void OnDestroy() { activity?.Dispose(); if (current == this) current = null; }
}
