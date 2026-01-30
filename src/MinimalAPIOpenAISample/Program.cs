var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddSingleton<TodoStore>();

var app = builder.Build();

app.MapOpenApi();

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok("Healthy!"))
   .WithName("Health");

// Todo CRUD endpoints
var todosGroup = app.MapGroup("/todos");

todosGroup.MapGet("/", (TodoStore store) => store.GetAll())
    .WithName("GetAllTodos");

todosGroup.MapGet("/{id:int}", (int id, TodoStore store) =>
    store.GetById(id) is Todo todo
        ? Results.Ok(todo)
        : Results.NotFound())
    .WithName("GetTodoById");

todosGroup.MapPost("/", (CreateTodoRequest request, TodoStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest("Title is required and cannot be empty.");
    }

    var todo = store.Create(request.Title, request.IsComplete);
    return Results.Created($"/todos/{todo.Id}", todo);
})
    .WithName("CreateTodo");

todosGroup.MapPut("/{id:int}", (int id, UpdateTodoRequest request, TodoStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest("Title is required and cannot be empty.");
    }

    var updated = store.Update(id, request.Title, request.IsComplete);
    return updated is not null ? Results.Ok(updated) : Results.NotFound();
})
    .WithName("UpdateTodo");

todosGroup.MapDelete("/{id:int}", (int id, TodoStore store) =>
    store.Delete(id) ? Results.NoContent() : Results.NotFound())
    .WithName("DeleteTodo");

app.Run();

// Models
public record Todo(int Id, string Title, bool IsComplete);

public record CreateTodoRequest(string Title, bool IsComplete = false);

public record UpdateTodoRequest(string Title, bool IsComplete);

// In-memory store
public class TodoStore
{
    private readonly Dictionary<int, Todo> _todos = [];
    private int _nextId = 1;
    private readonly Lock _lock = new();

    public IEnumerable<Todo> GetAll()
    {
        lock (_lock)
        {
            return [.. _todos.Values];
        }
    }

    public Todo? GetById(int id)
    {
        lock (_lock)
        {
            return _todos.GetValueOrDefault(id);
        }
    }

    public Todo Create(string title, bool isComplete)
    {
        lock (_lock)
        {
            var todo = new Todo(_nextId++, title, isComplete);
            _todos[todo.Id] = todo;
            return todo;
        }
    }

    public Todo? Update(int id, string title, bool isComplete)
    {
        lock (_lock)
        {
            if (!_todos.ContainsKey(id))
            {
                return null;
            }

            var todo = new Todo(id, title, isComplete);
            _todos[id] = todo;
            return todo;
        }
    }

    public bool Delete(int id)
    {
        lock (_lock)
        {
            return _todos.Remove(id);
        }
    }
}