using UnityEngine;
using System.Collections.Generic;

public class YJ_ChoiceButtonBox : MonoBehaviour
{
    [SerializeField] private GameObject choiceButtonPrefab;
    [SerializeField] private List<GameObject> buttons;

    void Start()
    {
        buttons.Clear();
    }

    public void ButtonCreate(int num)
    {
        if (choiceButtonPrefab == null || num <= 0 || num > 3)
            return;

        for (int i = 0; i < num; i++)
        {
            GameObject button = Instantiate(choiceButtonPrefab, this.gameObject.transform);
            buttons.Add(button);
        }
    }

    public void ButtonTextSet(List<string> title, List<string> content)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            YJ_ChoiceButton buttonObject = buttons[i].GetComponent<YJ_ChoiceButton>();
            buttonObject.ButtonTitleSet(title[i]);
            buttonObject.ButtonContentSet(content[i]);
        }
    }
}
