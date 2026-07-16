using TMPro;
using UnityEngine;

namespace NHN.Presentation.Battle
{
    /// <summary>전투 결과 표시 + 재시작 버튼.</summary>
    public sealed class BattleHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text resultText;

        private BattleTestBootstrap _bootstrap;

        public void Initialize(BattleTestBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
        }

        public void Clear()
        {
            resultText.text = string.Empty;
        }

        public void ShowResult(string message)
        {
            resultText.text = message;
        }

        /// <summary>씬의 Restart 버튼 OnClick(persistent listener)에서 호출된다.</summary>
        public void OnRestartButton()
        {
            _bootstrap.StartBattle();
        }
    }
}
