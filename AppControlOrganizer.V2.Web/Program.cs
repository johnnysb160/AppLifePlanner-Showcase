//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

using AppControlOrganizer.V2.Web.Components;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Data.Auth;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Endpoints;
using AppControlOrganizer.V2.Web.Security;
using AppControlOrganizer.V2.Web.Services;
using AppControlOrganizer.V2.Web.Services.Analytics;
using AppControlOrganizer.V2.Web.Services.Demo;
using AppControlOrganizer.V2.Web.Services.Email;
using AppControlOrganizer.V2.Web.Services.Localization;
using AppControlOrganizer.V2.Web.Services.Logging;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
const string AllowPublicRegistrationKey = "AllowPublicRegistration";
const string CorrelationIdHeaderName = "X-Correlation-Id";

builder.Configuration.ConfigureAppSettings(builder.Environment.EnvironmentName, builder.Environment.ContentRootPath);

InMemoryLogSink inMemoryLogSink = new InMemoryLogSink();

LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
  .MinimumLevel.Information()
  .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
  .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
  .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
  .Enrich.FromLogContext()
  .Enrich.WithProperty("Application", "AppLifePlanner")
  .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
  .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
  .WriteTo.Sink(inMemoryLogSink);

if (builder.Environment.IsDevelopment())
{
  loggerConfiguration.WriteTo.File(
    Path.Combine(builder.Environment.ContentRootPath, "logs", "app-.log"),
    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
    rollingInterval: RollingInterval.Day,
    fileSizeLimitBytes: 10 * 1024 * 1024,
    rollOnFileSizeLimit: true,
    retainedFileCountLimit: 14,
    shared: true);
}

Log.Logger = loggerConfiguration.CreateLogger();
builder.Host.UseSerilog();
builder.Services.AddSingleton<IInMemoryLogBuffer>(inMemoryLogSink);

builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureIOC(builder.Configuration);
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
  CultureInfo[] supportedCultures =
  [
    CultureInfo.GetCultureInfo("en-US"),
    CultureInfo.GetCultureInfo("pt-BR")
  ];

  options.DefaultRequestCulture = new RequestCulture("en-US");
  options.SupportedCultures = supportedCultures;
  options.SupportedUICultures = supportedCultures;
  options.RequestCultureProviders.Insert(0, new UserLanguageRequestCultureProvider());
});

// ASP.NET Core Identity
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
	options.Password.RequiredLength = 8;
	options.Password.RequireDigit = true;
	options.Password.RequireUppercase = true;
	options.User.RequireUniqueEmail = true;
	// Account lockout after 5 failed attempts; automatically unlocks after 15 minutes.
	options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
	options.Lockout.MaxFailedAccessAttempts = 5;
	options.Lockout.AllowedForNewUsers = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<LifePlannerContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

// Persist Data Protection keys in the database so the key ring (and therefore Identity
// cookies and antiforgery tokens) survives process restarts and redeploys instead of being
// regenerated in memory, which invalidates every outstanding cookie on each deploy.
builder.Services.AddDataProtection()
.PersistKeysToDbContext<LifePlannerContext>()
.SetApplicationName("LifePlanner");

// Authentication: Identity Cookies + JWT Bearer
string jwtIssuer = builder.Configuration["Jwt:Issuer"]
?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "LifePlanner";
string jwtAudience = builder.Configuration["Jwt:Audience"]
?? Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "LifePlanner";
string jwtKey = builder.Configuration["Jwt:Key"]
?? Environment.GetEnvironmentVariable("JWT_KEY")
?? throw new InvalidOperationException(
"JWT signing key is not configured. " +
"Set 'Jwt:Key' in configuration, 'JWT_KEY' environment variable, " +
"or via dotnet user-secrets (development only).");

AuthenticationBuilder authBuilder = builder.Services.AddAuthentication(options =>
{
	options.DefaultScheme = IdentityConstants.ApplicationScheme;
});
authBuilder.AddIdentityCookies();

