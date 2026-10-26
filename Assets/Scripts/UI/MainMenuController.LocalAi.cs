using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private Button localAiMenuButton;
    private static string AiText(string zh,string en,string ja)=>GamePreferences.Language.StartsWith("ja")?ja:GamePreferences.Language.StartsWith("en")?en:zh;
    private void CreateLocalAiEntry()
    {
        var button=localAiMenuButton=MenuVisualTheme.EnsureLocalPlayButton(startScreenGroup.transform);button.interactable=true;
        button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(OnLocalAi);
        button.GetComponentInChildren<Text>().GetComponent<PreferenceText>().Literal=true;RefreshLocalAiEntry();
        MenuVisualTheme.ApplyStartNavigation(startScreenGroup.transform);
    }
    private void RefreshLocalAiEntry(){if(localAiMenuButton!=null)localAiMenuButton.GetComponentInChildren<Text>().text=AiText("本地模式","Local play","ローカル対戦");}

    private void OnLocalAi()=>OpenLocalPlayPage();
}
