using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
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
