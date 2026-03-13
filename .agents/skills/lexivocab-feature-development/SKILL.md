---
name: lexivocab-feature-development
description: Standard guide for developing new features for LexiVocabAPI. Use when adding Entities, Commands, Queries, or API endpoints to ensure compliance with Clean Architecture, CQRS, and Testing.
---

# 🚀 LexiVocab Feature Development Skill

This skill provides a standard workflow for developing new features in the LexiVocabAPI project, ensuring strict adherence to **Clean Architecture**, **CQRS**, and the established **Testing Strategy**.

## 🏗️ 1. Development Workflow

ALWAYS follow the layers from the core outwards:

1.  **Domain Layer**: Define Entities, Enums, and Repository Interfaces.
2.  **Infrastructure Layer**: Configure EF Core, create Migrations, and implement Repositories.
3.  **Application Layer**: Define DTOs, Commands, Queries, Handlers, and Validators.
4.  **API Layer**: Create/Update Controllers and register routes.
5.  **Testing**: Write Unit Tests for Handlers and Integration Tests for Repos/API.

## 📝 2. Naming Conventions

| Component | Rule | Example |
| :--- | :--- | :--- |
| **Entity** | PascalCase, singular | `UserVocabulary` |
| **DTO** | PascalCase + `Dto` | `VocabularyDto` |
| **Command** | Verb + Noun + `Command` | `CreateVocabularyCommand` |
| **Query** | `Get` + Noun + `Query` | `GetVocabularyListQuery` |
| **Handler** | Command/Query name + `Handler` | `CreateVocabularyHandler` |
| **Repository** | `I` + Entity + `Repository` | `IVocabularyRepository` |
| **Controller** | Entity (plural) + `Controller` | `VocabulariesController` |
| **Test** | Handler + `Tests` | `CreateVocabularyHandlerTests` |

## 💎 3. Core Patterns & Best Practices

### Result<T> Pattern
DO NOT use `try-catch` for Business Logic. ALWAYS return `Result<T>`.
- `Result<T>.Success(data)` (200 OK)
- `Result<T>.Created(data)` (201 Created)
- `Result<T>.Failure(msg, 400)` (400 BadRequest)
- `Result<T>.NotFound(msg)` (404 NotFound)
- `Result<T>.Conflict(msg)` (409 Conflict)

### MediatR Pipelines (Opt-in)
Simply implement the interface on the Command/Query record:
- **Audit Logging**: Implement `IAuditedRequest` + define `AuditAction`.
- **Caching**: Implement `ICacheableQuery` + define `CacheKey`.
- **Validation**: Automatically runs if a corresponding `AbstractValidator<T>` exists.

### Data Security (Ownership)
ALWAYS check `UserId` before processing data to ensure Data Isolation:
```csharp
var entity = await _uow.Vocabularies.GetByIdAsync(id, ct);
if (entity == null || entity.UserId != _currentUser.UserId)
    return Result<T>.NotFound();
```

## 💾 4. EF Core & Database

### Migration Workflow
```bash
# Create migration
dotnet ef migrations add <Name> --project src/LexiVocab.Infrastructure --startup-project src/LexiVocab.API

# Update Database
dotnet ef database update --project src/LexiVocab.Infrastructure --startup-project src/LexiVocab.API
```

## 🧪 5. Testing Strategy

- **Unit Tests (Application)**: Mock `IUnitOfWork`, `ICurrentUserService`. Use `FluentAssertions` and `xUnit`.
- **Integration Tests (Infrastructure)**: Use `EF Core In-Memory` to test Repository queries.
- **API Tests**: Use `WebApplicationFactory<Program>` to test the full HTTP pipeline.

## ✅ 6. Pre-completion Checklist

- [ ] Entity inherits from `BaseEntity` (if Id/Audit fields are needed).
- [ ] Repository is registered in `IUnitOfWork` and `UnitOfWork`.
- [ ] Command has an accompanying Validator.
- [ ] DTO contains no processing logic.
- [ ] Controller has `[ProducesResponseType]` for Swagger/Scalar documentation.
- [ ] `dotnet build` and `dotnet test` pass 100%.

---
**Detailed Reference Documentation:**
- Architecture: [ARCHITECTURE.md](file:///e:/lexivocab-ex/LexiVocabAPI/ARCHITECTURE.md)
- Development Guide: [docs/DEVELOPMENT-GUIDE.md](file:///e:/lexivocab-ex/LexiVocabAPI/docs/DEVELOPMENT-GUIDE.md)
- Testing Strategy: [docs/TESTING.md](file:///e:/lexivocab-ex/LexiVocabAPI/docs/TESTING.md)
