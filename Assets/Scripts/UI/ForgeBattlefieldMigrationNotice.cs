using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ForgeBattlefieldMigrationNotice : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ShowForBattlefield()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene == "IntegratedBattlefield" && OnlineMatchService.Instance?.IsInMatch != true)
            new GameObject("ForgeBattlefieldMigrationNotice").AddComponent<ForgeBattlefieldMigrationNotice>();
    }
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, 130), GUI.skin.box);
        GUILayout.Label("请从联机大厅提交牌组并开局，战场会自动载入真实对局。");
        if (GUILayout.Button("返回主菜单")) SceneManager.LoadScene("Main");
        GUILayout.EndArea();
    }
}
