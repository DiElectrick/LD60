using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-9999)]
public class Bootstrap : MonoBehaviour
{
    //Interactor interactor;

    void Awake()
    {
        // A run always starts with the clock running. The scene may have been stopped while the
        // pause menu had the game frozen (Time.timeScale = 0 is not part of the scene, so it can
        // survive into the next play session) - without this the whole game would boot frozen.
        Time.timeScale = 1f;

        //CMS.Init();
        //interactor = new Interactor();
        //interactor.Init();

        //G.Register(interactor);

        var bootstraped = FindObjectsByType<MonoBehaviour>()
            .OfType<IBootstraped>()
            .ToList();

        foreach (var item in bootstraped)
        {
            var inst = item.GetRegistrInst();  // Получаем object
            G.Register(inst);  // ← Теперь регистрируем как object
            // G сам определит тип через inst.GetType()
        }

        foreach (var item in bootstraped)
        {
            if (!item.isInitialised)
            {
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


}