using UnityEngine;
using UnityEngine.UI;

namespace Core
{
    /// <summary>
    /// 화면 옵션(해상도/창모드/밝기/최대 FPS/품질) 5가지를 즉시 적용하고 DataManager에 저장하는 매니저.
    /// Set* 메서드는 전부 "적용 + 저장"을 한 번에 처리한다.
    ///
    /// !! 밝기는 URP Volume(ColorAdjustments.postExposure)이 아니라 화면 전체를 덮는 검은 오버레이의
    ///    알파로 구현했다. 씬마다 Global Volume을 따로 설정해두지 않아도 어디서든 동일하게 동작하게
    ///    하기 위한 임시 구현 - (1-brightness)를 오버레이 알파로 써서 brightness=1이면 안 보이고,
    ///    0이면 화면을 완전히 검게 덮는다. 나중에 정식으로 만들 때는 Volume 기반으로 교체할 수 있음.
    /// </summary>
    public class ScreenOptionsManager : Singleton<ScreenOptionsManager>
    {
        /// <summary>해상도 옵션 프리셋. KY_SettingsManager와 같은 4종 (나중에 병합 시 호환되도록).</summary>
        public static readonly (int width, int height)[] Resolutions =
        {
            (1280, 720),
            (1920, 1080),
            (2560, 1440),
            (3840, 2160),
        };

        public SystemOptionsData CurrentOptions { get; private set; }

        private Image brightnessOverlay;

        protected override void Awake()
        {
            base.Awake();
            CreateBrightnessOverlay();
            LoadAndApply();
        }

        /// <summary>DataManager에서 옵션을 불러와 5가지 화면 옵션을 전부 적용한다.</summary>
        public void LoadAndApply()
        {
            CurrentOptions = DataManager.Instance != null ? DataManager.Instance.LoadSystemOptions() : new SystemOptionsData();

            ApplyResolutionAndWindowMode();
            ApplyBrightness();
            ApplyMaxFrameRate();
            ApplyQualityLevel();
        }

        public void SetResolution(int resolutionIndex)
        {
            if (resolutionIndex < 0 || resolutionIndex >= Resolutions.Length)
            {
                Debug.LogWarning($"[ScreenOptionsManager] resolutionIndex는 0~{Resolutions.Length - 1}만 유효합니다: {resolutionIndex}");
                return;
            }

            CurrentOptions.resolutionIndex = resolutionIndex;
            ApplyResolutionAndWindowMode();
            Save();
        }

        public void SetWindowMode(FullScreenMode windowMode)
        {
            CurrentOptions.windowMode = windowMode;
            ApplyResolutionAndWindowMode();
            Save();
        }

        /// <summary>brightness: 0(완전 어둡게)~1(원본 밝기). 범위를 벗어나면 0~1로 clamp된다.</summary>
        public void SetBrightness(float brightness)
        {
            CurrentOptions.brightness = Mathf.Clamp01(brightness);
            ApplyBrightness();
            Save();
        }

        /// <summary>fps: 목표 프레임레이트. -1은 무제한.</summary>
        public void SetMaxFrameRate(int fps)
        {
            CurrentOptions.maxFrameRate = fps;
            ApplyMaxFrameRate();
            Save();
        }

        /// <summary>level: QualitySettings의 품질 레벨 인덱스 (이 프로젝트는 0~5).</summary>
        public void SetQualityLevel(int level)
        {
            CurrentOptions.qualityLevel = level;
            ApplyQualityLevel();
            Save();
        }

        private void ApplyResolutionAndWindowMode()
        {
            var (width, height) = Resolutions[CurrentOptions.resolutionIndex];
            Screen.SetResolution(width, height, CurrentOptions.windowMode);
        }

        private void ApplyBrightness()
        {
            if (brightnessOverlay == null)
                return;

            Color color = brightnessOverlay.color;
            color.a = 1f - CurrentOptions.brightness;
            brightnessOverlay.color = color;
        }

        private void ApplyMaxFrameRate()
        {
            Application.targetFrameRate = CurrentOptions.maxFrameRate;
        }

        private void ApplyQualityLevel()
        {
            QualitySettings.SetQualityLevel(CurrentOptions.qualityLevel, true);
        }

        private void Save()
        {
            DataManager.Instance?.SaveSystemOptions(CurrentOptions);
        }

        /// <summary>화면 전체를 덮는 검은 오버레이 Canvas/Image를 코드로 생성한다 (씬에 미리 배치할 필요 없음).</summary>
        private void CreateBrightnessOverlay()
        {
            GameObject canvasGO = new GameObject("BrightnessOverlay_Canvas");
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            canvasGO.AddComponent<CanvasScaler>();

            GameObject imageGO = new GameObject("Overlay");
            imageGO.transform.SetParent(canvasGO.transform, false);

            brightnessOverlay = imageGO.AddComponent<Image>();
            brightnessOverlay.color = new Color(0f, 0f, 0f, 0f);
            brightnessOverlay.raycastTarget = false;

            RectTransform rectTransform = imageGO.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
