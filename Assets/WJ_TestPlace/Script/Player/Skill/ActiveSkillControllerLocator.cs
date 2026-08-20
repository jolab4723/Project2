using UnityEngine;

/// <summary>
/// 지금 씬에서 활성 상태인 캐릭터의 ISkillController를 찾는다. 파이터/거너 등 여러 캐릭터 GameObject가
/// 동시에 존재해도, 비활성(SetActive(false))인 쪽은 FindObjectsOfType이 기본적으로 제외해주므로
/// 활성 캐릭터 하나만 찾힌다(캐릭터 전환 시 다른 쪽을 SetActive(false)하는 기존 관례에 의존).
///
/// DataManager의 액티브 스킬 저장/로드(107번)가 원래 이 스캔 로직을 자체적으로 갖고 있었는데,
/// SkillEvolutionSelectUI(K키 설정창)도 캐릭터가 바뀔 때마다 skillControllerBehaviour를 인스펙터에서
/// 손으로 재연결해야 하는 문제가 있어서(109~112번에서 반복적으로 남긴 미해결 메모) 공용 유틸로 뺐다(118번).
/// </summary>
public static class ActiveSkillControllerLocator
{
    public static ISkillController Find()
    {
        foreach (MonoBehaviour behaviour in Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (behaviour is ISkillController controller)
                return controller;
        }

        return null;
    }
}
