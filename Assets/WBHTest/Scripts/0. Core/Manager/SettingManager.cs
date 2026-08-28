using UnityEngine;
using UnityEngine.UI;
using System.IO;

namespace Core
{
    public class SettingManager : Singleton<SettingManager>, IManagerModule
    {
        public string ModuleName => "SettingManager";

        private KY_SettingsData currentData = new KY_SettingsData();
        private string savePath;

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
            savePath = Application.persistentDataPath + "/settings.json";
            Load();
            Debug.Log("[SettingManager] 활성화 완료.");
        }

        // 데이터 반환
        public KY_SettingsData GetData() => currentData;

        // 적용
        public void Apply(KY_SettingsData data)
        {
            currentData = data;
            ApplyDisplay();
            ApplyBrightness();
            ApplyAudio();
            ApplyGameplay();
            Save();
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
            AudioListener.volume = currentData.isMuted ? 0f : currentData.masterVolume;
        }

        void ApplyGameplay()
        {
            // 나중에 연결
        }

        void Save()
        {
            string json = JsonUtility.ToJson(currentData, true);
            File.WriteAllText(savePath, json);
        }

        void Load()
        {
            if (File.Exists(savePath))
            {
                string json = File.ReadAllText(savePath);
                currentData = JsonUtility.FromJson<KY_SettingsData>(json);
            }
            Apply(currentData);
        }
    }
}
