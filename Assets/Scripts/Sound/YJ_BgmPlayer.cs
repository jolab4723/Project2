// BGM 재생 기능과 별개로 씬 간 유지 및 중복 생성을 관리합니다.
public class YJ_BgmPlayer : Singleton<YJ_BgmPlayer>
{
    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            gameObject.SetActive(false);
    }
}
