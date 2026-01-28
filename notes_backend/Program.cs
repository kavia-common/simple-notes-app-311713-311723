using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "Simple Notes API";
    config.Version = "1.0.0";
    config.Description = "REST API for a simple notes app (no auth). Provides CRUD endpoints for notes.";
});

// Add CORS (frontend runs on http://localhost:3000)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
            .AllowCredentials()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Use CORS
app.UseCors("AllowAll");

// Configure OpenAPI/Swagger
app.UseOpenApi();
app.UseSwaggerUi(config =>
{
    config.Path = "/docs";
});

app.MapGet("/", () => new { message = "Healthy" })
    .WithName("Health")
    .WithTags("Health")
    .WithSummary("Health check")
    .WithDescription("Returns a simple health response.");

/*
    Notes API - in-memory storage
    - No auth
    - Notes fields: { id, title, content, createdAt, updatedAt }
*/

var store = new InMemoryNotesStore();

// PUBLIC_INTERFACE
app.MapGet("/notes", () =>
    {
        var notes = store.List()
            .OrderByDescending(n => n.UpdatedAt)
            .ToList();
        return Results.Ok(notes);
    })
    .WithName("ListNotes")
    .WithTags("Notes")
    .WithSummary("List notes")
    .WithDescription("Returns all notes sorted by updatedAt desc.")
    .Produces<List<Note>>(StatusCodes.Status200OK);

// PUBLIC_INTERFACE
app.MapGet("/notes/{id:guid}", (Guid id) =>
    {
        var note = store.Get(id);
        return note is null ? Results.NotFound() : Results.Ok(note);
    })
    .WithName("GetNote")
    .WithTags("Notes")
    .WithSummary("Get note")
    .WithDescription("Returns a single note by id.")
    .Produces<Note>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

// PUBLIC_INTERFACE
app.MapPost("/notes", (CreateNoteRequest request) =>
    {
        var now = DateTimeOffset.UtcNow;
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        store.Create(note);
        return Results.Created($"/notes/{note.Id}", note);
    })
    .WithName("CreateNote")
    .WithTags("Notes")
    .WithSummary("Create note")
    .WithDescription("Creates a new note with title and content.")
    .Accepts<CreateNoteRequest>("application/json")
    .Produces<Note>(StatusCodes.Status201Created)
    .ProducesValidationProblem();

// PUBLIC_INTERFACE
app.MapPut("/notes/{id:guid}", (Guid id, UpdateNoteRequest request) =>
    {
        var existing = store.Get(id);
        if (existing is null) return Results.NotFound();

        var updated = existing with
        {
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        store.Update(updated);
        return Results.Ok(updated);
    })
    .WithName("UpdateNote")
    .WithTags("Notes")
    .WithSummary("Update note")
    .WithDescription("Updates an existing note by id.")
    .Accepts<UpdateNoteRequest>("application/json")
    .Produces<Note>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .ProducesValidationProblem();

// PUBLIC_INTERFACE
app.MapDelete("/notes/{id:guid}", (Guid id) =>
    {
        var deleted = store.Delete(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteNote")
    .WithTags("Notes")
    .WithSummary("Delete note")
    .WithDescription("Deletes a note by id.")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

app.Run();

public sealed record Note
{
    public required Guid Id { get; init; }

    [Required]
    [MinLength(1)]
    public required string Title { get; init; }

    [Required]
    public required string Content { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed record CreateNoteRequest
{
    [Required]
    [MinLength(1)]
    public required string Title { get; init; }

    public string Content { get; init; } = string.Empty;
}

public sealed record UpdateNoteRequest
{
    [Required]
    [MinLength(1)]
    public required string Title { get; init; }

    public string Content { get; init; } = string.Empty;
}

internal sealed class InMemoryNotesStore
{
    private readonly ConcurrentDictionary<Guid, Note> _notes = new();

    public IReadOnlyCollection<Note> List() => _notes.Values.ToList();

    public Note? Get(Guid id)
    {
        _notes.TryGetValue(id, out var note);
        return note;
    }

    public void Create(Note note)
    {
        _notes[note.Id] = note;
    }

    public void Update(Note note)
    {
        _notes[note.Id] = note;
    }

    public bool Delete(Guid id)
    {
        return _notes.TryRemove(id, out _);
    }
}
