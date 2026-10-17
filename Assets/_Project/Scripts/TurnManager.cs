using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour, IBootstraped
{
    public List<ITurner> turners = new();

    bool isBattleStart = false;

    [SerializeField] float speedScale = 1f;

    public bool isInitialised { get ; set ; }

    public void Init()
    {
      
    }

    public void InitBattle(List<ITurner> enemies) {
        turners.Clear();
        turners.Add(G.Get<CityController>());
        turners.AddRange(enemies);

        foreach (var turner in turners) { 
            turner.Restart();
        }
        isBattleStart = true;
    }

    public void StopBattle() { 
        isBattleStart = false;
    }

    public void Update()
    {
        if (isBattleStart) {
            foreach (var turner in turners) {
                turner.AddFill(Time.deltaTime * speedScale);
            }
        }
    }

}


public interface ITurner {
    public void OnTurn();

    public float turnFill { get; set; }
    public void Restart() {
        turnFill = 0;
    }

    public void AddFill(float addedFill);

    public void UpdateInitiativeUI();

}
