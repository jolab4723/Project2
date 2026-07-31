import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "C:/Users/user/Desktop/UnknownStageTable.xlsx";
const outputDir = "C:/Users/user/Project2/outputs/unknown-stage-table-additional-20-20260727";
const outputPath = `${outputDir}/UnknownStageTable_40Quests.xlsx`;
const mode = process.argv[2] ?? "build";

await fs.mkdir(outputDir, { recursive: true });

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);
const sheet = workbook.worksheets.getItem("UnknownLangTable");

const originalCheck = await workbook.inspect({
  kind: "table",
  range: "UnknownLangTable!A1:J62",
  include: "values,formulas",
  tableMaxRows: 8,
  tableMaxCols: 10,
  tableMaxCellChars: 100,
  maxChars: 6000,
});
console.log(originalCheck.ndjson);

const originalPreview = await workbook.render({
  sheetName: sheet.name,
  range: "A1:J62",
  scale: 1,
  format: "png",
});
await fs.writeFile(
  `${outputDir}/original-preview.png`,
  new Uint8Array(await originalPreview.arrayBuffer()),
);

if (mode === "inspect") {
  process.exit(0);
}

const languages = [
  { key: "ko", offset: 0 },
  { key: "en", offset: 10000 },
  { key: "ja", offset: 20000 },
];

