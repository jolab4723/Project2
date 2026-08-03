using UnityEngine;


public interface IPlayableEffect
{
    void Play();
    void Stop();
}

public class KY_AutoPlay : MonoBehaviour
{
    private IPlayableEffect[] effects;

    void Awake()
    {
        effects = GetComponents<IPlayableEffect>(); // 이 오브젝트에 붙은 모든 이펙트를 찾음
    }

    void OnEnable()
    {
        foreach (var effect in effects) effect.Play();
    }

    void OnDisable()
    {
        foreach (var effect in effects) effect.Stop();
    }
}