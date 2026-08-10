using ActionsList;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SubPhases
{

    public class ActionSubPhase : GenericSubPhase
    {

        public override void Start()
        {
            base.Start();

            Name = "Action SubPhase";
            RequiredInitiative = PreviousSubPhase.RequiredInitiative;
            RequiredPlayer = PreviousSubPhase.RequiredPlayer;
            CanBePaused = true;
            UpdateHelpInfo();
        }

        public override void Initialize()
        {
            Selection.ThisShip.CallPerformActionStepStart();
            Phases.Events.CallBeforeActionSubPhaseTrigger();
            var ship = Selection.ThisShip;

            if (RulesList.ActionsRule.HasPerformActionStep(ship))
            {
                ship.GenerateAvailableActionsList();
                Triggers.RegisterTrigger(
                    new Trigger() {
                        Name = "Action",
                        TriggerOwner = Phases.CurrentPhasePlayer,
                        TriggerType = TriggerTypes.OnActionSubPhaseStart,
                        EventHandler = StartActionDecisionSubphase
                    }
                );

                Phases.Events.CallOnActionSubPhaseTrigger();
            }
            else
            {
                ship.CallMovementActivationFinish(Phases.Events.CallOnActionSubPhaseTrigger);
            }
        }

        private void StartActionDecisionSubphase(object sender, System.EventArgs e)
        {
            Phases.StartTemporarySubPhaseOld(
                "Action Decision",
                typeof(ActionDecisonSubPhase),
                (Action)delegate {
                    ActionsHolder.TakeActionFinish(
                        delegate {
                            ActionsHolder.EndActionDecisionSubhase(Finish);
                        }
                    ); 
                }
            );
        }

        private void Finish()
        {
            UI.HideSkipButton();

            Phases.FinishSubPhase(typeof(ActionDecisonSubPhase));

            Selection.ThisShip.CallMovementActivationFinish(
                delegate {
                    (Phases.CurrentPhase as MainPhases.ActivationPhase).ActivationShip = null;
                    Triggers.FinishTrigger();
                }
            );
        }

        public override void Next()
        {
            FinishPhase();
        }

        public override void FinishPhase()
        {
            GenericSubPhase activationSubPhase = new ActivationSubPhase();
            Phases.CurrentSubPhase = activationSubPhase;
            Phases.CurrentSubPhase.Start();
            Phases.CurrentSubPhase.RequiredInitiative = RequiredInitiative;
            Phases.CurrentSubPhase.RequiredPlayer = RequiredPlayer;

            Phases.CurrentSubPhase.Next();
        }

        public override bool ThisShipCanBeSelected(Ship.GenericShip ship, int mouseKeyIsPressed)
        {
            bool result = false;
            Messages.ShowErrorToHuman(ship.PilotName + " cannot be selected, perform an action first");
            return result;
        }

    }

}

namespace SubPhases
{

    public class ActionDecisonSubPhase : DecisionSubPhase
    {
        public bool ActionWasPerformed { get; private set; }

        public override void PrepareDecision(System.Action callBack)
        {
            decisions.Clear();
            DescriptionShort = "Perform Action step";

            ShowSkipButton = true;
            DefaultDecisionName = "Focus";

            List<GenericAction> availableActions = Selection.ThisShip.GetAvailableActions();

            if (availableActions.Count > 0)
            {
                GenerateActionButtons();
                callBack();
            }
            else
            {
                if (!DecisionWasPreparedAndShown)
                {
                    Messages.ShowErrorToHuman("This ship cannot perform any actions");
                    ActionsHolder.CurrentAction = null;
                    CallBack();
                }
            }
        }

        public void GenerateActionButtons()
        {
            //TODO: Use more global way of fix
            HideDecisionWindowUI();

            List<GenericAction> availableActions = Selection.ThisShip.GetAvailableActions();
            foreach (var action in availableActions)
            {
                bool addedDecisionWithLink = false;

                string decisionName = GetActionNameColored(action);

                foreach (var kv in Selection.ThisShip.ActionBar.LinkedActions)
                {
                    Type actionType = kv.Key;
                    GenericAction linkedAction = kv.Value;

                    if (action.GetType() == actionType)
                    {
                        string linkedActionName = GetActionNameColored(kv.Value);

                        if (!addedDecisionWithLink)
                        {
                            decisionName += " > " + linkedActionName;
                            addedDecisionWithLink = true;
                        }
                        else
                        {
                            decisionName += " / " + linkedActionName;
                        }
                    }
                }

                AddDecision(
                    decisionName,
                    delegate {
                        ActionWasPerformed = true;
                        Selection.ThisShip.CallBeforeActionIsPerformed(
                            (GenericAction)action,
                            (Action)delegate { ActionsHolder.TakeActionStart((GenericAction)action); },
                            isFree: false
                        );
                    },
                    action.ImageUrl
                );
            }
        }

        private string GetActionNameColored(GenericAction action)
        {
            string actionName = action.Name;

            Actions.ActionColor actionColor = action.Color;
            Selection.ThisShip.CallOnCheckActionComplexity(action, ref actionColor);

            switch (actionColor)
            {
                case Actions.ActionColor.Red:
                    actionName = "<color=red>" + actionName + "</color>";
                    break;
                case Actions.ActionColor.Purple:
                    actionName = "<color=purple>" + actionName + "</color>";
                    break;
                default:
                    break;
            }

            return actionName;
        }

        public override void Resume()
        {
            base.Resume();

            UI.ShowSkipButton();
        }