const events = [
  {
    ko: {
      title: "무전 속 목소리",
      description: "잡음뿐인 무전기에서 누군가 계속 구조를 요청합니다.\n다만 신호 상태보다 말투가 더 침착해서 조금 얄밉습니다.",
      choices: [
        ["응답한다.", "무작위 소모품 1개를 획득합니다.\n다음 전투에서 적의 공격력이 5% 증가합니다."],
        ["주파수를 맞춘다.", "다음 미지 스테이지의 선택지 효과를 미리 확인합니다."],
        ["전원을 끈다.", "체력을 최대 체력의 10%만큼 회복합니다. 조용함도 자원입니다."],
      ],
    },
    en: {
      title: "A Voice on the Radio",
      description: "A voice keeps asking for rescue through a radio full of static.\nIt sounds annoyingly calmer than the signal quality deserves.",
      choices: [
        ["Answer.", "Gain 1 random consumable.\nEnemy attack increases by 5% in the next battle."],
        ["Tune the frequency.", "Preview the effects of choices in the next Unknown Stage."],
        ["Turn it off.", "Restore 10% of maximum HP. Silence is a resource too."],
      ],
    },
    ja: {
      title: "無線機の声",
      description: "雑音だらけの無線機から、誰かが救助を求め続けています。\n通信状態に似合わないほど落ち着いた口調で、少し癪に障ります。",
      choices: [
        ["応答する。", "ランダムな消耗品を1個獲得します。\n次の戦闘で敵の攻撃力が5%増加します。"],
        ["周波数を合わせる。", "次の未知ステージの選択肢の効果を事前に確認します。"],
        ["電源を切る。", "最大HPの10%を回復します。静けさも資源です。"],
      ],
    },
  },
  {
    ko: {
      title: "거꾸로 가는 시계",
      description: "벽시계의 초침이 성실하게 과거로 달려가고 있습니다.\n마감도 같이 멀어지는지는 아직 검증되지 않았습니다.",
      choices: [
        ["시계를 앞으로 감는다.", "스킬 재사용 대기시간이 5% 감소합니다.\n최대 체력의 10%만큼 피해를 받습니다."],
        ["시간을 기다린다.", "다음 전투에서 처음 받는 능력치 감소 효과를 무시합니다."],
        ["시곗바늘을 떼어낸다.", "골드 30을 획득합니다. 시간은 금이라는 말이 사실이었습니다."],
      ],
    },
    en: {
      title: "The Backward Clock",
      description: "A wall clock is diligently running into the past.\nWhether deadlines are moving with it remains unverified.",
      choices: [
        ["Wind it forward.", "Skill cooldown decreases by 5%.\nTake damage equal to 10% of maximum HP."],
        ["Wait for time.", "Ignore the first stat-reduction effect in the next battle."],
        ["Remove the hands.", "Gain 30 Gold. Time really was money."],
      ],
    },
    ja: {
      title: "逆行する時計",
      description: "壁時計の秒針が、真面目に過去へ向かって走っています。\n締め切りまで遠ざかるかどうかは未検証です。",
      choices: [
        ["時計を進める。", "スキルのクールダウンが5%減少します。\n最大HPの10%分のダメージを受けます。"],
        ["時を待つ。", "次の戦闘で最初に受ける能力低下効果を無視します。"],
        ["針を取り外す。", "ゴールドを30獲得します。時は金なりでした。"],
      ],
    },
  },
  {
    ko: {
      title: "자동판매기와의 협상",
      description: "고장 난 자동판매기가 가격을 직접 흥정하기 시작했습니다.\n동전 투입구가 이렇게 협상력이 좋을 줄은 몰랐습니다.",
      choices: [
        ["골드 20을 넣는다.", "골드 20을 잃고 무작위 소모품 2개를 획득합니다."],
        ["기계를 협박한다.", "50% 확률로 무작위 유물 1개를 획득하고,\n50% 확률로 최대 체력의 15%만큼 피해를 받습니다."],
        ["디자인을 칭찬한다.", "체력을 최대 체력의 15%만큼 회복합니다. 기계도 칭찬에 약합니다."],
      ],
    },
    en: {
      title: "Negotiating with a Vending Machine",
      description: "A broken vending machine has begun haggling over its own prices.\nNo one expected the coin slot to be this persuasive.",
      choices: [
        ["Insert 20 Gold.", "Lose 20 Gold and gain 2 random consumables."],
        ["Threaten the machine.", "50% chance to gain 1 random relic,\nand 50% chance to take damage equal to 15% of maximum HP."],
        ["Compliment its design.", "Restore 15% of maximum HP. Machines enjoy compliments too."],
      ],
    },
    ja: {
      title: "自動販売機との交渉",
      description: "故障した自動販売機が、自分で値段交渉を始めました。\n硬貨投入口にこれほどの交渉力があるとは思いませんでした。",
      choices: [
        ["ゴールドを20入れる。", "ゴールドを20失い、ランダムな消耗品を2個獲得します。"],
        ["機械を脅す。", "50%の確率でランダムな遺物を1個獲得し、\n50%の確率で最大HPの15%分のダメージを受けます。"],
        ["デザインを褒める。", "最大HPの15%を回復します。機械も褒め言葉には弱いようです。"],
      ],
    },
  },
  {
    ko: {
      title: "전력세 체납",
      description: "시설 관리 단말기가 수백 년 치 전력세를 청구합니다.\n연체료 항목만 별도의 역사책이 필요할 정도입니다.",
      choices: [
        ["골드 25를 납부한다.", "골드 25를 잃고 공격력이 4% 증가합니다."],
        ["이의를 제기한다.", "50% 확률로 효과가 없고,\n50% 확률로 다음 전투에서 적의 공격력이 10% 감소합니다."],
        ["차단기를 내린다.", "무작위 유물 1개를 획득합니다.\n다음 전투에서 이동 속도가 10% 감소합니다."],
      ],
    },
    en: {
      title: "Overdue Power Bill",
      description: "A facility terminal presents several centuries of unpaid power bills.\nThe late-fee section needs its own history book.",
      choices: [
        ["Pay 25 Gold.", "Lose 25 Gold and increase attack by 4%."],
        ["File an appeal.", "50% chance of no effect,\nand 50% chance for enemy attack to decrease by 10% in the next battle."],
        ["Pull the breaker.", "Gain 1 random relic.\nMovement speed decreases by 10% in the next battle."],
      ],
    },
    ja: {
      title: "滞納された電気代",
      description: "施設管理端末が、数百年分の電気代を請求しています。\n延滞料の項目だけで歴史書が一冊必要そうです。",
      choices: [
        ["ゴールドを25支払う。", "ゴールドを25失い、攻撃力が4%増加します。"],
        ["異議を申し立てる。", "50%の確率で効果はなく、\n50%の確率で次の戦闘中、敵の攻撃力が10%減少します。"],
        ["ブレーカーを落とす。", "ランダムな遺物を1個獲得します。\n次の戦闘で移動速度が10%減少します。"],
      ],
    },
  },
  {
    ko: {
      title: "미니어처 전쟁터",
      description: "탁자 위에서 손바닥만 한 로봇들이 치열하게 전쟁 중입니다.\n전쟁 규모는 작지만 지휘관들의 목소리는 전혀 작지 않습니다.",
      choices: [
        ["청색 진영을 돕는다.", "방어력이 5% 증가합니다.\n현재 체력이 최대 체력의 8%만큼 감소합니다."],
        ["적색 진영을 돕는다.", "공격력이 5% 증가합니다.\n현재 체력이 최대 체력의 8%만큼 감소합니다."],
        ["전쟁을 중재한다.", "골드 20을 획득합니다. 양쪽 모두 당신이 떠나길 원했습니다."],
      ],
    },
    en: {
      title: "The Miniature Battlefield",
      description: "Palm-sized robots are waging a fierce war across a tabletop.\nThe war is small. The commanders are not.",
      choices: [
        ["Aid the blue side.", "Defense increases by 5%.\nCurrent HP decreases by 8% of maximum HP."],
        ["Aid the red side.", "Attack increases by 5%.\nCurrent HP decreases by 8% of maximum HP."],
        ["Mediate the war.", "Gain 20 Gold. Both sides mostly wanted you to leave."],
      ],
    },
    ja: {
      title: "ミニチュア戦場",
      description: "机の上で、手のひらサイズのロボットたちが激戦を繰り広げています。\n戦争の規模は小さくても、指揮官の声はまったく小さくありません。",
      choices: [
        ["青陣営を助ける。", "防御力が5%増加します。\n現在HPが最大HPの8%分減少します。"],
        ["赤陣営を助ける。", "攻撃力が5%増加します。\n現在HPが最大HPの8%分減少します。"],
        ["戦争を仲裁する。", "ゴールドを20獲得します。両陣営とも、あなたに帰ってほしかったようです。"],
      ],
    },
  },
  {
    ko: {
      title: "거울 속 정비사",
      description: "거울 속의 당신이 정비 도구를 들고 먼저 손을 흔듭니다.\n적어도 저쪽의 당신은 사용 설명서를 읽은 표정입니다.",
      choices: [
        ["수리를 맡긴다.", "최대 체력이 5% 증가합니다.\n현재 체력이 최대 체력의 10%만큼 감소합니다."],
        ["도구를 교환한다.", "무작위 소모품 2개를 획득합니다."],
        ["거울을 닦는다.", "체력을 최대 체력의 20%만큼 회복합니다. 얼룩만 고쳐졌습니다."],
      ],
    },
    en: {
      title: "The Mechanic in the Mirror",
      description: "Your reflection waves first, holding a set of maintenance tools.\nAt least that version of you looks like it read the manual.",
      choices: [
        ["Accept the repair.", "Maximum HP increases by 5%.\nCurrent HP decreases by 10% of maximum HP."],
        ["Trade tools.", "Gain 2 random consumables."],
        ["Clean the mirror.", "Restore 20% of maximum HP. Only the smudges were repaired."],
      ],
    },
    ja: {
      title: "鏡の中の整備士",
      description: "鏡の中のあなたが整備工具を持ち、先に手を振ってきます。\n少なくとも向こうのあなたは説明書を読んだ顔をしています。",
      choices: [
        ["修理を任せる。", "最大HPが5%増加します。\n現在HPが最大HPの10%分減少します。"],
        ["工具を交換する。", "ランダムな消耗品を2個獲得します。"],
        ["鏡を磨く。", "最大HPの20%を回復します。直ったのは汚れだけでした。"],
      ],
    },
  },
  {
    ko: {
      title: "냉각수 온천",
      description: "배관이 터진 자리에 따뜻한 냉각수 웅덩이가 생겼습니다.\n'냉각수'와 '따뜻함'의 조합부터 이미 신뢰가 가지 않습니다.",
      choices: [
        ["몸을 담근다.", "체력을 모두 회복합니다.\n다음 전투에서 방어력이 5% 감소합니다."],
        ["병에 담는다.", "무작위 소모품 2개를 획득합니다."],
        ["온도를 더 올린다.", "다음 전투에서 공격 속도가 8% 증가합니다."],
      ],
    },
    en: {
      title: "The Coolant Hot Spring",
      description: "A burst pipe has formed a pleasantly warm pool of coolant.\nThe words 'warm coolant' are already doing little to inspire confidence.",
      choices: [
        ["Take a soak.", "Fully restore HP.\nDefense decreases by 5% in the next battle."],
        ["Bottle some.", "Gain 2 random consumables."],
        ["Raise the temperature.", "Attack speed increases by 8% in the next battle."],
      ],
    },
    ja: {
      title: "冷却液温泉",
      description: "破裂した配管の下に、温かい冷却液の水たまりができています。\n「温かい冷却液」という時点で、すでに信用できません。",
      choices: [
        ["浸かる。", "HPを全回復します。\n次の戦闘で防御力が5%減少します。"],
        ["瓶に詰める。", "ランダムな消耗品を2個獲得します。"],
        ["温度を上げる。", "次の戦闘で攻撃速度が8%増加します。"],
      ],
    },
  },
  {
    ko: {
      title: "복권 프린터",
      description: "낡은 프린터가 당첨 확률 100%라고 적힌 복권을 출력합니다.\n작은 글씨에는 '무언가에는 당첨됨'이라고 적혀 있습니다.",
      choices: [
        ["골드 10으로 구매한다.", "골드 10을 잃습니다.\n30% 확률로 골드 60을 획득합니다."],
        ["프린터를 흔든다.", "무작위 유물 1개를 획득하고 최대 체력의 20%만큼 피해를 받습니다."],
        ["전원 코드를 뽑는다.", "골드 5를 획득합니다. 환불 절차가 의외로 빨랐습니다."],
      ],
    },
    en: {
      title: "The Lottery Printer",
      description: "An old printer produces a ticket advertising a 100% chance to win.\nThe fine print says, 'You will win something.'",
      choices: [
        ["Buy it for 10 Gold.", "Lose 10 Gold.\n30% chance to gain 60 Gold."],
        ["Shake the printer.", "Gain 1 random relic and take damage equal to 20% of maximum HP."],
        ["Unplug it.", "Gain 5 Gold. The refund process was surprisingly fast."],
      ],
    },
    ja: {
      title: "宝くじプリンター",
      description: "古いプリンターが「当選確率100%」と書かれたくじを印刷します。\n小さな文字には「何かには当たります」とあります。",
      choices: [
        ["ゴールド10で買う。", "ゴールドを10失います。\n30%の確率でゴールドを60獲得します。"],
        ["プリンターを揺らす。", "ランダムな遺物を1個獲得し、最大HPの20%分のダメージを受けます。"],
        ["電源を抜く。", "ゴールドを5獲得します。返金手続きは意外と迅速でした。"],
      ],
    },
  },
  {
    ko: {
      title: "수상한 안전 교육",
      description: "홀로그램 강사가 48시간짜리 안전 교육을 시작합니다.\n첫 강의 제목은 '긴 교육으로 인한 건강 위험'입니다.",
      choices: [
        ["끝까지 수강한다.", "방어력이 6% 증가하고 공격력이 2% 감소합니다."],
        ["시험만 본다.", "공격력이 4% 증가합니다.\n다음 전투에서 방어력이 4% 감소합니다."],
        ["비상구로 나간다.", "효과가 없습니다. 안전 교육의 핵심을 실천했습니다."],
      ],
    },
    en: {
      title: "Suspicious Safety Training",
      description: "A holographic instructor begins a 48-hour safety course.\nThe first lecture is titled 'Health Risks of Excessively Long Training.'",
      choices: [
        ["Complete the course.", "Defense increases by 6% and attack decreases by 2%."],
        ["Take only the exam.", "Attack increases by 4%.\nDefense decreases by 4% in the next battle."],
        ["Use the emergency exit.", "No effect. You demonstrated the core lesson."],
      ],
    },
    ja: {
      title: "怪しい安全講習",
      description: "ホログラム講師が48時間の安全講習を始めます。\n最初の講義は「長すぎる研修による健康被害」です。",
      choices: [
        ["最後まで受講する。", "防御力が6%増加し、攻撃力が2%減少します。"],
        ["試験だけ受ける。", "攻撃力が4%増加します。\n次の戦闘で防御力が4%減少します。"],
        ["非常口から出る。", "効果はありません。安全講習の要点を実践しました。"],
      ],
    },
  },
  {
    ko: {
      title: "배고픈 압축기",
      description: "산업용 압축기가 입을 벌린 채 금속을 달라고 표시합니다.\n기계에 입이 없다는 지적은 이미 압축된 듯합니다.",
      choices: [
        ["골드 15를 먹인다.", "골드 15를 잃고 무작위 소모품 2개를 획득합니다."],
        ["장비 부품을 먹인다.", "공격력이 3% 감소하고 무작위 유물 1개를 획득합니다."],
        ["아무것도 주지 않는다.", "최대 체력의 10%만큼 피해를 받습니다.\n다음 전투에서 이동 속도가 10% 증가합니다."],
      ],
    },
    en: {
      title: "The Hungry Compactor",
      description: "An industrial compactor waits with its mouth open, requesting metal.\nAny objection that machines have no mouths appears to have been compacted.",
      choices: [
        ["Feed it 15 Gold.", "Lose 15 Gold and gain 2 random consumables."],
        ["Feed it an equipment part.", "Attack decreases by 3% and you gain 1 random relic."],
        ["Feed it nothing.", "Take damage equal to 10% of maximum HP.\nMovement speed increases by 10% in the next battle."],
      ],
    },
    ja: {
      title: "腹を空かせた圧縮機",
      description: "産業用圧縮機が口を開け、金属を要求しています。\n機械に口はないという指摘は、すでに圧縮されたようです。",
      choices: [
        ["ゴールドを15食べさせる。", "ゴールドを15失い、ランダムな消耗品を2個獲得します。"],
        ["装備部品を食べさせる。", "攻撃力が3%減少し、ランダムな遺物を1個獲得します。"],
        ["何も与えない。", "最大HPの10%分のダメージを受けます。\n次の戦闘で移動速度が10%増加します。"],
      ],
    },
  },
  {
    ko: {
      title: "정전된 아케이드",
      description: "전원이 꺼진 게임기들이 당신을 향해 HIGH SCORE를 깜빡입니다.\n플레이하지도 않았는데 이미 기록 경쟁에 휘말렸습니다.",
      choices: [
        ["골드 10을 넣는다.", "골드 10을 잃고 공격 속도가 5% 증가합니다."],
        ["게임기를 수리한다.", "골드 30을 획득하고 최대 체력의 10%만큼 피해를 받습니다."],
        ["기록이 깨지길 기다린다.", "체력을 최대 체력의 20%만큼 회복합니다. 경쟁자가 없었습니다."],
      ],
    },
    en: {
      title: "The Blacked-Out Arcade",
      description: "Unpowered arcade cabinets blink HIGH SCORE in your direction.\nYou have somehow entered a competition without playing.",
      choices: [
        ["Insert 10 Gold.", "Lose 10 Gold and increase attack speed by 5%."],
        ["Repair a cabinet.", "Gain 30 Gold and take damage equal to 10% of maximum HP."],
        ["Wait for the record to fall.", "Restore 20% of maximum HP. There were no other players."],
      ],
    },
    ja: {
      title: "停電したゲームセンター",
      description: "電源の落ちたゲーム機が、あなたに向けてHIGH SCOREを点滅させます。\n遊んでもいないのに記録争いへ参加させられました。",
      choices: [
        ["ゴールドを10入れる。", "ゴールドを10失い、攻撃速度が5%増加します。"],
        ["ゲーム機を修理する。", "ゴールドを30獲得し、最大HPの10%分のダメージを受けます。"],
        ["記録が破られるのを待つ。", "最大HPの20%を回復します。他にプレイヤーはいませんでした。"],
      ],
    },
  },
  {
    ko: {
      title: "반품된 행운",
      description: "당신 이름으로 배송된 '행운' 상자가 반품 도장과 함께 놓여 있습니다.\n반품 사유는 '수취인 부재'. 조금 억울합니다.",
      choices: [
        ["상자를 연다.", "무작위 유물 1개를 획득합니다.\n다음 전투에서 적의 공격력이 5% 증가합니다."],
        ["발송인에게 돌려보낸다.", "골드 30을 획득합니다."],
        ["수취를 거부한다.", "최대 체력이 3% 증가합니다. 신중함도 운의 일부입니다."],
      ],
    },
    en: {
      title: "Returned Luck",
      description: "A crate labeled 'Luck' in your name sits under a RETURNED stamp.\nThe reason says 'Recipient absent.' That feels unfair.",
      choices: [
        ["Open the crate.", "Gain 1 random relic.\nEnemy attack increases by 5% in the next battle."],
        ["Return it to sender.", "Gain 30 Gold."],
        ["Refuse delivery.", "Maximum HP increases by 3%. Caution is part of luck."],
      ],
    },
    ja: {
      title: "返品された幸運",
      description: "あなた宛ての「幸運」と書かれた箱に、返品印が押されています。\n理由は「受取人不在」。少し納得できません。",
      choices: [
        ["箱を開ける。", "ランダムな遺物を1個獲得します。\n次の戦闘で敵の攻撃力が5%増加します。"],
        ["送り主へ返す。", "ゴールドを30獲得します。"],
        ["受け取りを拒否する。", "最大HPが3%増加します。慎重さも幸運の一部です。"],
      ],
    },
  },
  {
    ko: {
      title: "불법 개조 성소",
      description: "인증 마크가 하나도 없는 개조 장치가 경건하게 빛납니다.\n안전 규정은 없지만 분위기만큼은 정품입니다.",
      choices: [
        ["출력을 과충전한다.", "공격력이 8% 증가하고 최대 체력이 5% 감소합니다."],
        ["장갑을 보강한다.", "방어력이 8% 증가하고 이동 속도가 5% 감소합니다."],
        ["절만 하고 떠난다.", "체력을 최대 체력의 10%만큼 회복합니다."],
      ],
    },
    en: {
      title: "The Unauthorized Upgrade Shrine",
      description: "An upgrade device with no certification marks glows reverently.\nIt lacks safety standards, but the atmosphere feels completely official.",
      choices: [
        ["Overcharge the output.", "Attack increases by 8% and maximum HP decreases by 5%."],
        ["Reinforce the armor.", "Defense increases by 8% and movement speed decreases by 5%."],
        ["Bow and leave.", "Restore 10% of maximum HP."],
      ],
    },
    ja: {
      title: "違法改造の祭壇",
      description: "認証マークが一つもない改造装置が、神々しく輝いています。\n安全規格はありませんが、雰囲気だけは正規品です。",
      choices: [
        ["出力を過充電する。", "攻撃力が8%増加し、最大HPが5%減少します。"],
        ["装甲を強化する。", "防御力が8%増加し、移動速度が5%減少します。"],
        ["一礼して去る。", "最大HPの10%を回復します。"],
      ],
    },
  },
  {
    ko: {
      title: "잊힌 생일 파티",
      description: "아무도 없는 방에 생일 케이크와 고깔모자가 준비되어 있습니다.\n초의 개수를 세는 일은 중간에 포기하는 편이 정신 건강에 좋습니다.",
      choices: [
        ["초를 밝힌다.", "체력을 최대 체력의 25%만큼 회복하고 최대 체력이 2% 증가합니다."],
        ["케이크를 먹는다.", "체력을 모두 회복합니다.\n다음 전투에서 공격 속도가 8% 감소합니다."],
        ["선물만 챙긴다.", "무작위 유물 1개를 획득합니다. 예의는 포장지 안에 두고 왔습니다."],
      ],
    },
    en: {
      title: "The Forgotten Birthday Party",
      description: "A cake and party hats wait in an empty room.\nFor your mental health, it is best not to finish counting the candles.",
      choices: [
        ["Light the candles.", "Restore 25% of maximum HP and increase maximum HP by 2%."],
        ["Eat the cake.", "Fully restore HP.\nAttack speed decreases by 8% in the next battle."],
        ["Take only the gift.", "Gain 1 random relic. Your manners remain inside the wrapping."],
      ],
    },
    ja: {
      title: "忘れられた誕生日会",
      description: "誰もいない部屋に、誕生日ケーキとパーティー帽が用意されています。\nろうそくの本数は、途中で数えるのをやめた方が心に優しそうです。",
      choices: [
        ["ろうそくに火をつける。", "最大HPの25%を回復し、最大HPが2%増加します。"],
        ["ケーキを食べる。", "HPを全回復します。\n次の戦闘で攻撃速度が8%減少します。"],
        ["プレゼントだけ持っていく。", "ランダムな遺物を1個獲得します。礼儀は包装紙の中に置いてきました。"],
      ],
    },
  },
  {
    ko: {
      title: "궤도 엘리베이터 안내방송",
      description: "천장 스피커가 곧 궤도 엘리베이터가 도착한다고 안내합니다.\n엘리베이터도, 궤도도 보이지 않지만 방송은 자신만만합니다.",
      choices: [
        ["안내대로 엎드린다.", "다음 전투에서 방어력이 10% 증가합니다."],
        ["민원을 접수한다.", "골드 20을 획득합니다. 고객 보상금이라고 주장합니다."],
        ["비상 호출을 누른다.", "무작위 소모품 1개를 획득합니다."],
      ],
    },
    en: {
      title: "Orbital Elevator Announcement",
      description: "A ceiling speaker announces the imminent arrival of an orbital elevator.\nNeither an elevator nor an orbit is visible, but the announcement is confident.",
      choices: [
        ["Lie down as instructed.", "Defense increases by 10% in the next battle."],
        ["File a complaint.", "Gain 20 Gold. You insist it is customer compensation."],
        ["Press the emergency call.", "Gain 1 random consumable."],
      ],
    },
    ja: {
      title: "軌道エレベーターの案内放送",
      description: "天井のスピーカーが、まもなく軌道エレベーターが到着すると告げます。\nエレベーターも軌道も見えませんが、放送だけは自信満々です。",
      choices: [
        ["案内どおり伏せる。", "次の戦闘で防御力が10%増加します。"],
        ["苦情を申し立てる。", "ゴールドを20獲得します。顧客補償金だと言い張ります。"],
        ["非常呼出ボタンを押す。", "ランダムな消耗品を1個獲得します。"],
      ],
    },
  },
  {
    ko: {
      title: "먼지로 쓴 지도",
      description: "바닥의 먼지 위에 누군가 지름길을 그려 놓았습니다.\n지도 제작자의 손가락 크기로 보아 신뢰도는 꽤 큽니다.",
      choices: [
        ["지름길을 따른다.", "다음 전투에서 이동 속도가 10% 증가합니다.\n최대 체력의 10%만큼 피해를 받습니다."],
        ["지도를 닦아낸다.", "무작위 소모품 2개를 획득합니다."],
        ["지도를 복사한다.", "다음 미지 스테이지의 선택지 효과를 미리 확인합니다."],
      ],
    },
    en: {
      title: "The Map Drawn in Dust",
      description: "Someone has drawn a shortcut through the dust on the floor.\nJudging by the size of the cartographer's finger, it seems quite credible.",
      choices: [
        ["Follow the shortcut.", "Movement speed increases by 10% in the next battle.\nTake damage equal to 10% of maximum HP."],
        ["Wipe away the map.", "Gain 2 random consumables."],
        ["Copy the map.", "Preview the effects of choices in the next Unknown Stage."],
      ],
    },
    ja: {
      title: "埃に描かれた地図",
      description: "床の埃に、誰かが近道を描いています。\n地図製作者の指の大きさを見る限り、かなり信頼できそうです。",
      choices: [
        ["近道を進む。", "次の戦闘で移動速度が10%増加します。\n最大HPの10%分のダメージを受けます。"],
        ["地図を拭き取る。", "ランダムな消耗品を2個獲得します。"],
        ["地図を写す。", "次の未知ステージの選択肢の効果を事前に確認します。"],
      ],
    },
  },
  {
    ko: {
      title: "침묵의 경매",
      description: "홀로그램 경매사가 아무 말 없이 망치만 두드리고 있습니다.\n입찰가는 표시되지 않지만 수수료는 아주 선명합니다.",
      choices: [
        ["골드 30을 입찰한다.", "골드 30을 잃고 무작위 유물 1개를 획득합니다."],
        ["빈손으로 허세를 부린다.", "50% 확률로 무작위 유물 1개를 획득하고,\n50% 확률로 골드 20을 잃습니다."],
        ["같이 침묵한다.", "체력을 최대 체력의 15%만큼 회복합니다. 가장 조용한 낙찰입니다."],
      ],
    },
    en: {
      title: "The Silent Auction",
      description: "A holographic auctioneer says nothing and keeps striking the gavel.\nThe bids are invisible, but the service fee is remarkably clear.",
      choices: [
        ["Bid 30 Gold.", "Lose 30 Gold and gain 1 random relic."],
        ["Bluff with empty hands.", "50% chance to gain 1 random relic,\nand 50% chance to lose 20 Gold."],
        ["Remain silent too.", "Restore 15% of maximum HP. It is the quietest winning bid."],
      ],
    },
    ja: {
      title: "沈黙のオークション",
      description: "ホログラムの競売人が何も言わず、木槌だけを叩いています。\n入札額は見えませんが、手数料だけは鮮明です。",
      choices: [
        ["ゴールドを30入札する。", "ゴールドを30失い、ランダムな遺物を1個獲得します。"],
        ["手ぶらで虚勢を張る。", "50%の確率でランダムな遺物を1個獲得し、\n50%の確率でゴールドを20失います。"],
        ["こちらも黙る。", "最大HPの15%を回復します。最も静かな落札です。"],
      ],
    },
  },
  {
    ko: {
      title: "고장 난 복제기",
      description: "복제기가 무엇이든 거의 똑같이 복사한다고 광고합니다.\n'거의'라는 글자만 세 번 복제되어 있습니다.",
      choices: [
        ["소모품을 복제한다.", "무작위 소모품 2개를 획득하고 최대 체력의 15%만큼 피해를 받습니다."],
        ["골드를 복제한다.", "골드 25를 획득합니다."],
        ["자신을 복제한다.", "공격력과 방어력이 3% 증가하고 최대 체력이 5% 감소합니다."],
      ],
    },
    en: {
      title: "The Broken Duplicator",
      description: "A duplicator claims it can make an almost perfect copy of anything.\nThe word 'almost' has been printed three times.",
      choices: [
        ["Duplicate a consumable.", "Gain 2 random consumables and take damage equal to 15% of maximum HP."],
        ["Duplicate some Gold.", "Gain 25 Gold."],
        ["Duplicate yourself.", "Attack and defense increase by 3%, and maximum HP decreases by 5%."],
      ],
    },
    ja: {
      title: "故障した複製機",
      description: "複製機が、何でもほぼ完璧にコピーできると宣伝しています。\n「ほぼ」という文字だけが三回複製されています。",
      choices: [
        ["消耗品を複製する。", "ランダムな消耗品を2個獲得し、最大HPの15%分のダメージを受けます。"],
        ["ゴールドを複製する。", "ゴールドを25獲得します。"],
        ["自分を複製する。", "攻撃力と防御力が3%増加し、最大HPが5%減少します。"],
      ],
    },
  },
  {
    ko: {
      title: "승률 계산기",
      description: "전투 단말기가 당신의 다음 승률을 49.9%로 계산했습니다.\n화면 한쪽에는 '반올림 기능 별도 판매'라고 적혀 있습니다.",
      choices: [
        ["정밀 분석을 실행한다.", "다음 전투에서 적의 공격력이 5% 감소하고 자신의 공격력이 2% 감소합니다."],
        ["화면을 분해한다.", "골드 35를 획득합니다."],
        ["통계를 믿지 않는다.", "최대 체력이 4% 증가합니다. 자신감은 표본 크기를 무시합니다."],
      ],
    },
    en: {
      title: "The Win-Rate Calculator",
      description: "A combat terminal gives you a 49.9% chance of winning the next battle.\nA note reads, 'Rounding feature sold separately.'",
      choices: [
        ["Run a detailed analysis.", "Enemy attack decreases by 5% and your attack decreases by 2% in the next battle."],
        ["Dismantle the screen.", "Gain 35 Gold."],
        ["Reject the statistics.", "Maximum HP increases by 4%. Confidence ignores sample size."],
      ],
    },
    ja: {
      title: "勝率計算機",
      description: "戦闘端末が、次の勝率を49.9%と算出しました。\n画面の隅には「四捨五入機能は別売り」とあります。",
      choices: [
        ["精密分析を実行する。", "次の戦闘で敵の攻撃力が5%減少し、自分の攻撃力が2%減少します。"],
        ["画面を分解する。", "ゴールドを35獲得します。"],
        ["統計を信じない。", "最大HPが4%増加します。自信は標本数を無視します。"],
      ],
    },
  },
  {
    ko: {
      title: "문 앞의 경비 로봇",
      description: "작동하지 않는 문 앞에서 경비 로봇이 신분증을 요구합니다.\n문보다 직업 의식이 훨씬 잘 작동하고 있습니다.",
      choices: [
        ["임시 신분증을 보여준다.", "무작위 소모품 1개를 획득합니다."],
        ["골드 20으로 통행료를 낸다.", "골드 20을 잃고 무작위 유물 1개를 획득합니다."],
        ["정면으로 돌파한다.", "공격력이 5% 증가하고 최대 체력의 15%만큼 피해를 받습니다."],
      ],
    },
    en: {
      title: "The Robot at the Door",
      description: "A guard robot demands identification in front of a door that does not work.\nIts work ethic functions much better than the door.",
      choices: [
        ["Show a temporary ID.", "Gain 1 random consumable."],
        ["Pay a 20 Gold toll.", "Lose 20 Gold and gain 1 random relic."],
        ["Force your way through.", "Attack increases by 5% and you take damage equal to 15% of maximum HP."],
      ],
    },
    ja: {
      title: "扉の前の警備ロボット",
      description: "動かない扉の前で、警備ロボットが身分証を要求しています。\n扉よりも職業意識の方がよく動いています。",
      choices: [
        ["仮の身分証を見せる。", "ランダムな消耗品を1個獲得します。"],
        ["通行料としてゴールドを20払う。", "ゴールドを20失い、ランダムな遺物を1個獲得します。"],
        ["正面突破する。", "攻撃力が5%増加し、最大HPの15%分のダメージを受けます。"],
      ],
    },
  },
];

