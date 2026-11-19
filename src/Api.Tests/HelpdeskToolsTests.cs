using System.Text.Json;
using Api.Helpdesk;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

public class HelpdeskToolsTests
{
    static Dictionary<string, JsonElement> Input(object o) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(o))!;

    [Fact]
    public async Task Any_tool_failure_comes_back_as_an_error_result_not_an_exception()
    {
        var db = TestDb.Create();
        var tools = new HelpdeskTools(db, new LeaveService(db));
        var input = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""{"start_date": 5, "end_date": 6}""")!;
        var outcome = await tools.RunAsync("apply_leave", input, 6);
        Assert.True(outcome.IsError);
    }

    [Fact]
    public async Task Three_working_days_is_approved()
    {
        var db = TestDb.Create();
        var tools = new HelpdeskTools(db, new LeaveService(db));
        var o = await tools.RunAsync("apply_leave", Input(new { start_date = "2026-12-01", end_date = "2026-12-03" }), 1);
        Assert.False(o.IsError);
        Assert.Equal("approved", JsonDocument.Parse(o.Json).RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Over_three_working_days_needs_manager_and_stays_pending()
    {
        // Thu 3 Dec – Tue 8 Dec spans a weekend: 4 working days.
        var db = TestDb.Create();
        var tools = new HelpdeskTools(db, new LeaveService(db));
        var o = await tools.RunAsync("apply_leave", Input(new { start_date = "2026-12-03", end_date = "2026-12-08" }), 1);
        var json = JsonDocument.Parse(o.Json).RootElement;
        Assert.Equal("needs_manager_approval", json.GetProperty("status").GetString());
        var saved = await db.LeaveRequests.SingleAsync(r => r.Id == json.GetProperty("request_id").GetInt32());
        Assert.Equal(LeaveStatus.Pending, saved.Status);
    }
}
