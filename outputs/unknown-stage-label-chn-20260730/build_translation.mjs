import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "C:/Users/user/Desktop/UnknownStageLabel.xlsx";
const outputDir =
  "C:/Users/user/Project2/outputs/unknown-stage-label-chn-20260730";
const outputPath = `${outputDir}/UnknownStageLabel_CHN_Translated.xlsx`;

await fs.mkdir(outputDir, { recursive: true });

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const translations = [
  [
    "过度充电",
    "在工厂深处，发现了一个规格不匹配的大型充电器。\n虽然没找到转换接头，但硬要充电的话似乎也不是不行。",
    "充电。",
    "持续：攻击力提高5%。\n受到相当于最大生命值30%的伤害。",
    "离开。",
    "无事发生。",
    null,
    null,
  ],
  [
    "保质期：三个世纪前",
    "应急食品储藏柜敞开着。\n保质期早已过了，而且闻起来像动物饲料，不过里面的东西看上去还算完好。",
    "吃下去。",
    "恢复相当于最大生命值30%的生命值。\n仅限下一场战斗：防御力降低30%。",
    "悄悄关上。",
    "无事发生。",
    null,
    null,
  ],
  [
    "会唱歌的维修无人机",
    "一台损坏的维修无人机瘫在地上，一边发出哔哔声，一边等待有人来修理。\n听久了，这哔哔声甚至有点像一首歌。",
    "修理它。",
    "持续：防御力提高10%。",
    "拆下核心。",
    "获得维修无人机核心。",
    null,
    null,
  ],
  [
    "禁止按下的按钮",
    "走廊正中央有一个红色按钮。\n下方还非常贴心地写着“请勿按下”。",
    "按下。",
    "前往精英关卡。",
    "尊重警告。",
    "无事发生。",
    null,
    null,
  ],
  [
    "无主储物柜",
    "发现了一个老旧的储物柜。\n不知道是谁的，但上面挂着一把牢固的挂锁。\n你小心翼翼地猜起了密码。",
    "猜密码。",
    "有50%的概率随机获得1件独特品质装备。",
    "强行打开。",
    "随机获得2件高级品质装备。",
    null,
    null,
  ],
  [
    "磁暴",
    "巨大的磁暴让整栋建筑都在摇晃。\n口袋里的金属物品开始剧烈震动。\n想要离开这里，看来必须舍弃点什么。",
    "丢弃物品。",
    "从背包中选择并丢弃3件物品。",
    "跑出去。",
    "从背包中随机丢弃2件物品。",
    "趴在地上。",
    "失去所有信用点。",
  ],
  [
    "软件更新",
    "维修终端建议你更新到最新软件。\n一如既往，新程序总会带来一些新问题。",
    "更新到最新版。",
    "持续：攻击力提高8%。\n持续：防御力降低15%。",
    "安装稳定版。",
    "持续：防御力提高10%。\n持续：攻击力降低5%。",
    "稍后提醒我。",
    "无事发生。",
  ],
  [
    "威廉自动售货机",
    "这是威廉公司生产的著名饮料自动售货机。\n当然，出名的原因不太光彩。\n饮料中塞满了超出标准剂量或成分不明的东西。\n广告牌写着：“喝完还能活着的顾客给出了4.7分（满分5分）。”",
    "喝能量饮料。",
    "受到相当于最大生命值60%的伤害。\n持续：攻击速度提高20%。",
    "喝薄荷可乐。",
    "受到相当于最大生命值60%的伤害。\n持续：移动速度提高20%。",
    "喝红茶。",
    "受到相当于最大生命值60%的伤害。\n持续：MP恢复速度提高20%。",
  ],
  [
    "破损的保险柜",
    "曾经，有一位名叫西蒙的商人买下了这颗星球。\n他向前来倾倒垃圾的人收购废料，再加工后出售。\n生意一直很顺利。\n直到一群像是由废料拼成的陌生机器人闯进他的办公室。\n遗憾的是，那些机器人的目标似乎并不是信用点。",
    "打开保险柜。",
    "获得1,000信用点。",
    null,
    null,
    null,
    null,
  ],
  [
    "迷路的配送机器人",
    "一台配送机器人已经寻找收件人300年了。\n抢过包裹检查后，发现它似乎原本要送给一个叫西蒙的人。\n机器人正发出警告声，要求你归还包裹。",
    "打开包裹。",
    "获得耗尽能量的核心。\n前往精英关卡。",
    "归还包裹。",
    "无事发生。",
    null,
    null,
  ],
  [
    "应急通风管",
    "为了躲避追来的机器人军团，你藏进了通风管。\n里面满是灰尘，让你只想尽快离开。\n匆忙前进时，你遇到了一个岔路口。",
    "向左走。",
    "前往营地。",
    "向右走。",
    "前往普通关卡。",
    null,
    null,
  ],
  [
    "AI咨询室",
    "这是大型IT公司燃烧柠檬科技开发的AI咨询师。\n令人惊讶的是，它只用了0.003秒就分析完你的烦恼并得出了解决方案！\n你正要查看答案，一段60秒的广告视频开始播放。",
    "咨询未来。",
    "持续：攻击力提高5%。\n最大生命值降低5%。",
    "咨询过去。",
    "最大生命值提高5%。\n持续：攻击力降低5%。",
    "跳过广告。",
    "支付200信用点。\n持续：移动速度提高5%。",
  ],
  [
    "废料压缩机",
    "压缩机轰鸣着吞下成堆废料。\n被吞进去的废料压成立方体，堆放在机器后方。",
    "放入物品。",
    "从背包中选择并丢弃2件物品。\n获得400信用点。\n获得废料方块核心。",
    "离开。",
    "无事发生。",
    null,
    null,
  ],
  [
    "陌生的记忆数据",
    "一份不属于你的拾荒者记忆数据正在地面上闪烁。\n过去探索这里的拾荒者似乎遭遇了不幸的结局。\n检查内容后发现：80%是战斗技巧，20%是猫咪视频。",
    "安装战斗数据。",
    "持续：技能冷却时间缩短5%。\n受到相当于最大生命值5%的伤害。",
    "只看猫咪视频。",
    "恢复相当于最大生命值20%的生命值。",
    null,
    null,
  ],
];

