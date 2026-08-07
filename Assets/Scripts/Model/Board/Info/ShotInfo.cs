using Arcs;
using Obstacles;
using Ship;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Upgrade;

namespace BoardTools
{
    public class ShotInfo : GenericShipDistanceInfo
    {
        public bool IsShotAvailable { get; private set; }
        public bool InArc { get { return InArcInfo.Any(n => n.Value == true); } }
        public bool InPrimaryArc { get { return InArcByType(ArcType.Front); } }
        public RangeHolder NearestFailedDistance;
        
        private Dictionary<GenericArc, bool> InArcInfo { get; set; } = new Dictionary<GenericArc, bool>();
        private Dictionary<GenericArc, bool> InSectorInfo { get; set; } = new Dictionary<GenericArc, bool>();
        
        public bool IsObstructedByObstacle { get { return ObstructedByObstacles.Count > 0; } }
        public bool IsObstructedByBombToken { get; private set; }
        
        public List<GenericObstacle> ObstructedByObstacles { get; private set; } = new List<GenericObstacle>();
        public List<GenericShip> ObstructedByShips { get; private set; } = new List<GenericShip>();
        
        public IShipWeapon Weapon { get; private set; }
        public float DistanceReal { get { return MinDistance != null ? MinDistance.DistanceReal : 0f; } }
        
        private Action CallBack;
        private int updatesCount;
        private GameObject FiringLine;
        public List<GenericArc> ShotAvailableFromArcs { get; private set; } = new List<GenericArc>();

        public new int Range
        {
            get
            {
                int range = (MinDistance != null) ? MinDistance.Range : (NearestFailedDistance != null ? NearestFailedDistance.Range : 99);
                if (OnRangeIsMeasured != null) OnRangeIsMeasured(Ship1, Ship2, Weapon, ref range);
                return range;
            }
        }

        public delegate void EventHandlerShipShipWeaponInt(GenericShip thisShip, GenericShip anotherShip, IShipWeapon chosenWeapon, ref int range);
        public static event EventHandlerShipShipWeaponInt OnRangeIsMeasured;

        // COSTRUTTORE PER IL POOL
        internal ShotInfo() : base(null, null)
        {
            // Le collezioni sono già inizializzate inline
        }

        // COSTRUTTORI PUBBLICI ESISTENTI
        public ShotInfo(GenericShip ship1, GenericShip ship2, IShipWeapon weapon) : base(ship1, ship2)
        {
            ResetAndInitialize(ship1, ship2, weapon);
        }

        public ShotInfo(GenericShip ship1, GenericShip ship2, List<PrimaryWeaponClass> weapons) : base(ship1, ship2)
        {
            IShipWeapon weapon = (weapons != null && weapons.Count > 0) ? weapons.First() : ship1.PrimaryWeapons.First();
            ResetAndInitialize(ship1, ship2, weapon);
        }

        // METODI DI GESTIONE POOL
        public void ResetAndInitialize(GenericShip ship1, GenericShip ship2, IShipWeapon weapon)
        {
            if (ship1 == null || ship2 == null)
            {
                Debug.LogError($"[ShotInfo] Attempted to initialize with null ships! ship1={(ship1 == null ? "null" : ship1.PilotInfo?.PilotName)}, ship2={(ship2 == null ? "null" : ship2.PilotInfo?.PilotName)}");
                return;
            }

            Ship1 = ship1;
            Ship2 = ship2;

            // Reset completo dello stato locale.
            // IMPORTANTE: fatto prima della validazione dell'arma, così se il metodo
            // esce presto l'oggetto non resta con dati del ciclo precedente.
            Weapon = null;
            InArcInfo.Clear();
            InSectorInfo.Clear();
            ObstructedByObstacles.Clear();
            ObstructedByShips.Clear();
            ShotAvailableFromArcs.Clear();

            IsShotAvailable = false;
            IsObstructedByBombToken = false;
            MinDistance = null;
            NearestFailedDistance = null;

            if (weapon == null)
            {
                if (ship1.PrimaryWeapons == null || !ship1.PrimaryWeapons.Any())
                {
                    Debug.LogError($"[ShotInfo] Ship {ship1.PilotInfo?.PilotName} has no primary weapons!");
                    return;
                }

                weapon = ship1.PrimaryWeapons.First();
            }

            Weapon = weapon;

            CheckRange();
            CheckFailed();
        }

        public void ClearData()
        {
            InArcInfo.Clear();
            InSectorInfo.Clear();
            ObstructedByObstacles.Clear();
            ObstructedByShips.Clear();
            ShotAvailableFromArcs.Clear();
            
            IsShotAvailable = false;
            NearestFailedDistance = null;
            Weapon = null;
        }

