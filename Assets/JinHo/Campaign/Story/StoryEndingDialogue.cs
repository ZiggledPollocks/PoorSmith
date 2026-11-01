using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared ending dialogue only. Settlement, saving and travel remain in StoryTutorial.Ending.</summary>
public static class StoryEndingDialogue
{
    public static IEnumerator Main(StoryTutorialView view, IReadOnlyDictionary<string, string[]> dialogue, bool paid)
        { GameSound.OverrideMusic(view,"Dance",20); yield return view.Speak(dialogue[paid ? "EndingPaid" : "EndingFailed"], false); }

    public static IEnumerator AfterFailure(StoryTutorialView view, IReadOnlyDictionary<string, string[]> dialogue)
        => view.Speak(dialogue["EndingAfterFailure"], false);

    // Close speech first, then wait for an explicit presentation-only choice.
    public static IEnumerator Credits(StoryTutorialView view, Action<StoryEndingChoice> onChosen = null)
    {
        view.CancelPresentation();
        // Speech cleanup also cancels previews; the real credits card explicitly retains its music.
        GameSound.OverrideMusic(view,"Dance",20);
        var screen = StoryEndingScreen.Open(view.transform);
        while (screen != null && !screen.Chosen) yield return null;
        if (screen != null)
        {
            var choice = screen.Choice;
            screen.Close();
            GameSound.OverrideMusic(view,null);
            onChosen?.Invoke(choice);
        }
    }

    // Preview shares the real sections/order/input, omitting only world cues and all game-data effects.
    public static IEnumerator Preview(StoryTutorialView view, bool paid)
    {
        var dialogue = StoryTutorial.ReadDialogue(Resources.Load<TextAsset>("Story/Dialogue"));
        yield return Main(view, dialogue, paid);
        if (!paid) yield return AfterFailure(view, dialogue);
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime * view.Speed) yield return null;
        yield return Credits(view);
    }
}