const startQuestId = 20;
const newRows = [];

for (let eventIndex = 0; eventIndex < events.length; eventIndex += 1) {
  const questId = startQuestId + eventIndex;

  for (const language of languages) {
    const localized = events[eventIndex][language.key];
    const row = [
      language.offset + questId,
      localized.title,
      localized.description,
      localized.choices.length,
    ];

    for (let choiceIndex = 0; choiceIndex < 3; choiceIndex += 1) {
      const choice = localized.choices[choiceIndex];
      row.push(choice?.[0] ?? null, choice?.[1] ?? null);
    }

    newRows.push(row);
  }
}

if (events.length !== 20 || newRows.length !== 60) {
  throw new Error(`Unexpected data count: ${events.length} events, ${newRows.length} localized rows`);
}

newRows.forEach((row, rowIndex) => {
  const eventIndex = Math.floor(rowIndex / languages.length);
  const languageIndex = rowIndex % languages.length;
  const expectedId = languages[languageIndex].offset + startQuestId + eventIndex;

  if (row[0] !== expectedId) {
    throw new Error(`Invalid language ID: expected ${expectedId}, got ${row[0]}`);
  }

  const choiceCount = row[3];
  for (let choiceIndex = 0; choiceIndex < 3; choiceIndex += 1) {
    const title = row[4 + choiceIndex * 2];
    const description = row[5 + choiceIndex * 2];
    const shouldExist = choiceIndex < choiceCount;
    if (shouldExist !== Boolean(title && description)) {
      throw new Error(`Invalid choice data for ID ${expectedId}, choice ${choiceIndex + 1}`);
    }
  }
});

