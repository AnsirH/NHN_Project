using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Maps;
using OutGame.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// UI 스크린샷 캡처 — 시각 검증 루프용 (unity-ui-design 스킬의 핵심 부품 프로토타입).
    /// 캡처 결과: 프로젝트 루트 ui-captures/*.png → Claude가 이미지를 읽고 디자인을 판단·수정한다.
    /// batchmode(렌더 프레임 없음)에서는 자동 스킵.
    /// </summary>
    public class UICaptureTests
    {
        private static string CaptureDir
        {
            get
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ui-captures"));
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        [UnityTest]
        public IEnumerator Capture_RoomMapPanel_InitialAndMidRun()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("batchmode에서는 렌더 프레임이 없어 캡처 불가 — 에디터 열림 상태에서만 실행");
                yield break;
            }

            var canvasGo = new GameObject("CaptureCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            try
            {
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                GameObject prefab = Resources.Load<GameObject>("OutGame/RoomMapPanel");
                Assert.IsNotNull(prefab, "RoomMapPanel 프리팹 없음 — SceneSetupM2.Run() 실행 필요");
                var panel = Object.Instantiate(prefab, canvasGo.transform).GetComponent<RoomMapPanel>();

                MapState map = new MapGenerator(new MapGenerationConfig(), seed: 42).Generate();
                panel.Open(map);

                yield return CaptureToFile("RoomMapPanel_01_initial.png");

                // 2개 방 방문 후 상태 (현재/방문/선택가능/잠김 + 경로 강조 확인용)
                MapProgress.Visit(map, MapProgress.GetSelectableNodes(map)[0].point);
                MapProgress.Visit(map, MapProgress.GetSelectableNodes(map)[0].point);
                panel.Refresh();

                yield return CaptureToFile("RoomMapPanel_02_midrun.png");
            }
            finally
            {
                Object.Destroy(canvasGo);
            }
        }

        private static IEnumerator CaptureToFile(string fileName)
        {
            yield return new WaitForEndOfFrame(); // 렌더 완료 시점에 백버퍼 캡처

            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(CaptureDir, fileName), texture.EncodeToPNG());
                Debug.Log($"[UICapture] 저장: ui-captures/{fileName} ({texture.width}x{texture.height})");
            }
            finally
            {
                Object.Destroy(texture);
            }
        }
    }
}
