using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class StandbyController : MonoBehaviour, IEndDragHandler
{
    public GameObject shipPrefab; // 우주선 프리팹
    public TextMeshProUGUI countText; // 대기열 숫자 글자

    void Update()
    {
        if (GameManager.Instance != null && countText != null)
        {
            countText.text = "대기: " + GameManager.Instance.standbyCount;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 1. 마우스 위치를 월드 좌표로 바꿈
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0;

        // 2. GameManager에서 우주선 빼오기
        if (GameManager.Instance != null && GameManager.Instance.UseStandbyShip())
        {
            Instantiate(shipPrefab, worldPos, Quaternion.identity);
        }
    }
}