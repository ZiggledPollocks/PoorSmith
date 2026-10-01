// [코드 지도] FieldCaveLighting: 필드 동굴의 조명과 지역별 밝기 변화를 관리한다.
// 주요 함수: LateUpdate
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Field/FieldCaveLighting.cs.md

using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Switches the authored field lights with the existing forest/cave passage state.</summary>
[DisallowMultipleComponent]
public sealed class FieldCaveLighting : MonoBehaviour
{
    [SerializeField] private CaveEntranceBackgroundTransition entrance;
    [SerializeField] private Transform player;
    [SerializeField] private Light2D ambient;
    [SerializeField] private Light2D[] playerLights;
    [SerializeField, Range(0f, 1f)] private float caveAmbient = 0f;
    [SerializeField, Min(0f)] private float horizontalSpacing = 4.5f;
    [SerializeField] private float verticalOffset = 0.6f;

    private bool? wasInsideCave;

    // 핵심 분기: entrance == null || player == null || ambient == null || playerLights == null 판정.
    // 상태 변경: ambient.intensity 갱신.
    private void LateUpdate()
    {
        if (entrance == null || player == null || ambient == null || playerLights == null)
            return;

        bool insideCave = entrance.IsInsideCave;
        if (wasInsideCave != insideCave)
        {
            ambient.intensity = insideCave ? caveAmbient : 1f;
            foreach (Light2D light in playerLights)
                if (light != null) light.enabled = insideCave;
            wasInsideCave = insideCave;
        }

        // The authored horizontal ellipse lights only the player's current cave floor.
        for (int i = 0; i < playerLights.Length; i++)
        {
            if (playerLights[i] == null) continue;
            float offset = (i - (playerLights.Length - 1) * 0.5f) * horizontalSpacing;
            playerLights[i].transform.position = player.position + new Vector3(offset, verticalOffset, 0f);
        }
    }
}
