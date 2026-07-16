namespace NHN.Simulation
{
    /// <summary>
    /// NHN.Simulation 어셈블리 계약:
    /// - UnityEngine 참조 금지 (asmdef noEngineReferences로 강제). 순수 dotnet 환경에서도 실행 가능해야 한다.
    /// - 수학 타입은 System.Numerics로 통일하며, 시뮬레이션 좌표계는 평면(Vector2)이다.
    /// - 고정 틱 + 시드 주입 결정론: 같은 입력과 시드는 항상 같은 결과를 낸다.
    /// - 뷰 변환은 NHN.Presentation.SimViewMapper 한 곳에서만 수행한다.
    /// </summary>
    public static class SimulationAssembly
    {
        public const string Name = "NHN.Simulation";
    }
}
