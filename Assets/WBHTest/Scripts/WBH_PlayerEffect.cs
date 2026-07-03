using UnityEngine;

public class WBH_PlayerEffect : MonoBehaviour
{
    [SerializeField] ParticleSystem slashEffect;
    [SerializeField] ParticleSystem shotGunEffect;

    public void FighterAttackEffect()
    {
        slashEffect.Play();
    }

    public void ShotGunEffect()
    {
        shotGunEffect.Play();
    }
}
