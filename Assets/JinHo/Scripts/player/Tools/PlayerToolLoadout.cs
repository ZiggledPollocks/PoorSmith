// [코드 지도] PlayerToolLoadout: Inspector에서 지정한 시작 도구들을 플레이어 슬롯에 배치한다. 도구를 관리하는 Controller와 초기 구성을 분리한 컴포넌트다. Awake에서 한 번 적용하지만 public ApplyLoadout을 다시 호출하면 런타임 재설정도 가능하다. 저장 파일에서 소유 도구 목록을 복원하는 기능은 아니다.
// 주요 함수: ApplyLoadout, Awake
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Tools/PlayerToolLoadout.cs.md

using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
/// <summary>Applies the starting tool slots before the tool controller starts.</summary>
public class PlayerToolLoadout : MonoBehaviour
{
    [SerializeField] private PlayerToolController toolController;
    [SerializeField] private List<ToolData> startingTools = new();
    [SerializeField, Min(0)] private int startingSlotIndex;
    [SerializeField] private bool replaceExistingTools = true;

    private void Awake()
    {
        if (toolController == null)
        {
            toolController = GetComponent<PlayerToolController>();
        }

        ApplyLoadout();
    }

    // 핵심 분기: toolController == null 판정.
    // 다음 연결: PlayerToolController.ClearTools() 호출.
    public void ApplyLoadout()
    {
        if (toolController == null)
        {
            Debug.LogWarning("도구를 연결할 PlayerToolController가 없습니다.", this);
            return;
        }

        if (replaceExistingTools)
        {
            toolController.ClearTools();
        }

        for (int i = 0; i < startingTools.Count; i++)
        {
            toolController.SetToolSlot(i, startingTools[i]);
        }

        if (!toolController.SelectToolSlot(startingSlotIndex))
        {
            toolController.SelectFirstAvailableTool();
        }
    }
}
