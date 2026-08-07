using System.Collections.Generic;
using UnityEngine;
using Ship;

namespace BoardTools
{
    public static class DistanceInfoPool
    {
        private static readonly Stack<DistanceInfo> pool = new Stack<DistanceInfo>();
        private const int InitialCapacity = 30;
        private static bool isInitialized = false;

        private static void EnsureInitialized()
        {
            if (!isInitialized)
            {
                for (int i = 0; i < InitialCapacity; i++)
                {
                    pool.Push(new DistanceInfo());
                }
                isInitialized = true;
            }
        }

        public static DistanceInfo Get(GenericShip ship1, GenericShip ship2)
        {
            EnsureInitialized();
            DistanceInfo info = pool.Count > 0 ? pool.Pop() : new DistanceInfo();
            info.ResetAndInitialize(ship1, ship2);
            return info;
        }

        public static void Return(DistanceInfo info)
        {
            if (info != null)
            {
                info.ClearData();
                pool.Push(info);
            }
        }
    }
}