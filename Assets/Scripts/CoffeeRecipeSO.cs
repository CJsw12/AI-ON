using System.Collections.Generic;
using UnityEngine;

public enum CoffeeIngredient { Water, Shot, Milk, Syrup }

[CreateAssetMenu(fileName = "CoffeeRecipe", menuName = "Dugeundugeun Campus/Coffee Recipe")]
public sealed class CoffeeRecipeSO : ScriptableObject
{
    [SerializeField] string customerName;
    [SerializeField] string drinkName;
    [TextArea(2, 5)] [SerializeField] string orderDialogue;
    [SerializeField] List<CoffeeIngredient> ingredients = new List<CoffeeIngredient>();
    [SerializeField] bool requireOrder;
    [SerializeField] bool hideOrderNote;
    [Min(.1f)] [SerializeField] float noteVisibleSeconds = 3f;
    [Min(1)] [SerializeField] int cupCapacity = 8;

    public string CustomerName => customerName;
    public string DrinkName => drinkName;
    public string OrderDialogue => orderDialogue;
    public IReadOnlyList<CoffeeIngredient> Ingredients => ingredients;
    public bool RequireOrder => requireOrder;
    public bool HideOrderNote => hideOrderNote;
    public float NoteVisibleSeconds => noteVisibleSeconds;
    public int CupCapacity => cupCapacity;

    void OnValidate()
    {
        noteVisibleSeconds = Mathf.Max(.1f, noteVisibleSeconds);
        cupCapacity = Mathf.Max(1, cupCapacity);
        if (ingredients == null || ingredients.Count == 0)
            Debug.LogWarning("레시피에 재료를 추가하세요.", this);
        else if (ingredients.Count > cupCapacity)
            Debug.LogWarning("레시피 재료 수가 컵 용량을 초과합니다.", this);
    }
}
