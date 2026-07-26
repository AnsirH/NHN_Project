using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutGame.Logic.Augments
{
    /// <summary>
    /// run이 선택한 증강 id 목록(§4-27)을 AugmentData로 해석한다 — 정보 팝업과 전투력 계산 등
    /// 여러 화면이 각자 이 변환을 구현하면 한쪽만 고치고 다른 쪽을 놓치는 불일치 버그가 난다
    /// (2026-07-26, ArmyInfoPopup과 AllyFormationView가 동일 로직을 따로 구현하고 있던 사례를
    /// 코드 리뷰로 발견해 공용 헬퍼로 추출).
    /// </summary>
    public static class AugmentSelectionResolver
    {
        public static List<AugmentData> Resolve(
            IReadOnlyList<string> selectedAugmentIds,
            IReadOnlyDictionary<string, AugmentData> augmentDataById)
        {
            if (selectedAugmentIds == null) throw new ArgumentNullException(nameof(selectedAugmentIds));
            if (augmentDataById == null) throw new ArgumentNullException(nameof(augmentDataById));

            var result = new List<AugmentData>();
            foreach (string augmentId in selectedAugmentIds)
            {
                if (augmentDataById.TryGetValue(augmentId, out AugmentData data))
                    result.Add(data);
                else
                    Debug.LogWarning($"[AugmentSelectionResolver] 정의되지 않은 증강 id '{augmentId}'는 제외합니다.");
            }
            return result;
        }
    }
}
