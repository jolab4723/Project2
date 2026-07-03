using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Potion : MonoBehaviour
{
    [SerializeField] private Image potion;
    [SerializeField] private Sprite[] potionSprite;
    private int maxUse = 2;
    private int currentUse;

    void Start()
    {
        currentUse = maxUse;
    }

    void UsePotion()
    {
        currentUse--;
        potion.sprite = potionSprite[currentUse];
    }

    void Update()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if(currentUse > 0)
            {
                UsePotion();
            }
        }
    }
}
