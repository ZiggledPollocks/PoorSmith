using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerToolController : MonoBehaviour
{
    [Header("Tool Slots")]
    [SerializeField] private List<ToolData> toolSlots = new();
    [SerializeField, Min(0)] private int startingSlotIndex;

    [Header("Current Tool")]
    [SerializeField] private ToolData currentTool;

    private int currentSlotIndex = -1;

    public ToolData CurrentTool => currentTool;
    public int CurrentSlotIndex => currentSlotIndex;
    public int ToolSlotCount => toolSlots.Count;
    public IReadOnlyList<ToolData> ToolSlots => toolSlots;

    public event Action<ToolData, int> CurrentToolChanged;

    private void Start()
    {
        if (currentTool != null)
            return;

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
        CurrentToolChanged?.Invoke(currentTool, currentSlotIndex);

        Debug.Log($"현재 도구: {currentTool.ToolName} (슬롯 {currentSlotIndex})");
        return true;
    }

    public bool SelectToolType(ToolType toolType)
    {
        for (int i = 0; i < toolSlots.Count; i++)
        {
            ToolData tool = toolSlots[i];

            if (tool != null && tool.ToolType == toolType)
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

    public ToolData GetToolAtSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= toolSlots.Count)
            return null;

        return toolSlots[slotIndex];
    }

    public bool SetToolSlot(int slotIndex, ToolData toolData)
    {
        if (slotIndex < 0)
        {
            Debug.LogWarning("도구 슬롯 번호는 0 이상이어야 합니다.");
            return false;
        }

        EnsureSlotExists(slotIndex);
        toolSlots[slotIndex] = toolData;

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
                CurrentToolChanged?.Invoke(currentTool, currentSlotIndex);
            }
        }
        else if (currentTool == null && toolData != null)
        {
            SelectToolSlot(slotIndex);
        }

        return true;
    }

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

        return emptySlot;
    }

    public bool RemoveTool(ToolData toolData)
    {
        int slotIndex = toolSlots.IndexOf(toolData);

        if (slotIndex < 0)
            return false;

        return ClearToolSlot(slotIndex);
    }

    public bool ClearToolSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= toolSlots.Count)
            return false;

        bool wasCurrentSlot = currentSlotIndex == slotIndex;
        toolSlots[slotIndex] = null;

        if (wasCurrentSlot)
        {
            currentTool = null;
            currentSlotIndex = -1;

            if (!SelectFirstAvailableTool())
            {
                CurrentToolChanged?.Invoke(null, -1);
            }
        }

        return true;
    }

    public void ClearTools()
    {
        bool hadCurrentTool = currentTool != null;

        toolSlots.Clear();
        currentTool = null;
        currentSlotIndex = -1;

        if (hadCurrentTool)
        {
            CurrentToolChanged?.Invoke(null, -1);
        }
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

    private void EnsureSlotExists(int slotIndex)
    {
        while (toolSlots.Count <= slotIndex)
        {
            toolSlots.Add(null);
        }
    }
}
