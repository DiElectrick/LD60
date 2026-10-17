using System.Collections.Generic;
using UnityEngine;

public class Main : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }
    [SerializeField] BaseEnemy testEnemy;

    bool isBattleStage = false;

    public void Init()
    {

    }

    private void Start()
    {
        //G.Get<TurnManager>().InitBattle(new List<ITurner>() {testEnemy});
    }

}
