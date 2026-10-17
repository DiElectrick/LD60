using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
using UnityEngine.UI;

public class BaseEnemy : MonoBehaviour, ITurner
{
    public float turnFill { get; set; }

    Stats enemyStats = new();

    [SerializeField] Image initiativeFill;

    public void AddFill(float addedFill)
    {
        turnFill += addedFill * (float)enemyStats.speed / 100f;
        UpdateInitiativeUI();
        if (turnFill >= 1f)
        {
            turnFill -= 1f;
            OnTurn();
        }
    }

    public void OnTurn()
    {
        G.Get<CityController>().TakeDamage(enemyStats.atack);
    }

    public void UpdateInitiativeUI()
    {
        initiativeFill.fillAmount = turnFill;
    }
}
