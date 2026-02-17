# LanguageSchoolERP - Codex Instructions

## Architecture
- WPF UI project: LanguageSchoolERP.App
- EF Core + DbContext project: LanguageSchoolERP.Data
- Models project: LanguageSchoolERP.Core
- MVVM uses CommunityToolkit.Mvvm (ObservableObject, RelayCommand, etc.)

## Goal
Replace all hardcoded "Programs" logic with database-backed StudyProgram entities.
Add a new Programs management screen that allows:
- list programs
- add program
- edit program
- delete program (with confirmation)
Fields: Name (required), HasTransport, HasStudyLab, HasBooks.

## Constraints
- Use SchoolDbContext.
- Use async EF Core calls where possible.
- Keep changes minimal and consistent with existing patterns in the app.
- Do not break existing Enrollment/Payment flows.
- If Programs are referenced by Enrollment later, deletion should be blocked with a friendly message (if relationship exists). If not, allow deletion.

## Deliverables
1) Data access: ProgramService (CRUD)
2) ViewModels: ProgramsListViewModel + ProgramEditViewModel (or dialog)
3) Views: ProgramsView (DataGrid) + Add/Edit dialog
4) Replace hardcoded program sources in enrollment/payment UIs with DB-backed list
5) Add tests if existing test project exists; otherwise add at least basic runtime validation.
