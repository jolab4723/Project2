// 환경설정의 데이터를 들고있는 코드입니다.

using UnityEngine;

[System.Serializable]
public class KY_SettingsData
{
    [Header("화면")]
    public int resolutionIndex = 1;     // 해상도 (기본값 1920x1080)
    public bool isFullscreen = true;    // 창모드
    public float brightness = 1f;       // 밝기
    public int graphicsQuality = 2;     // 품질 (0=낮음, 1=중간, 2=높음)
    public int targetFrameRate = 1;     // FPS (0=30, 1=60, 2=무제한)

    [Header("소리")]
    public float masterVolume = 1f;     // 마스터
    public float bgmVolume = 1f;        // 배경음
    public float sfxVolume = 1f;        // 효과음
    public bool isMuted = false;        // 음소거

    [Header("게임플레이")]
    public int enemyHpDisplay = 0;      // 적 체력 표시 (0=항상, 1=피격시, 2=표시안함)(기본값 1)
    public bool showDamage = true;      // 데미지 표시
    public bool screenShake = true;     // 화면 흔들림
    public bool showAlliedBuffRanges = true; // SW 수정 : 타인 버프 범위의 로컬 표시만 제어한다.
    public int tutorialDisplay = 1;     // 튜토리얼 (0=항상, 1=한번만, 2=표시안함)
    // "한번만" 표시 설정에서 이미 안내를 끝냈는지 저장한다. 표시 방식과 완료 여부는 분리한다.
    public bool tutorialCompleted;
    public int language = 0;            // 언어 (GameLanguage 순서: 0=KOR, 1=ENG, 2=JPN, 3=CHN)
}
