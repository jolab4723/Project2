using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using System.IO;

namespace Core
{
    public class SettingManager : Singleton<SettingManager>, IManagerModule
    {
        public string ModuleName => "SettingManager";

        private KY_SettingsData currentData = new KY_SettingsData();
        private string savePath;

        [Header("Audio")]
        [SerializeField] private AudioMixer audioMixer;
        private bool audioReady;

        private void Start()
        {
            // AudioMixer.SetFloat는 Awake/OnEnable 대신 Start 이후에 적용합니다.
            audioReady = true;
            ApplyMixerVolumes();
        }

        // 밝기 - Unity 기본 API(Screen.brightness)는 모바일 전용이라 PC 빌드에선 못 쓴다. 화면 전체를
        // 덮는 반투명 검은 오버레이로 흉내 낸다(밝기가 낮을수록 오버레이가 진해짐 - 그래서 이 방식으로는
        // "원래 화면보다 밝게"는 못 만들고 어둡게만 가능하다, 대부분의 게임이 쓰는 방식과 동일).
        private Canvas brightnessOverlayCanvas;
        private Image brightnessOverlayImage;

        // brightness가 이보다 낮아지면 화면이 아예 안 보여서 설정 화면조차 못 찾는 상황을 막기 위해
        // 오버레이 알파의 최댓값을 제한한다(완전 암전 방지).
        private const float MaxBrightnessOverlayAlpha = 0.85f;

        private readonly int[] frameRates = { 30, 60, -1 }; // -1은 무제한

        private readonly (int width, int height)[] resolutions =
        {
        (1280, 720),
        (1920, 1080),
        (2560, 1440),
        (3840, 2160)
    };

        public void Activate()
        {
            // SW 수정: --test-login 개발 계정은 같은 PC의 다른 인스턴스와 설정 파일을 공유하지 않는다.
            string testAccount = FirebaseService.Default.IsLocalTestAccount ? "_" + FirebaseService.Default.LocalTestUserId : string.Empty;
            savePath = Application.persistentDataPath + "/settings" + testAccount + ".json";
            Load();
            Debug.Log("[SettingManager] 활성화 완료.");
        }

        // 데이터 반환
        public KY_SettingsData GetData() => currentData;

        // 적용
        public void Apply(KY_SettingsData data)
        {
            ApplyWithoutSave(data);
            Save();
        }

        // SW 수정: 파일에서 읽은 값은 적용만 하고 다시 쓰지 않는다. 씬마다 Activate가 불려도 디스크 쓰기가 생기지 않는다.
        private void ApplyWithoutSave(KY_SettingsData data)
        {
            currentData = data;
            ApplyDisplay();
            ApplyBrightness();
            ApplyAudio();
            ApplyGameplay();
            ApplyLanguage();
        }

        void ApplyDisplay()
        {
            var res = resolutions[currentData.resolutionIndex];
            Screen.SetResolution(res.width, res.height, currentData.isFullscreen);
            QualitySettings.SetQualityLevel(currentData.graphicsQuality);
            Application.targetFrameRate = frameRates[currentData.targetFrameRate];
        }

        void ApplyBrightness()
        {
            EnsureBrightnessOverlay();

            // brightness 1(기본값)이면 오버레이 없음(알파 0), 낮을수록 어두워진다. 슬라이더가 1을
            // 넘는 값도 허용하더라도(더 밝게는 이 방식으로 못 만드므로) 1 이상은 전부 알파 0으로 처리한다.
            float darkness = Mathf.Clamp01(1f - currentData.brightness);
            Color color = brightnessOverlayImage.color;
            color.a = darkness * MaxBrightnessOverlayAlpha;
            brightnessOverlayImage.color = color;
        }

