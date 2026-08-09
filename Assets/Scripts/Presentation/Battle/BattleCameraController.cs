using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

        [Header("피사계 심도 (줌 연동 — URP DepthOfField.focalLength)")]
        [Tooltip("Focal Length를 줌에 연동시킬 Volume — 비우면 연동하지 않는다 (Bokeh 모드 DepthOfField 필요)")]
        [SerializeField] private Volume depthOfFieldVolume;
        [Tooltip("최대로 확대(근접, minDistance)했을 때의 Focal Length")]
        [SerializeField, Range(0f, 100f)] private float focalLengthAtMinDistance = 100f;
        [Tooltip("최대로 축소(원경, maxDistance)했을 때의 Focal Length")]
        [SerializeField, Range(0f, 100f)] private float focalLengthAtMaxDistance = 0f;

        [Header("연출")]
        [Tooltip("줌 감쇠 계수(1/초) — 클수록 즉각적. 팬은 지면 고정이라 감쇠를 걸지 않는다")]
        [SerializeField] private float zoomDamping = 14f;
        [Tooltip("전투 개시 시 근접 구도에서 기본 구도로 물러나는 시간(초). 0이면 연출 없음")]
        [SerializeField] private float introSeconds = 1.6f;
        [Tooltip("셰이크가 잦아드는 속도(1/초)")]
        [SerializeField] private float shakeDecay = 6f;
        [Tooltip("셰이크 최대 진폭(월드 단위) — 누적값을 여기서 자른다")]
        [SerializeField] private float maxShake = 0.9f;

        private Camera _camera;
        private Vector3 _focus;
        /// <summary>줌 목표 거리. 실제 적용값은 <see cref="_appliedDistance"/>가 감쇠하며 따라간다.</summary>
        private float _distance;
        private float _appliedDistance;
        private Vector3 _defaultFocus;
        private float _defaultDistance;
        private float _introRemaining;
        private float _introFrom;
        private float _shake;
        private Vector3 _shakeOffset;

        // 입력 상태 (프레임 간 유지)
        private bool _pressing;
        private bool _dragging;
        private Vector2 _pressStartScreen;
        private Vector2 _lastScreen;
        private bool _pinching;
        private float _lastPinchDistance;
        private float _lastTapTime = -10f;

        /// <summary>depthOfFieldVolume에서 1회 조회해 캐시 — 못 찾으면 null(연동 비활성).</summary>
        private DepthOfField _depthOfField;

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
            // profile(공유 아님, 인스턴스 카피)로 조회해야 에셋 원본을 건드리지 않는다 —
            // Volume.profile은 sharedProfile을 처음 읽을 때 자동으로 인스턴스를 만들어 돌려준다.
            if (depthOfFieldVolume != null && depthOfFieldVolume.profile != null)
            {
                depthOfFieldVolume.profile.TryGet(out _depthOfField);
            }
            ResetView();
        }

        private void LateUpdate()
        {
            if (!HandlePinch()) // 핀치 중에는 드래그·탭 판정을 하지 않는다
            {
                HandleWheel();
                HandleDragAndTap();
            }
            UpdateIntro(Time.deltaTime);
            UpdateZoomDamping(Time.deltaTime);
            UpdateShake(Time.deltaTime);
            ApplyTransform();
            UpdateDepthOfField();
        }

        /// <summary>
        /// Focal Length를 현재 줌 거리(_appliedDistance)에 선형 연동한다 — 근접(minDistance)일수록
        /// focalLengthAtMinDistance, 원경(maxDistance)일수록 focalLengthAtMaxDistance (둘 다 인스펙터
        /// 설정 가능, 기본 100~0). 실제 카메라 위치를 결정하는 감쇠된 거리를 기준으로 삼아 DoF도
        /// 카메라 움직임과 같은 속도로 부드럽게 따라간다.
        /// </summary>
        private void UpdateDepthOfField()
        {
            if (_depthOfField == null)
            {
                return;
            }
            float t = maxDistance > minDistance
                ? Mathf.InverseLerp(minDistance, maxDistance, _appliedDistance)
                : 0f;
            _depthOfField.focalLength.value = Mathf.Lerp(focalLengthAtMinDistance, focalLengthAtMaxDistance, t);
        }

        /// <summary>
        /// 전투 개시 연출 시작 — 근접에서 기본 구도로 물러난다. 전투 시작 시점에 외부(부트스트랩)가 부른다.
        /// </summary>
        public void PlayIntro()
        {
            if (introSeconds <= 0f)
            {
                return;
            }
            _introFrom = minDistance;
            _introRemaining = introSeconds;
            _appliedDistance = _introFrom;
            ApplyTransform();
        }

        /// <summary>화면 흔들기 누적 — 스킬 시전 같은 강조 순간에 부른다. 진폭은 상한에서 잘린다.</summary>
        public void AddShake(float strength)
        {
            _shake = Mathf.Min(_shake + Mathf.Max(strength, 0f), maxShake);
        }

        private void CancelIntro()
        {
            _introRemaining = 0f;
        }

        private void UpdateIntro(float deltaTime)
        {
            if (_introRemaining <= 0f)
            {
                return;
            }
            _introRemaining -= deltaTime;
            float progress = introSeconds > 0f ? 1f - Mathf.Clamp01(_introRemaining / introSeconds) : 1f;
            _appliedDistance = Mathf.Lerp(_introFrom, _distance, Mathf.SmoothStep(0f, 1f, progress));
        }

        private void UpdateZoomDamping(float deltaTime)
        {
            if (_introRemaining > 0f)
            {
                return; // 인트로가 거리 제어권을 갖는 동안에는 감쇠를 건너뛴다
            }
            // 프레임레이트 독립 감쇠 — deltaTime이 커도 오버슈트하지 않는다.
            float t = zoomDamping > 0f ? 1f - Mathf.Exp(-zoomDamping * deltaTime) : 1f;
            _appliedDistance = Mathf.Lerp(_appliedDistance, _distance, t);
        }

        private void UpdateShake(float deltaTime)
        {
            if (_shake <= 0.0001f)
            {
                _shake = 0f;
                _shakeOffset = Vector3.zero;
                return;
            }
            _shake *= Mathf.Exp(-shakeDecay * deltaTime);
            // 카메라 로컬 평면(상/우)으로만 흔든다 — 앞뒤로 흔들면 줌이 흔들리는 것처럼 보인다.
            _shakeOffset = (transform.right * Random.Range(-1f, 1f) + transform.up * Random.Range(-1f, 1f)) * _shake;
        }

        // ── 카메라 연산 (입력과 분리 — 검증·외부 호출 가능) ──

        /// <summary>초점을 지면 XZ로 이동 (경계 클램프).</summary>
        public void PanBy(Vector3 groundDelta)
        {
            CancelIntro(); // 조작이 들어오면 연출보다 플레이어 의도가 우선
            _focus += new Vector3(groundDelta.x, 0f, groundDelta.z);
            _focus.x = Mathf.Clamp(_focus.x, -boundsHalfX, boundsHalfX);
            _focus.z = Mathf.Clamp(_focus.z, -boundsHalfZ, boundsHalfZ);
            ApplyTransform();
        }

        /// <summary>초점까지의 거리를 설정 (범위 클램프). 배율 줌은 현재 거리 × 비율로 호출.</summary>
        public void SetDistance(float distance)
        {
            CancelIntro();
            _distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        /// <summary>줌 목표 거리 — 감쇠 중인 실제 거리가 아니라 목표값이다 (조작·테스트 기준).</summary>
        public float Distance => _distance;

        /// <summary>기본 구도(씬 저장 자세)로 복귀.</summary>
        public void ResetView()
        {
            CancelIntro();
            _focus = _defaultFocus;
            _distance = _defaultDistance;
            _appliedDistance = _defaultDistance;
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            // 회전 고정 — 초점에서 시선 반대 방향으로 거리만큼 물러난 위치. 셰이크는 마지막에 얹는다.
            transform.position = _focus - transform.forward * _appliedDistance + _shakeOffset;
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