sheet.getRange("A3:J62").copyTo(sheet.getRange("A63:J122"), "all");
sheet.getRange("A63:J122").values = newRows;
sheet.getRange("A63:A122").format.numberFormat = "00000000";
sheet.getRange("A63:A122").format.fill = "#92D050";
sheet.getRange("A63:A122").format.borders = {
  right: { style: "medium", color: "#222222" },
};
sheet.getRange("A63:A122").format.horizontalAlignment = "center";
sheet.getRange("D63:D122").format.horizontalAlignment = "center";
sheet.getRange("A63:J122").format.verticalAlignment = "top";
sheet.getRange("B63:J122").format.wrapText = true;
sheet.getRange("63:122").format.rowHeight = 48;

const newDataCheck = await workbook.inspect({
  kind: "table",
  range: "UnknownLangTable!A60:J68",
  include: "values,formulas",
  tableMaxRows: 9,
  tableMaxCols: 10,
  tableMaxCellChars: 100,
  maxChars: 7000,
});
console.log(newDataCheck.ndjson);

const finalRowsCheck = await workbook.inspect({
  kind: "table",
  range: "UnknownLangTable!A117:J122",
  include: "values,formulas",
  tableMaxRows: 6,
  tableMaxCols: 10,
  tableMaxCellChars: 100,
  maxChars: 5000,
});
console.log(finalRowsCheck.ndjson);

const errorCheck = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
console.log(errorCheck.ndjson);

for (const [name, range] of [
  ["final-preview-top.png", "A1:J62"],
  ["final-preview-bottom.png", "A63:J122"],
]) {
  const preview = await workbook.render({
    sheetName: sheet.name,
    range,
    scale: 1,
    format: "png",
  });
  await fs.writeFile(`${outputDir}/${name}`, new Uint8Array(await preview.arrayBuffer()));
}

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(JSON.stringify({
  outputPath,
  existingEventCount: 20,
  addedEventCount: events.length,
  totalEventCount: 40,
  totalLocalizedRowCount: 120,
}));
