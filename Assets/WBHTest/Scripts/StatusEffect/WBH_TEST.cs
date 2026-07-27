using UnityEngine;

public class WBH_TEST : MonoBehaviour
{
    [SerializeField]
    private WBH_StatusEffectController target;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            target.AddStatusEffect(new WBH_StatusEffectData(
                WBH_StatusEffectType.Burn,
                5f,
                0.05f,
                1f));
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            target.AddStatusEffect(new WBH_StatusEffectData(
                WBH_StatusEffectType.Slow,
                5f,
                0.5f));
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            target.AddStatusEffect(new WBH_StatusEffectData(
                WBH_StatusEffectType.Freeze,
                5f,
                0.5f));
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            target.AddStatusEffect(new WBH_StatusEffectData(
                WBH_StatusEffectType.Electric,
                5f,
                0.7f));
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            target.AddStatusEffect(new WBH_StatusEffectData(
                WBH_StatusEffectType.Stun,
                3f));
        }

        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            target.AddStatusEffect(new WBH_StatusEffectData(
                WBH_StatusEffectType.KnockBack,
                0.15f,
                3f,
                0f,
                transform.forward));
        }

        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            target.AddStatusEffect(new WBH_StatusEffectData(
                WBH_StatusEffectType.Airborne,
                1f,
                2f));
        }
    }
}
