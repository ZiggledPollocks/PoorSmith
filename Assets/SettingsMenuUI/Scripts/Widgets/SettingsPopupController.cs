// [코드 지도] SettingsPopupController: 설정 확인 팝업의 열기·닫기와 응답을 처리한다.
// 주요 함수: Fade, ShowWarning, OnDisable
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/SettingsMenuUI/Scripts/Widgets/SettingsPopupController.cs.md

using System.Collections;
using TMPro;
using UnityEngine;

namespace SettingsMenuUI
{
    [DisallowMultipleComponent]
    public sealed class SettingsPopupController : MonoBehaviour
    {
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text messageText;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.12f;
        [SerializeField, Min(0f)] private float visibleDuration = 1.6f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.22f;

        private Coroutine popupRoutine;

        public bool IsVisible => popupRoot != null && popupRoot.activeSelf;
        public string CurrentMessage => messageText != null ? messageText.text : string.Empty;

        public void Configure(GameObject root, CanvasGroup group, TMP_Text text)
        {
            popupRoot = root;
            canvasGroup = group;
            messageText = text;
        }

        public void ShowWarning(string message)
        {
            if (popupRoot == null || canvasGroup == null || messageText == null)
            {
                return;
            }

            if (popupRoutine != null)
            {
                StopCoroutine(popupRoutine);
            }

            messageText.text = message;
            popupRoot.SetActive(true);
            popupRoot.transform.SetAsLastSibling();
            popupRoutine = StartCoroutine(ShowRoutine());
        }

        private void OnDisable()
        {
            if (popupRoutine != null)
            {
                StopCoroutine(popupRoutine);
                popupRoutine = null;
            }

            if (popupRoot != null && popupRoot != gameObject)
            {
                popupRoot.SetActive(false);
            }
        }

        private IEnumerator ShowRoutine()
        {
            yield return Fade(0f, 1f, fadeInDuration);
            yield return new WaitForSecondsRealtime(visibleDuration);
            yield return Fade(1f, 0f, fadeOutDuration);
            popupRoot.SetActive(false);
            popupRoutine = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                canvasGroup.alpha = to;
                yield break;
            }

            double startedAt = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - startedAt < duration)
            {
                float elapsed = (float)(Time.realtimeSinceStartupAsDouble - startedAt);
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            canvasGroup.alpha = to;
        }
    }
}
