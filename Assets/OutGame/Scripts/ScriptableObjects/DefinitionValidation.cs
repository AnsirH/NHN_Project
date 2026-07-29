using System;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// ToData()/ToConfig()에서 반복되는 "검증 실패 시 에셋 이름을 포함한 InvalidOperationException으로
    /// 감싸 던진다" 패턴을 한 곳에 모은다 — fail-fast 원칙(§콘텐츠 결함은 로드 시점에 드러낸다)을
    /// 개별 Definition마다 다시 구현하다 빠뜨리는 것을 막기 위함.
    /// </summary>
    internal static class DefinitionValidation
    {
        public static void Validate(string assetName, Action validate)
        {
            try
            {
                validate();
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"{assetName}: 설정값이 유효하지 않습니다 — {e.Message}", e);
            }
        }
    }
}
