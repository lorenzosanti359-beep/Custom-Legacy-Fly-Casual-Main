using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // Added for smooth UI animations and transitions
using Mods;
using SquadBuilderNS;
using Players;
using Upgrade;
using Migrations;
using ExtraOptions;
using Obstacles;

public partial class MainMenu : MonoBehaviour {

    string NewVersionUrl;
    const string PatreonUrl = "https://www.patreon.com/c/user?u=84527220";

    public static MainMenu CurrentMainMenu;

    private Faction CurrentAvatarsFaction = Faction.Rebel;
    
    void Start ()
    {
        MigrationsManager.PerformMigrations();
        InitializeMenu();
    }

    private void InitializeMenu()
    {
        CurrentMainMenu = this;
        SetCurrentPanel();

        DontDestroyOnLoad(GameObject.Find("GlobalUI").gameObject);

        ModsManager.Initialize();
        Options.ReadOptions();
        Options.UpdateVolume();
        ExtraOptionsManager.Initialize();
        SetBackground();
        UpdateVersionInfo();
        ClearBatchAiSquadsTestingMode();

        PrepareUpdateChecker();

        new ObstaclesManager();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void OnUpdateAvailableClick()
    {
        Application.OpenURL(NewVersionUrl);
    }

    public void OnSupportOnPatreonClick()
    {
        Application.OpenURL(PatreonUrl);
    }

    private void UpdateVersionInfo()
    {
        GameObject.Find("UI/Panels/MainMenuPanel/Background/Version/Version Text").GetComponent<Text>().text = Global.CurrentVersion;
    }

    private void ClearBatchAiSquadsTestingMode()
    {
        ExtraOptions.ExtraOptionsList.BatchAiSquadsTestingModeExtraOption.ClearResults();
    }

    public static void SetBackground()
    {
        Sprite background = null;

        if (Options.BackgroundImage != "_RANDOM")
        {
            background = Resources.Load<Sprite>("Sprites/Backgrounds/MainMenu/" + Options.BackgroundImage);
            if (background == null)
            {
                background = GetRandomMenuBackground();
                Options.BackgroundImage = "_RANDOM";
                PlayerPrefs.SetString("BackgroundImage", "_RANDOM");
            }
        }
        else
        {
            background = GetRandomMenuBackground();
        }

        GameObject.Find("UI/BackgroundImage").GetComponent<Image>().sprite = background;

        GameObject.Find("UI/BackgroundImage").GetComponent<AspectRatioFitter>().enabled = false;
        GameObject.Find("UI/BackgroundImage").GetComponent<AspectRatioFitter>().enabled = true;
    }

    public static Sprite GetRandomMenuBackground()
    {
        List<Sprite> spritesList = Resources.LoadAll<Sprite>("Sprites/Backgrounds/MainMenu/")
            .Where(n => n.name != "_RANDOM")
            .ToList();
        return spritesList[UnityEngine.Random.Range(0, spritesList.Count)];
    }

    private void PrepareUpdateChecker()
    {
        RemoteSettings.Completed += CheckUpdateNotification;
        RemoteSettings.Completed += CheckPatreonSupportNotification;
        RemoteSettings.ForceUpdate();
    }

    private void CheckUpdateNotification(bool wasUpdatedFromServer, bool settingsChanged, int serverResponse)
    {
        Global.LatestVersionInt = RemoteSettings.GetInt("UpdateLatestVersionInt", Global.CurrentVersionInt);
        if (Global.LatestVersionInt > Global.CurrentVersionInt)
        {
            string latestVersion    = RemoteSettings.GetString("UpdateLatestVersion", Global.CurrentVersion);
            string updateLink       = RemoteSettings.GetString("UpdateLink");
            ShowNewVersionIsAvailable(latestVersion, updateLink);
        }

        RemoteSettings.Completed -= CheckUpdateNotification;
    }

    private void CheckPatreonSupportNotification(bool arg1, bool arg2, int arg3)
    {
        int support = RemoteSettings.GetInt("SupportOnPatreon", -1);
        if (support != -1) ShowSupportOnPatreon(support);

        RemoteSettings.Completed -= CheckPatreonSupportNotification;
    }

    public void CreateMatch()
    {
        string roomName = GameObject.Find("UI/Panels/CreateMatchPanel/Panel/Name").GetComponentInChildren<InputField>().text;
        string password = GameObject.Find("UI/Panels/CreateMatchPanel/Panel/Password").GetComponentInChildren<InputField>().text;

        GameController.Initialize();
        ReplaysManager.TryInitialize(ReplaysMode.Write);

        Network.CreateMatch(roomName, password);

        ChangePanel("WaitingForOpponentsPanel");
    }

    public void JoinRoomByIp(Text ipText)
    {
        Network.ServerUri = "tcp4://" + ipText.text;
        Network.JoinRoom(null);
    }

    public void BrowseMatches()
    {
        StartCoroutine(BrowseMatchesAsync());
    }

    private IEnumerator BrowseMatchesAsync()
    {
        BrowseNetworkRoomsUI.Instance.ShowLoading();
        Network.BrowseMatches();

        yield return new WaitForSeconds(3);

        BrowseNetworkRoomsUI.Instance.ShowRooms();
    }

    public void JoinMatch(GameObject panel)
    {
        string password = panel.transform.Find("Password").GetComponentInChildren<InputField>().text;
        Network.JoinRoom(password);
    }

    public void CancelWaitingForOpponent()
    {
        Network.CancelWaitingForOpponent();
        ChangePanel("MultiplayerDecisionPanel");
    }

    public void StartSquadBuilerMode(string modeName)
    {
        ChangePanel("LoadingCardsStubPanel");
        Global.Instance.StartCoroutine(InitializeSquadBuilderCoroutine(modeName));
    }

    private IEnumerator InitializeSquadBuilderCoroutine(string modeName)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        InitializeSquadBuilder(modeName);
        if(Global.IsCampaignGame)
        {
            ChangePanel("BrowseCampaignMissionsPanel");
        }
        else
        {
            ChangePanel("SelectFactionPanel");
        }
    }

