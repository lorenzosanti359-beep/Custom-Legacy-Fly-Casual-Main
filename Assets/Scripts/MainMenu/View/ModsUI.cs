using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mods;

public class ModsUI : MonoBehaviour {

    public void Start()
    {
        ModsManager.UI = this;
    }

    public void InitializePanel()
    {
        GameObject prefab = (GameObject)Resources.Load("Prefabs/UI/ModPanel", typeof(GameObject));
        GameObject ModsPanel = GameObject.Find("UI/Panels").transform.Find("ModsPanel").Find("Scroll View/Viewport/Content").gameObject;

        // Modernization (Point 3): Strip out rigid procedural coordinate math and use layout components instead
        // Clean up old elements natively
        foreach (Transform transform in ModsPanel.transform)
        {
            Destroy(transform.gameObject);
        }

        // Programmatically guarantee layout systems are ready and modular
        VerticalLayoutGroup layoutGroup = ModsPanel.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = ModsPanel.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 25f; // Replaces manual calculation FREE_SPACE
            layoutGroup.padding = new RectOffset(15, 15, 20, 20);
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlHeight = true;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
        }

        ContentSizeFitter sizeFitter = ModsPanel.GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
        {
            sizeFitter = ModsPanel.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        // Handle the scroll viewport natively via ScrollRect component initialization
        ScrollRect scrollRect = GameObject.Find("UI/Panels").transform.Find("ModsPanel").Find("Scroll View").GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f; // Set to the top (1.0f instead of old 0.0f bug)
        }

        foreach (var mod in ModsManager.GetAllMods())
        {
            GameObject ModRecord = MonoBehaviour.Instantiate(prefab, ModsPanel.transform);
            ModRecord.name = mod.Key.ToString();

            ModRecord.transform.Find("Label").GetComponent<Text>().text = mod.Value.Name;

            Text description = ModRecord.transform.Find("Text").GetComponent<Text>();
            description.text = mod.Value.Description;
            
            // REMOVED manual preferredHeight mapping and sizeDelta modifications.
            // A dynamic ContentSizeFitter on the ModPanel prefab handles internal text heights natively!

            ModRecord.transform.Find("Toggle").GetComponent<Toggle>().isOn = ModsManager.Mods[mod.Key].IsOn;
        }
    }
}