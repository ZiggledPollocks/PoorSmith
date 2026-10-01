// [코드 지도] CraftGestureInput: 포인터 동작을 도구별 클릭·상향·왕복 제스처로 변환한다.
// 주요 함수: OnDrag, OnPointerDown, OnPointerUp
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/UI/CraftGestureInput.cs.md

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blacksmith
{
    /// <summary>Turns pointer movement into crafting gesture events.</summary>
    public class CraftGestureInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public Action Click, Upstroke, Roundtrip, Released;
        public bool Holding { get; private set; }

        float origin, last, travel;
        int direction, turns;
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left)
                return;
            Holding = true;
            origin = last = e.position.y;
            travel = 0;
            direction = turns = 0;
            Click?.Invoke();
        }

        // 핵심 분기: !Holding 판정.
        // 상태 변경: last 갱신.
        public void OnDrag(PointerEventData e)
        {
            if (!Holding)
                return;
            float dy = e.position.y - last;
            last = e.position.y;
            float threshold = 40 * Mathf.Max(.4f, Screen.height / 900f);
            if (e.position.y - origin > threshold)
            {
                Upstroke?.Invoke();
                origin = e.position.y;
            }

            if (dy < 0)
                origin = Mathf.Min(origin, e.position.y);
            if (Mathf.Abs(dy) < .5f)
                return;
            int dir = dy > 0 ? 1 : -1;
            if (direction == 0)
                direction = dir;
            if (direction == dir)
                travel += Mathf.Abs(dy);
            else if (travel >= threshold)
            {
                turns++;
                direction = dir;
                travel = Mathf.Abs(dy);
            }
            else
            {
                direction = dir;
                travel = 0;
            }

            if (turns >= 1 && travel >= threshold)
            {
                Roundtrip?.Invoke();
                turns = 0;
                travel = 0;
                direction = 0;
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!Holding)
                return;
            Holding = false;
            Released?.Invoke();
        }

        void OnDisable()
        {
            Holding = false;
        }
    }
}