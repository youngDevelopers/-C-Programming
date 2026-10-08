using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITaskService, InMemoryTaskService>();

var app = builder.Build();

// Add a rewrite rule or middleware to redirect requests from /tasks/{id} to /todos/{id}
app.UseRewriter(new RewriteOptions().AddRedirect("tasks/(.*)", "todos/$1"));
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/tasks"))
    {
        context.Response.Redirect(context.Request.Path.ToString().Replace("/tasks", "/todos"));
        return;
    }
    Console.WriteLine($"[{context.Request.Method}] {context.Request.Path} - {context.Response.StatusCode}");
    await next();
    Console.WriteLine($"[{context.Request.Method}] {context.Request.Path} - {context.Response.StatusCode}");
});

var todos = new List<Todo>();

app.MapGet("/todos", (ITaskService taskService) => taskService.GetAllTodos());

app.MapGet("/todos/{id}", Results<Ok<Todo>, NotFound> (int id, ITaskService taskService) =>
{
    var todo = taskService.GetTaskById(id);
    return todo is not null ? TypedResults.Ok(todo) : TypedResults.NotFound();
});

app.MapPost("/todos", (Todo task, ITaskService taskService) =>
{
    taskService.AddTask(task);
    return TypedResults.Created($"/todos/{task.id}", task);
}).AddEndpointFilter(async (context, next) =>
{
    var todo = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(todo.name))
    {
        errors.Add("name", new[] { "The 'name' field is required." });
    }
    if (todo.dueDate < DateTime.UtcNow)
    {
        errors.Add("dueDate", new[] { "The 'dueDate' field must be in the future." });
    }
    if (todo.isComplete)
    {
        errors.Add("isComplete", new[] { "The 'isComplete' field must be false when creating a new task." });
    }
    if (errors.Count > 0)
    {
        return TypedResults.BadRequest(errors);
    }
    return await next(context);
});

app.MapDelete("/todos/{id}", Results<NoContent, NotFound> (int id, ITaskService taskService) =>
{
    taskService.DeleteTask(id);
    return TypedResults.NoContent();
});

app.Run();

public record Todo(int id, string name, DateTime dueDate, bool isComplete);

interface ITaskService
{
    List<Todo> GetAllTodos();
    Todo? GetTaskById(int id);
    Todo AddTask(Todo task);
    void DeleteTask(int id);
}

class InMemoryTaskService : ITaskService
{
    private readonly List<Todo> _todos = [];

    public List<Todo> GetAllTodos() => _todos;

    public Todo? GetTaskById(int id) => _todos.FirstOrDefault(t => t.id == id);

    public Todo AddTask(Todo task)
    {
        _todos.Add(task);
        return task;
    }

    public void DeleteTask(int id)
    {
        var todo = GetTaskById(id);
        if (todo is not null)
        {
            _todos.Remove(todo);
        }
    }
}