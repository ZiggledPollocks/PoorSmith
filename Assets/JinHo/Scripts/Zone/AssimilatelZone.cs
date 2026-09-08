using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AssimilatelZone : MonoBehaviour
{
    [Tooltip("Assimilate(1)을 호출하는 간격(초)")]
    [SerializeField, Min(0.01f)] private float assimilateAmountPerTick = 1f;

    private PlayerAssimilate currentPlayerAssimilation;
    private float elapsedTime;
    private int playerColliderCount;

    private void Update()
    {
        if (currentPlayerAssimilation == null)
            return;

        elapsedTime += Time.deltaTime;
        float intervalSeconds = Mathf.Max(0.01f, assimilateAmountPerTick);

        while (elapsedTime >= intervalSeconds)
        {
            elapsedTime -= intervalSeconds;
            currentPlayerAssimilation.Assimilate(1);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerAssimilate assimilation =
            other.GetComponentInParent<PlayerAssimilate>();

        if (assimilation == null)
            return;

        if (currentPlayerAssimilation != null &&
            currentPlayerAssimilation != assimilation)
            return;

        if (currentPlayerAssimilation == assimilation)
        {
            playerColliderCount++;
            return;
        }

        currentPlayerAssimilation = assimilation;
        playerColliderCount = 1;
        elapsedTime = 0f;

        Debug.Log("동화 구역 진입");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerAssimilate assimilation =
            other.GetComponentInParent<PlayerAssimilate>();

        if (assimilation == null || assimilation != currentPlayerAssimilation)
            return;

        playerColliderCount = Mathf.Max(0, playerColliderCount - 1);

        if (playerColliderCount > 0)
            return;

        currentPlayerAssimilation = null;
        elapsedTime = 0f;

        Debug.Log("동화 구역 이탈");
    }

    private void OnDisable()
    {
        currentPlayerAssimilation = null;
        elapsedTime = 0f;
        playerColliderCount = 0;
    }

    private void OnValidate()
    {
        assimilateAmountPerTick = Mathf.Max(0.01f, assimilateAmountPerTick);
    }
}
