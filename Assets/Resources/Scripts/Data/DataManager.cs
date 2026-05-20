using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class DataManager : MonoBehaviour
{
    /// <summary>
    /// Resources 폴더 안의 JSON 파일을 읽어 지정한 타입의 리스트로 반환하는 제네릭 함수
    /// </summary>
    /// <typeparam name="T">파싱할 데이터 클래스 타입</typeparam>
    /// <param name="resourcePath">Resources 폴더 기준의 파일 경로 (확장자 제외)</param>
    public List<T> LoadGameData<T>(string resourcePath)
    {
        // 1. Resources.Load를 통해 TextAsset으로 JSON 파일을 불러옵니다.
        TextAsset jsonAsset = Resources.Load<TextAsset>(resourcePath);

        if (jsonAsset == null)
        {
            Debug.LogError($"JSON 파일을 찾을 수 없습니다: Resources/{resourcePath}");
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
            Debug.LogError($"{resourcePath} JSON 파싱 중 오류 발생: {ex.Message}");
            return null;
        }
    }
}