using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DamageFontTester : MonoBehaviour
{
    [Header("Font")]
    [SerializeField] private TMP_FontAsset damageFont;

    [Header("Text")]
    [SerializeField] private float fontSize = 4f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField] private float outlineWidth = 0.25f;

    [Header("Spawn")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private float lifeTime = 0.8f;
    [SerializeField] private float moveUpDistance = 1.2f;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame)
        {
            SpawnDamageText();
        }
    }

    private void SpawnDamageText()
    {
        GameObject textObj = new GameObject("Damage Text Test");
        textObj.transform.position = transform.position + spawnOffset;

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = Random.Range(1000, 10000).ToString();
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;

        if (damageFont != null)
            tmp.font = damageFont;

        tmp.enableWordWrapping = false;
        tmp.outlineColor = outlineColor;
        tmp.outlineWidth = outlineWidth;

        if (mainCamera != null)
        {
            textObj.transform.rotation = Quaternion.LookRotation(
                textObj.transform.position - mainCamera.transform.position
            );
        }

        StartCoroutine(AnimateDamageText(textObj, tmp));
    }

    private IEnumerator AnimateDamageText(GameObject textObj, TextMeshPro tmp)
    {
        Vector3 startPos = textObj.transform.position;
        Vector3 endPos = startPos + Vector3.up * moveUpDistance;

        Color startColor = tmp.color;
        float timer = 0f;

        while (timer < lifeTime)
        {
            timer += Time.deltaTime;
            float t = timer / lifeTime;

            textObj.transform.position = Vector3.Lerp(startPos, endPos, t);

            Color color = startColor;
            color.a = Mathf.Lerp(1f, 0f, t);
            tmp.color = color;

            if (mainCamera != null)
            {
                textObj.transform.rotation = Quaternion.LookRotation(
                    textObj.transform.position - mainCamera.transform.position
                );
            }

            yield return null;
        }

        Destroy(textObj);
    }
}