using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BrowseNetworkRoomsUI : MonoBehaviour
{
    public static BrowseNetworkRoomsUI Instance;

    public GameObject BottomControls;
    public GameObject LoadingMessage;
    public GameObject NoRoomsPanel;

    public GameObject RoomListPanel;
    public GameObject RoomInfoPrefab;

    void Awake()
    {
        Instance = this;
    }

    public void ShowLoading()
    {
        BottomControls.SetActive(false);
        NoRoomsPanel.SetActive(false);
        LoadingMessage.SetActive(true);
    }

    public void ShowRooms()
    {
        BottomControls.SetActive(true);
        NoRoomsPanel.SetActive(false);
        LoadingMessage.SetActive(false);

        ShowListofRooms();
    }

    private void ShowListofRooms()
    {
        // Modernization (Point 3): Prevent duplicate list overlay by clearing previous instances dynamically
        foreach (Transform child in RoomListPanel.transform)
        {
            Destroy(child.gameObject);
        }

        // Programmatically hook up flexible Layout Elements to auto-manage dimensions across all resolutions
        VerticalLayoutGroup layoutGroup = RoomListPanel.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = RoomListPanel.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 20f; // Standard clean separation
            layoutGroup.padding = new RectOffset(10, 10, 15, 15);
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlHeight = true; // Respects layout dimensions of the prefab
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
        }

        ContentSizeFitter sizeFitter = RoomListPanel.GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
        {
            sizeFitter = RoomListPanel.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        int roomsCount = 1;

        for (int i = 0; i < roomsCount; i++)
        {
            GameObject newRoom = GameObject.Instantiate(RoomInfoPrefab, RoomListPanel.transform);
            newRoom.name = "Room" + i;

            // REMOVED: manual transform.localPosition math calculation that broke responsiveness!
            // Positioning is completely offloaded to the VerticalLayoutGroup.

            newRoom.GetComponentInChildren<Button>().onClick.AddListener(delegate { 
                Network.JoinRoom(null);
            });
        }
    }

    public void ShowNoRooms()
    {
        BottomControls.SetActive(true);
        NoRoomsPanel.SetActive(true);
        LoadingMessage.SetActive(false);

        CountdownToRoomsRefresh.Reset();
    }
}