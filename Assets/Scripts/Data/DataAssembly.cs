using NHN.Simulation;

namespace NHN.Data
{
    public static class DataAssembly
    {
        public const string Name = "NHN.Data";

        // Data → Simulation 참조 방향이 컴파일됨을 보장하는 마커.
        public const string SimulationDependency = SimulationAssembly.Name;
    }
}