        // LOGICA ORIGINALE CON CONTROLLI NULL
           private void CheckRange()
    {
        // Controllo di sicurezza aggiuntivo
        if (Ship1 == null || Ship2 == null)
        {
            Debug.LogError("[ShotInfo.CheckRange] Ship1 or Ship2 is null!");
            return;
        }
        if (Ship1.ArcsInfo == null || Ship1.SectorsInfo == null)
        {
            Debug.LogError($"[ShotInfo.CheckRange] Ship {Ship1.PilotInfo?.PilotName} has no ArcsInfo or SectorsInfo!");
            return;
        }
    
        // FIX 2: NON creare nuove istanze! Usa .Clear() per riutilizzare i dizionari del Pool
        InArcInfo.Clear();
        InSectorInfo.Clear();
    
        foreach (var arc in Ship1.ArcsInfo.Arcs)
        {
            // ATTENZIONE: Anche qui c'è una "new ShotInfoArc". 
            // Per ora lo lasciamo così perché ShotInfoArc non è ancora nel Pool, 
            // ma è il prossimo candidato per l'ottimizzazione se i GC Spikes persistono.
            ShotInfoArc shotInfoArc = new ShotInfoArc(Ship1, Ship2, arc);
            InArcInfo.Add(arc, shotInfoArc.InArc);
        }
            
            List<GenericArc> sectorsAndTurrets = new List<GenericArc>();
            sectorsAndTurrets.AddRange(Ship1.SectorsInfo.Arcs);
            sectorsAndTurrets.AddRange(Ship1.ArcsInfo.Arcs.Where(a => a.ArcType == ArcType.SingleTurret));
            
            foreach (var arc in sectorsAndTurrets)
            {
                ShotInfoArc shotInfoArc = new ShotInfoArc(Ship1, Ship2, arc, Weapon);
                InSectorInfo.Add(arc, shotInfoArc.InArc);
                
                if (Weapon.WeaponInfo.ArcRestrictions.Count > 0 && !Weapon.WeaponInfo.ArcRestrictions.Contains(arc.ArcType))
                    continue;
                    
                bool result = shotInfoArc.IsShotAvailable;
                if (arc.ArcType == ArcType.Bullseye) Ship1.CallOnBullseyeArcCheck(Ship2, ref result);
                
                if (result)
                {
                    if (IsShotAvailable == false)
                    {
                        MinDistance = shotInfoArc.MinDistance;
                        ObstructedByShips = shotInfoArc.ObstructedByShips;
                        ObstructedByObstacles = shotInfoArc.ObstructedByObstacles;
                        IsObstructedByBombToken = shotInfoArc.IsObstructedByBombToken;
                    }
                    else
                    {
                        if (shotInfoArc.MinDistance.DistanceReal < MinDistance.DistanceReal)
                        {
                            MinDistance = shotInfoArc.MinDistance;
                            ObstructedByShips = shotInfoArc.ObstructedByShips;
                            ObstructedByObstacles = shotInfoArc.ObstructedByObstacles;
                            IsObstructedByBombToken = shotInfoArc.IsObstructedByBombToken;
                        }
                    }
                    IsShotAvailable = true;
                    if (!(arc is ArcBullseye) || (Weapon.WeaponInfo.ArcRestrictions.Count > 0 && Weapon.WeaponInfo.ArcRestrictions.Contains(ArcType.Bullseye)))
                    {
                        ShotAvailableFromArcs.Add(arc);
                    }
                }
                
                if (NearestFailedDistance == null)
                {
                    NearestFailedDistance = shotInfoArc.MinDistance;
                }
                else if (shotInfoArc.MinDistance.DistanceReal < NearestFailedDistance.DistanceReal)
                {
                    NearestFailedDistance = shotInfoArc.MinDistance;
                }
            }

            if (Weapon.WeaponInfo.CanShootOutsideArc)
            {
                DistanceInfo distInfo = DistanceInfoPool.Get(Ship1, Ship2);
                try 
                {
                    if (distInfo.Range < 4)
                    {
                        MinDistance = distInfo.MinDistance;
                        IsShotAvailable = true;
                    }
                    else
                    {
                        NearestFailedDistance = distInfo.MinDistance;
                    }
                }
                finally 
                {
                    DistanceInfoPool.Return(distInfo);
                }
            }
        }

        private void CheckFailed()
        {
            if (MinDistance == null) MinDistance = NearestFailedDistance;
        }

        public bool InArcByType(ArcType arcType)
        {
            var filteredInfo = InArcInfo.Where(a => a.Key.ArcType == arcType).ToDictionary(a => a.Key, a => a.Value);
            if (filteredInfo == null || filteredInfo.Count == 0) return false;
            foreach (var arcInfo in filteredInfo)
            {
                if (arcInfo.Value) return true;
            }
            return false;
        }
    }
}