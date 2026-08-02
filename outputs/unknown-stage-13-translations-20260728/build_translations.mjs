import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "C:/Users/user/Desktop/UnknownStageTable.xlsx";
const outputDir = "C:/Users/user/Project2/outputs/unknown-stage-13-translations-20260728";
const outputPath = `${outputDir}/UnknownStageTable_Translated.xlsx`;
const mode = process.argv[2] ?? "build";

await fs.mkdir(outputDir, { recursive: true });

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);
const sheet = workbook.worksheets.getItem("UnknownLangTable");

const workbookCheck = await workbook.inspect({
  kind: "workbook,sheet,table",
  include: "id,name,values,formulas",
  tableMaxRows: 10,
  tableMaxCols: 10,
  tableMaxCellChars: 120,
  maxChars: 8000,
});
console.log(workbookCheck.ndjson);

const usedRange = sheet.getUsedRange(true);
const sourceCheck = await workbook.inspect({
  kind: "table",
  range: `UnknownLangTable!${usedRange.address}`,
  include: "values,formulas",
  tableMaxRows: 200,
  tableMaxCols: 10,
  tableMaxCellChars: 200,
  maxChars: 50000,
});
await fs.writeFile(`${outputDir}/source-inspect.ndjson`, sourceCheck.ndjson, "utf8");
console.log(JSON.stringify({ usedRange: usedRange.address, inspectPath: `${outputDir}/source-inspect.ndjson` }));

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

throw new Error("Translation data has not been added yet.");

