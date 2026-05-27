using System;

namespace Smart_Road
{
    // 파일 저장 작업의 결과를 나타내는 클래스
    // DataManager의 CSV 저장 메서드가 성공/실패 여부와 상세 메시지를 반환할 때 사용
    // UI에서 사용자에게 저장 결과를 알리는 데 필요한 모든 정보를 포함
    public class SaveResult
    {
        // 저장 성공 여부 (true면 성공, false면 실패)
        public bool Success { get; }
        // 결과 메시지 (성공 시 "저장 완료" 또는 실패 사유 등)
        public string Message { get; }
        // 저장된 파일의 절대 경로 (저장 실패 시 null)
        public string FilePath { get; }

        // 저장 결과 객체 생성
        // filePath는 선택사항 (저장 실패 시 전달하지 않음)
        public SaveResult(bool success, string message, string filePath = null)
        {
            Success = success;
            Message = message;
            FilePath = filePath;
        }
    }
}
