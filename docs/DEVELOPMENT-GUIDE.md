# 🧑‍💻 Hướng dẫn Phát triển Tính năng Mới

> Step-by-step guide để thêm feature mới theo đúng conventions.

---

## 1. Checklist Thêm Tính năng Mới

### Ví dụ: Thêm tính năng "Vocabulary Tags" (gắn tag cho từ vựng)

```
Bước 1: Domain Layer
  ☐ Tạo entity: Tag.cs, VocabularyTag.cs (many-to-many)
  ☐ Tạo enum nếu cần
  ☐ Tạo interface: ITagRepository.cs
  ☐ Thêm property navigation vào UserVocabulary

Bước 2: Infrastructure Layer
  ☐ Tạo EF Configuration: TagConfiguration.cs
  ☐ Thêm DbSet<Tag> vào AppDbContext
  ☐ Tạo EF Migration: dotnet ef migrations add AddTags
  ☐ Tạo repository: TagRepository.cs
  ☐ Register trong IUnitOfWork + UnitOfWork
  ☐ Register trong DependencyInjection.cs

Bước 3: Application Layer
  ☐ Tạo DTOs: TagDto.cs
  ☐ Tạo Command: AddTagToVocabularyCommand
  ☐ Tạo Query: GetTagsQuery
  ☐ Tạo Handler: AddTagToVocabularyHandler
  ☐ Tạo Validator (nếu cần): AddTagToVocabularyValidator
  ☐ Thêm IAuditedRequest nếu cần audit
  ☐ Thêm ICacheableQuery nếu cần cache

Bước 4: API Layer
  ☐ Tạo/update Controller: VocabulariesController hoặc TagsController
  ☐ Thêm [Authorize], [HttpPost/Get/...]
  ☐ Thêm [ProducesResponseType]

Bước 5: Tests
  ☐ Unit test: AddTagToVocabularyHandlerTests
  ☐ Integration test (nếu complex query)

Bước 6: Verify
  ☐ dotnet build → 0 errors
  ☐ dotnet test → all green
  ☐ Test qua Scalar UI
```

---

## 2. Template cho từng loại file

### 2.1 Domain Entity

```csharp
// src/LexiVocab.Domain/Entities/Tag.cs
using LexiVocab.Domain.Common;

namespace LexiVocab.Domain.Entities;

public class Tag : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    
    // Navigation
    public User User { get; set; } = null!;
    public ICollection<UserVocabulary> Vocabularies { get; set; } = [];
}
```

### 2.2 Repository Interface

```csharp
// src/LexiVocab.Domain/Interfaces/ITagRepository.cs
using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

public interface ITagRepository : IRepository<Tag>
{
    Task<IReadOnlyList<Tag>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> NameExistsForUserAsync(Guid userId, string name, CancellationToken ct = default);
}
```

### 2.3 Command + Handler

```csharp
// src/LexiVocab.Application/Features/Tags/Commands/TagCommands.cs
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Tags.Commands;

public record CreateTagCommand(string Name, string? Color)
    : IRequest<Result<TagDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.TagCreated; // Thêm enum value
    public string? EntityType => "Tag";
}

public class CreateTagHandler : IRequestHandler<CreateTagCommand, Result<TagDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateTagHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<TagDto>> Handle(CreateTagCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        // Business rules
        if (await _uow.Tags.NameExistsForUserAsync(userId, request.Name, ct))
            return Result<TagDto>.Conflict("Tag already exists.");

        var tag = new Tag
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Color = request.Color
        };

        await _uow.Tags.AddAsync(tag, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<TagDto>.Created(new TagDto(tag.Id, tag.Name, tag.Color));
    }
}
```

### 2.4 Query + Handler (with caching)

```csharp
public record GetTagsQuery : IRequest<Result<List<TagDto>>>, ICacheableQuery
{
    // ICacheableQuery implementation
    public string CacheKey => $"tags:{UserId}";
    public DistributedCacheEntryOptions? CacheOptions => new()
    {
        SlidingExpiration = TimeSpan.FromHours(1)
    };
    
    [JsonIgnore] public Guid UserId { get; init; }
}
```

### 2.5 Validator

```csharp
// src/LexiVocab.Application/Validators/CreateTagValidator.cs
using FluentValidation;
using LexiVocab.Application.Features.Tags.Commands;

namespace LexiVocab.Application.Validators;

public class CreateTagValidator : AbstractValidator<CreateTagCommand>
{
    public CreateTagValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tag name is required.")
            .MaximumLength(50).WithMessage("Tag name must be ≤ 50 characters.");
        
        RuleFor(x => x.Color)
            .Matches("^#[0-9A-Fa-f]{6}$").When(x => x.Color != null)
            .WithMessage("Color must be a valid hex code (e.g., #FF5733).");
    }
}
```

### 2.6 Controller

