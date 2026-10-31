// [코드 지도] WorldTorchDayNight: 월드 횃불의 낮·밤 시각 효과를 전환한다.
// 주요 함수: Awake, Apply, LateUpdate
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/World/WorldTorchDayNight.cs.md

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Uses the saved smithing day/night state for authored town or smithy torches.</summary>
[DisallowMultipleComponent]
public sealed class WorldTorchDayNight : MonoBehaviour
{
    [SerializeField] private bool smithyTorches;

    private readonly List<Light2D> lights = new List<Light2D>();
    private readonly List<GameObject> flames = new List<GameObject>();
    private bool? appliedNight;

    // 핵심 분기: part.name.StartsWith(lightPrefix, System.StringComparison.Ordinal) 판정.
    // 다음 연결: WorldTorchDayNight.AddFlame(UnityEngine.Transform, string) 호출.
    private void Awake()
    {
        string lightPrefix = smithyTorches ? "Smithy Torch Light " : "Town Torch Light ";
        string torchPrefix = smithyTorches ? "Smithy Wall Torch " : "Town Pillar Torch ";
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
            {
                if (part.name.StartsWith(lightPrefix, System.StringComparison.Ordinal))
                {
                    Light2D light = part.GetComponent<Light2D>();
                    if (light != null) lights.Add(light);
                }
                else if (part.name.StartsWith(torchPrefix, System.StringComparison.Ordinal))
                {
                    if (smithyTorches)
                    {
                        AddFlame(part, "Fire");
                        AddFlame(part, "Glow");
                        AddFlame(part, "Spark");
                    }
                    else
                    {
                        AddFlame(part, "Lit");
                    }
                }
            }
        }

        Apply(false); // Avoid a loading-frame flash until the saved time is available.
    }

    private void AddFlame(Transform torch, string childName)
    {
        Transform child = torch.Find(childName);
        if (child != null) flames.Add(child.gameObject);
    }

    private void LateUpdate()
    {
        SmithingLoop loop = SmithingLoop.Instance;
        if (loop == null || !loop.Initialized) return;
        bool night = loop.SmithData.night;
        if (appliedNight != night) Apply(night);
    }

    private void Apply(bool night)
    {
        foreach (Light2D light in lights)
            if (light != null) light.enabled = night;
        foreach (GameObject flame in flames)
            if (flame != null) flame.SetActive(night);
        appliedNight = night;
    }
}
