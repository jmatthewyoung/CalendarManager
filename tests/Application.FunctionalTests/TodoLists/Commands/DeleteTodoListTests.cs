using CalendarManager.Application.TodoLists.Commands.CreateTodoList;
using CalendarManager.Application.TodoLists.Commands.DeleteTodoList;
using CalendarManager.Domain.Entities;

namespace CalendarManager.Application.FunctionalTests.TodoLists.Commands;

public class DeleteTodoListTests : TestBase
{
    [Test]
    public async Task ShouldRequireValidTodoListId()
    {
        var command = new DeleteTodoListCommand(99);
        await Should.ThrowAsync<NotFoundException>(() => TestApp.SendAsync(command));
    }

    [Test]
    public async Task ShouldDeleteTodoList()
    {
        var listId = await TestApp.SendAsync(new CreateTodoListCommand
        {
            Title = "New List"
        });

        await TestApp.SendAsync(new DeleteTodoListCommand(listId));

        var list = await TestApp.FindAsync<TodoList>(listId);

        list.ShouldBeNull();
    }
}
