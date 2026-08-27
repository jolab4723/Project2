using System;

// 확인 다이얼로그(예/아니오)용 데이터 구조체.
public struct KY_DialogData
{
    public string title;        // 상단 제목 (예: "포기 확인")
    public string message;      // 본문 안내 문구 (예: "게임을 포기하시겠습니까?")
    public string warningText;  // 강조 경고 문구, 없으면 null 또는 "" (예: "진행 중인 데이터가 사라집니다.")
    public Action onYes;        // 예 버튼 클릭 시 실행할 로직
    public Action onNo;         // 아니오 버튼 클릭 시 실행할 로직 (없으면 null)
}