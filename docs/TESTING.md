# 🧪 Testing Strategy

> Cấu trúc test projects, cách viết test mới, chạy test.

---

## 1. Test Projects Overview

```
tests/
├── LexiVocab.Domain.UnitTests/          # 1 test  — Pure domain logic
├── LexiVocab.Application.UnitTests/     # 19 tests — Handlers + Services (Moq)
├── LexiVocab.Infrastructure.IntegrationTests/ # 2 tests — Real DB (In-Memory)
└── LexiVocab.API.IntegrationTests/      # 2 tests — Full HTTP pipeline (WebApplicationFactory)
```

### Test Framework Stack

| Thành phần | Công nghệ |
|-----------|-----------|
| Test Runner | xUnit |
| Mocking | Moq |
| Assertions | FluentAssertions |
| Integration DB | EF Core In-Memory Provider |
| API Test Host | WebApplicationFactory<Program> |

---

## 2. Unit Tests (Application Layer)

### Cấu trúc test file

```csharp
public class CreateVocabularyHandlerTests
{
    // ─── Mocks ──────────────────────────────────────
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IFeatureGatingService> _mockFeatureGating;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly CreateVocabularyHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public CreateVocabularyHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockFeatureGating = new Mock<IFeatureGatingService>();
        _mockCache = new Mock<IDistributedCache>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);

        _handler = new CreateVocabularyHandler(
            _mockUow.Object,
            _mockCurrentUser.Object,
            _mockFeatureGating.Object,
            _mockCache.Object);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldCreateVocabulary()
    {
        // Arrange
        var command = new CreateVocabularyCommand("apple", "quả táo", null, null);
        
        _mockFeatureGating.Setup(x => x.CanCreateVocabularyAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockUow.Setup(x => x.Vocabularies.WordExistsForUserAsync(_userId, "apple", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        
        _mockUow.Verify(x => x.Vocabularies.AddAsync(
            It.Is<UserVocabulary>(v => v.WordText == "apple"), 
            It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### Existing Unit Tests

| File | Tests | Coverage |
|------|-------|----------|
| `CreateVocabularyHandlerTests` | 4 | Create success, quota exceeded, duplicate, master vocab lookup |
| `UpdateVocabularyHandlerTests` | 2 | Update success, not found |
| `ArchiveVocabularyHandlerTests` | 2 | Toggle archive, not found |
| `DeleteVocabularyHandlerTests` | 2 | Delete success, not found |
| `BatchImportHandlerTests` | 2 | Import success (with dedup), premium required |
| `ReviewCommandsTests` | 3 | Submit success, SM-2 reset (q<3), not found |
| `SrsAlgorithmServiceTests` | 4 | Perfect recall, failed recall, EF floor, interval progression |

### Naming Convention

```
Method_Scenario_ExpectedResult

Ví dụ:
Handle_WhenQuotaExceeded_ShouldReturnForbidden
Handle_WhenValid_ShouldCreateVocabulary  
Calculate_WhenQualityBelow3_ShouldResetRepetition
```

---

## 3. Integration Tests

### Infrastructure Integration (Real DB)

```csharp
// Base class tạo In-Memory DbContext
public abstract class BaseIntegrationTest : IDisposable
{
    protected readonly AppDbContext Context;

    protected BaseIntegrationTest()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Context = new AppDbContext(options);
    }

    public void Dispose() => Context.Dispose();
}

// Test repository queries with real EF Core
public class VocabularyRepositoryTests : BaseIntegrationTest
{
    [Fact]
    public async Task GetDueForReviewAsync_ShouldReturnOnlyDueCards()
    {
        // Seed data
        var userId = Guid.NewGuid();
        Context.UserVocabularies.Add(new UserVocabulary 
        { 
            UserId = userId, 
            WordText = "due",
            NextReviewDate = DateTime.UtcNow.AddHours(-1) // Due
        });
        Context.UserVocabularies.Add(new UserVocabulary 
        { 
            UserId = userId, 
            WordText = "not-due",
            NextReviewDate = DateTime.UtcNow.AddDays(5) // Not yet
        });
        await Context.SaveChangesAsync();

        var repo = new VocabularyRepository(Context);
        var result = await repo.GetDueForReviewAsync(userId);

        result.Should().HaveCount(1);
        result[0].WordText.Should().Be("due");
    }
}
```

### API Integration (Full Pipeline)

```csharp
// CustomWebApplicationFactory — replaces real DB/services with test doubles
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace PostgreSQL with In-Memory
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opt => 
                opt.UseInMemoryDatabase("TestDb"));
            
            // Replace Redis with In-Memory
            services.AddDistributedMemoryCache();
        });
    }
}

// Test full HTTP endpoint
public class MasterVocabControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MasterVocabControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Search_ShouldReturnResults()
    {
        var response = await _client.GetAsync("/api/v1/master-vocab/search?q=test");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

---

## 4. Chạy Tests

```bash
# Chạy tất cả tests
dotnet test

# Chạy 1 project
dotnet test tests/LexiVocab.Application.UnitTests/

# Chạy 1 test class
dotnet test --filter "FullyQualifiedName~CreateVocabularyHandlerTests"

# Chạy 1 test method
dotnet test --filter "Handle_WhenValid_ShouldCreateVocabulary"

# Chạy với verbose output
dotnet test --verbosity detailed

# Chạy với coverage report
dotnet test --collect:"XPlat Code Coverage"
```

---

## 5. Viết Test Mới — Checklist

### Khi thêm Command handler mới:

```
1. Tạo file: tests/LexiVocab.Application.UnitTests/Features/{Module}/Commands/{Handler}Tests.cs

2. Setup:
   - Mock IUnitOfWork (DefaultValue.Mock)
   - Mock ICurrentUserService (return userId)
   - Mock các service dependencies
   - Instantiate handler

3. Test cases tối thiểu:
   ☐ Happy path (success case)
   ☐ Not found (invalid ID)
   ☐ Unauthorized (wrong user)
   ☐ Validation failures
   ☐ Business rule violations (quota, duplicate, etc.)

4. Verify:
   ☐ Correct return type (Result<T>.Success/Failure)
   ☐ Correct status code
   ☐ Repository methods called with correct args
   ☐ SaveChangesAsync called
   ☐ Cache invalidated (if applicable)
```

### Khi thêm Query handler mới:

```
1. Tương tự Command nhưng:
   ☐ Test cache hit (return cached data)
   ☐ Test cache miss (query DB, cache result)
   ☐ Test empty result
```

### Khi thêm Repository method mới:

```
1. Tạo file: tests/LexiVocab.Infrastructure.IntegrationTests/Repositories/{Repo}Tests.cs

2. Extend BaseIntegrationTest

3. Seed test data → verify query results
```
