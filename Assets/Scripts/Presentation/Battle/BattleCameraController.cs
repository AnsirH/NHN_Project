using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NHN.Presentation.Battle
{
    /// <summary>
    /// 전투 관전 카메라 (뷰 전용 — 시뮬 무관, 2026-08-02 설계 합의):
    /// 한 손가락(마우스) 드래그 = 팬, 핀치/휠 = 줌, 더블탭 = 기본 구도 복귀.
    /// 앵글(회전)은 고정 — 씬에 저장된 시작 자세가 곧 기본 구도(줌 최대)다.
    /// 팬은 지면 고정 방식: 손가락 아래 지면 지점이 손가락을 따라오도록 초점을 옮긴다.
    /// 스킬 시전(탭)과의 구분은 <see cref="DragThresholdPixels"/> 공유 문턱으로 한다 —
    /// 문턱 이상 움직이면 팬(시전 취소), 움직임 없이 떼면 탭(BattleTestBootstrap 쪽 판정).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BattleCameraController : MonoBehaviour
    {
        /// <summary>탭/드래그 판정 문턱(픽셀) — 스킬 시전(BattleTestBootstrap)과 공유하는 상수.</summary>
        public const float DragThresholdPixels = 20f;

        [Header("줌 (초점까지의 거리)")]
        [Tooltip("최소 거리 — 유닛 개체·애니메이션이 잘 보이는 근접")]
        [SerializeField] private float minDistance = 8f;
        [Tooltip("최대 거리 — 0이면 씬에 저장된 시작 구도의 거리를 그대로 사용")]
        [SerializeField] private float maxDistance;
        [Tooltip("마우스 휠 한 칸당 거리 변화 (에디터 테스트용)")]
        [SerializeField] private float wheelZoomStep = 3f;

        [Header("팬 경계 (초점이 머물 수 있는 범위 — 전장 80x60 + 여유)")]
        [SerializeField] private float boundsHalfX = 42f;
        [SerializeField] private float boundsHalfZ = 32f;

        [Header("더블탭 기본 구도 복귀")]
        [SerializeField] private float doubleTapSeconds = 0.3f;

        private Camera _camera;
        private Vector3 _focus;
        private float _distance;
        private Vector3 _defaultFocus;
        private float _defaultDistance;

        // 입력 상태 (프레임 간 유지)
        private bool _pressing;
        private bool _dragging;
        private Vector2 _pressStartScreen;
        private Vector2 _lastScreen;
        private bool _pinching;
        private float _lastPinchDistance;
        private float _lastTapTime = -10f;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            // 씬에 저장된 자세에서 기본 구도 도출: 화면 중앙 광선의 지면(y=0) 교점 = 초점.
            Ray center = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float t = -center.origin.y / Mathf.Min(center.direction.y, -1e-4f);
            _defaultFocus = center.origin + center.direction * t;
            _defaultDistance = Vector3.Distance(transform.position, _defaultFocus);
            if (maxDistance <= 0f)
            {
                maxDistance = _defaultDistance;
            }
            ResetView();
        }

        private void LateUpdate()
        {
            if (HandlePinch())
            {
                return; // 핀치 중에는 드래그·탭 판정을 하지 않는다
            }
            HandleWheel();
            HandleDragAndTap();
        }

        // ── 카메라 연산 (입력과 분리 — 검증·외부 호출 가능) ──

        /// <summary>초점을 지면 XZ로 이동 (경계 클램프).</summary>
        public void PanBy(Vector3 groundDelta)
        {
            _focus += new Vector3(groundDelta.x, 0f, groundDelta.z);
            _focus.x = Mathf.Clamp(_focus.x, -boundsHalfX, boundsHalfX);
            _focus.z = Mathf.Clamp(_focus.z, -boundsHalfZ, boundsHalfZ);
            ApplyTransform();
        }

        /// <summary>초점까지의 거리를 설정 (범위 클램프). 배율 줌은 현재 거리 × 비율로 호출.</summary>
        public void SetDistance(float distance)
        {
            _distance = Mathf.Clamp(distance, minDistance, maxDistance);
            ApplyTransform();
        }

        public float Distance => _distance;

        /// <summary>기본 구도(씬 저장 자세)로 복귀.</summary>
        public void ResetView()
        {
            _focus = _defaultFocus;
            _distance = _defaultDistance;
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            // 회전 고정 — 초점에서 시선 반대 방향으로 거리만큼 물러난 위치.
            transform.position = _focus - transform.forward * _distance;
        }

        // ── 입력 처리 ──

        private bool HandlePinch()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return false;
            }
            int active = 0;
            Vector2 p0 = default, p1 = default;
            foreach (var touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                {
                    continue;
                }
                if (active == 0) p0 = touch.position.ReadValue();
                else if (active == 1) p1 = touch.position.ReadValue();
                active++;
            }
            if (active < 2)
            {
                _pinching = false;
                return false;
            }

            float pinchDistance = Vector2.Distance(p0, p1);
            if (_pinching && _lastPinchDistance > 1f)
            {
                // 손가락이 벌어지면(비율 > 1) 가까워진다 — 거리 = 현재 거리 / 비율.
                SetDistance(_distance * (_lastPinchDistance / pinchDistance));
            }
            _pinching = true;
            _lastPinchDistance = pinchDistance;
            _pressing = false; // 두 손가락이 되면 진행 중이던 드래그·탭은 무효
            _dragging = false;
            return true;
        }

        private void HandleWheel()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                SetDistance(_distance - Mathf.Sign(scroll) * wheelZoomStep);
            }
        }

        private void HandleDragAndTap()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }
            Vector2 screen = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                // UI 위에서 시작한 터치는 카메라가 받지 않는다 (버튼 조작 보호).
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }
                _pressing = true;
                _dragging = false;
                _pressStartScreen = screen;
                _lastScreen = screen;
                return;
            }
            if (!_pressing)
            {
                return;
            }

            if (pointer.press.isPressed)
            {
                if (!_dragging
                    && (screen - _pressStartScreen).sqrMagnitude > DragThresholdPixels * DragThresholdPixels)
                {
                    _dragging = true; // 문턱 초과 — 이제부터 팬 (탭·시전은 부트스트랩 쪽에서 같은 문턱으로 취소)
                }
                if (_dragging && TryGround(_lastScreen, out Vector3 previous) && TryGround(screen, out Vector3 current))
                {
                    PanBy(previous - current); // 지면 고정: 손가락 아래 지점이 손가락을 따라온다
                }
                _lastScreen = screen;
                return;
            }

            // 릴리즈: 드래그가 아니었으면 탭 — 더블탭이면 기본 구도 복귀.
            _pressing = false;
            if (!_dragging)
            {
                if (Time.unscaledTime - _lastTapTime < doubleTapSeconds)
                {
                    ResetView();
                    _lastTapTime = -10f;
                }
                else
                {
                    _lastTapTime = Time.unscaledTime;
                }
            }
            _dragging = false;
        }

        /// <summary>화면 좌표 → 지면(y=0) 교점.</summary>
        private bool TryGround(Vector2 screen, out Vector3 ground)
        {
            Ray ray = _camera.ScreenPointToRay(screen);
            if (ray.direction.y > -1e-4f)
            {
                ground = default;
                return false;
            }
            float t = -ray.origin.y / ray.direction.y;
            ground = ray.origin + ray.direction * t;
            return true;
        }
    }
}
