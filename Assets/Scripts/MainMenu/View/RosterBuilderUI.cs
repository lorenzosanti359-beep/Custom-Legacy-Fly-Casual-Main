using System;
using UnityEngine;
using UnityEngine.UI;
using Players;
using DG.Tweening; // Added for tactical UI juice and transitions

public class RosterBuilderUI : MonoBehaviour {

    public void CopyToClipboard()
    {
        GUIUtility.systemCopyBuffer = GameObject.Find("UI/Panels/ImportExportPanel/Content/InputField").GetComponent<InputField>().text;
        Messages.ShowInfo("Copied to clipboard");
    }

    public void PasteFromClipboard()
    {
        GameObject.Find("UI/Panels/ImportExportPanel/Content/InputField").GetComponent<InputField>().text = GUIUtility.systemCopyBuffer;
    }

    public void Import()
    {
        Global.SquadBuilder.CurrentSquad.CreateSquadFromImportedJson(GameObject.Find("UI/Panels/ImportExportPanel/Content/InputField").GetComponent<InputField>().text);
        MainMenu.CurrentMainMenu.ChangePanel("SquadBuilderPanel");
    }

    public void SetCurrentPlayerFactionAndNext(string factionText)
    {
        Faction faction = (Faction) Enum.Parse(typeof(Faction), factionText);
        Global.SquadBuilder.CurrentSquad.SquadFaction = faction;

        Global.SquadBuilder.View.ReturnToSquadBuilder();
    }

    public void RemoveCurrentShip()
    {
        Global.SquadBuilder.CurrentSquad.RemoveShip(Global.SquadBuilder.CurrentShip);
        Global.SquadBuilder.View.ReturnToSquadBuilder();
    }

    public void NextPlayer()
    {
        if (Global.SquadBuilder.CurrentSquad.IsValid)
        {
            if(Global.IsCampaignGame)
            {
                StartBattle();
            }
            if (Global.IsVsAiGame)
            {
                MainMenu.CurrentMainMenu.ChangePanel("AiDecisionPanel");
            }
            else if (Global.IsVsNetworkOpponent)
            {
                MainMenu.CurrentMainMenu.ChangePanel("MultiplayerDecisionPanel");
            }
            else
            {
                NextPlayerOpen();
            }
        }
    }

    public void NextPlayerOpen()
    {
        Global.SquadBuilder.CurrentPlayer = PlayerNo.Player2;
        MainMenu.CurrentMainMenu.ChangePanel("SelectFactionPanel");
    }

    public void NextPlayerRandomAi()
    {
        Global.SquadBuilder.View.SetRandomAiSquad();
        StartBattle();
    }

    public void FactionSelectionBackIsPressed()
    {
        if (Global.SquadBuilder.CurrentPlayer == PlayerNo.Player1)
        {
            MainMenu.CurrentMainMenu.ChangePanel("GameModeDecisionPanel");
        }
        else if (Global.SquadBuilder.CurrentPlayer == PlayerNo.Player2)
        {
            if (!Global.IsVsAiGame)
            {
                Global.SquadBuilder.CurrentPlayer = PlayerNo.Player1;
                Global.SquadBuilder.View.ReturnToSquadBuilder();
            }
            else
            {
                MainMenu.CurrentMainMenu.ChangePanel("AiDecisionPanel");
            }
        }
    }

    public void AiDecisionPanelBack()
    {
        Global.SquadBuilder.CurrentPlayer = PlayerNo.Player1;
        MainMenu.CurrentMainMenu.ChangePanel("SquadBuilderPanel");
    }

    public void ClearSquadList()
    {
        Global.SquadBuilder.CurrentSquad.ClearAll();
        Global.SquadBuilder.View.ReturnToSquadBuilder();
    }

    public void OpenImportPanel()
    {
        Global.SquadBuilder.View.OpenImportExportPanel(true);
    }

    public void OpenExportPanel()
    {
        Global.SquadBuilder.View.OpenImportExportPanel(false);
    }

    public void TrySaveSquadron()
    {
        string squadName = GameObject.Find("UI/Panels/SaveSquadronPanel/Panel/Name/InputField").GetComponent<InputField>().text;
        if (squadName == "") squadName = "Unnamed squadron";

        if (Global.IsCampaignGame)
        {
            Global.SquadBuilder.CurrentSquad.SaveCampaignSquadronToFile(squadName);
        } else
        {
            Global.SquadBuilder.CurrentSquad.SaveSquadronToFile(squadName);
        }        
        Global.SquadBuilder.View.ReturnToSquadBuilder();
    }

    public void StartBattle()
    {
        if (Global.SquadBuilder.CurrentSquad.IsValid)
        {
            Global.SquadBuilder.SaveAutosaveSquadConfigurations();

            if (!Global.IsVsNetworkOpponent)
            {
                Global.SquadBuilder.GenerateSavedConfigurationsLocal();
                GameController.StartBattle();
            }
            else
            {
                MainMenu.CurrentMainMenu.ChangePanel("MultiplayerDecisionPanel");
            }
        }
    }

    public void LoadSquadDirectly()
    {
        MainMenu.CurrentMainMenu.ChangePanel("BrowseSavedSquadsPanel");
    }

    public void ClearUpgradesOfCurrentShip()
    {
        Global.SquadBuilder.CurrentShip.ClearUpgrades();
        Global.SquadBuilder.View.ShowPilotWithSlots();
    }

    public void CopyCurrentShip()
    {
        Global.SquadBuilder.CurrentSquad.CopyShip(Global.SquadBuilder.CurrentShip);
        Global.SquadBuilder.View.UpdateSquadCost("ShipSlotsPanel");

        // Modernization (Point 1): Add tangible tactile feedback punch animation when duplicate action occurs
        GameObject shipPanel = GameObject.Find("UI/Panels/SquadBuilderPanel/ShipSlotsPanel");
        if (shipPanel != null)
        {
            shipPanel.transform.DOPunchScale(new Vector3(0.04f, 0.04f, 0.04f), 0.3f, 10, 1f);
        }
    }

    public void SetDefaultObstacles()
    {
        Global.SquadBuilder.View.SetDefaultObstacles();
    }

    public void OnUpgradeFilterTextChanged(InputField input)
    {
        Global.SquadBuilder.View.FilterVisibleUpgrades(input.text.ToLower());
    }

    public void OnPilotFilterTextChanged(InputField input)
    {
        Global.SquadBuilder.View.FilterVisiblePilots(input.text.ToLower());
    }

    public void ConfirmGoBack()
    {
        GameObject.Find("UI").transform.Find("ConfirmGoBack").gameObject.SetActive(false);
        MainMenu.CurrentMainMenu.ChangePanel("SelectFactionPanel");
    }

    public void ConfirmDontGoBack()
    {
        // Modernization (Point 1): Cleanly shrink down modal window before disabling it
        GameObject confirmPanel = GameObject.Find("UI").transform.Find("ConfirmGoBack").gameObject;
        confirmPanel.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() => {
            confirmPanel.SetActive(false);
        });
    }

    public void GoBackToFactionSelect()
    {
        if (Global.SquadBuilder.CurrentSquad.Ships.Count > 0)
        {
            GameObject confirmPanel = GameObject.Find("UI").transform.Find("ConfirmGoBack").gameObject;
            confirmPanel.SetActive(true);
            
            // Modernization (Point 1): Pop out the modal smoothly with an elastic bounce
            confirmPanel.transform.localScale = Vector3.zero;
            confirmPanel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        }
        else
        {
            MainMenu.CurrentMainMenu.ChangePanel("SelectFactionPanel");
        }
    }
}