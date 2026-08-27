using System;

// 확인(Confirm) 다이얼로그
public struct KY_AlertData
{
    public string title;
    public string message;
    public string warningText;  // 필요 없으면 null/빈 문자열
    public Action onConfirm;    // 확인 버튼 클릭 시 실행할 로직 (없으면 null도 가능)
}