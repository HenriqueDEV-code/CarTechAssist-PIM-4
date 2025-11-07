using CarTechAssist.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Session configuration
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// HTTP Context Accessor
builder.Services.AddHttpContextAccessor();

// HttpClient configuration com suporte a certificados self-signed em desenvolvimento
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpClient<CarTechAssist.Web.Services.ApiClientService>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = 
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
}
else
{
builder.Services.AddHttpClient<CarTechAssist.Web.Services.ApiClientService>();
}

// Services
builder.Services.AddScoped<CarTechAssist.Web.Services.ApiClientService>();
builder.Services.AddScoped<CarTechAssist.Web.Services.AuthService>();
builder.Services.AddScoped<CarTechAssist.Web.Services.ChamadosService>();
builder.Services.AddScoped<CarTechAssist.Web.Services.UsuariosService>();
builder.Services.AddScoped<CarTechAssist.Web.Services.CategoriasService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Middleware to check authentication on protected pages
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";
    
    // Allow access to login, register, forgot password, reset password and error pages without authentication
    if (path == "/login" || path == "/register" || path == "/forgotpassword" || path == "/resetpassword" ||
        path == "/error" || path.StartsWith("/css") || path.StartsWith("/js") || 
        path.StartsWith("/lib") || path.StartsWith("/favicon") || path.StartsWith("/img"))
    {
        await next();
        return;
    }

    var session = context.Session;
    var token = session.GetString("Token");
    
    if (string.IsNullOrEmpty(token) && path != "/")
    {
        context.Response.Redirect("/Login");
        return;
    }

    await next();
});

app.MapRazorPages();

app.Run();