        void EnsureBrightnessOverlay()
        {
            if (brightnessOverlayCanvas != null)
                return;

            var canvasObj = new GameObject("BrightnessOverlayCanvas");
            DontDestroyOnLoad(canvasObj);

            brightnessOverlayCanvas = canvasObj.AddComponent<Canvas>();
            brightnessOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            brightnessOverlayCanvas.sortingOrder = short.MaxValue - 10; // 다른 UI/팝업보다 위에 그려지게

            canvasObj.AddComponent<CanvasScaler>();

            var imageObj = new GameObject("BrightnessOverlayImage");
            imageObj.transform.SetParent(canvasObj.transform, false);

            brightnessOverlayImage = imageObj.AddComponent<Image>();
            brightnessOverlayImage.color = new Color(0f, 0f, 0f, 0f);
            brightnessOverlayImage.raycastTarget = false; // 클릭/입력을 가리지 않음

            RectTransform rect = brightnessOverlayImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        void ApplyAudio()
        {
            currentData.masterVolume = Mathf.Clamp01(currentData.masterVolume);
            currentData.bgmVolume = Mathf.Clamp01(currentData.bgmVolume);
            currentData.sfxVolume = Mathf.Clamp01(currentData.sfxVolume);
            AudioListener.volume = currentData.isMuted ? 0f : currentData.masterVolume;
            if (audioReady)
                ApplyMixerVolumes();
        }

        private void ApplyMixerVolumes()
        {
            if (audioMixer == null)
            {
                Debug.LogWarning("[SettingManager] Audio Mixer를 연결하세요. BGM/SFX 개별 음량은 적용되지 않습니다.", this);
                return;
            }

            // 전체 음량/음소거는 AudioListener에서만 처리하여 중복 곱셈을 피합니다.
            if (!audioMixer.SetFloat("BgmVolume", ToDecibels(currentData.bgmVolume)))
                Debug.LogWarning("[SettingManager] 믹서의 BgmVolume 노출 파라미터를 확인하세요.", this);
            if (!audioMixer.SetFloat("SfxVolume", ToDecibels(currentData.sfxVolume)))
                Debug.LogWarning("[SettingManager] 믹서의 SfxVolume 노출 파라미터를 확인하세요.", this);
        }

        private static float ToDecibels(float volume)
        {
            volume = Mathf.Clamp01(volume);
            return volume <= 0.0001f ? -80f : 20f * Mathf.Log10(volume);
        }

        void ApplyGameplay()
        {
            // 나중에 연결
        }

        // 언어 - KY_SettingsPopup의 드롭다운이 즉시 미리보기로 YJ_LanguageManager를 직접 부르지만,
        // 그건 디스크에 저장되지 않는다. Apply()에서 저장된 값을 다시 넣어줘야 앱을 재시작해도
        // 마지막으로 고른 언어가 유지된다(SetLanguage는 같은 언어면 아무 것도 안 하므로 중복 호출 안전).
        void ApplyLanguage()
        {
            if (YJ_LanguageManager.Instance != null)
                YJ_LanguageManager.Instance.SetLanguage((GameLanguage)currentData.language);
        }

        void Save()
        {
            string json = JsonUtility.ToJson(currentData, true);
            // SW 수정: 다른 프로세스가 파일을 쓰는 중이면 이번 저장만 건너뛰고 호출한 화면 초기화는 계속한다.
            try { File.WriteAllText(savePath, json); }
            catch (IOException exception) { Debug.LogWarning($"[SettingManager] 설정 저장 실패: {exception.Message}"); }
        }

        void Load()
        {
            bool exists = File.Exists(savePath);
            if (exists)
            {
                // SW 수정: 읽기 충돌이면 현재 값으로 진행한다.
                try { currentData = JsonUtility.FromJson<KY_SettingsData>(File.ReadAllText(savePath)); }
                catch (IOException exception) { Debug.LogWarning($"[SettingManager] 설정 읽기 실패: {exception.Message}"); }
            }
            ApplyWithoutSave(currentData);
            // 처음 실행하면 기존처럼 기본값 파일을 한 번 만든다.
            if (!exists) Save();
        }
    }
}
