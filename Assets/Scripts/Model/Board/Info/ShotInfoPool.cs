using System.Collections.Generic;
using UnityEngine;
using Ship;
using Upgrade;

namespace BoardTools
{
    public static class ShotInfoPool
    {
        private static readonly Stack<ShotInfo> pool = new Stack<ShotInfo>();
        private const int InitialCapacity = 50;
        private static bool isInitialized = false;

        // Inizializzazione automatica al primo utilizzo (Lazy Initialization)
        private static void EnsureInitialized()
        {
            if (!isInitialized)
            {
                for (int i = 0; i < InitialCapacity; i++)
                {
                    pool.Push(new ShotInfo());
                }
                isInitialized = true;
                Debug.Log($"[ShotInfoPool] Inizializzato automaticamente con {InitialCapacity} oggetti pre-allocati.");
            }
        }

        public static ShotInfo Get(GenericShip ship1, GenericShip ship2, IShipWeapon weapon)
        {
            EnsureInitialized(); // Si assicura che il pool esista
            
            ShotInfo info;
            if (pool.Count > 0)
            {
                info = pool.Pop();
            }
            else
            {
                // Fallback di sicurezza: se il pool si esaurisce, ne crea uno nuovo al volo
                info = new ShotInfo();
            }

            info.ResetAndInitialize(ship1, ship2, weapon);
            return info;
        }

        public static void Return(ShotInfo info)
        {
            if (info != null)
            {
                info.ClearData(); // Pulisce i riferimenti per evitare memory leak
                pool.Push(info);
            }
        }
    }
}