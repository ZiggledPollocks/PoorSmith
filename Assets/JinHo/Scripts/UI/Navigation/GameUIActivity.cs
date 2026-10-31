using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Part of the existing UI owner: tokens replace unrelated systems restoring shared flags.
public sealed partial class GameUIController
{
    public sealed class Activity : IDisposable
    {
        internal UnityEngine.Object owner;
        internal Action close;
        internal bool pause, block;
        internal int priority;
        internal long order;
        internal bool disposed;
        public void Dispose()
        {
            if(disposed)return;disposed=true;activities.Remove(this);SynchronizeActivities();
            consumedCancelFrame=Time.frameCount; // Dismissal never becomes an attack/menu toggle.
        }
    }
    static readonly List<Activity> activities=new();
    static EventSystem pointerSystem;
    static PointerEventData pointerData;
    static readonly List<RaycastResult> pointerHits=new();
    static int pointerFrame=-1;static Vector2 pointerPosition;static bool pointerBlocked;
    // Query the actual graphic raycast before UI click callbacks; do not depend on last frame's hover.
    public static bool PointerBlocksGameplay
    {
        get
        {
            var system=EventSystem.current;var mouse=Mouse.current;
            if(system==null||mouse==null)return false;
            var point=mouse.position.ReadValue();
            if(pointerFrame==Time.frameCount&&pointerPosition==point&&pointerSystem==system)return pointerBlocked;
            if(pointerSystem!=system||pointerData==null){pointerSystem=system;pointerData=new PointerEventData(system);}
            pointerPosition=point;pointerFrame=Time.frameCount;pointerData.position=point;pointerHits.Clear();system.RaycastAll(pointerData,pointerHits);
            pointerBlocked=pointerHits.Any(hit=>hit.module is GraphicRaycaster);return pointerBlocked;
        }
    }
    static bool legacyExternal, tokenPause;
    static float baseTimeScale=1;
    static long activityOrder;
    static int consumedCancelFrame=-1;
    Activity menuActivity, focusActivity;
    bool focusPaused;
    public static bool ExternalActivity { get=>legacyExternal||activities.Any(a=>a.block&&!a.disposed);set=>legacyExternal=value; }
    public static bool CancelPressed => consumedCancelFrame!=Time.frameCount &&
        (Keyboard.current?.escapeKey.wasPressedThisFrame==true || Gamepad.current?.buttonEast.wasPressedThisFrame==true);
    public static Activity Acquire(UnityEngine.Object owner,Action close=null,bool pause=true,bool block=true,int priority=300)
    {
        if(owner==null)throw new ArgumentNullException(nameof(owner));
        var a=new Activity{owner=owner,close=close,pause=pause,block=block,priority=priority,order=++activityOrder};
        activities.Add(a);SynchronizeActivities();
        return a;
    }
    static void SynchronizeActivities()
    {
        pointerFrame=-1;
        bool mustPause=activities.Any(a=>a.pause&&!a.disposed);
        if(mustPause&&!tokenPause){CombatHitFeedback2D.FinishHitStopBeforePause();baseTimeScale=Time.timeScale;tokenPause=true;}
        if(mustPause)Time.timeScale=0;
        else if(tokenPause){Time.timeScale=baseTimeScale;tokenPause=false;}
    }
    public static bool HandleEscape()
    {
        if(!CancelPressed)return consumedCancelFrame==Time.frameCount;
        var top=activities.Where(a=>!a.disposed&&a.owner!=null).OrderByDescending(a=>a.priority).ThenByDescending(a=>a.order).FirstOrDefault();
        if(top==null)return false;
        consumedCancelFrame=Time.frameCount;top.close?.Invoke();return true;
    }
    static void ResetActivities()
    {
        pointerSystem=null;pointerData=null;pointerHits.Clear();pointerFrame=-1;
        activities.Clear();legacyExternal=false;tokenPause=false;baseTimeScale=1;activityOrder=0;consumedCancelFrame=-1;
    }
    void LateUpdate()
    {
        for(int i=activities.Count-1;i>=0;i--)
        {var a=activities[i];if(a.owner==null || a.owner is Behaviour b&&!b.isActiveAndEnabled)a.Dispose();}
        SynchronizeActivities();
    }
    void OnApplicationFocus(bool focused)
    {
        if(!focused && !focusPaused)
        {
            focusPaused=true;focusActivity=Acquire(this,null,true,true,30000);
            input?.ClearGameplayInput();
        }
        else if(focused && focusPaused)
        {
            // Resume only focus-owned pause; open menus/decisions retain their own activity tokens.
            focusPaused=false;focusActivity?.Dispose();focusActivity=null;
            input?.ClearGameplayInput();
        }
    }
    void CloseOwnedScreen()
    {
        if(settings?.IsSettingsOpen==true)CloseSettings();
        else if(assimilationOffering?.IsOpen==true)CloseAssimilationOffering();
        else if(inventory?.IsOpen==true)CloseInventory();
    }
}
