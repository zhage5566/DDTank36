using Game.Logic.AI;
using System;

namespace GameServerScript.AI.Game
{
    public class Game32 : APVEGameControl
    {
        public static Random random = new Random();

        public override void OnCreated()
        {
            int rand = random.Next(100);
            if (rand >= 80)
            {
                Game.SetupMissions("3201,3204");
            }
            else if (rand >= 40)
            {
                Game.SetupMissions("3201,3203");
            }
            else
            {
                Game.SetupMissions("3201,3202");
            }
            Game.TotalMissionCount = 2;
        }

        public override void OnPrepated()
        {
            base.OnPrepated();
        }

        public override int CalculateScoreGrade(int score)
        {
            if (score > 800)
            {
                return 3;
            }
            else if (score > 725)
            {
                return 2;
            }
            else if (score > 650)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public override void OnGameOverAllSession()
        {
            base.OnGameOverAllSession();
        }
    }
}