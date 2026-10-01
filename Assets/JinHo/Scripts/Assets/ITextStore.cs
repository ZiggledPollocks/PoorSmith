// [코드 지도] ITextStore: 텍스트 존재 여부·읽기·쓰기·저장 확정을 추상화한다. JSON 구조와 저장 시점은 호출자 PlayerSaveSystem 등이 결정한다.
// 주요 함수: Read, Write, Flush
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Assets/ITextStore.cs.md

/// <summary>Text persistence mechanism. Callers retain schema, logging and save policy.</summary>
public interface ITextStore
{
    bool Exists { get; }
    string Read();
    void Write(string text);
    void Flush();
}
