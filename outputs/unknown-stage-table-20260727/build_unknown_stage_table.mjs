import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "C:/Users/user/Desktop/UnknownStageTable.xlsx";
const outputDir = "C:/Users/user/Project2/outputs/unknown-stage-table-20260727";
const outputPath = `${outputDir}/UnknownStageTable_Localized.xlsx`;

const events = [
  {
    ko: ["과충전", "공장 안쪽에서 규격이 맞지 않는 대형 충전기를 발견했습니다.\n경고등은 빨갛지만, 적어도 아주 자신 있게 빨갛습니다.", [["충전한다.", "공격력이 5% 증가합니다.\n최대 체력의 20%만큼 피해를 받습니다."], ["떠난다.", "효과가 없습니다. 안전은 대체로 지루합니다."]]],
    en: ["Overcharge", "You find an industrial charger that clearly does not fit your specifications.\nIts warning light is red, but at least it is confidently red.", [["Plug in.", "Attack increases by 5%.\nTake damage equal to 20% of maximum HP."], ["Leave.", "No effect. Safety is usually rather boring."]]],
    ja: ["過充電", "工場の奥で、どう見ても規格の合わない大型充電器を見つけました。\n警告灯は赤ですが、少なくとも自信満々に赤く光っています。", [["充電する。", "攻撃力が5%増加します。\n最大HPの20%分のダメージを受けます。"], ["立ち去る。", "効果はありません。安全とは、たいてい退屈なものです。"]]],
  },
  {
    ko: ["유통기한: 3세기 전", "비상식량 보관함이 멀쩡하게 열렸습니다.\n내용물도 멀쩡해 보입니다. '보인다'는 부분이 핵심입니다.", [["먹는다.", "체력을 최대 체력의 30%만큼 회복합니다.\n다음 전투에서 방어력이 5% 감소합니다."], ["성분을 분석한다.", "무작위 소모품 1개를 획득합니다."], ["조용히 닫는다.", "효과가 없습니다. 보관함도 안도한 듯합니다."]]],
    en: ["Best Before: Three Centuries Ago", "An emergency ration locker opens without complaint.\nThe contents look fine. The word 'look' is doing important work here.", [["Eat it.", "Restore 30% of maximum HP.\nDefense decreases by 5% in the next battle."], ["Analyze it.", "Gain 1 random consumable."], ["Close it gently.", "No effect. The locker seems relieved too."]]],
    ja: ["賞味期限：三世紀前", "非常食ロッカーが何の抵抗もなく開きました。\n中身も無事に「見えます」。大事なのは「見える」という部分です。", [["食べる。", "最大HPの30%を回復します。\n次の戦闘で防御力が5%減少します。"], ["成分を分析する。", "ランダムな消耗品を1個獲得します。"], ["そっと閉じる。", "効果はありません。ロッカーも安心したようです。"]]],
  },
  {
    ko: ["노래하는 정비 드론", "고장 난 정비 드론이 같은 네 음을 무한 반복하고 있습니다.\n음정은 틀렸지만 열정만큼은 정비할 필요가 없어 보입니다.", [["수리한다.", "최대 체력이 5% 증가합니다.\n현재 체력이 10% 감소합니다."], ["같이 노래한다.", "체력을 최대 체력의 15%만큼 회복합니다."], ["전원을 끈다.", "골드 20을 획득합니다. 공연료는 셀프 정산입니다."]]],
    en: ["The Singing Maintenance Drone", "A broken maintenance drone is repeating the same four notes forever.\nIt is off-key, but its enthusiasm needs no repairs.", [["Repair it.", "Maximum HP increases by 5%.\nCurrent HP decreases by 10%."], ["Sing along.", "Restore 15% of maximum HP."], ["Power it down.", "Gain 20 Gold. You handle the performance fee yourself."]]],
    ja: ["歌う整備ドローン", "故障した整備ドローンが、同じ四音を永遠に繰り返しています。\n音程は外れていますが、情熱だけは修理不要のようです。", [["修理する。", "最大HPが5%増加します。\n現在HPが10%減少します。"], ["一緒に歌う。", "最大HPの15%を回復します。"], ["電源を切る。", "ゴールドを20獲得します。出演料は自己精算です。"]]],
  },
  {
    ko: ["누르면 안 되는 버튼", "복도 한가운데에 빨간 버튼이 하나 있습니다.\n그 아래에는 친절하게도 '누르지 마시오'라고 적혀 있습니다.", [["누른다.", "무작위 유물 1개를 획득합니다.\n최대 체력의 25%만큼 피해를 받습니다."], ["주변을 조사한다.", "골드 30을 획득합니다."], ["경고를 존중한다.", "효과가 없습니다. 누군가는 규칙을 읽어야 합니다."]]],
    en: ["The Button You Should Not Press", "A single red button sits in the middle of the corridor.\nA helpful label underneath says, 'DO NOT PRESS.'", [["Press it.", "Gain 1 random relic.\nTake damage equal to 25% of maximum HP."], ["Search nearby.", "Gain 30 Gold."], ["Respect the warning.", "No effect. Someone has to read the rules."]]],
    ja: ["押してはいけないボタン", "廊下の真ん中に赤いボタンがひとつ置かれています。\nその下には親切にも「押すな」と書かれています。", [["押す。", "ランダムな遺物を1個獲得します。\n最大HPの25%分のダメージを受けます。"], ["周囲を調べる。", "ゴールドを30獲得します。"], ["警告を尊重する。", "効果はありません。誰かは規則を読まなければなりません。"]]],
  },
  {
    ko: ["주인 없는 사물함", "낡은 사물함에서 규칙적인 두드림 소리가 들립니다.\n안에 누가 있는지보다, 왜 박자를 맞추는지가 더 신경 쓰입니다.", [["강제로 연다.", "무작위 소모품 2개를 획득합니다.\n최대 체력의 10%만큼 피해를 받습니다."], ["비밀번호를 추측한다.", "50% 확률로 무작위 유물 1개를 획득합니다."], ["박수로 화답한다.", "체력을 최대 체력의 10%만큼 회복합니다."]]],
    en: ["The Unclaimed Locker", "A steady knocking comes from an old locker.\nWho is inside matters less than why they are keeping perfect rhythm.", [["Force it open.", "Gain 2 random consumables.\nTake damage equal to 10% of maximum HP."], ["Guess the code.", "50% chance to gain 1 random relic."], ["Clap along.", "Restore 10% of maximum HP."]]],
    ja: ["持ち主のいないロッカー", "古びたロッカーから規則正しいノック音が聞こえます。\n中に誰がいるかより、なぜ完璧なリズムなのかが気になります。", [["こじ開ける。", "ランダムな消耗品を2個獲得します。\n最大HPの10%分のダメージを受けます。"], ["暗証番号を推測する。", "50%の確率でランダムな遺物を1個獲得します。"], ["手拍子で応える。", "最大HPの10%を回復します。"]]],
  },
  {
    ko: ["자기 폭풍", "복도 전체가 거대한 자석처럼 울리기 시작합니다.\n주머니 속 나사들이 먼저 떠날 준비를 마쳤습니다.", [["몸을 낮춘다.", "방어력이 5% 증가합니다."], ["달려서 통과한다.", "현재 체력이 15% 감소합니다.\n다음 전투에서 이동 속도가 10% 증가합니다."], ["금속 장비를 붙잡는다.", "골드 15를 잃고 무작위 소모품 1개를 획득합니다."]]],
    en: ["Magnetic Storm", "The entire corridor begins to hum like an enormous magnet.\nThe screws in your pockets are already preparing to leave.", [["Stay low.", "Defense increases by 5%."], ["Run through.", "Current HP decreases by 15%.\nMovement speed increases by 10% in the next battle."], ["Hold onto your gear.", "Lose 15 Gold and gain 1 random consumable."]]],
    ja: ["磁気嵐", "廊下全体が巨大な磁石のように唸り始めます。\nポケットのネジは、すでに旅立つ準備を終えています。", [["姿勢を低くする。", "防御力が5%増加します。"], ["走り抜ける。", "現在HPが15%減少します。\n次の戦闘で移動速度が10%増加します。"], ["装備を押さえる。", "ゴールドを15失い、ランダムな消耗品を1個獲得します。"]]],
  },
  {
    ko: ["보증 기간 만료", "정비 단말기가 당신을 '지원 종료 예정 장치'로 분류했습니다.\n예정 날짜는 어제였습니다. 서비스가 참 신속합니다.", [["강제 업데이트한다.", "최대 체력이 8% 증가합니다.\n공격력이 3% 감소합니다."], ["업데이트를 취소한다.", "공격력이 3% 증가합니다."], ["나중에 알림을 선택한다.", "효과가 없습니다. 알림은 언젠가 다시 옵니다."]]],
    en: ["Warranty Expired", "A maintenance terminal classifies you as a 'device nearing end of support.'\nThe listed date was yesterday. Admirably prompt service.", [["Force the update.", "Maximum HP increases by 8%.\nAttack decreases by 3%."], ["Cancel the update.", "Attack increases by 3%."], ["Remind me later.", "No effect. The reminder will return someday."]]],
    ja: ["保証期間終了", "整備端末があなたを「サポート終了予定機」に分類しました。\n予定日は昨日です。実に迅速なサービスです。", [["強制更新する。", "最大HPが8%増加します。\n攻撃力が3%減少します。"], ["更新を中止する。", "攻撃力が3%増加します。"], ["後で通知する。", "効果はありません。通知はいつか戻ってきます。"]]],
  },
  {
    ko: ["의심스러운 커피", "온도를 유지한 채 수백 년을 버틴 커피가 있습니다.\n커피가 대단한 건지 컵이 대단한 건지는 아직 논쟁 중입니다.", [["마신다.", "체력을 모두 회복합니다.\n다음 전투에서 공격 속도가 10% 감소합니다."], ["향만 맡는다.", "최대 체력이 3% 증가합니다."], ["바닥에 붓는다.", "무작위 소모품 1개를 획득합니다. 바닥이 깨어났습니다."]]],
    en: ["Suspicious Coffee", "A cup of coffee has stayed hot for several centuries.\nExperts still debate whether the coffee or the cup deserves the credit.", [["Drink it.", "Fully restore HP.\nAttack speed decreases by 10% in the next battle."], ["Smell it.", "Maximum HP increases by 3%."], ["Pour it out.", "Gain 1 random consumable. The floor is now awake."]]],
    ja: ["怪しいコーヒー", "数百年もの間、温かさを保ち続けたコーヒーがあります。\nすごいのがコーヒーなのかカップなのか、いまだ議論中です。", [["飲む。", "HPを全回復します。\n次の戦闘で攻撃速度が10%減少します。"], ["香りだけ嗅ぐ。", "最大HPが3%増加します。"], ["床に捨てる。", "ランダムな消耗品を1個獲得します。床が目を覚ましました。"]]],
  },
  {
    ko: ["양자 동전", "앞면과 뒷면이 동시에 보이는 동전이 공중에 떠 있습니다.\n관측하기 전까지는 부자이면서 가난한 상태일지도 모릅니다.", [["동전을 던진다.", "50% 확률로 골드 50을 획득하고,\n50% 확률로 골드 20을 잃습니다."], ["결과를 엿본다.", "골드 20을 획득합니다.\n최대 체력의 10%만큼 피해를 받습니다."], ["관측하지 않는다.", "효과가 없습니다. 동전도 결정을 미룹니다."]]],
    en: ["Quantum Coin", "A coin showing heads and tails at once floats in the air.\nUntil observed, you may be both rich and broke.", [["Flip it.", "50% chance to gain 50 Gold,\nand 50% chance to lose 20 Gold."], ["Peek at the result.", "Gain 20 Gold.\nTake damage equal to 10% of maximum HP."], ["Do not observe it.", "No effect. The coin postpones its decision too."]]],
    ja: ["量子コイン", "表と裏が同時に見えるコインが空中に浮いています。\n観測するまでは、金持ちであり一文無しでもあるのかもしれません。", [["投げる。", "50%の確率でゴールドを50獲得し、\n50%の確率でゴールドを20失います。"], ["結果を盗み見る。", "ゴールドを20獲得します。\n最大HPの10%分のダメージを受けます。"], ["観測しない。", "効果はありません。コインも決断を先送りします。"]]],
  },
  {
    ko: ["길 잃은 배송 로봇", "배송 로봇이 수취인을 847년째 찾고 있습니다.\n직업 정신은 훌륭하지만 길 찾기 기능은 환불 대상입니다.", [["소포를 돌려준다.", "골드 25와 무작위 소모품 1개를 획득합니다."], ["소포를 연다.", "무작위 유물 1개를 획득합니다.\n다음 전투에서 적 공격력이 10% 증가합니다."], ["새 주소를 입력한다.", "체력을 최대 체력의 20%만큼 회복합니다."]]],
    en: ["The Lost Delivery Robot", "A delivery robot has been searching for its recipient for 847 years.\nIts work ethic is excellent; its navigation system deserves a refund.", [["Return the parcel.", "Gain 25 Gold and 1 random consumable."], ["Open the parcel.", "Gain 1 random relic.\nEnemy attack increases by 10% in the next battle."], ["Enter a new address.", "Restore 20% of maximum HP."]]],
    ja: ["迷子の配送ロボット", "配送ロボットが847年間、受取人を探し続けています。\n職業意識は立派ですが、ナビ機能は返金対象です。", [["荷物を返す。", "ゴールドを25とランダムな消耗品を1個獲得します。"], ["荷物を開ける。", "ランダムな遺物を1個獲得します。\n次の戦闘で敵の攻撃力が10%増加します。"], ["新しい住所を入力する。", "最大HPの20%を回復します。"]]],
  },
  {
    ko: ["비상 환풍구", "환풍구 너머에서 시원한 바람과 수상한 박수 소리가 들립니다.\n둘 중 하나는 정상입니다. 아마 바람 쪽일 겁니다.", [["안으로 기어간다.", "무작위 유물 1개를 획득합니다.\n현재 체력이 15% 감소합니다."], ["환풍기를 멈춘다.", "무작위 소모품 2개를 획득합니다."], ["박수로 답한다.", "다음 전투에서 공격 속도가 8% 증가합니다."]]],
    en: ["Emergency Vent", "Cool air and suspicious applause drift from beyond the vent.\nOne of those is normal. Probably the air.", [["Crawl inside.", "Gain 1 random relic.\nCurrent HP decreases by 15%."], ["Stop the fan.", "Gain 2 random consumables."], ["Applaud back.", "Attack speed increases by 8% in the next battle."]]],
    ja: ["非常用ダクト", "ダクトの奥から涼しい風と怪しい拍手が聞こえます。\nどちらか一方は正常です。たぶん風の方でしょう。", [["中へ這って入る。", "ランダムな遺物を1個獲得します。\n現在HPが15%減少します。"], ["換気扇を止める。", "ランダムな消耗品を2個獲得します。"], ["拍手を返す。", "次の戦闘で攻撃速度が8%増加します。"]]],
  },
  {
    ko: ["AI 상담실", "낡은 상담 AI가 당신의 고민을 0.003초 만에 분석했습니다.\n답변을 준비하는 데는 광고 포함 30초가 걸립니다.", [["전투 기록을 업로드한다.", "공격력이 5% 증가합니다.\n최대 체력이 5% 감소합니다."], ["오늘의 운세를 묻는다.", "다음 미지 스테이지의 선택지 효과를 미리 확인합니다."], ["광고를 건너뛴다.", "골드 10을 잃습니다. 프리미엄 기능이었습니다."]]],
    en: ["AI Counseling", "An old counseling AI analyzes your concerns in 0.003 seconds.\nPreparing the answer takes 30 seconds, including advertisements.", [["Upload combat logs.", "Attack increases by 5%.\nMaximum HP decreases by 5%."], ["Ask for today's fortune.", "Preview the effects of choices in the next Unknown Stage."], ["Skip the advertisement.", "Lose 10 Gold. It was a premium feature."]]],
    ja: ["AI相談室", "古い相談AIがあなたの悩みを0.003秒で分析しました。\n回答の準備には広告込みで30秒かかります。", [["戦闘記録を送信する。", "攻撃力が5%増加します。\n最大HPが5%減少します。"], ["今日の運勢を聞く。", "次の未知ステージの選択効果を事前に確認します。"], ["広告をスキップする。", "ゴールドを10失います。プレミアム機能でした。"]]],
  },
  {
    ko: ["고철 왕좌", "부서진 기계 부품으로 만든 왕좌가 방 한가운데 놓여 있습니다.\n누가 왕인지는 모르지만 허리 건강은 확실히 포기한 듯합니다.", [["왕좌에 앉는다.", "방어력이 8% 증가합니다.\n이동 속도가 5% 감소합니다."], ["분해한다.", "골드 40을 획득합니다."], ["예를 갖춘다.", "체력을 최대 체력의 15%만큼 회복합니다."]]],
    en: ["The Scrap Throne", "A throne made of broken machine parts sits in the center of the room.\nWhoever ruled here clearly surrendered their lower back first.", [["Sit on the throne.", "Defense increases by 8%.\nMovement speed decreases by 5%."], ["Dismantle it.", "Gain 40 Gold."], ["Pay your respects.", "Restore 15% of maximum HP."]]],
    ja: ["スクラップの玉座", "壊れた機械部品で作られた玉座が部屋の中央にあります。\n誰が王だったにせよ、腰の健康は真っ先に諦めたようです。", [["玉座に座る。", "防御力が8%増加します。\n移動速度が5%減少します。"], ["分解する。", "ゴールドを40獲得します。"], ["敬意を示す。", "最大HPの15%を回復します。"]]],
  },
  {
    ko: ["낯선 기억 조각", "당신의 것이 아닌 기억 데이터가 바닥에서 깜빡이고 있습니다.\n내용은 전투 기술 80%, 고양이 영상 20%입니다.", [["기억을 설치한다.", "스킬 재사용 대기시간이 5% 감소합니다.\n최대 체력의 10%만큼 피해를 받습니다."], ["데이터를 판매한다.", "골드 35를 획득합니다."], ["고양이 영상만 본다.", "체력을 최대 체력의 15%만큼 회복합니다."]]],
    en: ["An Unfamiliar Memory", "A memory fragment that is not yours flickers on the floor.\nIts contents are 80% combat techniques and 20% cat videos.", [["Install the memory.", "Skill cooldown decreases by 5%.\nTake damage equal to 10% of maximum HP."], ["Sell the data.", "Gain 35 Gold."], ["Watch only the cat videos.", "Restore 15% of maximum HP."]]],
    ja: ["見知らぬ記憶片", "あなたのものではない記憶データが床で点滅しています。\n内容は戦闘技術80%、猫動画20%です。", [["記憶をインストールする。", "スキルのクールダウンが5%減少します。\n最大HPの10%分のダメージを受けます。"], ["データを売る。", "ゴールドを35獲得します。"], ["猫動画だけ見る。", "最大HPの15%を回復します。"]]],
  },
  {
    ko: ["중력 점검 시간", "방 안의 모든 물건이 천장으로 떨어지고 있습니다.\n천장도 이 상황을 처음 겪는 표정입니다.", [["떠다니는 상자를 잡는다.", "무작위 소모품 2개를 획득합니다."], ["자기 부츠를 가동한다.", "방어력이 5% 증가합니다.\n이동 속도가 5% 감소합니다."], ["중력이 돌아오길 기다린다.", "체력을 최대 체력의 10%만큼 회복합니다."]]],
    en: ["Gravity Maintenance", "Everything in the room is falling toward the ceiling.\nThe ceiling looks equally surprised.", [["Catch a floating crate.", "Gain 2 random consumables."], ["Activate magnetic boots.", "Defense increases by 5%.\nMovement speed decreases by 5%."], ["Wait for gravity.", "Restore 10% of maximum HP."]]],
    ja: ["重力点検中", "部屋のあらゆる物が天井へ向かって落ちています。\n天井も同じくらい驚いているようです。", [["浮かぶ箱をつかむ。", "ランダムな消耗品を2個獲得します。"], ["磁気ブーツを起動する。", "防御力が5%増加します。\n移動速度が5%減少します。"], ["重力を待つ。", "最大HPの10%を回復します。"]]],
  },
  {
    ko: ["잠든 포탑", "자동 포탑이 절전 모드로 꾸벅꾸벅 졸고 있습니다.\n총구가 당신을 따라 움직이는 건 잠버릇일 겁니다. 아마도요.", [["분해한다.", "무작위 소모품 2개를 획득합니다.\n현재 체력이 15% 감소합니다."], ["재프로그래밍한다.", "다음 전투 시작 시 적 전체에게 피해를 줍니다."], ["발끝으로 지나간다.", "효과가 없습니다. 포탑은 코를 골지 않습니다."]]],
    en: ["The Sleeping Turret", "An automated turret nods in and out of power-saving mode.\nThe barrel following you is probably just a sleep habit. Probably.", [["Dismantle it.", "Gain 2 random consumables.\nCurrent HP decreases by 15%."], ["Reprogram it.", "Deal damage to all enemies at the start of the next battle."], ["Tiptoe past.", "No effect. Turrets do not snore."]]],
    ja: ["眠るタレット", "自動タレットが省電力モードでうとうとしています。\n銃口があなたを追うのは寝癖でしょう。たぶん。", [["分解する。", "ランダムな消耗品を2個獲得します。\n現在HPが15%減少します。"], ["再プログラムする。", "次の戦闘開始時、敵全体にダメージを与えます。"], ["忍び足で通る。", "効果はありません。タレットはいびきをかきません。"]]],
  },
  {
    ko: ["출구처럼 생긴 벽", "선명한 EXIT 표지 아래에 완벽한 벽이 서 있습니다.\n건축가와 표지판 제작자 중 한 명은 아주 큰 실수를 했습니다.", [["벽을 밀어본다.", "체력을 최대 체력의 20%만큼 회복합니다.\n벽 뒤에서 구급 상자가 나옵니다."], ["표지판을 떼어낸다.", "골드 25를 획득합니다."], ["진짜 출구인 척한다.", "다음 전투에서 방어력이 8% 증가합니다. 자신감의 힘입니다."]]],
    en: ["The Wall That Looks Like an Exit", "A perfectly solid wall stands beneath a bright EXIT sign.\nEither the architect or the sign maker made a spectacular mistake.", [["Push the wall.", "Restore 20% of maximum HP.\nA medical kit falls out from behind it."], ["Take the sign.", "Gain 25 Gold."], ["Pretend it is a real exit.", "Defense increases by 8% in the next battle. Confidence helps."]]],
    ja: ["出口に見える壁", "明るいEXIT表示の下に、完璧な壁が立っています。\n建築家か看板屋のどちらかが盛大に間違えました。", [["壁を押す。", "最大HPの20%を回復します。\n壁の裏から救急箱が落ちてきます。"], ["看板を外す。", "ゴールドを25獲得します。"], ["本物の出口のふりをする。", "次の戦闘で防御力が8%増加します。自信の力です。"]]],
  },
  {
    ko: ["나노봇 소나기", "천장에서 반짝이는 나노봇이 비처럼 쏟아집니다.\n일기예보는 맑음이었지만 이 시설의 예보관은 오래전에 퇴근했습니다.", [["비를 맞는다.", "체력을 최대 체력의 30%만큼 회복합니다.\n다음 전투에서 방어력이 5% 감소합니다."], ["나노봇을 수집한다.", "무작위 소모품 2개를 획득합니다."], ["우산을 편다.", "최대 체력이 3% 증가합니다. 우산은 의외로 기술적입니다."]]],
    en: ["Nanobot Shower", "Glittering nanobots rain from the ceiling.\nThe forecast said clear skies, but the facility meteorologist left centuries ago.", [["Stand in the rain.", "Restore 30% of maximum HP.\nDefense decreases by 5% in the next battle."], ["Collect the nanobots.", "Gain 2 random consumables."], ["Open an umbrella.", "Maximum HP increases by 3%. The umbrella is surprisingly advanced."]]],
    ja: ["ナノボットの雨", "天井から輝くナノボットが雨のように降ってきます。\n予報は晴れでしたが、施設の予報士は数世紀前に退勤しました。", [["雨を浴びる。", "最大HPの30%を回復します。\n次の戦闘で防御力が5%減少します。"], ["ナノボットを集める。", "ランダムな消耗品を2個獲得します。"], ["傘を差す。", "最大HPが3%増加します。傘は意外と高性能です。"]]],
  },
  {
    ko: ["시간이 느린 방", "방 안에 던진 먼지가 10초 뒤에야 바닥에 닿습니다.\n여기서는 마감 기한도 느려질 것 같지만, 그건 환상입니다.", [["잠시 쉰다.", "체력을 최대 체력의 25%만큼 회복합니다.\n골드 20을 잃습니다."], ["빠르게 통과한다.", "현재 체력이 10% 감소합니다.\n다음 전투에서 이동 속도가 12% 증가합니다."], ["시간축을 동기화한다.", "최대 체력이 5% 증가합니다."]]],
    en: ["The Slow-Time Room", "Dust thrown into the room takes ten seconds to reach the floor.\nDeadlines may seem slower here too. That part is an illusion.", [["Take a break.", "Restore 25% of maximum HP.\nLose 20 Gold."], ["Rush through.", "Current HP decreases by 10%.\nMovement speed increases by 12% in the next battle."], ["Synchronize the timeline.", "Maximum HP increases by 5%."]]],
    ja: ["時間の遅い部屋", "部屋へ投げたほこりが、床に着くまで10秒もかかります。\n締め切りも遅く見えますが、それは気のせいです。", [["休憩する。", "最大HPの25%を回復します。\nゴールドを20失います。"], ["急いで通る。", "現在HPが10%減少します。\n次の戦闘で移動速度が12%増加します。"], ["時間軸を同期する。", "最大HPが5%増加します。"]]],
  },
  {
    ko: ["마지막 남은 의자", "수많은 전투와 재난을 견딘 의자 하나가 덩그러니 놓여 있습니다.\n쿠션 상태가 지나치게 좋아 오히려 불안합니다.", [["앉아서 쉰다.", "체력을 최대 체력의 35%만큼 회복합니다.\n다음 전투 시작 시 2초 동안 이동할 수 없습니다."], ["의자를 분해한다.", "골드 30과 무작위 소모품 1개를 획득합니다."], ["의자에게 양보한다.", "공격력이 3% 증가합니다. 예의는 힘입니다."]]],
    en: ["The Last Chair Standing", "One chair has survived countless battles and disasters.\nIts cushion is in suspiciously good condition.", [["Sit and rest.", "Restore 35% of maximum HP.\nYou cannot move for 2 seconds at the start of the next battle."], ["Dismantle the chair.", "Gain 30 Gold and 1 random consumable."], ["Offer the seat to the chair.", "Attack increases by 3%. Courtesy is power."]]],
    ja: ["最後まで残った椅子", "幾多の戦闘と災害を生き延びた椅子が、ぽつんと置かれています。\nクッションの状態が良すぎて、かえって不安です。", [["座って休む。", "最大HPの35%を回復します。\n次の戦闘開始時、2秒間移動できません。"], ["椅子を分解する。", "ゴールドを30とランダムな消耗品を1個獲得します。"], ["椅子に席を譲る。", "攻撃力が3%増加します。礼儀は力です。"]]],
  },
];

