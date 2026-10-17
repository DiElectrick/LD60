using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CityController : MonoBehaviour, ITurner, IBootstraped
{
    public bool isInitialised { get; set; }
    public float turnFill { get; set; }

    List<Platform> platforms = new();
    Stats baseStats = new Stats();
    Stats stats = new Stats();

    [SerializeField] Image initiativeFill;
    

    public void Init()
    {
        
    }

    public void OnTurn()
    {
        
    }

    public void AddFill(float addedFill)
    {
        turnFill += addedFill * (float)stats.speed / 100f;
        UpdateInitiativeUI();
        if (turnFill >= 1f) {
            turnFill -= 1f;
            OnTurn();
        }
    }

    public void UpdateInitiativeUI()
    {
        initiativeFill.fillAmount = turnFill;
    }
}
