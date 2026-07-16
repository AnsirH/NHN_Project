namespace NHN.Simulation.Battle
{
    /// <summary>전투 출력. Winner: 0=A군, 1=B군, -1=무승부(시간 상한 도달).</summary>
    public readonly struct BattleResult
    {
        public const int DrawWinner = -1;

        public readonly int Winner;
        public readonly int SurvivorsTeamA;
        public readonly int SurvivorsTeamB;
        public readonly int ElapsedTicks;

        public BattleResult(int winner, int survivorsTeamA, int survivorsTeamB, int elapsedTicks)
        {
            Winner = winner;
            SurvivorsTeamA = survivorsTeamA;
            SurvivorsTeamB = survivorsTeamB;
            ElapsedTicks = elapsedTicks;
        }
    }
}