```csharp
// Thêm endpoints vào controller
[HttpPost("tags")]
[ProducesResponseType(typeof(TagDto), StatusCodes.Status201Created)]
public async Task<IActionResult> CreateTag([FromBody] CreateTagRequest request, CancellationToken ct)
{
    var result = await _mediator.Send(new CreateTagCommand(request.Name, request.Color), ct);
    return ToActionResult(result);
}
```

### 2.7 Unit Test

```csharp
public class CreateTagHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly CreateTagHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public CreateTagHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);
        _handler = new CreateTagHandler(_mockUow.Object, _mockCurrentUser.Object);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldCreateTag()
    {
        // Arrange
        var command = new CreateTagCommand("Grammar", "#FF5733");
        _mockUow.Setup(x => x.Tags.NameExistsForUserAsync(_userId, "Grammar", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDuplicate_ShouldReturnConflict()
    {
        var command = new CreateTagCommand("Grammar", null);
        _mockUow.Setup(x => x.Tags.NameExistsForUserAsync(_userId, "Grammar", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }
}
```

---

## 3. Conventions quan trọng

### Naming

| Thành phần | Convention | Ví dụ |
|-----------|-----------|-------|
| Entity | PascalCase, số ít | `UserVocabulary` |
| DTO | PascalCase + `Dto` | `VocabularyDto` |
| Command | Verb + Noun + `Command` | `CreateVocabularyCommand` |
| Query | `Get` + Noun + `Query` | `GetVocabularyListQuery` |
| Handler | Command/Query name + `Handler` | `CreateVocabularyHandler` |
| Repository | `I` + Entity + `Repository` | `IVocabularyRepository` |
| Controller | Entity + `Controller` | `VocabulariesController` |
| Test | Handler + `Tests` | `CreateVocabularyHandlerTests` |

### Result<T> Usage

```csharp
// ✅ Dùng Result<T> cho business failures
return Result<T>.Success(data);          // 200
return Result<T>.Created(data);          // 201
return Result<T>.Failure("error", 400);  // 400
return Result<T>.NotFound("not found");  // 404
return Result<T>.Unauthorized();         // 401
return Result<T>.Forbidden();            // 403
return Result<T>.Conflict("duplicate");  // 409

// ❌ KHÔNG throw exception cho business logic
throw new Exception("Not found"); // ← SAI
```

### Cache Invalidation Pattern

```csharp
// Sau mỗi write operation:
await _cache.SetStringAsync($"vocab-v:{userId}", Guid.NewGuid().ToString(), ct);
// → Tất cả read cache keys chứa version cũ sẽ tự hết hạn
```

### Audit Logging (opt-in)

```csharp
// Chỉ cần implement IAuditedRequest trên Command record:
public record MyCommand(...) : IRequest<Result<T>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.MyAction;
    public string? EntityType => "MyEntity";
}
// → AuditLoggingBehavior tự động log trước/sau handler
```

### Sensitive Data Protection

```csharp
// Dùng [JsonIgnore] cho fields không nên log:
public record LoginCommand(
    string Email,
    [property: JsonIgnore] string Password,  // ← sẽ không xuất hiện trong AuditLog
    ...
) : IRequest<Result<AuthResponse>>;
```

---

## 4. EF Core Migration Workflow

```bash
# 1. Sửa entity / thêm entity mới

# 2. Tạo migration
dotnet ef migrations add <DescriptiveName> \
    --project src/LexiVocab.Infrastructure \
    --startup-project src/LexiVocab.API

# 3. Review migration file (QUAN TRỌNG!)
# Kiểm tra: Up(), Down(), indexes, foreign keys

# 4. Áp dụng
dotnet ef database update \
    --project src/LexiVocab.Infrastructure \
    --startup-project src/LexiVocab.API

# 5. Nếu cần rollback
dotnet ef database update <PreviousMigrationName> \
    --project src/LexiVocab.Infrastructure \
    --startup-project src/LexiVocab.API
```

---

## 5. Common Patterns Reference

### Ownership Check (bảo vệ data isolation)

```csharp
var entity = await _uow.Vocabularies.GetByIdAsync(id, ct);
if (entity is null || entity.UserId != userId)     // ← LUÔN check UserId
    return Result<T>.NotFound("Not found.");
```

### Feature Gating Check

```csharp
var permissions = await _featureGating.GetPermissionsAsync(userId, ct);
if (!permissions.CanBatchImport)
    return Result<T>.Failure("ERR_PREMIUM_REQUIRED", 403);
```

### Batch Operations (tránh N+1)

```csharp
// ❌ N+1 queries
foreach (var word in words)
{
    var exists = await _repo.WordExistsAsync(userId, word); // 1 query mỗi iteration
}

// ✅ Batch: 1 query
var existingWords = await _repo.GetExistingWordsAsync(userId, words); // 1 WHERE IN query
foreach (var word in words)
{
    if (existingWords.Contains(word)) continue; // HashSet O(1) lookup
}
```