        public override void SkipButton()
        {
            ActionsHolder.CurrentAction = null;
            CallBack();
        }

    }

}

namespace SubPhases
{

    public class FreeActionDecisonSubPhase : DecisionSubPhase
    {
        public bool ActionWasPerformed { get; private set; }

        public override void PrepareDecision(System.Action callBack)
        {
            DescriptionShort = DescriptionShort ?? "Select free action";

            List<GenericAction> availableActions = Selection.ThisShip.GetAvailableFreeActions();

            if (availableActions.Count > 0)
            {
                // FIX: "Focus" era hardcoded, indipendente da quali azioni libere
                // fossero davvero disponibili o da quanto fossero valide
                // tatticamente (es. dopo un Coordinate di Lieutenant Sai, che ora
                // alza la priorità di Coordinate stesso ma non tocca in alcun modo
                // quale azione libera risultante sia la migliore). Calcolato ora
                // con la stessa logica — GetActionPriority() + hook
                // Ai.CallGetActionPriority — già usata in
                // AggressorAiPlayer.PerformActionFromList per l'azione principale
                // della nave, così un'AI che riceve un'azione libera (da Coordinate,
                // da altre abilità, ecc.) sceglie davvero la migliore disponibile in
                // quel momento, non sempre e comunque Focus. Stesso principio già
                // in uso per ReinforceAction/ReinforceSideSubphase (DefaultDecisionName
                // calcolato dinamicamente in ActionTake()), qui applicato allo stesso
                // meccanismo generico DoDefault()/DefaultDecisionName di DecisionSubPhase.
                DefaultDecisionName = GetBestFreeActionName(availableActions);
                GenerateFreeActionButtons();
                callBack();
            }
            else
            {
                Messages.ShowErrorToHuman(Selection.ThisShip.PilotInfo.PilotName + " cannot perform any free actions");
                Selection.ThisShip.IsFreeActionSkipped = true;
                ActionsHolder.CurrentAction = null;
                CallBack();
            }
        }

        // Sceglie l'azione libera con priorità più alta tra quelle disponibili,
        // usando la STESSA fonte di verità di PerformActionFromList (priorità base
        // dell'azione + eventuale bonus di un'abilità agganciata a Ai.OnGetActionPriority,
        // es. il bonus di Lieutenant Sai su Coordinate) — non una logica separata o
        // ridondante. Ritorna "Focus" solo come ultima rete di sicurezza se
        // availableActions fosse vuota (non dovrebbe accadere: già filtrato dal
        // chiamante), non come default arbitrario.
        private string GetBestFreeActionName(List<GenericAction> availableActions)
        {
            GenericAction bestAction = null;
            int bestPriority = int.MinValue;

            foreach (GenericAction action in availableActions)
            {
                int priority = action.GetActionPriority();
                Selection.ThisShip.Ai.CallGetActionPriority(action, ref priority);

                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestAction = action;
                }
            }

            return (bestAction != null) ? GetFullDecisionName(bestAction) : "Focus";
        }

        // Estratta dal corpo di GenerateFreeActionButtons: garantisce che
        // DefaultDecisionName combaci SEMPRE, carattere per carattere, con
        // l'etichetta del pulsante realmente generato per la stessa azione (colore +
        // eventuali azioni collegate) — nessuna duplicazione della logica tra le due,
        // nessun rischio che il nome calcolato come "migliore" non corrisponda a
        // nessun pulsante realmente presente.
        private string GetFullDecisionName(GenericAction action)
        {
            bool addedDecisionWithLink = false;
            string decisionName = GetActionNameColored(action);

            foreach (var kv in Selection.ThisShip.ActionBar.LinkedActions)
            {
                if (action.GetType() == kv.Key)
                {
                    string linkedActionName = GetActionNameColored(kv.Value);
                    decisionName += addedDecisionWithLink ? " / " + linkedActionName : " > " + linkedActionName;
                    addedDecisionWithLink = true;
                }
            }

            return decisionName;
        }

        public void GenerateFreeActionButtons()
		{
			Selection.ThisShip.IsFreeActionSkipped = false;
            List<GenericAction> availableActions = Selection.ThisShip.GetAvailableFreeActions();

            foreach (var action in availableActions)
            {
                AddDecision(
                    GetFullDecisionName(action),
                    delegate {
                        ActionWasPerformed = true;
                        Selection.ThisShip.CallBeforeActionIsPerformed(
                            (GenericAction)action,
                            (Action)delegate { ActionsHolder.TakeActionStart((GenericAction)action); },
                            isFree: false
                        );
                    },
                    action.ImageUrl
                );
            }
        }

        private string GetActionNameColored(GenericAction action)
        {
            string actionName = action.Name;

            Actions.ActionColor actionColor = action.Color;
            Selection.ThisShip.CallOnCheckActionComplexity(action, ref actionColor);

            switch (actionColor)
            {
                case Actions.ActionColor.Red:
                    actionName = "<color=red>" + actionName + "</color>";
                    break;
                case Actions.ActionColor.Purple:
                    actionName = "<color=purple>" + actionName + "</color>";
                    break;
                default:
                    break;
            }

            return actionName;
        }

        public override void Resume()
        {
            base.Resume();

            if (ShowSkipButton) UI.ShowSkipButton(); else UI.HideSkipButton();
        }

        public override void SkipButton()
        {
            UI.HideSkipButton();
            ActionsHolder.CurrentAction = null;
            Selection.ThisShip.IsFreeActionSkipped = true;
            CallBack();
        }

    }

}