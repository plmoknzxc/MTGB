using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ForgeBattlefieldMigrationNotice : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void GuardLegacyScene()
    {
        SceneManager.sceneLoaded-=RedirectLegacyScene;
        SceneManager.sceneLoaded+=RedirectLegacyScene;
    }
    private static void RedirectLegacyScene(Scene scene,LoadSceneMode mode)
    {
        if(scene.name=="IntegratedBattlefield")SceneManager.LoadScene(ForgeBattlefieldController.SceneName);
    }
}
