#if !UNITY_EDITOR
namespace UnityEngine.SocialPlatforms.GameCenter
{
}

namespace Microsoft.Unity.VisualStudio.Editor
{
    /// <summary>
    /// 팀원 원본의 Editor 전용 Image 타입 참조가 Player 빌드를 막지 않도록 하는 테스트 빌드용 호환 타입입니다.
    /// 실제 게임 데이터나 UI에는 사용하지 않습니다.
    /// </summary>
    public sealed class Image
    {
    }
}
#endif
