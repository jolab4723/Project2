using System;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 4. 시스템 옵션 데이터. 게임 진행(세이브 슬롯)과 무관하게 기기/계정 단위로 유지되는 설정이라
    /// GameplaySaveData와는 별도 파일로 저장한다.
    /// 화면 옵션(해상도/창모드/밝기/최대 FPS/품질)은 ScreenOptionsManager가 적용/저장을 전담한다.
    /// TODO: 사운드/게임플레이 옵션 UI가 생기면 관련 필드 계속 채우기.
    /// </summary>
    [Serializable]
    public class SystemOptionsData
    {
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        // ===== 화면 옵션 =====

        /// <summary>ScreenOptionsManager.Resolutions 배열의 인덱스. 기본값 1 = 1920x1080.</summary>
        public int resolutionIndex = 1;

        public FullScreenMode windowMode = FullScreenMode.FullScreenWindow;

        /// <summary>0(완전 어둡게)~1(원본 밝기). 화면 전체를 덮는 검은 오버레이의 알파를 (1-brightness)로 계산하는 데 사용.</summary>
        [Range(0f, 1f)] public float brightness = 1f;

        /// <summary>Application.targetFrameRate에 그대로 사용. -1 = 무제한.</summary>
        public int maxFrameRate = 60;

        /// <summary>QualitySettings.SetQualityLevel 인덱스 (이 프로젝트는 0~5: Very Low~Ultra).</summary>
        public int qualityLevel = 2;
    }
}
