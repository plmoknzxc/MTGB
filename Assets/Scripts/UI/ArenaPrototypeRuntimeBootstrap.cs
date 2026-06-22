using UnityEngine;

[DisallowMultipleComponent]
public sealed class ArenaPrototypeRuntimeBootstrap : MonoBehaviour
{
    [SerializeField] private bool buildOnAwake = true;
    [SerializeField] private bool reuseExistingCamera = true;

    private void Awake()
    {
        if (!buildOnAwake) return;
        ArenaPrototypeSceneFactory.Build(reuseExistingCamera);
    }
}
