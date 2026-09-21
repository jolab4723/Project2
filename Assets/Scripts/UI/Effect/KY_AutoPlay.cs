using UnityEngine;


public interface IPlayableEffect
{
    void Play();
    void Stop();
}

/// <summary>같은 오브젝트의 재생 가능 효과를 활성화 상태에 맞춰 자동으로 시작하고 중지한다.</summary>
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
