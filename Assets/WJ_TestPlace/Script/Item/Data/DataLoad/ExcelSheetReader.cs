using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ExcelDataReader;
using UnityEngine;

namespace DataSystem.Excel
{
    /// <summary>
    /// 엑셀 시트를 읽어서 헤더 이름 기준으로 임의의 타입 T에 매핑해주는 범용 유틸리티.
    /// 특정 데이터(아이템 등)를 몰라도 되는 순수 파싱 로직만 담당한다.
    /// 새로운 엑셀 기반 데이터(스킬트리, 스테이지 등)가 생기면 이 클래스를 그대로 재사용하면 됨.
    /// </summary>
    public static class ExcelSheetReader
    {
        /// <summary>
        /// 헤더 바로 밑에 타입 힌트 행("string"/"int"/"float"/"bool")이 있는 시트를 위한 필터.
        /// 한 행의 모든 셀 값이 이 토큰 중 하나면 데이터가 아니라 주석 행으로 보고 건너뛴다.
        /// </summary>
        private static readonly HashSet<string> TypeAnnotationTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "string", "int", "float", "bool"
        };

        /// <summary>현재 커서가 위치한 시트를 읽어서 헤더 이름 -> 셀 값 딕셔너리 목록으로 반환한다.</summary>
        public static List<Dictionary<string, string>> ReadSheetRows(IExcelDataReader reader)
        {
            List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();
            List<string> headers = new List<string>();
            bool headerRead = false;

            while (reader.Read())
            {
                if (!headerRead)
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string header = CellToString(reader.GetValue(i));
                        headers.Add(header);
                    }

                    headerRead = true;
                    continue;
                }

                Dictionary<string, string> row = new Dictionary<string, string>();
                bool hasData = false;

                for (int i = 0; i < reader.FieldCount && i < headers.Count; i++)
                {
                    string header = headers[i];
                    if (string.IsNullOrWhiteSpace(header))
                        continue;

                    string value = CellToString(reader.GetValue(i));
                    if (!string.IsNullOrWhiteSpace(value))
                        hasData = true;

                    row[header] = value;
                }

                if (!hasData)
                    continue;

                // 헤더 바로 밑에 타입 힌트 행("string, string, float, ...")이 오는 경우, 실제 데이터가 아니므로 건너뜀.
                if (IsTypeAnnotationRow(row))
                    continue;

                result.Add(row);
            }

            return result;
        }

        /// <summary>행 딕셔너리 목록을 리플렉션으로 T의 public 필드에 이름 매칭해서 채운다.</summary>
        public static List<T> MapRows<T>(List<Dictionary<string, string>> rows) where T : new()
        {
            List<T> result = new List<T>();
            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

            if (rows.Count > 0)
            {
                foreach (FieldInfo field in fields)
                {
                    if (!rows[0].ContainsKey(field.Name))
                        Debug.LogWarning($"[ExcelSheetReader] {typeof(T).Name}.{field.Name}에 대응하는 엑셀 헤더를 못 찾았습니다. 해당 값은 언제나 기본값으로만 채워집니다.");
                }
            }

            foreach (Dictionary<string, string> row in rows)
            {
                T item = new T();

                foreach (FieldInfo field in fields)
                {
                    if (!row.TryGetValue(field.Name, out string value))
                        continue;

                    field.SetValue(item, ConvertValue(value, field.FieldType));
                }

                result.Add(item);
            }

            return result;
        }

        /// <summary>행의 모든 셀 값이 타입 이름(string/int/float/bool)이면 주석 행으로 간주한다.</summary>
        private static bool IsTypeAnnotationRow(Dictionary<string, string> row)
        {
            foreach (string value in row.Values)
            {
                if (!TypeAnnotationTokens.Contains((value ?? string.Empty).Trim()))
                    return false;
            }

            return true;
        }

        private static object ConvertValue(string value, Type targetType)
        {
            if (targetType == typeof(string))
                return value ?? string.Empty;

            if (targetType == typeof(int))
            {
                if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int intValue))
                    return intValue;
                if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatValue))
                    return Mathf.RoundToInt(floatValue);
                return 0;
            }

            if (targetType == typeof(float))
            {
                if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatValue))
                    return floatValue;
                return 0f;
            }

            if (targetType == typeof(bool))
            {
                string lower = (value ?? string.Empty).Trim().ToLowerInvariant();
                return lower == "true" || lower == "1" || lower == "yes" || lower == "y" || lower == "o" || lower == "체크";
            }

            return null;
        }

        private static string CellToString(object value)
        {
            if (value == null)
                return string.Empty;

            if (value is double doubleValue)
                return doubleValue.ToString(CultureInfo.InvariantCulture);

            if (value is float floatValue)
                return floatValue.ToString(CultureInfo.InvariantCulture);

            if (value is int intValue)
                return intValue.ToString(CultureInfo.InvariantCulture);

            if (value is bool boolValue)
                return boolValue ? "true" : "false";

            return value.ToString().Trim();
        }
    }
}
