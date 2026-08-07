using Ship;
using System.Linq;
using UnityEngine;

namespace BoardTools
{
    public class DistanceInfo : GenericShipDistanceInfo
    {
        // Costruttore per il Pool (internal per risolvere CS0122)
        internal DistanceInfo() : base(null, null) { }

        // Costruttore pubblico originale (retrocompatibilità)
        public DistanceInfo(GenericShip ship1, GenericShip ship2) : base(ship1, ship2)
        {
            ResetAndInitialize(ship1, ship2);
        }

      public void ResetAndInitialize(GenericShip ship1, GenericShip ship2)
        {
            if (ship1 == null || ship2 == null)
            {
                Debug.LogError("[DistanceInfo] Attempted to initialize with null ships!");
                return;
            }

            Ship1 = ship1;
            Ship2 = ship2;

            ResetBaseDistances();

            CheckRange();
        }

        public void ClearData()
        {
            // Pulizia eventuale se necessario
        }

        private void CheckRange()
        {
            if (Ship1 == null || Ship2 == null)
            {
                Debug.LogError("[DistanceInfo.CheckRange] Ship1 or Ship2 is null!");
                return;
            }
            
            if (Ship1.ShipBase == null)
            {
                Debug.LogError($"[DistanceInfo.CheckRange] Ship {Ship1.PilotInfo?.PilotName} has no ShipBase!");
                return;
            }
            
            FindNearestDistances(Ship1.ShipBase.GetBaseEdges().Values.ToList());
            TryFindPerpendicularDistanceA();
            TryFindPerpendicularDistanceB();
            SetFinalMinDistance();
        }
    }
}