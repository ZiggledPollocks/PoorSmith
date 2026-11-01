// [코드 지도] PlayerToolController: 플레이어가 사용할 도구 목록과 현재 선택을 관리하는 컴포넌트다. 공격이나 채집 자체를 실행하지 않고, 다른 시스템이 읽을 현재 `ToolData`를 제공한다. 선택 변경은 이벤트로 전달하며 HUD와 저장 시스템이 이를 구독한다. 초기 목록을 넣는 책임은 `PlayerToolLoadout`과 분리되어 있다.
// 주요 함수: SelectRelativeWeapon, SetToolSlot, AddTool
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Tools/PlayerToolController.cs.md

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns tool slots and the currently selected tool.</summary>
public class PlayerToolController : MonoBehaviour
{
    [Header("Tool Slots")]
    [SerializeField] private List<ToolData> toolSlots = new();
    [SerializeField, Min(0)] private int startingSlotIndex;

    [Header("Current Tool")]
    [SerializeField] private ToolData currentTool;

    private int currentSlotIndex = -1;
    private int lastWeaponSlotIndex = -1;

    public ToolData CurrentTool => currentTool;
    public int CurrentSlotIndex => currentSlotIndex;
    public int ToolSlotCount => toolSlots.Count;
    public IReadOnlyList<ToolData> ToolSlots => toolSlots;

    public event Action<ToolData, int> CurrentToolChanged;
    public event Action ToolSlotsChanged;

    private void Start()
    {
        if (currentTool != null)
        {
            currentSlotIndex = toolSlots.IndexOf(currentTool);
            if (currentTool.IsWeapon)
                lastWeaponSlotIndex = currentSlotIndex;
            return;
        }

        if (GetToolAtSlot(startingSlotIndex) != null)
        {
            SelectToolSlot(startingSlotIndex);
            return;
        }

        SelectFirstAvailableTool();
    }

    public bool SelectToolSlot(int slotIndex)
    {
        ToolData selectedTool = GetToolAtSlot(slotIndex);

        if (selectedTool == null)
        {
            Debug.LogWarning($"도구 슬롯 {slotIndex}이 비어 있거나 존재하지 않습니다.");
            return false;
        }

        currentSlotIndex = slotIndex;
        currentTool = selectedTool;
        if (currentTool.IsWeapon)
            lastWeaponSlotIndex = currentSlotIndex;
        CurrentToolChanged?.Invoke(currentTool, currentSlotIndex);

        Debug.Log($"현재 도구: {currentTool.ToolName} (슬롯 {currentSlotIndex})");
        return true;
    }

    public bool SelectToolType(ToolType toolType)
    {
        for (int i = 0; i < toolSlots.Count; i++)
        {
            ToolData tool = toolSlots[i];

            if (tool != null && (tool.ToolType == toolType || toolType == ToolType.Sword && tool.IsWeapon))
                return SelectToolSlot(i);
        }

        Debug.LogWarning($"{toolType} 타입 도구가 등록되어 있지 않습니다.", this);
        return false;
    }

    public bool SelectToolId(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            return false;

        string normalizedId = toolId.Trim();
        for (int i = 0; i < toolSlots.Count; i++)
        {
            ToolData tool = toolSlots[i];
            if (tool != null && string.Equals(
                    tool.ToolId?.Trim(),
                    normalizedId,
                    StringComparison.Ordinal))
            {
                return SelectToolSlot(i);
            }
        }

        return false;
    }

    public void SelectNextTool()
    {
        SelectRelativeTool(1);
    }

    public void SelectPreviousTool()
    {
        SelectRelativeTool(-1);
    }

    public bool SelectNextWeapon()
    {
        return SelectRelativeWeapon(1);
    }

    public bool SelectPreviousWeapon()
    {
        return SelectRelativeWeapon(-1);
    }