const languages = [
  { key: "ko", code: 0 },
  { key: "en", code: 1 },
  { key: "ja", code: 2 },
];

function makeRow(eventId, language) {
  const [title, description, choices] = events[eventId][language.key];
  const paddedChoices = [...choices];

  while (paddedChoices.length < 3) {
    paddedChoices.push([null, null]);
  }

  return [
    language.code * 10000 + eventId,
    title,
    description,
    choices.length,
    paddedChoices[0][0],
    paddedChoices[0][1],
    paddedChoices[1][0],
    paddedChoices[1][1],
    paddedChoices[2][0],
    paddedChoices[2][1],
  ];
}

const rows = [];
for (let eventId = 0; eventId < events.length; eventId += 1) {
  for (const language of languages) {
    rows.push(makeRow(eventId, language));
  }
}

if (events.length !== 20 || rows.length !== 60) {
  throw new Error(`Unexpected data count: ${events.length} events, ${rows.length} localized rows`);
}

for (let eventId = 0; eventId < events.length; eventId += 1) {
  const localizedRows = rows.slice(eventId * languages.length, (eventId + 1) * languages.length);

  localizedRows.forEach((row, languageIndex) => {
    const expectedId = languageIndex * 10000 + eventId;
    if (row[0] !== expectedId) {
      throw new Error(`Invalid language ID for event ${eventId}: expected ${expectedId}, got ${row[0]}`);
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
}

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);
const sheet = workbook.worksheets.getItem("UnknownLangTable");

sheet.getRange("A2:J62").values = [
  ["int", "string", "string", "int", "string", "string", "string", "string", "string", "string"],
  ...rows,
];

sheet.getRange("A3:A62").format.numberFormat = "00000000";
sheet.getRange("A3:A62").format.fill = "#92D050";
sheet.getRange("A3:A62").format.borders = {
  right: { style: "medium", color: "#222222" },
};
sheet.getRange("A2:A62").format.horizontalAlignment = "center";
sheet.getRange("D2:D62").format.horizontalAlignment = "center";
sheet.getRange("A2:J62").format.verticalAlignment = "top";
sheet.getRange("B3:J62").format.wrapText = true;

sheet.getRange("A:A").format.columnWidth = 16;
sheet.getRange("B:B").format.columnWidth = 24;
sheet.getRange("C:C").format.columnWidth = 58;
sheet.getRange("D:D").format.columnWidth = 14;
sheet.getRange("E:E").format.columnWidth = 22;
sheet.getRange("F:F").format.columnWidth = 46;
sheet.getRange("G:G").format.columnWidth = 22;
sheet.getRange("H:H").format.columnWidth = 46;
sheet.getRange("I:I").format.columnWidth = 22;
sheet.getRange("J:J").format.columnWidth = 46;
sheet.getRange("3:62").format.rowHeight = 48;
sheet.freezePanes.freezeRows(2);

await fs.mkdir(outputDir, { recursive: true });

const valueCheck = await workbook.inspect({
  kind: "table",
  range: "UnknownLangTable!A1:J8",
  include: "values,formulas",
  tableMaxRows: 8,
  tableMaxCols: 10,
  tableMaxCellChars: 120,
  maxChars: 6000,
});
console.log(valueCheck.ndjson);

const errorCheck = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
console.log(errorCheck.ndjson);

const preview = await workbook.render({
  sheetName: sheet.name,
  range: "A1:J62",
  scale: 1,
  format: "png",
});
await fs.writeFile(
  `${outputDir}/final-preview.png`,
  new Uint8Array(await preview.arrayBuffer()),
);

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(JSON.stringify({ outputPath, eventCount: events.length, localizedRowCount: rows.length }));