    public void StartReplay()
    {
        GameController.StartBattle(ReplaysMode.Read);
    }

    public void InitializeSquadBuilder(string modeName)
    {
        if ("Campaign".Equals(modeName))
        {
            Global.IsCampaignGame = true;
        }
        else
        {
            Global.IsCampaignGame = false;
        }
        Global.SquadBuilder = new SquadBuilder();
        new SquadBuilderView(Global.SquadBuilder);
        
        Global.SquadBuilder.SetPlayers(modeName);
    }

    public void InitializePlayerCustomization()
    {
        InitializeAvatars("Rebel");
        InitializeNickName();
        InitializeTitle();
    }

    private void InitializeNickName()
    {
        GameObject.Find("UI/Panels/AvatarsPanel/NickName/InputField").gameObject.GetComponent<InputField>().text = Options.NickName;
    }

    private void InitializeTitle()
    {
        GameObject.Find("UI/Panels/AvatarsPanel/Title/InputField").gameObject.GetComponent<InputField>().text = Options.Title;
    }

    public void InitializeAvatars(string factionString)
    {
        CurrentAvatarsFaction = (Faction) Enum.Parse(typeof(Faction), factionString);

        ClearAvatarsPanel();

        int count = 0;

        List<Type> typelist = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => String.Equals(t.Namespace, "UpgradesList.FirstEdition", StringComparison.Ordinal))
            .ToList();

