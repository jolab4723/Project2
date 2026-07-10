using System;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 4. 시스템 옵션 데이터. 게임 진행(세이브 슬롯)과 무관하게 기기/계정 단위로 유지되는 설정이라
    /// GameplaySaveData와는 별도 파일로 저장한다.
    /// TODO: 실제 옵션 시스템(사운드/그래픽 설정 UI 등)이 생기면 필드 채우기. 지금은 예시로 볼륨만 잡아둠.
    /// </summary>
    [Serializable]
    public class SystemOptionsData
    {
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
    }
}
