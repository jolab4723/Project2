using Core;
using NUnit.Framework;

namespace Project2.Tests.FirebasePersistence
{
    public sealed class PlayerProfileCreditTests
    {
        /// <summary>
        /// 액트 보상은 정상 금액만 더하고 음수나 int 범위를 넘는 값은 프로필을 바꾸지 않는지 확인합니다.
        /// </summary>
        [Test]
        public void TryApplyCredit_AcceptsRewardAndRejectsInvalidAmount()
        {
            PlayerProfileData profile = new PlayerProfileData { credit = 2000 };

            Assert.That(profile.TryApplyCredit(350), Is.True);
            Assert.That(profile.credit, Is.EqualTo(2350));
            Assert.That(profile.TryApplyCredit(-1), Is.False);
            Assert.That(profile.credit, Is.EqualTo(2350));

            profile.credit = int.MaxValue;
            Assert.That(profile.TryApplyCredit(1), Is.False);
            Assert.That(profile.credit, Is.EqualTo(int.MaxValue));
        }
    }
}