        foreach (var type in typelist)
        {
            if (type.MemberType == MemberTypes.NestedType) continue;

            GenericUpgrade newUpgradeContainer = (GenericUpgrade)System.Activator.CreateInstance(type);
            if (newUpgradeContainer.UpgradeInfo.Name != null)
            {
                if (newUpgradeContainer.Avatar != null && newUpgradeContainer.Avatar.AvatarFaction == CurrentAvatarsFaction) AddAvailableAvatar(newUpgradeContainer, count++);
            }
        }
    }

    private void ClearAvatarsPanel()
    {
        GameObject avatarsPanel = GameObject.Find("UI/Panels/AvatarsPanel/ContentPanel").gameObject;
        foreach (Transform transform in avatarsPanel.transform)
        {
            GameObject.Destroy(transform.gameObject);
        }

        GameObject.Find("UI/Panels/AvatarsPanel/AvatarSelector").GetComponent<Image>().enabled = false;

        Resources.UnloadUnusedAssets();
    }

    private void AddAvailableAvatar(GenericUpgrade avatarUpgrade, int count)
    {
        GameObject prefab = (GameObject)Resources.Load("Prefabs/MainMenu/AvatarImage", typeof(GameObject));
        GameObject avatarPanel = MonoBehaviour.Instantiate(prefab, GameObject.Find("UI/Panels/AvatarsPanel/ContentPanel").transform);

        int row = count / 8;
        int column = count - row * 8;

        avatarPanel.transform.localPosition = new Vector2(20 + column * 120, -20 - row * 110);
        avatarPanel.name = avatarUpgrade.GetType().ToString();

        AvatarFromUpgrade avatar = avatarPanel.GetComponent<AvatarFromUpgrade>();
        avatar.Initialize(avatarUpgrade.GetType().ToString(), ChangeAvatar);

        if (avatarUpgrade.GetType().ToString() == Options.Avatar)
        {
            SetAvatarSelected(avatarPanel.transform.position);
        }
    }

    private void ChangeAvatar(string avatarName)
    {
        Options.Avatar = avatarName;
        Options.ChangeParameterValue("Avatar", avatarName);

        SetAvatarSelected(GameObject.Find("UI/Panels/AvatarsPanel/ContentPanel/" + avatarName).transform.position);
    }

    public void SetAvatarSelected(Vector3 position)
    {
        GameObject selector = GameObject.Find("UI/Panels/AvatarsPanel/AvatarSelector");
        selector.GetComponent<Image>().enabled = true;
        
        // Modernization (Point 1): Smoothly move the selection square via DOTween instead of snapping
        selector.transform.DOMove(position, 0.25f).SetEase(Ease.OutQuad);
    }

    public void ChangeNickName(string text)
    {
        Options.NickName = text;
        Options.ChangeParameterValue("NickName", text);
    }

    public void ChangeTitle(string text)
    {
        Options.Title = text;
        Options.ChangeParameterValue("Title", text);
    }

    public static void ShowLoadingScreenNetwork()
    {
        if ((Global.SquadBuilder.SquadLists[PlayerNo.Player2].PlayerType == typeof(AggressorAiPlayer)) && (!Options.DontShowAiInfo))
        {
            GameObject.Find("GlobalUI/LoadingScreen").transform.Find("AiInformation").gameObject.SetActive(true);
            GameObject.Find("GlobalUI/LoadingScreen/AiInformation").transform.Find("ToggleBlock").gameObject.SetActive(false);
        }
    }

    public static void ShowLoadingScreenNetworkContinue()
    {
        GameObject.Find("GlobalUI/LoadingScreen/LoadingInfoPanel").gameObject.SetActive(false);

        GameObject.Find("GlobalUI/LoadingScreen/AiInformation").transform.Find("ToggleBlock").gameObject.SetActive(true);
        GameObject.Find("GlobalUI/LoadingScreen").transform.Find("StartPanel").gameObject.SetActive(true);

        Button startButton = GameObject.Find("GlobalUI/LoadingScreen/StartPanel/StartButton").GetComponent<Button>();
        startButton.onClick.RemoveAllListeners();
        startButton.onClick.AddListener(delegate
        {
            Options.DontShowAiInfo = true;
            Options.ChangeParameterValue("DontShowAiInfo", GameObject.Find("GlobalUI/LoadingScreen/AiInformation/ToggleBlock/DontShowAgain").GetComponent<Toggle>().isOn);
            Global.StartBattle();
        });
    }

    public static void ScalePanel(Transform panelTransform, float maxScale = float.MaxValue, bool twoBorders = false)
    {
        float bordersSize = (twoBorders) ? 250f : 125f;
        float globalUiScale = GameObject.Find("UI").GetComponent<RectTransform>().localScale.y;

        float scaleX = Screen.width / panelTransform.GetComponent<RectTransform>().sizeDelta.x / globalUiScale;
        float scaleY = (Screen.height - bordersSize * globalUiScale) / panelTransform.GetComponent<RectTransform>().sizeDelta.y / globalUiScale;
        float scale = Mathf.Min(scaleX, scaleY);
        scale = Mathf.Min(scale, maxScale);

        // Modernization (Point 1): Smoothly animate the scale transformation of panels on loading/resize
        panelTransform.DOScale(new Vector3(scale, scale, scale), 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    // Modernization architectural bridge: call this helper method to transition screens with full juice
    public void AnimatePanelEntrance(GameObject panelObject)
    {
        if (panelObject == null) return;

        CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = panelObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        panelObject.transform.localScale = Vector3.one * 0.93f;

        canvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutCubic);
        panelObject.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
    }
}