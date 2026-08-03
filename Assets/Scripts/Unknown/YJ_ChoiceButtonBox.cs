using UnityEngine;
using System.Collections.Generic;

public class YJ_ChoiceButtonBox : MonoBehaviour
{
    [SerializeField] private GameObject choiceButtonPrefab;
    [SerializeField] private List<GameObject> buttons = new();

    private void Awake()
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
        if (title == null || content == null)
            return;

        int textCount = Mathf.Min(buttons.Count, title.Count, content.Count);

        for (int i = 0; i < textCount; i++)
        {
            YJ_ChoiceButton buttonObject = buttons[i].GetComponent<YJ_ChoiceButton>();

            if (buttonObject == null)
                continue;

            buttonObject.ButtonTitleSet(title[i]);
            buttonObject.ButtonContentSet(content[i]);
        }
    }
}
