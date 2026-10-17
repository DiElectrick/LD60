using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-9999)]
public class Bootstrap : MonoBehaviour
{
    //Interactor interactor;

    void Awake()
    {
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