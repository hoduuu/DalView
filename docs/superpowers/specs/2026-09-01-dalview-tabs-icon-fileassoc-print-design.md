# 달뷰 (DalView) — 탭 지원 / 아이콘 / 파일 연결 / 인쇄 설계 문서

- Date: 2026-09-01
- Status: Approved (design), pending implementation plan
- Base: builds on [2026-09-01-dalview-pdf-reader-design.md](2026-09-01-dalview-pdf-reader-design.md) (v1, already shipped)

## 배경 / 동기

v1(단일 문서 뷰어)이 정상 동작함을 확인한 뒤 사용자가 요청한 4가지 후속 기능:

1. 여러 PDF를 탭으로 전환하며 보기
2. 앱 아이콘 적용
3. PDF 파일 더블클릭 시 달뷰로 열기 (실행 중이면 새 탭으로)
4. 인쇄

## 범위

**포함:**
- 탭 UI: 파일마다 독립된 탭, 탭 헤더는 파일명 + 닫기 버튼
- 탭별로 완전히 독립적인 상태 (문서, 페이지, 줌, 검색, 북마크, 썸네일 — 서로 간섭 없음)
- 마지막 탭을 닫으면 앱 종료
- 앱 아이콘 (창 아이콘 + 작업 표시줄/탐색기 아이콘)
- 단일 인스턴스 + named pipe로 파일 경로 전달 (이미 실행 중이면 새 창 대신 새 탭으로 열기)
- 인쇄: 현재 탭 기준, Windows 기본 인쇄 대화상자

**명시적으로 제외 (YAGNI):**
- 레지스트리 자동 등록 (파일 연결은 사용자가 Windows "연결 프로그램"에서 수동으로 1회 지정 — VideoPlayer와 동일한 방식, 앱은 `Main`의 `args[0]`만 받으면 됨)
- 탭 드래그로 순서 변경, 탭 분리(새 창으로 드래그) 등 브라우저급 탭 고급 기능
- 인쇄 미리보기, 페이지별 커스텀 인쇄 설정 UI (Windows 기본 대화상자가 제공하는 것 이상은 안 만듦)
- v1에서 이미 제외한 것들(편집, 주석, 서명, 클라우드 동기화)은 계속 제외

## 아키텍처 변경

### 탭 도입에 따른 ViewModel 재구조화

v1의 `MainViewModel`이 갖고 있던 문서별 상태 전부를 새 `DocumentTabViewModel`로 옮긴다:

- `DocumentTabViewModel` (신규, `MainViewModel`의 구현을 거의 그대로 옮김): `PdfPath`, `Document`, `Page`, `DisplayPage`, `PageCount`, `Zoom`/`ZoomMin`/`ZoomMax`, `FitWidth`, `StatusMessage`, `SearchText`/`Matches`/`MatchIndex`/`HighlightAllMatches`, `Thumbnails`, `PasswordRequired` 이벤트, `OpenPath` 및 관련 커맨드(`ZoomInCommand`/`ZoomOutCommand`/`SearchCommand`/`NextMatchCommand`/`PreviousMatchCommand`) 전부 이관. 추가로 `Title` (읽기전용, `Path.GetFileName(PdfPath)` — 탭 헤더 표시용), `PrintCommand` 신규 추가.
- `MainViewModel` (재정의, 얇아짐): `ObservableCollection<DocumentTabViewModel> Tabs`, `DocumentTabViewModel? SelectedTab`, `OpenFileCommand`(파일 다이얼로그 → 새 `DocumentTabViewModel` 생성 → `Tabs`에 추가 → `SelectedTab`으로 설정), `CloseTabCommand(DocumentTabViewModel)`(제거, `Tabs`가 비면 앱 종료), `OpenPathAsNewTab(string path)`(파일 다이얼로그를 거치지 않고 외부에서 넘어온 경로를 새 탭으로 여는 공용 진입점 — 파이프 수신부와 `OpenFileCommand` 둘 다 이 경로를 사용).

### MainWindow.xaml

- 상단에 `TabControl` 추가: `ItemsSource="{Binding Tabs}"`, `SelectedItem="{Binding SelectedTab}"`. `TabItem.Header`는 커스텀 `HeaderTemplate`(파일명 텍스트 + 작은 × 닫기 버튼, 클릭 시 `CloseTabCommand`). `TabItem`의 콘텐츠(`ContentTemplate`)가 v1의 툴바+사이드바+PDFViewer 전체 — `DocumentTabViewModel`을 DataContext로 바인딩.
- `PasswordDialog` 표시 로직: `DocumentTabViewModel` 생성 시점에 그 인스턴스의 `PasswordRequired`를 구독(코드비하인드에서, 새 탭이 추가될 때 한 번). v1처럼 창 생성자에서 한 번만 구독하는 방식에서 "탭 추가할 때마다 구독"으로 변경.

