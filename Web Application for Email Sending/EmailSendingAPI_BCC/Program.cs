using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using EmailVerificationAPI.Services;
using EmailVerificationAPI.Data;
using EmailVerificationAPI.Models;
using EmailVerificationAPI.Swagger;

// Logging configuration
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information() 
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning) // Reduce framework logs
    .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics", Serilog.Events.LogEventLevel.Error) // Errors only for diagnostics
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u4}] {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.File(
        path: "logs/mail-service-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u4}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

var configuration = builder.Configuration;

// ---------------------- Services ----------------------

builder.Services.AddControllers();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// DB Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));

// Services
builder.Services.Configure<SmtpSettings>(configuration.GetSection("SMTP"));
builder.Services.AddScoped<EmailService>();

// JWT configuration
var jwtSection = configuration.GetSection("Jwt");
string jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("JWT Key is not configured.");
string jwtIssuer = jwtSection["Issuer"] ?? "MyAppIssuer";
string jwtAudience = jwtSection["Audience"] ?? "MyAppAudience";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero 
    };
});

builder.Services.AddAuthorization(); 

// Swagger with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Email API", Version = "v1" });
    c.OperationFilter<FormFileOperationFilter>();
    c.SchemaFilter<HidePropertySchemaFilter>();
    c.EnableAnnotations();
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter JWT token with Bearer prefix (e.g., 'Bearer ey...')",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddDirectoryBrowser();

// ---------------------- App Pipeline ----------------------

var app = builder.Build();

app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        // Skip logging for static file paths
        var path = httpContext.Request.Path.Value ?? string.Empty; // Handle null path
        if (path.Contains("_framework") || path.Contains("_vs") || path.Contains("favicon.ico") || path.Contains("aspnetcore-browser-refresh.js"))
        {
            return Serilog.Events.LogEventLevel.Verbose; 
        }
        return Serilog.Events.LogEventLevel.Information;
    };
    options.MessageTemplate = "Processed {RequestMethod} {RequestPath} with status {StatusCode} in {Elapsed:0.2f} ms";
});

app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Email API V1");
});

// For serving static files and also set auth.html as the default
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new List<string> { "auth.html" }
});
app.UseStaticFiles();

//app.UseHttpsRedirection();

app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();

Console.WriteLine("App is now running on Console.");

app.Run();