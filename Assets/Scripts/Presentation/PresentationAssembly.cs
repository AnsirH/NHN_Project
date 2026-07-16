namespace NHN.Presentation
{
    public static class PresentationAssembly
    {
        public const string Name = "NHN.Presentation";

        // Presentation → Simulation/Data/Infra 정방향 asmdef 참조가 컴파일됨을 보장하는 마커.
        public const string ReferencedAssemblies =
            NHN.Simulation.SimulationAssembly.Name + "," +
            NHN.Data.DataAssembly.Name + "," +
            NHN.Infra.InfraAssembly.Name;
    }
}