// Override the default Identity login path (/Account/Login) to match the app's auth routes.
builder.Services.ConfigureApplicationCookie(options =>
{
  options.LoginPath = "/auth/login";
  options.AccessDeniedPath = "/auth/login";
  options.ExpireTimeSpan = TimeSpan.FromDays(15);
  options.SlidingExpiration = true;
  options.Cookie.SameSite = SameSiteMode.Lax;
  options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
  options.Cookie.HttpOnly = true;
});

authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
	options.TokenValidationParameters = new TokenValidationParameters
	{
		ValidateIssuer = true,
		ValidIssuer = jwtIssuer,
		ValidateAudience = true,
		ValidAudience = jwtAudience,
		ValidateIssuerSigningKey = true,
		IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
		ValidateLifetime = true
	};
});

// Authorization with AdminOnly policy and dual-scheme support (Cookies + JWT)
builder.Services.AddAuthorization(options =>
{
	options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));

	// Set default policy to accept both JWT Bearer and Identity Cookies.
	AuthorizationPolicy defaultPolicy = new AuthorizationPolicyBuilder(
		JwtBearerDefaults.AuthenticationScheme,
		IdentityConstants.ApplicationScheme)
		.RequireAuthenticatedUser()
		.Build();

	options.DefaultPolicy = defaultPolicy;
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
  options.AddPolicy(
    RateLimitPolicies.Login,
    httpContext => RateLimitPartition.GetFixedWindowLimiter(
      GetClientRateLimitKey(httpContext),
      _ => new FixedWindowRateLimiterOptions
      {
        PermitLimit = 10,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
      }));

  options.AddPolicy(
    RateLimitPolicies.Api,
    httpContext => RateLimitPartition.GetFixedWindowLimiter(
      GetClientRateLimitKey(httpContext),
      _ => new FixedWindowRateLimiterOptions
      {
        PermitLimit = 200,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
      }));

// Cap on total authentication posts regardless of client address: per-IP buckets can be sidestepped by
	// rotating or spoofing addresses, so this also bounds a distributed guessing run.
	options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
	{
		bool isAuthPost = HttpMethods.IsPost(httpContext.Request.Method)
			&& (httpContext.Request.Path.StartsWithSegments("/auth") || httpContext.Request.Path.StartsWithSegments("/api/auth"));
		return isAuthPost
			? RateLimitPartition.GetFixedWindowLimiter(
				"auth-global",
				_ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
			: RateLimitPartition.GetNoLimiter("unrestricted");
	});

	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.OnRejected = async (context, cancellationToken) =>
	{
    string key = GetClientRateLimitKey(context.HttpContext);
    context.HttpContext.RequestServices
      .GetRequiredService<ILoggerFactory>()
      .CreateLogger("RateLimiting")
      .LogWarning(
        "Rate limit rejected {Method} {Path} for client hash {ClientHash}.",
        context.HttpContext.Request.Method,
        context.HttpContext.Request.Path,
        HashForLog(key));

		context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
		await context.HttpContext.Response.WriteAsJsonAsync(
		new { error = "Too many requests. Please try again shortly." },
		cancellationToken);
	};
});

// Antiforgery cookie for the authentication form posts: HTTPS only, never readable by scripts.
builder.Services.AddAntiforgery(options =>
{
  options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
  options.Cookie.HttpOnly = true;
  options.Cookie.SameSite = SameSiteMode.Strict;
});

// Blazor authentication state
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// SMTP email sender (config via appsettings "Smtp" section or env Smtp__Host, Smtp__Port, etc.)
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, SmtpEmailSender>();

