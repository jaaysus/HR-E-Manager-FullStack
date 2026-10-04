using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace HrETracker.Api.Tests;

public sealed class AuthenticationTests : IAsyncLifetime
{
    private readonly AuthApiFactory factory = new();
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await factory.InitializeAsync();
        client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
    }

    public Task DisposeAsync()
    {
        client.Dispose();
        factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Anonymous_requests_get_401_and_viewers_get_403_for_admin_and_inventory_actions()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/employees")).StatusCode);
        var tokens = await LoginAsync("viewer@example.test");
        Authorize(tokens);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/inventory/receipts", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/employees", new { })).StatusCode);
    }

    [Theory]
    [InlineData("viewer@example.test", false, false)]
    [InlineData("inventory@example.test", true, false)]
    [InlineData("admin@example.test", true, true)]
    public async Task Effective_permissions_match_server_policies_and_do_not_grant_future_modules(
        string email, bool inventoryWrite, bool administration)
    {
        var tokens = await LoginAsync(email);
        Authorize(tokens);
        var response = await client.GetAsync("/api/auth/me");
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var permissions = document.RootElement.GetProperty("permissions").EnumerateArray()
            .Select(value => value.GetString()!).ToArray();
        Assert.Contains("employees.read", permissions);
        Assert.Equal(email != "viewer@example.test", permissions.Contains("inventory.read"));
        Assert.Equal(inventoryWrite, permissions.Contains("inventory.manage"));
        Assert.Equal(inventoryWrite, permissions.Contains("coats.provide"));
        Assert.Equal(administration, permissions.Contains("employees.manage"));
        Assert.Equal(administration, permissions.Contains("platform.users.manage"));
        Assert.DoesNotContain("payroll.manage", permissions);
        Assert.Equal(AccessPolicies.ForRoles(tokens.Roles), permissions);
        var unread = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/notifications/unread-count");
        Assert.Equal(0, unread.GetProperty("unreadCount").GetInt32());
        Assert.Equal(administration ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/users")).StatusCode);

        // Malformed writes still prove authorization runs before input validation.
        foreach (var endpoint in new[] { "/api/inventory/items", "/api/inventory/receipts", "/api/inventory/adjustments",
            $"/api/coat-requests/{Guid.NewGuid()}/provide" })
            Assert.Equal(inventoryWrite ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden,
                (await client.PostAsJsonAsync(endpoint, new { })).StatusCode);
        foreach (var endpoint in new[] { "/api/employees", "/api/users",
            $"/api/coat-requests/{Guid.NewGuid()}/cancel", $"/api/coat-requests/{Guid.NewGuid()}/assign-item" })
            Assert.Equal(administration ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden,
                (await client.PostAsJsonAsync(endpoint, new { })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "FutureModuleManager") }, "test"));
        foreach (var policy in AccessPolicies.Permissions.Keys)
            Assert.False((await authorization.AuthorizeAsync(principal, null, policy)).Succeeded);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_logout_revokes_both_tokens()
    {
        var original = await LoginAsync("admin@example.test");
        using (var scope = factory.Services.CreateScope())
        {
            var session = await scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>().AuthSessions.SingleAsync();
            Assert.NotEqual(original.RefreshToken, session.RefreshTokenHash);
            Assert.Equal(64, session.RefreshTokenHash.Length);
        }
        var refresh = await RefreshAsync(original.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = await ReadSessionAsync(refresh);
        Assert.NotEqual(original.RefreshToken, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(original.RefreshToken)).StatusCode);
        Authorize(rotated);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(rotated.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_user_and_role_changes_and_disabling_revoke_sessions()
    {
        Authorize(await LoginAsync("admin@example.test"));
        var create = await client.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Email = "new@example.test", FullName = "New User", Password = AuthApiFactory.Password, Roles = ["Viewer"]
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var user = (await create.Content.ReadFromJsonAsync<UserResponse>())!;
        var tokens = await LoginAsync(user.Email);
        var update = await client.PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserRequest { FullName = user.FullName, IsActive = true, Roles = ["InventoryManager"] });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Authorize(tokens);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var fresh = await LoginAsync(user.Email);
        Assert.Contains("InventoryManager", fresh.Roles);
        Authorize(await LoginAsync("admin@example.test"));
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserRequest { FullName = user.FullName, IsActive = false, Roles = ["InventoryManager"] })).StatusCode);
        Authorize(fresh);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = user.Email, Password = AuthApiFactory.Password })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(fresh.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Last_admin_is_protected_and_invalid_roles_are_rejected()
    {
        Authorize(await LoginAsync("admin@example.test"));
        var admin = (await client.GetFromJsonAsync<UserResponse>("/api/auth/me"))!;
        // Leave only this administrator active before asserting last-admin protection.
        var users = (await client.GetFromJsonAsync<UserResponse[]>("/api/users"))!;
        var otherAdmin = users.Single(u => u.Email == "hradmin@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/users/{otherAdmin.Id}", new UpdateUserRequest { FullName = otherAdmin.FullName, IsActive = false, Roles = ["HrAdministrator"] })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/users/{admin.Id}", new UpdateUserRequest { FullName = admin.FullName, IsActive = false, Roles = ["Viewer"] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = "bad@example.test", FullName = "Bad Role", Password = AuthApiFactory.Password, Roles = ["SuperAdmin"] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/users?page=0")).StatusCode);
    }

    [Fact]
    public async Task Password_change_revokes_old_sessions_and_changes_login_password()
    {
        var tokens = await LoginAsync("viewer@example.test");
        Authorize(tokens);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest { CurrentPassword = AuthApiFactory.Password, NewPassword = "Changed-password42!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tokens.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = tokens.Email, Password = AuthApiFactory.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = tokens.Email, Password = "Changed-password42!" })).StatusCode);
    }

    [Fact]
    public async Task Five_failed_password_attempts_lock_the_account()
    {
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "viewer@example.test", Password = "Wrong-password42!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "viewer@example.test", Password = AuthApiFactory.Password })).StatusCode);
    }

    [Fact]
    public async Task Expired_sessions_cannot_be_used_or_refreshed()
    {
        var tokens = await LoginAsync("viewer@example.test");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>().AuthSessions
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        Authorize(tokens);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tokens.RefreshToken)).StatusCode);
    }

    private async Task<AuthenticatedUserResponse> LoginAsync(string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = AuthApiFactory.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadSessionAsync(response);
    }

    private static async Task<AuthenticatedUserResponse> ReadSessionAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("refreshToken", out _));
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("hr_et_refresh="));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("secure", cookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        Assert.Contains("path=/api/auth", cookie.ToLowerInvariant());
        Assert.Contains("expires=", cookie.ToLowerInvariant());
        var session = System.Text.Json.JsonSerializer.Deserialize<AuthenticatedUserResponse>(json,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        return session with { RefreshToken = cookie.Split(';')[0]["hr_et_refresh=".Length..] };
    }

    private Task<HttpResponseMessage> RefreshAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", "hr_et_refresh=" + token);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Cookie_restores_session_without_bearer_and_logout_prevents_restoration()
    {
        var original = await LoginAsync("viewer@example.test");
        var restored = await RefreshAsync(original.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        var rotated = await ReadSessionAsync(restored);
        Assert.Equal(original.Email, rotated.Email);
        Assert.Equal(original.RefreshTokenExpiresAtUtc, rotated.RefreshTokenExpiresAtUtc);
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", "hr_et_refresh=" + rotated.RefreshToken);
        var result = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Contains("hr_et_refresh=;", result.Headers.GetValues("Set-Cookie").Single());
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(rotated.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Cookie_auth_requires_custom_header_and_missing_cookie_returns_401()
    {
        var session = await LoginAsync("viewer@example.test");
        client.DefaultRequestHeaders.Remove("X-HR-Auth");
        Assert.Equal(HttpStatusCode.Forbidden, (await RefreshAsync(session.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = session.Email, Password = AuthApiFactory.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        client.DefaultRequestHeaders.Add("X-HR-Auth", "1");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(session.RefreshToken)).StatusCode);
    }

    private void Authorize(AuthenticatedUserResponse tokens) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

    [Theory]
    [InlineData("http://localhost:8443", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task Cookie_auth_preflight_only_allows_configured_origins(string origin, bool allowed)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/refresh");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "X-HR-Auth");
        var result = await client.SendAsync(request);
        Assert.Equal(allowed, result.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed)
        {
            Assert.Equal(origin, result.Headers.GetValues("Access-Control-Allow-Origin").Single());
            Assert.Equal("true", result.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        }
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expiry")]
    [InlineData("signature")]
    public async Task Invalid_JWT_envelopes_are_rejected(string invalidField)
    {
        var tokens = await LoginAsync("viewer@example.test");
        var original = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        var key = invalidField == "signature" ? new string('x', 64) : AuthApiFactory.SigningKey;
        var token = new JwtSecurityToken(
            invalidField == "issuer" ? "wrong-issuer" : original.Issuer,
            invalidField == "audience" ? "wrong-audience" : original.Audiences.Single(),
            original.Claims, DateTime.UtcNow.AddHours(-1),
            invalidField == "expiry" ? DateTime.UtcNow.AddMinutes(-2) : DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Authentication_endpoints_are_rate_limited()
    {
        for (var attempt = 0; attempt < 20; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "unknown@example.test", Password = AuthApiFactory.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "unknown@example.test", Password = AuthApiFactory.Password })).StatusCode);
    }

    [Fact]
    public async Task Inventory_managers_can_read_and_reach_stock_validation_but_cannot_manage_users()
    {
        Authorize(await LoginAsync("inventory@example.test"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/inventory/items")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/inventory/receipts", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Theory]
    [InlineData("hradmin@example.test", false, false, true)]
    [InlineData("viewer@example.test", false, false, false)]
    [InlineData("clothing@example.test", true, true, false)]
    [InlineData("clothingviewer@example.test", true, false, false)]
    public async Task Clothing_module_requires_an_assigned_role_independently_of_HR_administration(string email, bool read, bool write, bool users)
    {
        Authorize(await LoginAsync(email));
        Assert.Equal(read ? HttpStatusCode.OK : HttpStatusCode.Forbidden, (await client.PostAsync("/api/program/snapshot", null)).StatusCode);
        Assert.Equal(read ? HttpStatusCode.OK : HttpStatusCode.Forbidden, (await client.GetAsync("/api/inventory/items")).StatusCode);
        Assert.Equal(write ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/inventory/receipts", new { })).StatusCode);
        Assert.Equal(users ? HttpStatusCode.OK : HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/employees")).StatusCode);
    }

    [Fact]
    public async Task Clothing_notifications_are_hidden_from_HR_only_roles()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrETracker.Data.HrETrackerDbContext>();
            db.Notifications.Add(new Notification { Type = "RequestDue", Message = "Clothing request", RelatedEntityType = "CoatRequest", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        Authorize(await LoginAsync("hradmin@example.test"));
        var notifications = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/notifications");
        Assert.Equal(0, notifications.GetProperty("totalCount").GetInt32());
        Authorize(await LoginAsync("clothingviewer@example.test"));
        notifications = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/notifications");
        Assert.Equal(1, notifications.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Clothing_role_assignment_and_removal_change_access_and_revoke_existing_sessions()
    {
        var admin = await LoginAsync("admin@example.test");
        Authorize(admin);
        var created = await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = "assigned@example.test", FullName = "Assigned User", Password = AuthApiFactory.Password, Roles = ["Viewer", "ClothingViewer"] });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = (await created.Content.ReadFromJsonAsync<UserResponse>())!;
        var session = await LoginAsync(user.Email);
        Authorize(session);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/program/snapshot", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/inventory/receipts", new { })).StatusCode);
        Authorize(admin);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserRequest { FullName = user.FullName, IsActive = true, Roles = ["Viewer"] })).StatusCode);
        Authorize(session);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Authorize(await LoginAsync(user.Email));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/program/snapshot", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/employees")).StatusCode);
    }

    [Fact]
    public void OpenAPI_documents_bearer_authentication_and_public_login()
    {
        using var scope = factory.Services.CreateScope();
        var document = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        Assert.True(document.Components!.SecuritySchemes!.ContainsKey("bearer"));
        Assert.NotEmpty(document.Security!);
        Assert.True(document.Paths.ContainsKey("/api/users"));
        Assert.DoesNotContain(document.Paths.Keys, path => path.StartsWith("/api/v1/"));
        Assert.All(document.Paths["/api/auth/login"].Operations!.Values, operation => Assert.Empty(operation.Security!));
    }
}

