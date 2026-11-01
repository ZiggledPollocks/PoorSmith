DaegyeongJAM 폰트 공유 묶음

원본 프로젝트 버전: Unity 6000.3.19f1 / com.unity.ugui 2.0.0 (TextMeshPro 포함)
원본 프로젝트 파일을 변경하지 않고 복사했습니다.

포함 내용
- 한글재민4 원본 TTF 및 SDF 에셋 2개
- LiberationSans 원본 TTF, SDF 및 fallback 에셋
- 연결된 머티리얼, 셰이더, 로컬 include 파일, .meta 파일
- 프로젝트에 있던 LiberationSans OFL 라이선스 문서
- 아틀라스와 기본 머티리얼은 SDF .asset 안에 내장되어 있으며 그대로 포함됩니다.

사용 방법
1. ZIP을 풀고 Assets 폴더 내용을 대상 Unity 프로젝트의 Assets에 병합합니다. .meta 파일도 함께 복사합니다.
2. 대상에 TextMeshPro를 제공하는 Unity UI 패키지가 있어야 합니다. 원본과 같은 Unity 버전이 가장 확실합니다.
3. 기존 TextMesh Pro 파일이 있으면 먼저 백업하고 충돌을 확인하세요. 프로젝트 전역 TMP Settings는 포함하지 않았습니다.
4. TMP 텍스트의 Font Asset에 원하는 Hangeuljaemin4 SDF 에셋을 지정합니다.

검증 및 제한
복사한 모든 파일의 SHA-256 일치를 확인했으며 목록과 참조 관계는 inventory.json에 기록했습니다.
한글재민4 라이선스 문서는 프로젝트 Font 폴더에서 발견되지 않았습니다. 배포 허용 조건은 이 묶음에서 확인되지 않았습니다.
Unity 에디터에서 실제 임포트/렌더링 검증은 수행하지 않았습니다.

테스트 방법
대상 프로젝트의 테스트 씬에 TextMeshPro UI 텍스트를 만들고 각 SDF 에셋을 지정하세요.
한글과 영문을 입력하여 글자가 표시되는지, 분홍색 머티리얼 또는 Missing 참조가 없는지 확인하세요.