// Add Blazor with SignalR configuration for large file uploads
builder.Services
.AddRazorComponents()
.AddInteractiveServerComponents()
.AddHubOptions(options =>
{
	// The circuit is reachable before authentication, so this cap is a denial-of-service control:
	// it bounds how much memory one anonymous connection can force the server to buffer per message.
	// File uploads are unaffected -- InputFile streams over the circuit in 32KB chunks, so imports
	// never need a message anywhere near this size.
	options.MaximumReceiveMessageSize = 2 * 1024 * 1024; // 2MB
	options.ClientTimeoutInterval = TimeSpan.FromSeconds(120);
	options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// HttpClient for logo fetching with short timeout
builder.Services
.AddHttpClient("LogoFetch", client =>
{
	client.Timeout = TimeSpan.FromSeconds(6);
	client.DefaultRequestHeaders.UserAgent.ParseAdd("AppControlOrganizer.V2");
});

// Add Swagger for API documentation in development
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "LifePlanner", Version = "v1" }));

WebApplication app = builder.Build();

// Ensure encryption service is available to EF value converters
IEncryptionService? encryptionService = app.Services.GetService<IEncryptionService>();
if (encryptionService != null)
{
	LifePlannerContext.EncryptionService = encryptionService;
}

// Trust X-Forwarded-Proto / X-Forwarded-For from the reverse proxy (Render, Cloudflare, nginx, etc.).
// This must run BEFORE HSTS, HTTPS redirection and authentication: those middlewares read Request.Scheme,
// which stays "http" behind the proxy until the forwarded headers are applied.
// KnownNetworks/KnownProxies are cleared because the proxy address is not known ahead of time on Render;
// the platform is the only ingress, so the hop it appends is the trusted one.
ForwardedHeadersOptions forwardedHeadersOptions = new ForwardedHeadersOptions
{
  ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
  ForwardLimit = 1,
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Baseline security response headers. Content-Security-Policy is intentionally NOT set here:
// several pages render inline <script> blocks (chart setup, dropdown positioning), so a strict policy
// would break them. Introduce CSP separately once those inlines carry a nonce.
// Content Security Policy: blocks foreign script hosts, framing, <base> and form hijacking, and plugins.
// Inline scripts and styles stay allowed because pages render inline blocks and style attributes. It starts
// in report-only mode (violations show in the browser console) until Security:CspEnforce is set to true.
string contentSecurityPolicy = string.Join("; ",
[
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net",
  "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com",
  "font-src 'self' data: https://cdn.jsdelivr.net https://fonts.gstatic.com",
  "img-src 'self' data: blob: https:",
  "connect-src 'self' wss:",
  "worker-src 'self'",
  "manifest-src 'self'",
  "object-src 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "frame-ancestors 'self'",
]);
string contentSecurityPolicyHeader = builder.Configuration.GetValue<bool>("Security:CspEnforce")
  ? "Content-Security-Policy"
  : "Content-Security-Policy-Report-Only";
app.Use(async (context, next) =>
{
  IHeaderDictionary headers = context.Response.Headers;
  headers[contentSecurityPolicyHeader] = contentSecurityPolicy;
  headers["X-Content-Type-Options"] = "nosniff";
  headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
  headers["X-Frame-Options"] = "SAMEORIGIN";
  headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=()";
  await next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  // Validate database connection without crashing the development host.
	IDbContextFactory<LifePlannerContext> factory = app.Services.GetRequiredService<IDbContextFactory<LifePlannerContext>>();

	try
	{
		using LifePlannerContext db = factory.CreateDbContext();
		bool canConnect = await db.Database.CanConnectAsync();
		if (!canConnect)
		{
      app.Logger.LogWarning("Database connection validation failed. Supabase primary and Neon secondary settings should be checked.");
		}
	}
	catch (Exception ex)
	{
    app.Logger.LogWarning(ex, "Database connection validation failed. Supabase primary and Neon secondary settings should be checked.");
	}

	app.UseSwagger();
	app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AppControlOrganizer V2 API v1"));
}
else
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.Use(async (httpContext, next) =>
{
  string correlationId = ResolveCorrelationId(httpContext);
  httpContext.Response.Headers[CorrelationIdHeaderName] = correlationId;

  string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
  string userIdHash = string.IsNullOrWhiteSpace(userId) ? "anonymous" : HashForLog(userId);
  string operation = $"{httpContext.Request.Method} {httpContext.Request.Path}";

  using IDisposable correlationScope = LogContext.PushProperty("CorrelationId", correlationId);
  using IDisposable userScope = LogContext.PushProperty("UserIdHash", userIdHash);
  using IDisposable processScope = LogContext.PushProperty("Process", "HTTP");
  using IDisposable operationScope = LogContext.PushProperty("Operation", operation);

  await next();

  if (AccessAnalyticsCapturePolicy.ShouldRecordPageLoad(
    httpContext.Request.Method,
    httpContext.Response.StatusCode,
    httpContext.Response.ContentType,
    httpContext.Request.Path))
  {
    httpContext.RequestServices.GetRequiredService<IAccessAnalyticsRecorder>().Record(
      AccessEventKind.PageView,
      userId,
      httpContext.Connection.RemoteIpAddress?.ToString(),
      httpContext.Request.Headers.UserAgent.ToString(),
      httpContext.Request.Path.Value,
      sessionId: null);
  }
});
app.UseSerilogRequestLogging(options =>
{
  options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
  options.GetLevel = (httpContext, elapsed, exception) =>
  {
    if (exception != null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
    {
      return LogEventLevel.Error;
    }

    if (httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest)
    {
      return LogEventLevel.Warning;
    }

    return elapsed >= 1000
      ? LogEventLevel.Warning
      : LogEventLevel.Information;
  };
  options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
  {
    string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    diagnosticContext.Set("CorrelationId", ResolveCorrelationId(httpContext));
    diagnosticContext.Set("UserIdHash", string.IsNullOrWhiteSpace(userId) ? "anonymous" : HashForLog(userId));
    diagnosticContext.Set("Process", "HTTP");
    diagnosticContext.Set("Operation", $"{httpContext.Request.Method} {httpContext.Request.Path}");
  };
});
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

// Map Minimal API endpoints
app.MapAccountEndpoints();
app.MapAllocationEndpoints();
app.MapTransactionEndpoints();
app.MapCreditCardEndpoints();
app.MapWidgetEndpoints();

// Map Blazor components
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// Health check with database probe
app.MapGet("/health", async (IDbContextFactory<LifePlannerContext> factory, ILogger<Program> logger) =>
{
  try
  {
    using LifePlannerContext db = factory.CreateDbContext();
    bool ok = await db.Database.CanConnectAsync();
    logger.LogInformation("Health check completed. DatabaseReachable={DatabaseReachable}.", ok);
    return ok ? Results.Ok("ok") : Results.StatusCode(503);
  }
  catch (Exception ex)
  {
    logger.LogError(ex, "Health check failed while probing database.");
    return Results.StatusCode(503);
  }
}).WithName("Health");

// Auth: logout
app.MapGet("/auth/logout", async (SignInManager<ApplicationUser> signInManager, ILogger<Program> logger) =>
{
  string? userId = signInManager.Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
	await signInManager.SignOutAsync();
  logger.LogInformation(
    "User signed out. UserIdHash={UserIdHash}.",
    string.IsNullOrWhiteSpace(userId) ? "anonymous" : HashForLog(userId));
	return Results.Redirect("/auth/login");
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Login);

// Auth: issues the antiforgery token that the browser sends back with each authentication form post.
app.MapGet("/auth/csrf", (HttpContext http, IAntiforgery antiforgery) =>
{
  AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(http);
  http.Response.Headers.CacheControl = "no-store";
  return Results.Json(new { token = tokens.RequestToken });
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Api);

// Auth: login handler for browser POST so authentication cookie is set on the HTTP response
app.MapPost("/auth/login-local", async (HttpContext http, SignInManager<ApplicationUser> signInManager, IAntiforgery antiforgery, ILogger<Program> logger) =>
{
	if (!await IsAntiforgeryValidAsync(http, antiforgery, logger))
	{
		return Results.Redirect("/auth/login");
	}

	IFormCollection form = await http.Request.ReadFormAsync();
	string identifier = form["Email"].ToString(); // email or username
	string password = form["Password"].ToString();
	bool remember = string.Equals(form["RememberMe"].ToString(), "true", StringComparison.OrdinalIgnoreCase)
	|| string.Equals(form["RememberMe"].ToString(), "on", StringComparison.OrdinalIgnoreCase);
	string returnUrl = form["returnUrl"].ToString();
	if (string.IsNullOrWhiteSpace(returnUrl))
	{
		returnUrl = "/";
	}

  if (!IsLocalReturnUrl(returnUrl))
  {
    logger.LogWarning("Login return URL rejected. ReturnUrlHash={ReturnUrlHash}.", HashForLog(returnUrl));
    returnUrl = "/";
  }

	// if identifier looks like email, resolve username first
	string userName = identifier;
  string identifierHash = HashForLog(identifier);
	if (identifier.Contains("@"))
	{
		ApplicationUser? userByEmail = await signInManager.UserManager.FindByEmailAsync(identifier);
		if (userByEmail is not null)
		{
			userName = userByEmail.UserName ?? identifier;
		}
	}

	// Unknown accounts must cost the same as known ones, otherwise response time reveals which names exist.
	if (await signInManager.UserManager.FindByNameAsync(userName) is null)
	{
		AuthTiming.Equalize(signInManager.UserManager, password);
	}

	SignInResult result = await signInManager.PasswordSignInAsync(userName, password, remember, lockoutOnFailure: true);
	if (result.Succeeded)
	{
    ApplicationUser? signedInUser = await signInManager.UserManager.FindByNameAsync(userName);
    logger.LogInformation(
      "Browser login succeeded. UserIdHash={UserIdHash}, IdentifierHash={IdentifierHash}, RememberMe={RememberMe}.",
      signedInUser == null ? "none" : HashForLog(signedInUser.Id),
      identifierHash,
      remember);
		return Results.Redirect(returnUrl);
	}

	if (result.IsLockedOut)
	{
    logger.LogWarning("Browser login locked out. IdentifierHash={IdentifierHash}.", identifierHash);
		return Results.Redirect("/auth/login?locked=1");
	}

  logger.LogWarning("Browser login failed. IdentifierHash={IdentifierHash}.", identifierHash);
	return Results.Redirect("/auth/login?failed=1");
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Login);

// Auth: public registration endpoint. The AppSettings gate is checked before user lookup or creation.
app.MapPost("/auth/register-local", async (
  HttpContext http,
  UserManager<ApplicationUser> userManager,
  IAppSettingRepository appSettings,
  IAntiforgery antiforgery,
  ILogger<Program> logger) =>
{
  if (!await IsAntiforgeryValidAsync(http, antiforgery, logger))
  {
    return Results.Redirect("/auth/register");
  }

  IFormCollection form = await http.Request.ReadFormAsync();
  AppSetting? setting = await appSettings.SelectAsync(AllowPublicRegistrationKey, http.RequestAborted);
  bool allowPublicRegistration = string.Equals(setting?.Value, bool.TrueString, StringComparison.OrdinalIgnoreCase);
  if (!allowPublicRegistration)
  {
    logger.LogWarning("Public registration rejected because the feature is disabled.");
    return Results.Redirect("/auth/register?registration=disabled");
  }

  string username = form["Username"].ToString().Trim();
  string email = form["Email"].ToString().Trim();
  string password = form["Password"].ToString();
  string confirmPassword = form["ConfirmPassword"].ToString();
  if (string.IsNullOrWhiteSpace(username)
      || string.IsNullOrWhiteSpace(email)
      || string.IsNullOrWhiteSpace(password)
      || !string.Equals(password, confirmPassword, StringComparison.Ordinal))
  {
    logger.LogWarning("Public registration rejected due to invalid input. EmailHash={EmailHash}.", HashForLog(email));
    return Results.Redirect("/auth/register?registration=invalid");
  }

  bool emailTaken = await userManager.FindByEmailAsync(email) != null;
  bool usernameTaken = await userManager.FindByNameAsync(username) != null;
  if (emailTaken || usernameTaken)
  {
    logger.LogWarning(
      "Public registration rejected because identity is unavailable. EmailHash={EmailHash}, UserNameHash={UserNameHash}.",
      HashForLog(email),
      HashForLog(username));
    return Results.Redirect("/auth/register?registration=unavailable");
  }

  ApplicationUser user = new()
  {
    UserName = username,
    Email = email,
    DisplayName = username,
  };

  IdentityResult result = await userManager.CreateAsync(user, password);
  if (result.Succeeded)
  {
    logger.LogInformation("Public registration created user. UserIdHash={UserIdHash}.", HashForLog(user.Id));
  }
  else
  {
    logger.LogWarning("Public registration failed for EmailHash={EmailHash}.", HashForLog(email));
  }

  return result.Succeeded
    ? Results.Redirect("/auth/register?registration=created")
    : Results.Redirect("/auth/register?registration=invalid");
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Login);

// Auth: password reset request. Public response stays generic to avoid account enumeration.
app.MapPost("/auth/forgot-password-local", async (
  HttpContext http,
  UserManager<ApplicationUser> userManager,
  IEmailSender emailSender,
  IMemoryCache memoryCache,
  IAntiforgery antiforgery,
  ILogger<Program> logger) =>
{
  if (!await IsAntiforgeryValidAsync(http, antiforgery, logger))
  {
    return Results.Redirect("/auth/forgot-password");
  }

  IFormCollection form = await http.Request.ReadFormAsync();
  string email = form["Email"].ToString().Trim();
  string normalizedEmail = email.ToUpperInvariant();
  if (!string.IsNullOrWhiteSpace(normalizedEmail) && !IsForgotEmailAllowed(memoryCache, normalizedEmail))
  {
    logger.LogWarning("Password reset request throttled for normalized email hash {EmailHash}.", HashForLog(normalizedEmail));
    return Results.Redirect("/auth/forgot-password?sent=1");
  }

  ApplicationUser? user = string.IsNullOrWhiteSpace(email)
    ? null
    : await userManager.FindByEmailAsync(email);
  if (user != null && !string.IsNullOrWhiteSpace(user.Email))
  {
    string token = await userManager.GeneratePasswordResetTokenAsync(user);
    string encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    string encodedEmail = Uri.EscapeDataString(user.Email);
    string callbackUrl = $"{GetPublicBaseUri(http).TrimEnd('/')}/auth/reset-password?email={encodedEmail}&token={encodedToken}";

    string htmlBody = $"""
      <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;padding:24px;">
        <h2 style="color:#0d6efd;">Life Planner</h2>
        <p>You requested a password reset. Click the link below to choose a new password:</p>
        <p><a href="{callbackUrl}" style="display:inline-block;padding:10px 24px;background:#0d6efd;color:#fff;text-decoration:none;border-radius:6px;">Reset Password</a></p>
        <p style="color:#6c757d;font-size:13px;">If you did not request this, ignore this email.</p>
      </div>
      """;

    await emailSender.SendEmailAsync(user.Email, "Reset your password - Life Planner", htmlBody);
    logger.LogInformation("Password reset email sent for user. UserIdHash={UserIdHash}.", HashForLog(user.Id));
  }

  return Results.Redirect("/auth/forgot-password?sent=1");
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Login);

// Auth: password reset submission. Failures stay generic.
app.MapPost("/auth/reset-password-local", async (
  HttpContext http,
  UserManager<ApplicationUser> userManager,
  IAntiforgery antiforgery,
  ILogger<Program> logger) =>
{
  if (!await IsAntiforgeryValidAsync(http, antiforgery, logger))
  {
    return Results.Redirect("/auth/reset-password?reset=failed");
  }

  IFormCollection form = await http.Request.ReadFormAsync();
  string email = form["Email"].ToString();
  string token = form["Token"].ToString();
  string password = form["NewPassword"].ToString();
  string confirmPassword = form["ConfirmPassword"].ToString();
  if (string.IsNullOrWhiteSpace(email)
      || string.IsNullOrWhiteSpace(token)
      || string.IsNullOrWhiteSpace(password)
      || !string.Equals(password, confirmPassword, StringComparison.Ordinal))
  {
    return Results.Redirect("/auth/reset-password?reset=failed");
  }

  ApplicationUser? user = await userManager.FindByEmailAsync(email);
  if (user == null)
  {
    return Results.Redirect("/auth/reset-password?reset=failed");
  }

  IdentityResult result = await userManager.ResetPasswordAsync(user, token, password);
  if (!result.Succeeded)
  {
    return Results.Redirect("/auth/reset-password?reset=failed");
  }

  logger.LogInformation("Password reset successful for user. UserIdHash={UserIdHash}.", HashForLog(user.Id));
  return Results.Redirect("/auth/reset-password?reset=success");
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Login);

// Auth: JWT token endpoint
app.MapPost("/api/auth/token", async (
UserManager<ApplicationUser> userManager,
SignInManager<ApplicationUser> signInManager,
LoginRequest login,
ILogger<Program> logger) =>
{
	ApplicationUser? user = await userManager.FindByEmailAsync(login.Email);
	if (user is null)
	{
    AuthTiming.Equalize(userManager, login.Password);
    logger.LogWarning("JWT token request rejected for unknown email hash {EmailHash}.", HashForLog(login.Email));
		return Results.Unauthorized();
	}

	// CheckPasswordSignInAsync (not CheckPasswordAsync) so failures feed the Identity lockout counter.
	// Otherwise this endpoint is an offline-speed password oracle that bypasses the 5-attempt lockout
	// enforced on the browser login path.
	SignInResult passwordCheck = await signInManager.CheckPasswordSignInAsync(user, login.Password, lockoutOnFailure: true);
	if (!passwordCheck.Succeeded)
	{
    logger.LogWarning(
      "JWT token request rejected for user. UserIdHash={UserIdHash}. LockedOut={LockedOut}.",
      HashForLog(user.Id),
      passwordCheck.IsLockedOut);
		return Results.Unauthorized();
	}

	IList<string> roles = await userManager.GetRolesAsync(user);

	List<Claim> claims = new()
{
new Claim(JwtRegisteredClaimNames.Sub, user.Id),
new Claim(ClaimTypes.NameIdentifier, user.Id),
new Claim(ClaimTypes.Email, user.Email!)
};
	foreach (string role in roles)
	{
		claims.Add(new Claim(ClaimTypes.Role, role));
	}

	SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(jwtKey));
	SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);
	DateTime expiresUtc = DateTime.UtcNow.AddHours(8);
  logger.LogInformation("JWT token issued for user. UserIdHash={UserIdHash}. ExpiresUtc={ExpiresUtc:u}.", HashForLog(user.Id), expiresUtc);

	JwtSecurityToken token = new(
	issuer: jwtIssuer,
	audience: jwtAudience,
	claims: claims,
	expires: expiresUtc,
	signingCredentials: creds);

	return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), expiresUtc });
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Login);

// Apply pending migrations on startup (if any)
try
{
	using IServiceScope migrationScope = app.Services.CreateScope();
	LifePlannerContext migrationContext = migrationScope.ServiceProvider.GetRequiredService<LifePlannerContext>();
	IEnumerable<string> pendingMigrations = await migrationContext.Database.GetPendingMigrationsAsync();

	if (pendingMigrations.Any())
	{
		app.Logger.LogInformation("Applying {Count} pending migration(s)...", pendingMigrations.Count());
		await migrationContext.Database.MigrateAsync();
		app.Logger.LogInformation("Migrations applied successfully.");
	}
}
catch (Exception ex)
{
	app.Logger.LogError(ex, "Error applying database migrations on startup. Application will continue.");
}

try
{
  app.Logger.LogInformation("Starting AppLifePlanner. Environment={Environment}.", app.Environment.EnvironmentName);

  // Seed demo data for unauthenticated navigation
  await DemoDataSeeder.SeedAsync(app.Services, CancellationToken.None);
  app.Logger.LogInformation("Demo data seeding completed.");

  app.Run();
}
finally
{
  Log.CloseAndFlush();
}

static string GetClientRateLimitKey(HttpContext httpContext)
{
  // Never partition on a raw X-Forwarded-For value: it is attacker-controlled, so a spoofed header
  // would mint a fresh bucket per request and defeat the limiter entirely. UseForwardedHeaders has
  // already rewritten RemoteIpAddress from the single trusted proxy hop by the time this runs.
  return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static string GetPublicBaseUri(HttpContext httpContext)
{
  string scheme = httpContext.Request.Scheme;
  HostString host = httpContext.Request.Host;
  PathString pathBase = httpContext.Request.PathBase;
  return $"{scheme}://{host}{pathBase}";
}

// Rejects authentication form posts that lack a valid antiforgery token or come from another site.
static async Task<bool> IsAntiforgeryValidAsync(HttpContext http, IAntiforgery antiforgery, ILogger<Program> logger)
{
  if (string.Equals(http.Request.Headers["Sec-Fetch-Site"].ToString(), "cross-site", StringComparison.OrdinalIgnoreCase))
  {
    logger.LogWarning("Authentication form post rejected: cross-site request.");
    return false;
  }

  try
  {
    await antiforgery.ValidateRequestAsync(http);
    return true;
  }
  catch (AntiforgeryValidationException)
  {
    logger.LogWarning("Authentication form post rejected: invalid antiforgery token.");
    return false;
  }
}

static bool IsForgotEmailAllowed(IMemoryCache memoryCache, string normalizedEmail)
{
  string cacheKey = $"Auth.ForgotPassword.{HashForLog(normalizedEmail)}";
  int attemptCount = memoryCache.Get<int?>(cacheKey) ?? 0;
  if (attemptCount >= 3)
  {
    return false;
  }

  memoryCache.Set(cacheKey, attemptCount + 1, TimeSpan.FromMinutes(15));
  return true;
}

static bool IsLocalReturnUrl(string returnUrl)
{
  if (string.IsNullOrWhiteSpace(returnUrl))
  {
    return false;
  }

  return returnUrl[0] == '/'
    && (returnUrl.Length == 1 || returnUrl[1] != '/')
    && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);
}

static string HashForLog(string value)
{
  byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
  return Convert.ToHexString(bytes, 0, 8);
}

static string ResolveCorrelationId(HttpContext httpContext)
{
  string? headerValue = httpContext.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();
  if (!string.IsNullOrWhiteSpace(headerValue) && headerValue.Length <= 128)
  {
    return headerValue;
  }

  return httpContext.TraceIdentifier;
}

/// <summary>Credentials payload for the JWT token endpoint.</summary>
internal sealed record LoginRequest(string Email, string Password);

/// <summary>Equalizes the cost of authentication attempts for accounts that do not exist.</summary>
internal static class AuthTiming
{
  private static readonly ApplicationUser Placeholder = new() { UserName = "timing-placeholder" };
  private static string? placeholderHash;

  /// <summary>Verifies the supplied password against a throwaway hash so unknown accounts take as long as real ones.</summary>
  /// <param name="userManager">The user manager that owns the configured password hasher.</param>
  /// <param name="password">The password the caller supplied.</param>
  internal static void Equalize(UserManager<ApplicationUser> userManager, string? password)
  {
    placeholderHash ??= userManager.PasswordHasher.HashPassword(Placeholder, Guid.NewGuid().ToString("N"));
    userManager.PasswordHasher.VerifyHashedPassword(Placeholder, placeholderHash, password ?? string.Empty);
  }
}
