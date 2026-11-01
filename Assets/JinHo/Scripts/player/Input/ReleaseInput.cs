using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>Reads the actual rebound controls even while cinematic gameplay actions are disabled.</summary>
public static class ReleaseInput
{
    static InputActionAsset asset;
    static readonly Dictionary<string,ButtonControl[]> controls=new();
    public static event Action Changed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        asset=null;controls.Clear();Changed=null;
        InputSystem.onDeviceChange-=DevicesChanged;InputSystem.onDeviceChange+=DevicesChanged;
    }
    // Startup can precede keyboard discovery; refresh an empty cache when devices become available.
    static void DevicesChanged(InputDevice device,InputDeviceChange change){controls.Clear();Changed?.Invoke();}
    public static void Configure(InputActionAsset value){asset=value;Refresh();}
    public static void Refresh(){controls.Clear();Changed?.Invoke();}
    static ButtonControl[] Buttons(string name)
    {
        if(controls.TryGetValue(name,out var result))return result;
        var list=new List<ButtonControl>();var action=asset?.FindAction("Player/"+name,false);
        if(action!=null)foreach(var binding in action.bindings)
        {
            if(binding.isComposite||binding.isPartOfComposite)continue;
            if(InputSystem.FindControl(binding.effectivePath) is ButtonControl button)list.Add(button);
        }
        if(list.Count==0&&Keyboard.current!=null)
        {if(name=="DialogueAdvance")list.Add(Keyboard.current.spaceKey);else if(name=="DialogueFastForward")list.Add(Keyboard.current.sKey);}
        return controls[name]=list.ToArray();
    }
    public static bool Pressed(string name){foreach(var b in Buttons(name))if(b.wasPressedThisFrame)return true;return false;}
    public static bool Held(string name){foreach(var b in Buttons(name))if(b.isPressed)return true;return false;}
    public static string Label(string name)
    {
        var action=asset?.FindAction("Player/"+name,false);
        if(action!=null)for(int i=0;i<action.bindings.Count;i++)
            if(action.bindings[i].effectivePath.StartsWith("<Keyboard>",StringComparison.Ordinal))return action.GetBindingDisplayString(i, name=="Interact" ? InputBinding.DisplayStringOptions.DontIncludeInteractions : default);
        return name=="Interact"?"F":name=="DialogueAdvance"?"Space":"S";
    }
    public static string Hints(string text)=>text?.Replace("[F]","["+Label("Interact")+"]").Replace("Space로 계속",Label("DialogueAdvance")+"로 계속");
}
