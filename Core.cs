using ProjectM;
using Stunlock.Core;
using System.Collections.Generic;
using Unity.Entities;

namespace NameOfYourMod
{
    internal static class Core
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

        public static PrefabCollectionSystem PrefabCollectionSystem
        {
            get
            {
                var server = Server;
                return server != null
                    ? server.GetExistingSystemManaged<PrefabCollectionSystem>()
                    : null;
            }
        }

        private static Dictionary<PrefabGUID, string> _prefabGuidsToNames;

        public static Dictionary<PrefabGUID, string> PrefabGuidsToNames
        {
            get
            {
                if (_prefabGuidsToNames == null)
                    InitializePrefabGuidsToNames();
                return _prefabGuidsToNames;
            }
        }

        private static void InitializePrefabGuidsToNames()
        {
            _prefabGuidsToNames = new Dictionary<PrefabGUID, string>();
            var prefabSystem = PrefabCollectionSystem;
            if (prefabSystem != null)
            {
                foreach (var kvp in prefabSystem.SpawnableNameToPrefabGuidDictionary)
                {
                    _prefabGuidsToNames[kvp.Value] = kvp.Key;
                }
            }
        }
    }


}
