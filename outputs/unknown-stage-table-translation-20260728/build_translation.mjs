import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "C:/Users/user/Desktop/UnknownStageTable.xlsx";
const outputDir = "C:/Users/user/Project2/outputs/unknown-stage-table-translation-20260728";
const outputPath = `${outputDir}/UnknownStageTable_Translated.xlsx`;
const mode = process.argv[2] ?? "build";

await fs.mkdir(outputDir, { recursive: true });

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);
const sheet = workbook.worksheets.getItem("UnknownLangTable");
const usedRange = sheet.getUsedRange(true);

const overview = await workbook.inspect({
  kind: "table",
  range: `UnknownLangTable!${usedRange.address}`,
  include: "values,formulas",
  tableMaxRows: 45,
  tableMaxCols: 10,
  tableMaxCellChars: 180,
  maxChars: 30000,
});
console.log(overview.ndjson);

const originalPreview = await workbook.render({
  sheetName: sheet.name,
  range: usedRange.address,
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

const translations = [
  {
    en: {
      title: "Overcharge",
      description: "Deep inside the factory, you find a large charger that does not match your specifications.\nYou could not find an adapter, but it looks like you could force a charge if you really wanted to.",
      choices: [
        ["Plug in.", "Persistent: Attack increases by 5%.\nTake damage equal to 30% of maximum HP."],
        ["Leave.", "No effect."],
      ],
    },
    ja: {
      title: "過充電",
      description: "工場の奥で、規格の合わない大型充電器を発見しました。\n変換アダプターは見つかりませんでしたが、無理やりなら充電できそうです。",
      choices: [
        ["充電する。", "持続：攻撃力が5%増加します。\n最大HPの30%分のダメージを受けます。"],
        ["立ち去る。", "効果はありません。"],
      ],
    },
  },
  {
    en: {
      title: "Best Before: Three Centuries Ago",
      description: "An emergency ration locker stands open.\nThe expiration date is long past and it smells like animal feed, but the contents appear perfectly fine.",
      choices: [
        ["Eat it.", "Restore 30% of maximum HP.\nNext battle only: Defense decreases by 30%."],
        ["Close it quietly.", "No effect."],
      ],
    },
    ja: {
      title: "賞味期限：三世紀前",
      description: "非常食の保管庫が開いています。\n賞味期限はとうに過ぎ、動物の餌のような匂いがしますが、中身は無事に見えます。",
      choices: [
        ["食べる。", "最大HPの30%を回復します。\n次の戦闘のみ：防御力が30%減少します。"],
        ["静かに閉じる。", "効果はありません。"],
      ],
    },
  },
  {
    en: {
      title: "The Singing Maintenance Drone",
      description: "A broken maintenance drone lies on the floor, beeping as it waits for someone to repair it.\nAfter listening for a while, the beeps almost begin to sound like a song.",
      choices: [
        ["Repair it.", "Persistent: Defense increases by 10%."],
        ["Rip out the core.", "Obtain the Maintenance Drone Core."],
      ],
    },
    ja: {
      title: "歌う整備ドローン",
      description: "故障した整備ドローンが床に転がり、誰かに修理されるのを待ちながら電子音を鳴らしています。\n聞き続けていると、だんだん歌のようにも思えてきます。",
      choices: [
        ["修理する。", "持続：防御力が10%増加します。"],
        ["コアを取り出す。", "整備ドローンのコアを獲得します。"],
      ],
    },
  },
  {
    en: {
      title: "The Button You Should Not Press",
      description: "A red button sits in the middle of the corridor.\nA helpful label beneath it reads, 'DO NOT PRESS.'",
      choices: [
        ["Press it.", "Move to an Elite Stage."],
        ["Respect the warning.", "No effect."],
      ],
    },
    ja: {
      title: "押してはいけないボタン",
      description: "廊下の真ん中に赤いボタンがあります。\nその下には親切にも「押すな」と書かれています。",
      choices: [
        ["押す。", "エリートステージへ移動します。"],
        ["警告を尊重する。", "効果はありません。"],
      ],
    },
  },
  {
    en: {
      title: "The Unclaimed Locker",
      description: "You find an old locker.\nYou do not know who owns it, but it is secured with a sturdy padlock.\nYou cautiously try to guess the combination.",
      choices: [
        ["Guess the combination.", "50% chance to obtain 1 random Unique-grade piece of equipment."],
        ["Force it open.", "Obtain 2 random Advanced-grade pieces of equipment."],
      ],
    },
    ja: {
      title: "持ち主のいないロッカー",
      description: "古びたロッカーを見つけました。\n誰のものかは分かりませんが、頑丈な南京錠で固く閉ざされています。\nあなたは慎重に暗証番号を推測します。",
      choices: [
        ["暗証番号を推測する。", "50%の確率でランダムなユニーク等級装備を1個獲得します。"],
        ["無理やり開ける。", "ランダムな上級等級装備を2個獲得します。"],
      ],
    },
  },
  {
    en: {
      title: "Magnetic Storm",
      description: "A massive magnetic storm shakes the entire building.\nThe metal objects in your pockets begin thrashing around.\nYou will have to give something up to escape.",
      choices: [
        ["Discard some items.", "Select and discard 3 items from your inventory."],
        ["Run for the exit.", "Discard 2 random items from your inventory."],
        ["Drop to the floor.", "Lose all Credits."],
      ],
    },
    ja: {
      title: "磁気嵐",
      description: "巨大な磁気嵐で建物全体が揺れています。\nポケットの中の金属製品が激しく動き始めました。\nここを抜け出すには、何かを諦める必要がありそうです。",
      choices: [
        ["アイテムを捨てる。", "インベントリからアイテムを3個選んで捨てます。"],
        ["走って脱出する。", "インベントリからランダムなアイテムを2個捨てます。"],
        ["床に伏せる。", "すべてのクレジットを失います。"],
      ],
    },
  },
  {
    en: {
      title: "Software Update",
      description: "A maintenance terminal recommends updating to the latest software.\nAs always, new software tends to arrive with new problems.",
      choices: [
        ["Update to the latest version.", "Persistent: Attack increases by 8%.\nPersistent: Defense decreases by 15%."],
        ["Install the stable version.", "Persistent: Defense increases by 10%.\nPersistent: Attack decreases by 5%."],
        ["Remind me later.", "No effect."],
      ],
    },
    ja: {
      title: "ソフトウェアアップデート",
      description: "整備端末が、最新ソフトウェアへの更新を勧めています。\nいつものように、新しいプログラムには問題が付きものです。",
      choices: [
        ["最新版に更新する。", "持続：攻撃力が8%増加します。\n持続：防御力が15%減少します。"],
        ["安定版に更新する。", "持続：防御力が10%増加します。\n持続：攻撃力が5%減少します。"],
        ["後で通知する。", "効果はありません。"],
      ],
    },
  },
  {
    en: {
      title: "William Vending Machine",
      description: "This is a famous beverage vending machine made by William Corporation.\nFamous for all the wrong reasons, of course.\nIts drinks are packed with ingredients that exceed recommended limits or cannot be identified at all.\nThe advertisement proudly gives the drinks a 4.7 out of 5 rating from customers who survived them.",
      choices: [
        ["Drink the energy drink.", "Take damage equal to 60% of maximum HP.\nPersistent: Attack speed increases by 20%."],
        ["Drink the mint cola.", "Take damage equal to 60% of maximum HP.\nPersistent: Movement speed increases by 20%."],
        ["Drink the black tea.", "Take damage equal to 60% of maximum HP.\nPersistent: MP recovery increases by 20%."],
      ],
    },
    ja: {
      title: "ウィリアム自動販売機",
      description: "ウィリアム社製の有名な飲料自動販売機です。\nもちろん、悪い意味で有名です。\n飲料には基準値を超えた成分や、正体不明の成分が詰まっています。\n広告には「飲んで生き残ったお客様から5点満点中4.7点」と書かれています。",
      choices: [
        ["エナジードリンクを飲む。", "最大HPの60%分のダメージを受けます。\n持続：攻撃速度が20%増加します。"],
        ["ミントコーラを飲む。", "最大HPの60%分のダメージを受けます。\n持続：移動速度が20%増加します。"],
        ["紅茶を飲む。", "最大HPの60%分のダメージを受けます。\n持続：MP回復力が20%増加します。"],
      ],
    },
  },
  {
    en: {
      title: "The Broken Safe",
      description: "There was once a businessman named Simon who purchased this planet.\nHe bought scrap from people who came to dump their waste, processed it, and sold it for profit.\nBusiness proceeded smoothly.\nAt least, until unfamiliar robots seemingly made of scrap stormed his office.\nUnfortunately, Credits do not appear to have been what the robots wanted.",
      choices: [
        ["Open the safe.", "Gain 1,000 Credits."],
      ],
    },
    ja: {
      title: "壊れた金庫",
      description: "かつて、この惑星を購入したサイモンという実業家がいました。\n彼はゴミを捨てに来る人々から廃材を買い取り、加工して販売する事業を始めました。\n事業は順調に進みました。\n廃材で作られたような見知らぬロボットたちが、事務所へ押し入ってくるまでは。\n残念ながら、ロボットたちの目的はクレジットではなかったようです。",
      choices: [
        ["金庫を開ける。", "1,000クレジットを獲得します。"],
      ],
    },
  },
  {
    en: {
      title: "The Lost Delivery Robot",
      description: "A delivery robot has been searching for its recipient for 300 years.\nAfter taking the package and examining it, you discover that it was meant for someone named Simon.\nThe robot emits a warning tone, demanding the package back.",
      choices: [
        ["Open the package.", "Obtain a Depleted Core.\nMove to an Elite Stage."],
        ["Return the package.", "No effect."],
      ],
    },
    ja: {
      title: "迷子の配送ロボット",
      description: "配送ロボットが300年もの間、受取人を探し続けています。\n荷物を奪って確認すると、サイモンという人物に届けるものだったようです。\nロボットは荷物を返せと警告音を鳴らしています。",
      choices: [
        ["荷物を開ける。", "放電したコアを獲得します。\nエリートステージへ移動します。"],
        ["荷物を返す。", "効果はありません。"],
      ],
    },
  },
  {
    en: {
      title: "Emergency Vent",
      description: "You hide inside a ventilation duct to escape the pursuing robot army.\nThe duct is filled with dust, and you would rather leave as soon as possible.\nAs you hurry forward, the passage splits in two.",
      choices: [
        ["Go left.", "Move to Camp."],
        ["Go right.", "Move to a Normal Stage."],
      ],
    },
    ja: {
      title: "非常用ダクト",
      description: "追ってくるロボット軍団から逃れるため、換気ダクトに身を隠しました。\n中は埃だらけで、できるだけ早く出たいところです。\n急いで進んでいると、二手に分かれた道へ突き当たります。",
      choices: [
        ["左へ進む。", "キャンプへ移動します。"],
        ["右へ進む。", "通常ステージへ移動します。"],
      ],
    },
  },
  {
    en: {
      title: "AI Counseling Room",
      description: "This AI counselor was created by the leading IT company Burning Lemon Tech.\nRemarkably, it analyzes your concerns and produces a solution in just 0.003 seconds!\nAs you try to view the answer, a 60-second advertisement begins playing.",
      choices: [
        ["Ask about the future.", "Persistent: Attack increases by 5%.\nMaximum HP decreases by 5%."],
        ["Ask about the past.", "Maximum HP increases by 5%.\nPersistent: Attack decreases by 5%."],
        ["Skip the advertisement.", "Pay 200 Credits.\nPersistent: Movement speed increases by 5%."],
      ],
    },
    ja: {
      title: "AI相談室",
      description: "大手IT企業バーニングレモンテックが開発したAIカウンセラーです。\n驚くべきことに、あなたの悩みを0.003秒で分析し、解決策を導き出しました！\n回答を確認しようとすると、60秒の広告動画が再生されます。",
      choices: [
        ["未来について相談する。", "持続：攻撃力が5%増加します。\n最大HPが5%減少します。"],
        ["過去について相談する。", "最大HPが5%増加します。\n持続：攻撃力が5%減少します。"],
        ["広告をスキップする。", "200クレジットを支払います。\n持続：移動速度が5%増加します。"],
      ],
    },
  },
  {
    en: {
      title: "Scrap Compactor",
      description: "The compactor roars as it devours piles of scrap.\nThe swallowed metal is compressed into cubes and stacked behind the machine.",
      choices: [
        ["Put in some items.", "Select and discard 2 items from your inventory.\nGain 400 Credits.\nObtain the Scrap Cube Core."],
        ["Leave.", "No effect."],
      ],
    },
    ja: {
      title: "スクラップ圧縮機",
      description: "圧縮機が轟音を上げながら廃材を飲み込んでいます。\n飲み込まれた廃材は立方体に圧縮され、機械の後ろへ積み上げられています。",
      choices: [
        ["アイテムを入れる。", "インベントリからアイテムを2個選んで捨てます。\n400クレジットを獲得します。\nスクラップキューブコアを獲得します。"],
        ["立ち去る。", "効果はありません。"],
      ],
    },
  },
  {
    en: {
      title: "Unfamiliar Memory Data",
      description: "Memory data belonging to an unknown scavenger flickers on the floor.\nIt appears that the scavenger who explored this place before you met an unfortunate end.\nThe data contains 80% combat techniques and 20% cat videos.",
      choices: [
        ["Install the combat data.", "Persistent: Skill cooldown decreases by 5%.\nTake damage equal to 5% of maximum HP."],
        ["Watch only the cat videos.", "Restore 20% of maximum HP."],
      ],
    },
    ja: {
      title: "見知らぬ記憶データ",
      description: "あなたのものではない、スカベンジャーの記憶データが床で点滅しています。\n以前ここを探索したスカベンジャーは、不運な最期を迎えたようです。\nデータの内容は戦闘技術80%、猫動画20%です。",
      choices: [
        ["戦闘データをインストールする。", "持続：スキルのクールダウンが5%減少します。\n最大HPの5%分のダメージを受けます。"],
        ["猫動画だけを見る。", "最大HPの20%を回復します。"],
      ],
    },
  },
];

const dataRange = sheet.getRange("A3:J44");
const data = dataRange.values;

if (translations.length !== 14 || data.length !== 42) {
  throw new Error(`Unexpected source data: ${translations.length} translations, ${data.length} rows`);
}

function makeLocalizedRow(questId, languageOffset, localized) {
  const row = [
    languageOffset + questId,
    localized.title,
    localized.description,
    localized.choices.length,
  ];

  for (let choiceIndex = 0; choiceIndex < 3; choiceIndex += 1) {
    const choice = localized.choices[choiceIndex];
    row.push(choice?.[0] ?? null, choice?.[1] ?? null);
  }

  return row;
}

for (let questId = 0; questId < translations.length; questId += 1) {
  const koreanRow = data[questId * 3];
  const expectedChoiceCount = koreanRow[3];
  const translation = translations[questId];

  if (!koreanRow[1]) {
    throw new Error(`Korean source is empty for quest ${questId}`);
  }

  if (
    translation.en.choices.length !== expectedChoiceCount
    || translation.ja.choices.length !== expectedChoiceCount
  ) {
    throw new Error(`Choice count mismatch for quest ${questId}`);
  }

  data[questId * 3 + 1] = makeLocalizedRow(questId, 10000, translation.en);
  data[questId * 3 + 2] = makeLocalizedRow(questId, 20000, translation.ja);
}

dataRange.values = data;

const translationCheck = await workbook.inspect({
  kind: "table",
  range: "UnknownLangTable!A3:J44",
  include: "values,formulas",
  tableMaxRows: 12,
  tableMaxCols: 10,
  tableMaxCellChars: 140,
  maxChars: 12000,
});
console.log(translationCheck.ndjson);

const finalRowsCheck = await workbook.inspect({
  kind: "table",
  range: "UnknownLangTable!A39:J44",
  include: "values,formulas",
  tableMaxRows: 6,
  tableMaxCols: 10,
  tableMaxCellChars: 140,
  maxChars: 7000,
});
console.log(finalRowsCheck.ndjson);

const errorCheck = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
console.log(errorCheck.ndjson);

for (const [fileName, range] of [
  ["final-preview-top.png", "A1:J23"],
  ["final-preview-bottom.png", "A24:J44"],
]) {
  const finalPreview = await workbook.render({
    sheetName: sheet.name,
    range,
    scale: 1,
    format: "png",
  });
  await fs.writeFile(
    `${outputDir}/${fileName}`,
    new Uint8Array(await finalPreview.arrayBuffer()),
  );
}

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(JSON.stringify({
  outputPath,
  translatedQuestCount: translations.length,
  translatedRowCount: translations.length * 2,
}));