### 단일 인스턴스 + 파일 연결 (VideoPlayer 패턴 재사용)

`App.xaml.cs OnStartup`에서 VideoPlayer의 구현을 그대로 이식:
- Named Mutex (`"DalView_SingleInstance_Mutex"`)로 중복 실행 감지
- 이미 실행 중이면: 넘어온 파일 경로를 Named Pipe(`"DalView_OpenFile_Pipe"`)로 전송하고 즉시 종료 (창 띄우지 않음)
- 최초 실행이면: 파이프 서버 시작(백그라운드), `e.Args[0]`이 있으면 첫 탭으로 열기
- 파이프 서버가 경로를 받으면 `Dispatcher.Invoke`로 UI 스레드에서 `MainViewModel.OpenPathAsNewTab(path)` 호출 + 창을 최소화 상태였으면 복원 + `Activate()`로 포그라운드로

레지스트리 자동 등록은 하지 않음 — 사용자가 파일 탐색기에서 PDF 우클릭 → 연결 프로그램 → 다른 앱 선택 → 달뷰 찾아서 선택 → "항상 이 앱 사용"을 1회 수행하면 이후 자동 동작.

### 인쇄

각 `DocumentTabViewModel`에 `PrintCommand` 추가:
```csharp
[RelayCommand]
private void Print()
{
    if (Document == null) return;
    using var printDocument = Document.CreatePrintDocument();
    using var dialog = new System.Windows.Forms.PrintDialog { Document = printDocument };
    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
    {
        printDocument.Print();
    }
}
```
`PdfiumViewer.Core.IPdfDocument.CreatePrintDocument()`가 반환하는 `System.Drawing.Printing.PrintDocument`를 그대로 사용. Windows Forms의 네이티브 `PrintDialog`를 띄우기 위해 `DalView.csproj`에 `<UseWindowsForms>true</UseWindowsForms>` 추가 필요 (WPF 프로젝트에 WinForms 프린트 대화상자만 얹는 흔한 조합, 편집기/에디터 기능은 전혀 안 끌어옴).
툴바에 "인쇄" 버튼 추가, `PrintCommand`에 바인딩. 활성 탭(`SelectedTab`) 기준으로 동작.

### 아이콘

첨부받은 PNG(`D:\claude\DalView\icon.png`, 투명 배경, moon+PDF 디자인)를 다중 해상도 `.ico`(16/32/48/256px)로 변환해 `src/DalView/Assets/icon.ico`에 저장. `DalView.csproj`에 `<ApplicationIcon>Assets\icon.ico</ApplicationIcon>` 추가(작업표시줄/탐색기 아이콘), `MainWindow.xaml`에 `Icon="Assets/icon.ico"` 추가(창 좌상단 아이콘).

## 에러 처리

- 파이프 연결 실패(2초 타임아웃) 시 조용히 무시 — VideoPlayer와 동일하게, 사용자에게는 "파일 열기"로 보이는 동작에 에러 팝업을 띄우지 않음
- 인쇄 대화상자에서 프린터 없음/취소 시 아무 동작 안 함(예외 없이 조용히 리턴)
- 탭이 0개인 상태는 존재하지 않음(마지막 탭 닫으면 즉시 앱 종료이므로 빈 상태 UI 불필요)

## 테스트 방향

- `MainViewModel`(탭 컨테이너)의 `OpenFileCommand`/`CloseTabCommand`/`OpenPathAsNewTab` 로직: 실제 `PdfiumDocumentLoader` + `MinimalPdfBuilder` 조합으로 탭 추가/제거/전환 시 각 탭 상태가 서로 독립적인지(한 탭에서 검색해도 다른 탭 `Matches`에 영향 없음) 검증
- Named Pipe 송수신 로직은 실제 프로세스 2개를 띄워야 완전히 검증 가능 — 유닛 테스트로는 어려우니, v1 때처럼 명령줄 인자로 앱을 구동해 실제로 확인 + 정직하게 한계 기록
- 인쇄는 `PrintDialog.ShowDialog()`가 실제 UI를 띄우므로 자동화 테스트 대상이 아님 — 수동 확인(대화상자가 뜨고 취소해도 크래시 없음 정도)

## 프로젝트 정보

- 대상 프로젝트: `D:\claude\DalView` (기존 저장소, v1 위에 이어서 작업)
- 아이콘 원본: `D:\claude\DalView\icon.png`
