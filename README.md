# PatientTimelineBlazor

本儲存庫用於規劃與實作 **FHIR 病人時序圖（Patient Timeline）** 前端系統，目標技術為 **ASP.NET Core Blazor（Interactive Server / Global）**，並串接 Firely FHIR R4 API（`https://server.fire.ly`）。

## 文件導覽

- 完整操作指引（中文）：`docs/Codex_Blazor_Patient_Timeline_Implementation_Guide_zh-TW.md`

## 目前建議開發順序

1. 建立 Blazor Web App 專案骨架（Global Interactive Server）。
2. 加入 FHIR service layer（`IFhirDataService` / `FhirDataService`）。
3. 建立 Timeline mapping model 與聚合流程。
4. 實作可重用元件：Timeline / EventCard / DetailPanel。
5. 加上日期區間篩選、錯誤處理、loading 狀態。
6. 完成文件化、測試、與部署準備。

## 快速驗證命令

```bash
dotnet restore
dotnet build
dotnet test
```

> 若本機尚未安裝 .NET 8 SDK，請先安裝後再執行上述命令。
