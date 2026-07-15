using UnityEngine;
using Core;

/// <summary>
/// 씬 시작 시 DataManager.LoadPassiveData()로 프로필을 불러와 PassiveSkillManager에 연결한다.
/// 저장된 슬롯이 없는 진짜 첫 실행일 때만 테스트하기 편하도록 골드를 채워준다
/// (한 번이라도 저장되면 이후에는 실제 저장된 값을 그대로 불러온다).
/// </summary>
public class PassiveSkillTestInit : MonoBehaviour
{
    [SerializeField] private int testGoldForFreshProfile = 100000;

    private void Start()
    {
        if (DataManager.Instance == null || PassiveSkillManager.Instance == null)
        {
            Debug.LogWarning("[PassiveSkillTestInit] DataManager 또는 PassiveSkillManager.Instance가 없습니다.");
            return;
        }

        bool loadedExisting = DataManager.Instance.LoadPassiveData();
        if (!loadedExisting)
            PassiveSkillManager.Instance.CurrentProfile.gold = testGoldForFreshProfile;
    }
}
