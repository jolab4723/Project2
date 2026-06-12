using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

// JSON파일을 제네릭 리스트 형태로 불러오는 스크립트 입니다.

public class DataManager : MonoBehaviour
{
    public List<T> LoadGameData<T>(string resourcePath)
    {
        // 1. Resources.Load를 통해 TextAsset으로 JSON 파일을 불러옵니다.
        TextAsset jsonAsset = Resources.Load<TextAsset>(resourcePath);

        if (jsonAsset == null)
        {
            Log.Error($"JSON 파일을 찾을 수 없습니다: Resources/{resourcePath}");
            return null;
        }

        try
        {
            // 2. 텍스트 데이터를 읽어와 제네릭 리스트 형태로 직렬화를 해제(Deserialization)합니다.
            string jsonText = jsonAsset.text;
            List<T> dataList = JsonConvert.DeserializeObject<List<T>>(jsonText);

            return dataList;
        }

        catch (Exception ex)
        {
            Log.Error($"{resourcePath} JSON 파싱 중 오류 발생: {ex.Message}");
            return null;
        }
    }
}