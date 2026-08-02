using UnityEngine;

public class GameManager : MonoBehaviour
{
    // 어디서든 GameManager에 쉽게 접근할 수 있게 만드는 싱글톤 패턴
    public static GameManager Instance;

    [Header("대기열 정보")]
    public int standbyCount = 0; // 현재 스택에 대기 중인 우주선 수

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 우주선이 착륙을 완료하면 이 함수를 불러서 스택을 1개 올림
    public void AddStandbyShip()
    {
        standbyCount++;
        Debug.Log("우주선 스택 추가됨! 현재 대기열: " + standbyCount);
    }
    
    // 나중에 출항(이륙)할 때 스택을 1개 줄이는 함수
    public bool UseStandbyShip()
    {
        if (standbyCount > 0)
        {
            standbyCount--;
            Debug.Log("우주선 출항! 남은 대기열: " + standbyCount);
            return true;
        }
        return false;
    }
}