const korSheet = workbook.worksheets.getItem("KOR");
const chnSheet = workbook.worksheets.getItem("CHN");
const sourceIds = korSheet.getRange("A2:A15").values.flat();
const targetIds = chnSheet.getRange("A2:A15").values.flat();

if (translations.length !== sourceIds.length) {
  throw new Error(
    `Translation row count mismatch: ${translations.length} translations for ${sourceIds.length} source rows.`,
  );
}

for (let index = 0; index < sourceIds.length; index++) {
  if (sourceIds[index] !== targetIds[index]) {
    throw new Error(
      `stageId mismatch at row ${index + 2}: ${sourceIds[index]} != ${targetIds[index]}`,
    );
  }
}

chnSheet.getRange("B2:I15").values = translations;
chnSheet.getRange("B2:I15").format.wrapText = true;
chnSheet.getRange("A2:I15").format.autofitRows();

const finalCheck = await workbook.inspect({
  kind: "table",
  range: "CHN!A1:I15",
  include: "values,formulas",
  tableMaxRows: 15,
  tableMaxCols: 9,
  tableMaxCellChars: 180,
  maxChars: 14000,
});
console.log(finalCheck.ndjson);

const errorCheck = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
console.log(errorCheck.ndjson);

const finalPreview = await workbook.render({
  sheetName: "CHN",
  range: "A1:I15",
  scale: 1,
  format: "png",
});
await fs.writeFile(
  `${outputDir}/final-CHN.png`,
  new Uint8Array(await finalPreview.arrayBuffer()),
);

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(JSON.stringify({ outputPath, translatedRows: translations.length }));
