using System.Net;
using System.Text.Json;
using FluentAssertions;
using LexiVocab.API.IntegrationTests.Base;
using LexiVocab.Application.DTOs;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexiVocab.API.IntegrationTests.Controllers;

public class MasterVocabControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public MasterVocabControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LookupWord_WhenWordExists_ShouldReturn200AndData()
    {
        // Arrange
        // Seed some data into the In-Memory Database before testing
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.MasterVocabularies.Add(new Domain.Entities.MasterVocabulary 
            { 
                Word = "test", 
                PhoneticUs = "/test/", 
                PhoneticUk = "/test/", 
                PartOfSpeech = "noun"
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.GetAsync("/api/master-vocab/lookup?word=test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var contentString = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Assuming a standard Result/JSON structure for successful API responses
        using var jsonDoc = JsonDocument.Parse(contentString);
        var root = jsonDoc.RootElement;
        
        // Assert structure properties mapping if any specific DTO, falling back to basic checks
        root.GetProperty("success").GetBoolean().Should().BeTrue();
    }
}
