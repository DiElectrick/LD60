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
    [SerializeField] Image healthFill;

    int curentTakeDamage;

    public void Init()
    {
        RecalculateStats();
        curentTakeDamage = 0;
        UpdateHealthUI();
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

    void RecalculateStats() { 
    
    }

    public void UpdateInitiativeUI()
    {
        initiativeFill.fillAmount = turnFill;
    }

    public void UpdateHealthUI()
    {
        healthFill.fillAmount = (float)(stats.health-curentTakeDamage)/((float)stats.health);
    }

    public void TakeDamage(int damage) { 
        curentTakeDamage += Mathf.Max(damage-stats.def,1);

        Debug.Log(curentTakeDamage);

        UpdateHealthUI();
    }
}
