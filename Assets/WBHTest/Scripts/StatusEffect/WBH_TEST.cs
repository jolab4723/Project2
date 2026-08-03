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
                0.5f));
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
                type: WBH_StatusEffectType.KnockBack,
                duration: 1f,
                force: 10f,
                direction: transform.forward));
        }

        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            target.AddStatusEffect(new WBH_StatusEffectData(
                WBH_StatusEffectType.Airborne,
                duration: 3f,
                height: 7f));
        }
    }
}
