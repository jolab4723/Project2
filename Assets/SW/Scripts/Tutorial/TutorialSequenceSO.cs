using System;
using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>
/// 튜토리얼 단계 목록. 단계 추가·순서 변경·문구 수정은 이 에셋만 편집한다.
/// 각 단계는 완료 조건 하나를 가지며 TutorialRunner가 순서대로 실행한다.
/// </summary>
[CreateAssetMenu(fileName = "TutorialSequence", menuName = "SW/Tutorial/Sequence")]
public sealed class TutorialSequenceSO : ScriptableObject
{
    public enum Condition
    {
        Next,          // 다음 버튼
        ZoneEnter,     // zoneId 구역 진입
        StateEntered,  // state 상태에 count회 진입(공격·회피·스킬)
        PotionUsed,    // 포션 1회 사용
        WaveCleared,   // wave를 생성하고 모두 처치
        ItemAcquired,  // item을 드랍하고 획득
        ItemEquipped,  // item 장착
    }

    public enum ClassFilter { Common, Fighter, Gunner }

    [Serializable]
    public sealed class Step
    {
        [Tooltip("UILabelDatabase 키. 키가 없으면 fallbackText를 쓴다.")]
        public string labelKey;
        [Tooltip("한국어 기본 문구. {Dodge}, {Skill1}, {Potion}, {OpenInventory}처럼 입력 액션 이름을 쓰면 현재 키로 바뀐다.")]
        [TextArea(1, 3)] public string fallbackText;
        public ClassFilter classFilter;
        public Condition condition;

        public string zoneId;
        public PlayerState state = PlayerState.Attack;
        [Min(1)] public int count = 1;
        public WBH_WaveData wave;
        public ItemDefinitionSO item;

        public bool showDim;
        [Tooltip("문구를 보여 주는 동안 시간을 멈춘다. 행동을 기다리는 단계에서는 끈다.")]
        public bool pause;

        public bool Matches(CharacterClass character) =>
            classFilter == ClassFilter.Common ||
            (classFilter == ClassFilter.Fighter) == (character == CharacterClass.Fighter);
    }

    [SerializeField] private List<Step> steps = new();

    public IReadOnlyList<Step> Steps => steps;
}
