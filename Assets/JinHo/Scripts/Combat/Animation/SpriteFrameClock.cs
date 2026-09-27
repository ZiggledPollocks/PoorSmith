using UnityEngine;

/// <summary>Shared frame timing; species-specific frame effects remain in their controllers.</summary>
public static class SpriteFrameClock
{
    public static int Advance(ref float time, float deltaTime, float framesPerSecond, int count, bool loops)
    {
        time += deltaTime * framesPerSecond;
        int index = Mathf.FloorToInt(time);
        return loops ? index % count : Mathf.Min(index, count - 1);
    }
}
