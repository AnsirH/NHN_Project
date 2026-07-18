using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>버튼 클릭 시 지정된 씬으로 전환하는 범용 내비게이션 컴포넌트 (예: 맵 선택의 [뒤로]).</summary>
    [RequireComponent(typeof(Button))]
    public class SceneLoadButton : MonoBehaviour
    {
        [SerializeField] private string sceneName;

        /// <summary>테스트에서 실제 씬 전환 없이 호출을 가로챌 수 있게 하는 훅.</summary>
        public Action<string> LoadSceneAction = SceneManager.LoadScene;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new InvalidOperationException("SceneLoadButton의 sceneName이 비어 있습니다.");

            GetComponent<Button>().onClick.AddListener(() => LoadSceneAction(sceneName));
        }
    }
}
