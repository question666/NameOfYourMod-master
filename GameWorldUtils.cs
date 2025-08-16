using ProjectM;
using Unity.Entities;

namespace NameOfYourMod
{
    public static class GameWorldUtils
    {
        public static World Server
        {
            get
            {
                foreach (var world in World.All)
                {
                    if (world.Name == "Server")
                        return world;
                }
                return null;
            }
        }
    }
}
