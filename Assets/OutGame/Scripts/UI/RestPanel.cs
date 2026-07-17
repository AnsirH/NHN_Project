using System;
using System.Collections.Generic;
using OutGame.Logic.Rest;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 휴식 방 패널 (§5.5 재정의): 보유 군대 중 1개 선택 → 병사 수 영구 +20% 증원.
    /// </summary>
    public class RestPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform armyListContainer;
        [SerializeField] private Button armyOptionPrefab;
        [SerializeField] private Text resultText;
        [SerializeField] private Button continueButton;

        private readonly List<Button> spawnedOptions = new List<Button>();
        private RunState run;

        public event Action Completed;

        private void Awake()
        {
            if (armyListContainer == null || armyOptionPrefab == null || resultText == null || continueButton == null)
                throw new InvalidOperationException("RestPanel 프리팹의 필드가 배선되지 않았습니다.");

            continueButton.onClick.AddListener(OnContinueClicked);
            continueButton.gameObject.SetActive(false);
        }

        private void OnDestroy() => continueButton.onClick.RemoveListener(OnContinueClicked);

        public void Open(RunState runState, IReadOnlyDictionary<string, ArmyDefinition> armyDefsById)
        {
            if (runState == null) throw new ArgumentNullException(nameof(runState));
            if (armyDefsById == null) throw new ArgumentNullException(nameof(armyDefsById));

            run = runState;
            resultText.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);
            ClearOptions();

            foreach (ArmyInstance army in run.armies)
            {
                if (!armyDefsById.TryGetValue(army.armyDefId, out ArmyDefinition def))
                {
                    Debug.LogWarning($"[RestPanel] armyDefId '{army.armyDefId}'에 대한 ArmyDefinition을 찾을 수 없어 목록에서 제외합니다.");
                    continue;
                }

                var data = def.ToData();
                Button option = Instantiate(armyOptionPrefab, armyListContainer);
                option.GetComponentInChildren<Text>().text =
                    $"{data.displayName} ({data.baseSoldierCount + army.bonusSoldierCount}명)";
                option.onClick.AddListener(() => OnArmySelected(army, def));
                spawnedOptions.Add(option);
            }

            // 선택 가능한 군대가 없으면(데이터 누락 등) 소프트락 방지 — 바로 나갈 수 있게 한다
            if (spawnedOptions.Count == 0)
            {
                resultText.text = "증원할 수 있는 부대가 없습니다.";
                resultText.gameObject.SetActive(true);
                continueButton.gameObject.SetActive(true);
            }

            gameObject.SetActive(true);
        }

        private void OnArmySelected(ArmyInstance army, ArmyDefinition def)
        {
            var data = def.ToData();
            int added = RestService.Reinforce(army, data);

            foreach (Button option in spawnedOptions)
                if (option != null) option.gameObject.SetActive(false);

            resultText.text = $"{data.displayName} 부대에 {added}명이 증원되었습니다.";
            resultText.gameObject.SetActive(true);
            continueButton.gameObject.SetActive(true);
        }

        private void OnContinueClicked()
        {
            gameObject.SetActive(false);
            Completed?.Invoke();
        }

        private void ClearOptions()
        {
            foreach (Button option in spawnedOptions)
                if (option != null) Destroy(option.gameObject);
            spawnedOptions.Clear();
        }
    }
}
