# Security and Authentication

This document explains how authentication works with PushStream and why the
library does not include built-in authentication mechanisms.

---

## Design Philosophy

### Why No Built-in Auth?

PushStream deliberately does **not** include authentication abstractions.

**Reasons:**

1. **Authentication is application-specific**
   - JWT, cookies, API keys, OAuth — every app has different needs
   - A one-size-fits-all solution would be either too restrictive or too complex

2. **SSE uses standard HTTP**
   - Unlike WebSockets, SSE connections are regular HTTP requests
   - Your existing authentication middleware works naturally

3. **Separation of concerns**
   - Authentication belongs at the application/framework layer
   - PushStream focuses on what it does best: event streaming

4. **Flexibility**
   - You choose how to authenticate
   - No lock-in to a specific auth pattern

---

## Authentication Patterns

### Pattern 1: Cookie-Based Authentication

If your application uses cookie-based sessions:

```csharp
// The browser automatically sends cookies with SSE requests
app.MapEventStream("/events")
   .RequireAuthorization(); // Standard ASP.NET Core auth

// Access user info in the client identifier
app.MapEventStream("/events", context =>
{
    return context.User.Identity?.Name ?? "anonymous";
});
```

**Client-side:**
```javascript
// Cookies are sent automatically
const client = new EventClient('/events');
client.connect();
```

---

### Pattern 2: JWT Bearer Token

For API-based authentication with JWT:

```csharp
// Configure JWT auth as normal
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* your config */ });

app.UseAuthentication();
app.UseAuthorization();

app.MapEventStream("/events")
   .RequireAuthorization();
```

**Client-side (pass token via query string):**
```javascript
// EventSource doesn't support custom headers
// Pass token via query string
const token = getAccessToken();
const client = new EventClient(`/events?access_token=${token}`);
client.connect();
```

**Server-side (read token from query):**
```csharp
// Custom JWT validation from query string
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Read token from query string for SSE endpoints
                if (context.Request.Path.StartsWithSegments("/events"))
                {
                    context.Token = context.Request.Query["access_token"];
                }
                return Task.CompletedTask;
            }
        };
    });
```

---

### Pattern 3: API Key Authentication

For service-to-service or simple API key auth:

```csharp
app.MapEventStream("/events", context =>
{
    var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault()
                 ?? context.Request.Query["api_key"];
    
    if (!ValidateApiKey(apiKey))
    {
        context.Response.StatusCode = 401;
        return null; // Reject connection
    }
    
    return GetClientIdFromApiKey(apiKey);
});
```

---

## Client Identification

PushStream needs to identify clients to support targeted messaging.

### Default: Connection ID

By default, each connection gets a unique identifier:

```csharp
app.MapEventStream("/events"); // Uses Connection.Id internally
```

### Custom: User-Based Identification

For user-targeted events:

```csharp
app.MapEventStream("/events", context =>
{
    // Use authenticated user ID
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
    if (userId == null)
    {
        context.Response.StatusCode = 401;
        return null;
    }
    
    return userId;
});
```

### Custom: Session-Based Identification

For session-targeted events:

```csharp
app.MapEventStream("/events", context =>
{
    // Use session ID from query
    var sessionId = context.Request.Query["session"];
    
    if (string.IsNullOrEmpty(sessionId))
    {
        context.Response.StatusCode = 400;
        return null;
    }
    
    return sessionId;
});
```

---

## Security Best Practices

### 1. Always Use HTTPS

SSE connections should always be over HTTPS in production:
- Prevents token interception
- Protects event payload data
- Required for some browsers in secure contexts

### 2. Validate Tokens on Connection

Validate authentication at connection time, not per-event:

```csharp
app.MapEventStream("/events", async context =>
{
    // Validate once at connection
    if (!await ValidateUserAsync(context))
    {
        context.Response.StatusCode = 403;
        return null;
    }
    return context.User.GetUserId();
});
```

### 3. Avoid Sensitive Data in Events

Don't put sensitive information directly in event payloads:

```csharp
// ❌ Bad: Exposing sensitive data
await _publisher.PublishAsync("user.updated", new 
{ 
    userId,
    ssn = user.SSN,           // Don't do this
    creditCard = user.Card    // Don't do this
});

// ✅ Good: Reference only, fetch details securely
await _publisher.PublishAsync("user.updated", new 
{ 
    userId,
    updatedFields = new[] { "profile", "preferences" }
});
```

### 4. Rate Limit Connections

Protect against connection floods:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("sse", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.GetUserId() ?? context.Connection.RemoteIpAddress?.ToString(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));
});

app.MapEventStream("/events").RequireRateLimiting("sse");
```

### 5. Implement Connection Timeouts

Don't keep connections open indefinitely:

```csharp
builder.Services.AddPushStream(options =>
{
    options.ConnectionTimeout = TimeSpan.FromHours(1);
});
```

---

## CORS Configuration

SSE respects CORS policies. Configure appropriately:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("SSE", policy =>
    {
        policy.WithOrigins("https://yourdomain.com")
              .AllowCredentials()  // Required for cookies
              .WithHeaders("Authorization");
    });
});

app.UseCors("SSE");
```

---

## Summary

| Concern | Recommendation |
|---------|----------------|
| **Authentication** | Use your existing framework auth |
| **Token Passing** | Query string for JWT (EventSource limitation) |
| **Client ID** | Extract from authenticated user context |
| **Transport** | Always HTTPS |
| **Sensitive Data** | Keep out of event payloads |
| **Rate Limiting** | Implement at framework level |

PushStream stays out of your auth decisions — you know your security
requirements better than any library ever could.
