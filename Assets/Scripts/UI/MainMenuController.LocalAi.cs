using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private Button localModesMenuButton;
    private static string AiText(string zh,string en,string ja)=>GamePreferences.Language.StartsWith("ja")?ja:GamePreferences.Language.StartsWith("en")?en:zh;
    private void CreateLocalModesEntry()
    {
        var button=localModesMenuButton=MenuVisualTheme.EnsureLocalPlayButton(startScreenGroup.transform);button.interactable=true;
        button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(OpenLocalPlayPage);
        button.GetComponentInChildren<Text>().GetComponent<PreferenceText>().Literal=true;RefreshLocalModesEntry();
        MenuVisualTheme.ApplyStartNavigation(startScreenGroup.transform);
    }
    private void RefreshLocalModesEntry(){if(localModesMenuButton!=null)localModesMenuButton.GetComponentInChildren<Text>().text=AiText("本地模式","Local play","ローカルモード");}
}
