using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LineRenderer))]
public class ShipController : MonoBehaviour
{
    public enum ShipState { Flying, Docking, Landed }
    public ShipState currentState = ShipState.Flying;
    private Transform dockingTarget; // 착륙장 중심점

    [Header("이동 및 회전 설정")]
    public float speed = 1.5f;        // 직진 속도
    public float turnSpeed = 90f;     // 회전 속도 (초당 도)
    public float predictionLength = 5f; // 선을 그릴 때 앞을 내다볼 물리적인 총 길이

    [Header("착륙 설정")]
    public float landedWaitTime = 1.5f; // 착륙 후 스택으로 넘어가기 전 대기 시간
    private float landedTimer = 0f;

    private LineRenderer lr;
    private bool isDragging = false;
    private Vector3 predictionTarget;
    private Camera mainCamera;
    
    // 우주선이 회전해야 할 '목표 각도'를 저장하는 변수
    private float currentTargetAngle;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        mainCamera = Camera.main;

        lr.positionCount = 0;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.useWorldSpace = true;
        
        // 처음 스폰됐을 때는 자신의 현재 각도를 목표로 둠
        currentTargetAngle = transform.eulerAngles.z;

        // 1. Z축 0으로 강제 고정 (투명 박스와 Z축 깊이가 다르면 허공을 가름)
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        // 2. 강제로 Rigidbody2D 셋업 (없으면 충돌 자체가 안 일어남)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic; // 충돌 감지가 가장 확실한 Dynamic 모드
        rb.gravityScale = 0f; // 중력을 0으로 설정해 밑으로 안 떨어지게 함
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 뚫고 지나가는 버그 방지

        // 3. 콜라이더가 빠져있다면 강제로 원형 콜라이더 부착
        if (GetComponent<Collider2D>() == null)
        {
            gameObject.AddComponent<CircleCollider2D>();
        }
    }

    void Update()
    {
        if (currentState == ShipState.Flying)
        {
            // 우주선 본체가 즉시 꺾이는 게 아니라, turnSpeed에 맞춰 천천히 둥글게 회전함
            float currentAngle = transform.eulerAngles.z;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, currentTargetAngle, turnSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0, 0, newAngle);

            // 1. 우주선은 자신의 위쪽(Y축) 방향으로 항상 직진
            transform.Translate(Vector3.up * speed * Time.deltaTime);

            if (Mouse.current == null) return;

            Vector3 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
            mouseWorldPos.z = 0;

            // 2. 마우스를 처음 클릭했을 때 (Collider2D 필수)
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero);
                if (hit.collider != null && hit.collider.gameObject == gameObject)
                {
                    isDragging = true;
                }
            }

            // 3. 마우스를 드래그하는 중일 때
            if (isDragging && Mouse.current.leftButton.isPressed)
            {
                predictionTarget = mouseWorldPos;
                DrawPredictionCurve();
            }

            // 4. 마우스를 놓았을 때
            if (isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isDragging = false;
                lr.positionCount = 0; // 궤적 선 지우기

                Vector3 dir = predictionTarget - transform.position;
                if (dir.sqrMagnitude > 0.1f)
                {
                    // 마우스를 놨을 때 '목표 각도'만 업데이트시켜 천천히 회전하게 만듦
                    currentTargetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                }
            }
        }
        else if (currentState == ShipState.Docking)
        {
            // 착륙장 중앙으로 이동
            transform.position = Vector3.MoveTowards(transform.position, dockingTarget.position, speed * Time.deltaTime);
            
            // 크기를 75%까지 서서히 축소
            Vector3 targetScale = new Vector3(0.75f, 0.75f, 1f);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 3f);

            // 도킹 완료 시 Landed 상태 전환
            if (Vector3.Distance(transform.position, dockingTarget.position) < 0.05f)
            {
                currentState = ShipState.Landed;
                Debug.Log("<color=cyan>[도킹 완료] 패드 중앙 도착! 대기 타이머 시작.</color>");
            }
        }
        else if (currentState == ShipState.Landed)
        {
            // 착륙 완료 후 지정된 시간 대기
            landedTimer += Time.deltaTime;
            if (landedTimer >= landedWaitTime)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddStandbyShip();
                }
                else
                {
                    Debug.LogWarning("[경고] GameManager가 씬에 없습니다! 스택을 올릴 수 없습니다.");
                }
                
                Destroy(gameObject);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 1단계: 뭔가에 닿기라도 했는지 확인
        Debug.Log($"<color=yellow>[충돌 감지]</color> 닿은 오브젝트: {other.gameObject.name} / 태그: {other.tag}");

        if (currentState != ShipState.Flying) return;

        // 2단계: 태그가 정확히 EntryPoint인지 확인
        if (other.CompareTag("EntryPoint"))
        {
            if (other.transform.parent == null)
            {
                Debug.LogError("<color=red>[에러]</color> EntryPoint에 부모(착륙장)가 없습니다! Hierarchy에서 조립 상태를 확인하세요.");
                return;
            }

            Transform padTransform = other.transform.parent;
            
            // 우주선과 착륙장 각도 비교
            float myAngle = transform.eulerAngles.z;
            float padAngle = padTransform.eulerAngles.z;
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(myAngle, padAngle));

            // 3단계: 각도 계산 결과 확인
            Debug.Log($"<color=orange>[각도 계산]</color> 우주선: {myAngle:F1}도 / 패드: {padAngle:F1}도 / 오차: {angleDiff:F1}도");

            // 오차가 25도 이내면 도킹 승인
            if (angleDiff <= 25f)
            {
                Debug.Log("<color=green>[착륙 승인!]</color> 각도가 일치하여 도킹을 시작합니다.");
                currentState = ShipState.Docking;
                dockingTarget = padTransform;
                
                isDragging = false;
                lr.positionCount = 0;
            }
            else
            {
                Debug.Log($"<color=red>[착륙 거부]</color> 오차가 25도를 넘어서 통과시킵니다.");
            }
        }
    }

    void DrawPredictionCurve()
    {
        float stepDistance = 0.1f; 
        int steps = Mathf.CeilToInt(predictionLength / stepDistance);
        if (steps > 500) steps = 500;
        
        lr.positionCount = steps;

        Vector3 simPos = transform.position;
        float simAngle = transform.eulerAngles.z;

        Vector3 dirToMouse = predictionTarget - transform.position;
        float targetAngle = Mathf.Atan2(dirToMouse.y, dirToMouse.x) * Mathf.Rad2Deg - 90f;

        for (int i = 0; i < steps; i++)
        {
            float stepTime = stepDistance / speed; 
            
            simAngle = Mathf.MoveTowardsAngle(simAngle, targetAngle, turnSpeed * stepTime);
            Quaternion rotation = Quaternion.Euler(0, 0, simAngle);
            
            simPos += (rotation * Vector3.up) * stepDistance;
            lr.SetPosition(i, simPos);
        }
    }
}