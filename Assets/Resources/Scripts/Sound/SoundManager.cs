using UnityEngine;

public class SoundManager : Singleton<SoundManager>
{
    protected override void Awake()
    {
        base.Awake(); // 매우 중요: 싱글톤 초기화 로직 실행

        // 여기에 SoundManager만의 초기화 코드 작성
        Log.Print("사운드 매니저 초기화 완료");
    }

    public void PlayBGM(string clipName)
    {
        Log.Print($"{clipName} 배경음악 재생 중");
    }
}

// 사용 예시
//public class Player : MonoBehaviour
//{
//    void Start()
//    {
//        // 씬에 SoundManager 오브젝트가 없어도 자동으로 생성되며 작동합니다!
//        SoundManager.Instance.PlayBGM("MainTheme");
//    }
//}