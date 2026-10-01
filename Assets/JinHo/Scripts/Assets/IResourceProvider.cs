// [코드 지도] IResourceProvider: 자원 구현체가 제공하는 데이터와 채집 횟수 관련 속성 계약이다. 자신의 필드나 메서드 본문은 없다. 실제 값과 변경 규칙은 TreeInteractable 등 자원 클래스에 있다. IInteractable과 함께 구현하면 행동과 자원 정보를 서로 다른 계약으로 조회할 수 있다.
// 주요 함수: 필드·데이터 선언
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Assets/IResourceProvider.cs.md

/// <summary>Marks interactables that provide gatherable resources.</summary>
public interface IResourceProvider
{
    ResourceData ResourceData { get; }
    int Tier { get; }
    int AddItemInterval { get; set; }
    int MaxInteractCount { get; }
}