    public ToolData GetToolAtSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= toolSlots.Count)
            return null;

        return toolSlots[slotIndex];
    }

    // 핵심 분기: slotIndex < 0 판정.
    // 상태 변경: toolSlots[slotIndex] 갱신.
    // 다음 연결: PlayerToolController.EnsureSlotExists(int) 호출.
    public bool SetToolSlot(int slotIndex, ToolData toolData)
    {
        if (slotIndex < 0)
        {
            Debug.LogWarning("도구 슬롯 번호는 0 이상이어야 합니다.");
            return false;
        }

        EnsureSlotExists(slotIndex);
        toolSlots[slotIndex] = toolData;

        if (lastWeaponSlotIndex == slotIndex
            && (toolData == null || !toolData.IsWeapon))
            lastWeaponSlotIndex = -1;

        if (currentSlotIndex == slotIndex)
        {
            currentTool = toolData;

            if (currentTool == null)
            {
                currentSlotIndex = -1;
                SelectFirstAvailableTool();
            }
            else
            {
                if (currentTool.IsWeapon)
                    lastWeaponSlotIndex = currentSlotIndex;
                CurrentToolChanged?.Invoke(currentTool, currentSlotIndex);
            }
        }
        else if (currentTool == null && toolData != null)
        {
            SelectToolSlot(slotIndex);
        }

        ToolSlotsChanged?.Invoke();
        return true;
    }

    // 핵심 분기: toolData == null 판정.
    // 상태 변경: emptySlot 갱신.
    // 다음 연결: PlayerToolController.SelectToolSlot(int) 호출.
    public int AddTool(ToolData toolData)
    {
        if (toolData == null)
        {
            Debug.LogWarning("등록할 ToolData가 없습니다.");
            return -1;
        }

        int existingSlot = toolSlots.IndexOf(toolData);

        if (existingSlot >= 0)
            return existingSlot;

        int emptySlot = toolSlots.FindIndex(tool => tool == null);

        if (emptySlot < 0)
        {
            emptySlot = toolSlots.Count;
            toolSlots.Add(toolData);
        }
        else
        {
            toolSlots[emptySlot] = toolData;
        }

        if (currentTool == null)
        {
            SelectToolSlot(emptySlot);
        }

        ToolSlotsChanged?.Invoke();
        return emptySlot;
    }

    public bool RemoveTool(ToolData toolData)
    {
        int slotIndex = toolSlots.IndexOf(toolData);

        if (slotIndex < 0)
            return false;

        return ClearToolSlot(slotIndex);
    }

    // 핵심 분기: slotIndex < 0 || slotIndex >= toolSlots.Count 판정.
    // 상태 변경: toolSlots[slotIndex] 갱신.
    // 다음 연결: PlayerToolController.SelectFirstAvailableTool() 호출.
    public bool ClearToolSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= toolSlots.Count)
            return false;

        bool wasCurrentSlot = currentSlotIndex == slotIndex;
        toolSlots[slotIndex] = null;
        if (lastWeaponSlotIndex == slotIndex)
            lastWeaponSlotIndex = -1;

        if (wasCurrentSlot)
        {
            currentTool = null;
            currentSlotIndex = -1;

            if (!SelectFirstAvailableTool())
            {
                CurrentToolChanged?.Invoke(null, -1);
            }
        }

        ToolSlotsChanged?.Invoke();
        return true;
    }

    public void ClearTools()
    {
        bool hadCurrentTool = currentTool != null;

        toolSlots.Clear();
        currentTool = null;
        currentSlotIndex = -1;
        lastWeaponSlotIndex = -1;

        if (hadCurrentTool)
        {
            CurrentToolChanged?.Invoke(null, -1);
        }
        ToolSlotsChanged?.Invoke();
    }

    public bool SelectFirstAvailableTool()
    {
        for (int i = 0; i < toolSlots.Count; i++)
        {
            if (toolSlots[i] != null)
            {
                return SelectToolSlot(i);
            }
        }

        return false;
    }

    // 핵심 분기: toolSlots.Count == 0 판정.
    // 다음 연결: PlayerToolController.SelectFirstAvailableTool() 호출.
    private void SelectRelativeTool(int direction)
    {
        if (toolSlots.Count == 0)
        {
            Debug.LogWarning("선택할 수 있는 도구가 없습니다.");
            return;
        }

        if (currentSlotIndex < 0 || currentSlotIndex >= toolSlots.Count)
        {
            SelectFirstAvailableTool();
            return;
        }

        for (int offset = 1; offset <= toolSlots.Count; offset++)
        {
            int slotIndex =
                (currentSlotIndex + direction * offset + toolSlots.Count) % toolSlots.Count;

            if (toolSlots[slotIndex] == null)
                continue;

            SelectToolSlot(slotIndex);
            return;
        }

        Debug.LogWarning("선택할 수 있는 도구가 없습니다.");
    }

    // 핵심 분기: toolSlots.Count == 0 판정.
    // 다음 연결: PlayerToolController.IsWeaponSlot(int) 호출.
    private bool SelectRelativeWeapon(int direction)
    {
        if (toolSlots.Count == 0)
        {
            Debug.LogWarning("선택할 수 있는 무기가 없습니다.", this);
            return false;
        }

        bool currentIsWeapon = currentTool != null
            && currentTool.IsWeapon
            && currentSlotIndex >= 0
            && currentSlotIndex < toolSlots.Count;

        if (!currentIsWeapon)
        {
            if (IsWeaponSlot(lastWeaponSlotIndex))
                return SelectToolSlot(lastWeaponSlotIndex);

            for (int i = 0; i < toolSlots.Count; i++)
            {
                if (IsWeaponSlot(i))
                    return SelectToolSlot(i);
            }

            Debug.LogWarning("선택할 수 있는 무기가 없습니다.", this);
            return false;
        }

        for (int offset = 1; offset <= toolSlots.Count; offset++)
        {
            int slotIndex =
                (currentSlotIndex + direction * offset + toolSlots.Count) % toolSlots.Count;

            if (!IsWeaponSlot(slotIndex))
                continue;

            if (slotIndex == currentSlotIndex)
                return true;

            return SelectToolSlot(slotIndex);
        }

        Debug.LogWarning("선택할 수 있는 무기가 없습니다.", this);
        return false;
    }

    private bool IsWeaponSlot(int slotIndex)
    {
        ToolData tool = GetToolAtSlot(slotIndex);
        return tool != null && tool.IsWeapon;
    }

    private void EnsureSlotExists(int slotIndex)
    {
        while (toolSlots.Count <= slotIndex)
        {
            toolSlots.Add(null);
        }
    }
}
