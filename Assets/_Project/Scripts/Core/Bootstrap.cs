using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-9999)]
public class Bootstrap : MonoBehaviour
{
    void Awake()
    {
        // A run always starts with the clock running. The scene may have been stopped while the
        // pause menu had the game frozen (Time.timeScale = 0 is not part of the scene, so it can
        // survive into the next play session) - without this the whole game would boot frozen.
        Time.timeScale = 1f;

        var bootstraped = FindObjectsByType<MonoBehaviour>()
            .OfType<IBootstraped>()
            .ToList();

        // Registration first, so every Init can already look the other services up in G.
        foreach (var item in bootstraped)
        {
            G.Register(item.GetRegistrInst());
        }

        foreach (var item in bootstraped)
        {
            if (item.isInitialised) continue;

            try
            {
                item.Init();
            }
            catch (System.Exception e)
            {
                // One component failing to initialise must not silently take the rest of the
                // game down with it - report it and keep going.
                Debug.LogError($"[Bootstrap] {item.GetType().Name}.Init() threw: {e}", item as Object);
            }
        }
    }
}